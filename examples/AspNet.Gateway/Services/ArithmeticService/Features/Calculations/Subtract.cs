using Linbik.Core;
using Linbik.Slices;
using Linbik.Slices.Results;

namespace ArithmeticService.Features.Calculations;

// subtract: sadece Self.
[LinbikSlice("/api/arithmetic/subtract", Tag = "Arithmetic")]
[LFlow(LinbikDefaults.Flows.Self)]
public static partial class Subtract
{
    public sealed record Request(double A, double B) : ILinbikRequest<Response>;

    public sealed record Response(double Result);

    public sealed class Handler : ILinbikHandler<Request, Response>
    {
        public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
            => Result.Ok(new Response(request.A - request.B)).AsValueTask();
    }
}
