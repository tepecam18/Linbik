using System.Text.Json;
using Linbik.Slices;
using Linbik.Slices.Endpoints;
using Linbik.Slices.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests.Slices;

public sealed class LinbikEndpointToHttpTests
{
    private sealed record Ping(string Name) : ILinbikRequest<Pong>;

    private sealed record Pong(string Value);

    private sealed class FakeSender(Func<object, Result<Pong>> respond) : ILinbikSender
    {
        public List<object> Requests { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public ValueTask<Result<TResponse>> Send<TResponse>(
            ILinbikRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            LastToken = cancellationToken;
            return new ValueTask<Result<TResponse>>((Result<TResponse>)(object)respond(request));
        }
    }

    private static async Task<(int Status, JsonElement Body)> ExecuteAsync(IResult result)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, json.RootElement.Clone());
    }

    [Fact]
    public async Task SuccessMapsTo200WithDataEnvelope()
    {
        var (status, body) = await ExecuteAsync(Result.Ok(new Pong("hello")).ToHttp());

        Assert.Equal(200, status);
        Assert.True(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("hello", body.GetProperty("data").GetProperty("value").GetString());
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task FailureMapsToErrorStatusAndFriendlyMessage(LError error, int expectedStatus, string expectedTitle)
    {
        var (status, body) = await ExecuteAsync(Result.Fail<Pong>(error).ToHttp());

        Assert.Equal(expectedStatus, status);
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        var friendly = body.GetProperty("friendlyMessage");
        Assert.Equal(expectedTitle, friendly.GetProperty("title").GetString());
        Assert.Equal(error.Message, friendly.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    public static TheoryData<LError, int, string> Errors => new()
    {
        { LError.Validation("bad input"), 400, "validation_failed" },
        { LError.BadRequest("custom_code", "nope"), 400, "custom_code" },
        { LError.Forbidden("not yours"), 403, "forbidden" },
        { LError.NotFound("missing"), 404, "not_found" },
        { LError.Conflict("taken"), 409, "conflict" },
        { new LError(422, "unprocessable", "custom status"), 422, "unprocessable" },
    };

    [Fact]
    public async Task HandleSendsRequestToSenderAndMapsResult()
    {
        var sender = new FakeSender(request => Result.Ok(new Pong($"hi {((Ping)request).Name}")));
        using var cts = new CancellationTokenSource();

        var result = await LinbikEndpoint.Handle<Ping, Pong>(new Ping("bob"), sender, cts.Token);
        var (status, body) = await ExecuteAsync(result);

        Assert.Equal(200, status);
        Assert.Equal("hi bob", body.GetProperty("data").GetProperty("value").GetString());
        Assert.Equal(new Ping("bob"), Assert.Single(sender.Requests));
        Assert.Equal(cts.Token, sender.LastToken);
    }

    [Fact]
    public async Task HandleMapsSenderFailure()
    {
        var sender = new FakeSender(_ => Result.Fail<Pong>(LError.NotFound("gone")));

        var result = await LinbikEndpoint.Handle<Ping, Pong>(new Ping("bob"), sender, CancellationToken.None);
        var (status, body) = await ExecuteAsync(result);

        Assert.Equal(404, status);
        Assert.Equal("not_found", body.GetProperty("friendlyMessage").GetProperty("title").GetString());
    }

    [Fact]
    public async Task HandleRejectsNullSender()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            LinbikEndpoint.Handle<Ping, Pong>(new Ping("bob"), null!, CancellationToken.None));
    }
}
