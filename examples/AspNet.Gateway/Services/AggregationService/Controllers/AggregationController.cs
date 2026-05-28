using Linbik.Core.Attributes;
using Linbik.Core.Models;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AggregationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[LFlowAuthorize] // tüm aksiyonlar: gateway'den geçmiş ve authenticate edilmiş herhangi bir flow.
public class AggregationController : ControllerBase
{
    public record AggregationRequest(double[] Values);

    public record AggregationResult(double Result)
    {
        /// <summary>
        /// API Gateway tarafından doğrulanmış token'dan parse edilip
        /// <c>Linbik-*</c> header'ları olarak iletilen yetkilendirme bağlamı.
        /// </summary>
        public LGatewayAuthContext? Gateway { get; init; }
    }

    public record CountResult(int Result)
    {
        /// <inheritdoc cref="AggregationResult.Gateway"/>
        public LGatewayAuthContext? Gateway { get; init; }
    }

    [HttpPost("avg")]
    public ActionResult<LBaseResponse<AggregationResult>> Avg([FromBody] AggregationRequest request)
        => Aggregate(request, static v => v.Average());

    [HttpPost("min")]
    public ActionResult<LBaseResponse<AggregationResult>> Min([FromBody] AggregationRequest request)
        => Aggregate(request, static v => v.Min());

    [HttpPost("max")]
    public ActionResult<LBaseResponse<AggregationResult>> Max([FromBody] AggregationRequest request)
        => Aggregate(request, static v => v.Max());

    [HttpPost("count")]
    public ActionResult<LBaseResponse<CountResult>> Count([FromBody] AggregationRequest request)
    {
        var ctx = LGatewayAuthContext.FromRequest(Request);
        var result = new CountResult(request.Values?.Length ?? 0) { Gateway = ctx };
        return Ok(new LBaseResponse<CountResult>(result));
    }

    private ActionResult<LBaseResponse<AggregationResult>> Aggregate(
        AggregationRequest request,
        Func<double[], double> reducer)
    {
        if (request.Values is null || request.Values.Length == 0)
        {
            return BadRequest(new LBaseResponse<AggregationResult>(
                title: "Empty input",
                message: "Values array cannot be null or empty.",
                isSuccess: false));
        }

        var ctx = LGatewayAuthContext.FromRequest(Request);
        var result = new AggregationResult(reducer(request.Values)) { Gateway = ctx };
        return Ok(new LBaseResponse<AggregationResult>(result));
    }
}

