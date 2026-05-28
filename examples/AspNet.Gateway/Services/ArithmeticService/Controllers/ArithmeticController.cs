using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Models;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ArithmeticService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArithmeticController : ControllerBase
{
    public record BinaryOperationRequest(double A, double B);

    public record OperationResult(double Result)
    {
        /// <summary>
        /// API Gateway tarafından doğrulanmış token'dan parse edilip
        /// <c>Linbik-*</c> header'ları olarak iletilen yetkilendirme bağlamı.
        /// </summary>
        public LGatewayAuthContext? Gateway { get; init; }
    }

    // add: yetkilendirme şartı yok — her flow için (ve Linbik-Flow olmadan dahi) erişilebilir.
    [HttpPost("add")]
    public ActionResult<LBaseResponse<OperationResult>> Add([FromBody] BinaryOperationRequest request)
        => Ok(Wrap(new OperationResult(request.A + request.B)));

    // subtract: sadece Self.
    [HttpPost("subtract")]
    [LFlowAuthorize(LinbikDefaults.Flows.Self)]
    public ActionResult<LBaseResponse<OperationResult>> Subtract([FromBody] BinaryOperationRequest request)
        => Ok(Wrap(new OperationResult(request.A - request.B)));

    // multiply: Self + Delegated.
    [HttpPost("multiply")]
    [LFlowAuthorize(LinbikDefaults.Flows.Self, LinbikDefaults.Flows.Delegated)]
    public ActionResult<LBaseResponse<OperationResult>> Multiply([FromBody] BinaryOperationRequest request)
        => Ok(Wrap(new OperationResult(request.A * request.B)));

    // divide: Self + Application.
    [HttpPost("divide")]
    [LFlowAuthorize(LinbikDefaults.Flows.Self, LinbikDefaults.Flows.Application)]
    public ActionResult<LBaseResponse<OperationResult>> Divide([FromBody] BinaryOperationRequest request)
    {
        if (request.B == 0)
        {
            return BadRequest(new LBaseResponse<OperationResult>(
                title: "Division by zero",
                message: "The divisor (B) cannot be zero.",
                isSuccess: false));
        }

        return Ok(Wrap(new OperationResult(request.A / request.B)));
    }

    private LBaseResponse<OperationResult> Wrap(OperationResult result)
    {
        var ctx = LGatewayAuthContext.FromRequest(Request);
        return new LBaseResponse<OperationResult>(result with { Gateway = ctx });
    }
}


