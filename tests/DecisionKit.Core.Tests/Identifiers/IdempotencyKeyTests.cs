using System;
using DecisionKit.Identifiers;

namespace DecisionKit.Core.Tests.Identifiers;

public sealed class IdempotencyKeyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsMissingValue(string? value)
    {
        Assert.Throws<ArgumentException>(() => new IdempotencyKey(value!));
    }

    [Fact]
    public void New_ProducesDistinctUsableKeys()
    {
        IdempotencyKey first = IdempotencyKey.New();
        IdempotencyKey second = IdempotencyKey.New();

        Assert.False(first.IsEmpty);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equality_IsValueBasedAndOrdinal()
    {
        IdempotencyKey left = new("key-1");
        IdempotencyKey right = new("key-1");
        IdempotencyKey different = new("Key-1");

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left != different);
    }

    [Fact]
    public void Value_OnDefaultInstance_Throws()
    {
        IdempotencyKey key = default;

        Assert.True(key.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => key.Value);
    }

    [Fact]
    public void TryCreate_ReportsInvalidValueWithoutThrowing()
    {
        Assert.False(IdempotencyKey.TryCreate("\t", out IdempotencyKey key));
        Assert.True(key.IsEmpty);
    }
}
