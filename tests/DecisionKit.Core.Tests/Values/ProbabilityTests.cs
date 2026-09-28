using System;
using System.Globalization;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Values;

public sealed class ProbabilityTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_AcceptsTheClosedUnitInterval(double value)
    {
        Probability probability = new(value);

        Assert.Equal(value, probability.Value);
    }

    [Theory]
    [InlineData(-0.000001)]
    [InlineData(1.000001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_RejectsValuesOutsideTheUnitInterval(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Probability(value));
    }

    [Fact]
    public void Default_IsZeroAndUsable()
    {
        Probability probability = default;

        Assert.Equal(0.0, probability.Value);
        Assert.Equal(Probability.Zero, probability);
    }

    [Fact]
    public void One_IsTheUpperBound()
    {
        Assert.Equal(1.0, Probability.One.Value);
    }

    [Fact]
    public void Complement_InvertsTheProbability()
    {
        Probability probability = new(0.25);

        Assert.Equal(0.75, probability.Complement().Value);
    }

    [Fact]
    public void Comparison_OrdersByValue()
    {
        Probability low = new(0.2);
        Probability high = new(0.8);

        Assert.True(low < high);
        Assert.True(low <= high);
        Assert.True(high > low);
        Assert.True(high >= low);
        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Probability left = new(0.42);
        Probability right = new(0.42);

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(left != right);
    }

    [Fact]
    public void ToString_UsesTheInvariantCulture()
    {
        Probability probability = new(0.5);

        Assert.Equal("0.5", probability.ToString());
        Assert.Equal("0.50", probability.ToString("F2", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryCreate_ReportsInvalidValueWithoutThrowing()
    {
        Assert.False(Probability.TryCreate(1.5, out Probability probability));
        Assert.Equal(Probability.Zero, probability);
        Assert.True(Probability.TryCreate(0.3, out probability));
        Assert.Equal(0.3, probability.Value);
    }
}
