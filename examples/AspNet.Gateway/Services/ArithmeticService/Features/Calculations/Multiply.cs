using Linbik.Core;
using Linbik.Slices;
using Linbik.Slices.Results;

namespace ArithmeticService.Features.Calculations;

// multiply: Self + Delegated.
[LinbikSlice("/api/arithmetic/multiply", Tag = "Arithmetic")]
[LFlow(LinbikDefaults.Flows.Self, LinbikDefaults.Flows.Delegated)]
public static partial class Multiply
{
    public sealed record Request(double A, double B) : ILinbikRequest<Response>;

    public sealed record Response(double Result);

    public sealed class Handler : ILinbikHandler<Request, Response>
    {
        public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
            => Result.Ok(new Response(request.A * request.B)).AsValueTask();
    }
}
