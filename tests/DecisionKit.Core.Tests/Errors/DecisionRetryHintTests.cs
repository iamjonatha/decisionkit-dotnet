using System;
using DecisionKit.Errors;

namespace DecisionKit.Core.Tests.Errors;

public sealed class DecisionRetryHintTests
{
    [Fact]
    public void Unknown_IsTheDefaultValue()
    {
        Assert.Equal(default, DecisionRetryHint.Unknown);
        Assert.Equal(DecisionRetryability.Unknown, DecisionRetryHint.Unknown.Retryability);
        Assert.Null(DecisionRetryHint.Unknown.RetryAfter);
    }

    [Fact]
    public void After_RecordsTheDelayTheProviderAskedFor()
    {
        DecisionRetryHint hint = DecisionRetryHint.After(TimeSpan.FromSeconds(5));

        Assert.Equal(DecisionRetryability.Retryable, hint.Retryability);
        Assert.Equal(TimeSpan.FromSeconds(5), hint.RetryAfter);
    }

    [Fact]
    public void After_RejectsANegativeDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DecisionRetryHint.After(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void RetryableWithoutDelayIsNotEqualToRetryableWithDelay()
    {
        Assert.NotEqual(DecisionRetryHint.Retryable, DecisionRetryHint.After(TimeSpan.FromSeconds(5)));
        Assert.True(DecisionRetryHint.Retryable != DecisionRetryHint.After(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void EqualHintsCompareEqual()
    {
        Assert.True(DecisionRetryHint.After(TimeSpan.FromSeconds(5)) == DecisionRetryHint.After(TimeSpan.FromSeconds(5)));
        Assert.Equal(
            DecisionRetryHint.After(TimeSpan.FromSeconds(5)).GetHashCode(),
            DecisionRetryHint.After(TimeSpan.FromSeconds(5)).GetHashCode());
    }

    [Fact]
    public void NotRetryable_CarriesNoDelay()
    {
        Assert.Equal(DecisionRetryability.NotRetryable, DecisionRetryHint.NotRetryable.Retryability);
        Assert.Null(DecisionRetryHint.NotRetryable.RetryAfter);
    }
}
