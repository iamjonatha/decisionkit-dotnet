using System;
using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Questions;

public sealed class ScoreQuestionTests
{
    [Fact]
    public void Constructor_KeepsTheScale()
    {
        ScoreQuestion question = new(new QuestionId("frustration"), "How frustrated is the customer?", 0, 10);

        Assert.Equal(0, question.Minimum);
        Assert.Equal(10, question.Maximum);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 0)]
    [InlineData(double.NaN, 10)]
    [InlineData(0, double.PositiveInfinity)]
    public void Constructor_RejectsAnInvalidScale(double minimum, double maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreQuestion(new QuestionId("frustration"), "How frustrated?", minimum, maximum));
    }

    [Fact]
    public void CreateScore_ProducesAScoreOnTheQuestionScale()
    {
        ScoreQuestion question = new(new QuestionId("frustration"), "How frustrated?", 0, 10);

        Score score = question.CreateScore(7);

        Assert.Equal(7, score.Value);
        Assert.Equal(0.7, score.Normalize(), 10);
    }

    [Fact]
    public void CreateScore_RejectsAValueOutsideTheScale()
    {
        ScoreQuestion question = new(new QuestionId("frustration"), "How frustrated?", 0, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => question.CreateScore(11));
    }
}
