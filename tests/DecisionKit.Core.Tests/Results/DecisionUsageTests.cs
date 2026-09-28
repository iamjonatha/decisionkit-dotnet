using System;
using System.Collections.Generic;
using DecisionKit.Results;

namespace DecisionKit.Core.Tests.Results;

public sealed class DecisionUsageTests
{
    [Fact]
    public void Empty_ReportsNoMetric()
    {
        Assert.True(DecisionUsage.Empty.IsEmpty);
        Assert.Empty(DecisionUsage.Empty.Metrics);
        Assert.False(DecisionUsage.Empty.TryGetMetric(DecisionUsageKeys.Cost, out _));
    }

    [Fact]
    public void Metrics_AreKeyedByNameAndReadBack()
    {
        DecisionUsage usage = new(
        [
            new KeyValuePair<string, double>(DecisionUsageKeys.InputTokens, 100),
            new KeyValuePair<string, double>(DecisionUsageKeys.OutputTokens, 20),
            new KeyValuePair<string, double>("provider_specific", 3),
        ]);

        Assert.False(usage.IsEmpty);
        Assert.True(usage.TryGetMetric(DecisionUsageKeys.InputTokens, out double input));
        Assert.Equal(100, input);
        Assert.True(usage.TryGetMetric("provider_specific", out double custom));
        Assert.Equal(3, custom);
    }

    [Fact]
    public void Constructor_RejectsADuplicatedMetric()
    {
        Assert.Throws<ArgumentException>(() => new DecisionUsage(
        [
            new KeyValuePair<string, double>(DecisionUsageKeys.Cost, 1),
            new KeyValuePair<string, double>(DecisionUsageKeys.Cost, 2),
        ]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAMetricWithoutName(string name)
    {
        Assert.Throws<ArgumentException>(() => new DecisionUsage([new KeyValuePair<string, double>(name, 1)]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsANonFiniteMetric(double value)
    {
        Assert.Throws<ArgumentException>(() => new DecisionUsage([new KeyValuePair<string, double>(DecisionUsageKeys.Cost, value)]));
    }

    [Fact]
    public void Constructor_RejectsANullSequence()
    {
        Assert.Throws<ArgumentNullException>(() => new DecisionUsage(null!));
    }
}
