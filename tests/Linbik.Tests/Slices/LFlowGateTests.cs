using Linbik.Core.Attributes;
using Microsoft.Extensions.Primitives;

namespace Linbik.Tests.Slices;

public sealed class LFlowGateTests
{
    private static readonly string[] SelfApp = ["Self", "Application"];

    [Fact]
    public void PublicMarkerAllowsWithoutHeader()
    {
        var decision = LFlowGate.Evaluate(StringValues.Empty, ["*"]);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void PublicMarkerAllowsEvenWithDuplicateOrDirtyHeader()
    {
        Assert.True(LFlowGate.Evaluate(new StringValues(["Self", "Application"]), ["*"]).Allowed);
        Assert.True(LFlowGate.Evaluate("a, b", ["*"]).Allowed);
    }

    [Fact]
    public void PublicMarkerAmongOtherFlowsStillAllows()
    {
        Assert.True(LFlowGate.Evaluate(StringValues.Empty, ["Self", "*"]).Allowed);
    }

    [Fact]
    public void DuplicateHeaderIsRejectedAsUnauthorized()
    {
        var decision = LFlowGate.Evaluate(new StringValues(["Self", "Self"]), SelfApp);
        Assert.False(decision.Allowed);
        Assert.Equal(401, decision.StatusCode);
        Assert.Equal("unauthorized", decision.Title);
        Assert.Contains("exactly once", decision.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrBlankHeaderIsRejectedAsUnauthorized(string? header)
    {
        var values = header is null ? StringValues.Empty : new StringValues(header);
        var decision = LFlowGate.Evaluate(values, SelfApp);
        Assert.False(decision.Allowed);
        Assert.Equal(401, decision.StatusCode);
        Assert.Equal("unauthorized", decision.Title);
        Assert.Contains("missing", decision.Message);
    }

    [Theory]
    [InlineData("Self,Application")]
    [InlineData("Self Application")]
    [InlineData("Self;Application")]
    [InlineData("Self\tApplication")]
    [InlineData("Self\rApplication")]
    [InlineData("Self\nApplication")]
    public void MultiTokenOrControlCharacterHeaderIsRejectedAsUnauthorized(string header)
    {
        var decision = LFlowGate.Evaluate(header, SelfApp);
        Assert.False(decision.Allowed);
        Assert.Equal(401, decision.StatusCode);
        Assert.Contains("invalid characters", decision.Message);
    }

    [Fact]
    public void FlowNotInAllowedListIsForbiddenAndMessageListsAllowedFlows()
    {
        var decision = LFlowGate.Evaluate("Delegated", SelfApp);
        Assert.False(decision.Allowed);
        Assert.Equal(403, decision.StatusCode);
        Assert.Equal("forbidden_flow", decision.Title);
        Assert.Contains("'Delegated'", decision.Message);
        Assert.Contains("Self, Application", decision.Message);
    }

    [Theory]
    [InlineData("Self")]
    [InlineData("self")]
    [InlineData("SELF")]
    [InlineData("application")]
    [InlineData("  Self  ")]
    public void AllowedFlowMatchIsCaseInsensitiveAndTrimmed(string header)
    {
        Assert.True(LFlowGate.Evaluate(header, SelfApp).Allowed);
    }

    [Fact]
    public void EmptyAllowedListWithCleanHeaderAllows()
    {
        // Characterization: an empty allowed list means "any gateway-authenticated flow".
        Assert.True(LFlowGate.Evaluate("Anything", []).Allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("A,B")]
    [InlineData("A B")]
    public void EmptyAllowedListStillRejectsMissingOrDirtyHeader(string? header)
    {
        var values = header is null ? StringValues.Empty : new StringValues(header);
        var decision = LFlowGate.Evaluate(values, []);
        Assert.False(decision.Allowed);
        Assert.Equal(401, decision.StatusCode);
    }

    [Fact]
    public void NullAllowedFlowsBehavesLikeEmpty()
    {
        Assert.True(LFlowGate.Evaluate("Self", null!).Allowed);
        Assert.False(LFlowGate.Evaluate(StringValues.Empty, null!).Allowed);
    }
}
