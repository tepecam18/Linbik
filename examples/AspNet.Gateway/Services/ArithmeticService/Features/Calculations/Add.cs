using Linbik.Slices;
using Linbik.Slices.Results;

namespace ArithmeticService.Features.Calculations;

// add: bilinçli olarak public — her flow için (ve Linbik-Flow olmadan dahi) erişilebilir.
[LinbikSlice("/api/arithmetic/add", Tag = "Arithmetic")]
[LFlowPublic]
public static partial class Add
{
    public sealed record Request(double A, double B) : ILinbikRequest<Response>;

    public sealed record Response(double Result);

    public sealed class Handler : ILinbikHandler<Request, Response>
    {
        public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
            => Result.Ok(new Response(request.A + request.B)).AsValueTask();
    }
}
