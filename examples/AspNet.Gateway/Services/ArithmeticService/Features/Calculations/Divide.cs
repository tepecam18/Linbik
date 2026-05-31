using Linbik.Core;
using Linbik.Slices;
using Linbik.Slices.Results;

namespace ArithmeticService.Features.Calculations;

// divide: Self + Application. Validator bölen sıfır olamaz kuralını uygular;
// geçersizse handler hiç çağrılmaz, 400 + LBaseResponse FriendlyMessage döner.
[LinbikSlice("/api/arithmetic/divide", Tag = "Arithmetic")]
[LFlow(LinbikDefaults.Flows.Self, LinbikDefaults.Flows.Application)]
public static partial class Divide
{
    public sealed record Request(double A, double B) : ILinbikRequest<Response>;

    public sealed record Response(double Result);

    public sealed class Validator : ILinbikValidator<Request>
    {
        public ValueTask<LValidationResult> ValidateAsync(Request request, CancellationToken cancellationToken)
            => LValidation.For(request)
                .Ensure(static x => x.B != 0, field: "B", message: "The divisor (B) cannot be zero.")
                .BuildAsync();
    }

    public sealed class Handler : ILinbikHandler<Request, Response>
    {
        public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
            => Result.Ok(new Response(request.A / request.B)).AsValueTask();
    }
}
