using System;
using System.Collections.Generic;
using DecisionKit.Answers;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Results;

public sealed class DecisionResultTests
{
    private static readonly DecisionMetadata s_metadata = new("fake", DateTimeOffset.UnixEpoch);

    [Fact]
    public void Get_InfersTheAnswerTypeFromTheQuestion()
    {
        ScoreQuestion frustration = new(new QuestionId("frustration"), "How frustrated?", 0, 10);
        ChoiceQuestion<Department> routing = new(new QuestionId("routing"), "Which department?", [Department.Billing, Department.Sales]);

        DecisionResult result = new(
            [
                new ScoreAnswer(frustration.Id, frustration.CreateScore(7)),
                new ChoiceAnswer<Department>(routing.Id, Choice.Selected(Department.Billing)),
            ],
            s_metadata);

        ScoreAnswer score = result.Get(frustration);
        ChoiceAnswer<Department> choice = result.Get(routing);

        Assert.Equal(7, score.Value.Value);
        Assert.Equal(Department.Billing, choice.Value.Selection);
    }

    [Fact]
    public void Get_ThrowsWhenTheQuestionWasNotAnswered()
    {
        ProbabilityQuestion question = new(new QuestionId("urgent"), "Is it urgent?");
        DecisionResult result = new([], s_metadata);

        Assert.Throws<KeyNotFoundException>(() => result.Get(question));
    }

    [Fact]
    public void Get_ThrowsWhenTheProviderReturnedAnotherAnswerShape()
    {
        QuestionId id = new("frustration");
        ScoreQuestion question = new(id, "How frustrated?", 0, 10);

        DecisionResult result = new([new ProbabilityAnswer(id, new Probability(0.5))], s_metadata);

        Assert.Throws<InvalidOperationException>(() => result.Get(question));
    }

    [Fact]
    public void TryGet_ReportsAMissingOrMismatchedAnswerWithoutThrowing()
    {
        QuestionId id = new("frustration");
        ScoreQuestion question = new(id, "How frustrated?", 0, 10);

        DecisionResult mismatched = new([new ProbabilityAnswer(id, new Probability(0.5))], s_metadata);
        DecisionResult empty = new([], s_metadata);

        Assert.False(mismatched.TryGet(question, out ScoreAnswer? answer));
        Assert.Null(answer);
        Assert.False(empty.TryGet(question, out _));
    }

    [Fact]
    public void DynamicGet_ReadsAnAnswerByIdentifier()
    {
        DecisionResult result = new([new ProbabilityAnswer(new QuestionId("urgent"), Probability.One)], s_metadata);

        Answer answer = result.Get("urgent");

        Assert.IsType<ProbabilityAnswer>(answer);
        Assert.True(result.TryGet("urgent", out Answer? found));
        Assert.Same(answer, found);
        Assert.False(result.TryGet("  ", out _));
        Assert.Throws<KeyNotFoundException>(() => result.Get("missing"));
    }

    [Fact]
    public void Answers_PreserveProviderOrder()
    {
        DecisionResult result = new(
            [
                new ProbabilityAnswer(new QuestionId("c"), Probability.One),
                new ProbabilityAnswer(new QuestionId("a"), Probability.Zero),
                new ProbabilityAnswer(new QuestionId("b"), Probability.One),
            ],
            s_metadata);

        Assert.Equal(3, result.Count);
        Assert.Equal("c", result.Answers[0].QuestionId.Value);
        Assert.Equal("a", result.Answers[1].QuestionId.Value);
        Assert.Equal("b", result.Answers[2].QuestionId.Value);
    }

    [Fact]
    public void Constructor_RejectsTwoAnswersForTheSameQuestion()
    {
        Assert.Throws<ArgumentException>(() => new DecisionResult(
            [
                new ProbabilityAnswer(new QuestionId("urgent"), Probability.One),
                new ProbabilityAnswer(new QuestionId("urgent"), Probability.Zero),
            ],
            s_metadata));
    }

    [Fact]
    public void Constructor_RejectsANullAnswerOrNullMetadata()
    {
        Assert.Throws<ArgumentException>(() => new DecisionResult([null!], s_metadata));
        Assert.Throws<ArgumentNullException>(() => new DecisionResult([], null!));
        Assert.Throws<ArgumentNullException>(() => new DecisionResult(null!, s_metadata));
    }

    [Fact]
    public void UnknownAnswer_IsReadableThroughItsOwnQuestion()
    {
        UnknownQuestion question = new(new QuestionId("ranking"), "Rank them", "ranking");

        DecisionResult result = new(
            [
                new ProbabilityAnswer(new QuestionId("urgent"), Probability.One),
                new UnknownAnswer(question.Id, "ranking") { RawPayload = "{}" },
            ],
            s_metadata);

        UnknownAnswer answer = result.Get(question);

        Assert.Equal("ranking", answer.ProviderType);
        Assert.Equal("{}", answer.RawPayload);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Usage_DefaultsToEmpty()
    {
        DecisionResult result = new([], s_metadata);

        Assert.True(result.Usage.IsEmpty);
    }

    [Fact]
    public void Usage_CanBeSuppliedByTheProvider()
    {
        DecisionResult result = new([], s_metadata)
        {
            Usage = new DecisionUsage([new KeyValuePair<string, double>(DecisionUsageKeys.TotalTokens, 128)]),
        };

        Assert.True(result.Usage.TryGetMetric(DecisionUsageKeys.TotalTokens, out double tokens));
        Assert.Equal(128, tokens);
    }
}
