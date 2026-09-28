using System;
using System.Collections.Generic;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Answers;

public sealed class AnswerTests
{
    [Fact]
    public void Constructor_RejectsAnUninitializedQuestionIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new ProbabilityAnswer(default, Probability.One));
    }

    [Fact]
    public void ValueType_ReportsTheCarriedType()
    {
        Assert.Equal(typeof(Probability), new ProbabilityAnswer(new QuestionId("q"), Probability.One).ValueType);
        Assert.Equal(typeof(Score), new ScoreAnswer(new QuestionId("q"), new Score(7, 0, 10)).ValueType);
        Assert.Null(new UnknownAnswer(new QuestionId("q"), "ranking").ValueType);
    }

    [Fact]
    public void ScoreAnswer_RejectsAScoreWithoutScale()
    {
        Assert.Throws<ArgumentException>(() => new ScoreAnswer(new QuestionId("q"), default));
    }

    [Fact]
    public void Metadata_IsCopiedOnAssignment()
    {
        Dictionary<string, object?> source = new(StringComparer.Ordinal)
        {
            ["confidence"] = "high",
        };

        ProbabilityAnswer answer = new(new QuestionId("q"), Probability.One)
        {
            Metadata = source,
        };

        source["confidence"] = "low";

        Assert.Equal("high", answer.Metadata["confidence"]);
    }

    [Fact]
    public void UnknownAnswer_PreservesEverythingTheProviderSent()
    {
        UnknownAnswer answer = new(new QuestionId("ranking"), "ranking")
        {
            RawPayload = "{\"order\":[1,2,3]}",
            UnknownProperties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["order"] = "1,2,3",
            },
        };

        Assert.Equal("ranking", answer.ProviderType);
        Assert.Equal("{\"order\":[1,2,3]}", answer.RawPayload);
        Assert.Equal("1,2,3", answer.UnknownProperties["order"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UnknownAnswer_RejectsAMissingProviderType(string? providerType)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UnknownAnswer(new QuestionId("q"), providerType!));
    }
}
