using System;
using DecisionKit.Answers;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Questions;

namespace DecisionKit.Core.Tests.Questions;

public sealed class QuestionTests
{
    [Fact]
    public void AnswerType_IsDerivedFromTheQuestionType()
    {
        Assert.Equal(typeof(ProbabilityAnswer), new ProbabilityQuestion(new QuestionId("q"), "Is it urgent?").AnswerType);
        Assert.Equal(typeof(ScoreAnswer), new ScoreQuestion(new QuestionId("q"), "How frustrated?", 0, 10).AnswerType);
        Assert.Equal(typeof(ChoiceAnswer<Department>), new ChoiceQuestion<Department>(new QuestionId("q"), "Route to?", [Department.Billing]).AnswerType);
        Assert.Equal(typeof(UnknownAnswer), new UnknownQuestion(new QuestionId("q"), "Rank them", "ranking").AnswerType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsAMissingPrompt(string? prompt)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ProbabilityQuestion(new QuestionId("q"), prompt!));
    }

    [Fact]
    public void Constructor_RejectsAnUninitializedIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new ProbabilityQuestion(default, "Is it urgent?"));
    }

    [Fact]
    public void Constructor_TrimsThePrompt()
    {
        ProbabilityQuestion question = new(new QuestionId("q"), "  Is it urgent?  ");

        Assert.Equal("Is it urgent?", question.Prompt);
    }

    [Fact]
    public void Metadata_DefaultsToEmptyAndIsCopiedOnAssignment()
    {
        System.Collections.Generic.Dictionary<string, object?> source = new(StringComparer.Ordinal)
        {
            ["source"] = "crm",
        };

        ProbabilityQuestion question = new(new QuestionId("q"), "Is it urgent?")
        {
            Metadata = source,
        };

        source["source"] = "mutated";

        Assert.Equal("crm", question.Metadata["source"]);
        Assert.Empty(new ProbabilityQuestion(new QuestionId("q"), "Is it urgent?").Metadata);
    }

    [Fact]
    public void UnknownQuestion_PreservesTheProviderType()
    {
        UnknownQuestion question = new(new QuestionId("q"), "Rank them", "  ranking  ")
        {
            RawDefinition = "{\"type\":\"ranking\"}",
        };

        Assert.Equal("ranking", question.ProviderType);
        Assert.Equal("{\"type\":\"ranking\"}", question.RawDefinition);
    }
}
