using System;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Values;

public sealed class ScoreTests
{
    [Fact]
    public void Constructor_KeepsValueAndScale()
    {
        Score score = new(7, 0, 10);

        Assert.Equal(7, score.Value);
        Assert.Equal(0, score.Minimum);
        Assert.Equal(10, score.Maximum);
        Assert.True(score.HasScale);
    }

    [Theory]
    [InlineData(11, 0, 10)]
    [InlineData(-1, 0, 10)]
    [InlineData(double.NaN, 0, 10)]
    [InlineData(5, 10, 10)]
    [InlineData(5, 10, 0)]
    [InlineData(5, double.NegativeInfinity, 10)]
    [InlineData(5, 0, double.PositiveInfinity)]
    public void Constructor_RejectsAnInvalidScaleOrValue(double value, double minimum, double maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Score(value, minimum, maximum));
    }

    [Fact]
    public void Normalize_ProjectsOntoTheUnitInterval()
    {
        Assert.Equal(0.7, new Score(7, 0, 10).Normalize(), 10);
        Assert.Equal(0.5, new Score(3, 1, 5).Normalize(), 10);
    }

    [Fact]
    public void Normalize_OnDefaultInstance_Throws()
    {
        Score score = default;

        Assert.False(score.HasScale);
        Assert.Throws<InvalidOperationException>(() => score.Normalize());
    }

    [Fact]
    public void Equality_ComparesTheScaleAsWell()
    {
        Score left = new(7, 0, 10);
        Score same = new(7, 0, 10);
        Score otherScale = new(7, 0, 100);

        Assert.True(left == same);
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.True(left != otherScale);
    }

    [Fact]
    public void ToString_ShowsTheScale()
    {
        Assert.Equal("7 [0..10]", new Score(7, 0, 10).ToString());
    }

    [Fact]
    public void TryCreate_ReportsInvalidInputWithoutThrowing()
    {
        Assert.False(Score.TryCreate(11, 0, 10, out Score score));
        Assert.False(score.HasScale);
        Assert.True(Score.TryCreate(4, 0, 10, out score));
        Assert.Equal(4, score.Value);
    }
}
