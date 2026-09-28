using System;
using DecisionKit.Identifiers;

namespace DecisionKit.Core.Tests.Identifiers;

public sealed class RequestIdTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsMissingValue(string? value)
    {
        Assert.Throws<ArgumentException>(() => new RequestId(value!));
    }

    [Fact]
    public void New_ProducesDistinctUsableIdentifiers()
    {
        RequestId first = RequestId.New();
        RequestId second = RequestId.New();

        Assert.False(first.IsEmpty);
        Assert.False(second.IsEmpty);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equality_IsValueBasedAndOrdinal()
    {
        RequestId left = new("req-1");
        RequestId right = new("  req-1 ");
        RequestId different = new("REQ-1");

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left != different);
    }

    [Fact]
    public void Value_OnDefaultInstance_Throws()
    {
        RequestId requestId = default;

        Assert.True(requestId.IsEmpty);
        Assert.Equal(string.Empty, requestId.ToString());
        Assert.Throws<InvalidOperationException>(() => requestId.Value);
    }

    [Fact]
    public void TryCreate_ReportsInvalidValueWithoutThrowing()
    {
        Assert.False(RequestId.TryCreate(null, out RequestId requestId));
        Assert.True(requestId.IsEmpty);
    }
}
