using Linbik.Core.Identity;
using Linbik.Slices;
using Linbik.Slices.Pipeline;
using Linbik.Slices.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class SenderTests
{
    [Fact]
    public async Task OneRequestCanUseTwoResponseContracts()
    {
        using var services = new ServiceCollection()
            .AddSingleton<ILinbikHandler<DualRequest, string>, TextHandler>()
            .AddSingleton<ILinbikHandler<DualRequest, int>, NumberHandler>()
            .BuildServiceProvider();
        var sender = new LinbikSender(services);
        var request = new DualRequest();
        Assert.Equal("ok", (await sender.Send<string>(request)).Value);
        Assert.Equal(42, (await sender.Send<int>(request)).Value);
        Assert.Equal("ok", (await sender.Send<string>(request)).Value);
    }

    [Fact]
    public async Task InvalidRequestDoesNotResolveOrCallHandler()
    {
        using var services = new ServiceCollection()
            .AddSingleton<LActor>(LActor.Anonymous.Instance)
            .AddSingleton<ILinbikValidator<DualRequest>, RejectValidator>()
            .BuildServiceProvider();
        var result = await new LinbikSender(services).Send<string>(new DualRequest());
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task MissingHandlerNamesTheContractAndRegistrationFix()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new LinbikSender(services).Send<string>(new DualRequest()));
        Assert.Contains(nameof(DualRequest), error.Message);
        Assert.Contains("AddLinbikSlices", error.Message);
        Assert.Contains("AddScoped", error.Message);
    }

    [Fact]
    public async Task HandlerConstructionFailureIsNotMisreportedAsMissingRegistration()
    {
        var expected = new InvalidOperationException("handler dependency failed");
        using var services = new ServiceCollection()
            .AddSingleton<ILinbikHandler<DualRequest, string>>(_ => throw expected).BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new LinbikSender(services).Send<string>(new DualRequest()));
        Assert.Same(expected, error);
    }

    [Fact]
    public void NullProviderFailsAtConstruction()
        => Assert.Throws<ArgumentNullException>(() => new LinbikSender(null!));

    public sealed class DualRequest : ILinbikRequest<string>, ILinbikRequest<int>;
    public sealed class TextHandler : ILinbikHandler<DualRequest, string>
    {
        public ValueTask<Result<string>> HandleAsync(DualRequest request, CancellationToken cancellationToken) => Result.Ok("ok").AsValueTask();
    }
    public sealed class NumberHandler : ILinbikHandler<DualRequest, int>
    {
        public ValueTask<Result<int>> HandleAsync(DualRequest request, CancellationToken cancellationToken) => Result.Ok(42).AsValueTask();
    }
    public sealed class RejectValidator : ILinbikValidator<DualRequest>
    {
        public ValueTask<LValidationResult> ValidateAsync(DualRequest request, CancellationToken cancellationToken) =>
            new(LValidationResult.Fail([new LValidationError("value", "required")]));
    }
}
