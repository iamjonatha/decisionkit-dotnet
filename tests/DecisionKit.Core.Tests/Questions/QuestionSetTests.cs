using System;
using System.Collections.Generic;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Questions;

namespace DecisionKit.Core.Tests.Questions;

public sealed class QuestionSetTests
{
    [Fact]
    public void Constructor_PreservesOrder()
    {
        QuestionSet set = QuestionSet.Create(
            Probability("first"),
            Probability("second"),
            Probability("third"));

        Assert.Equal(3, set.Count);
        Assert.Equal("first", set[0].Id.Value);
        Assert.Equal("second", set[1].Id.Value);
        Assert.Equal("third", set[2].Id.Value);
    }

    [Fact]
    public void Constructor_RejectsDuplicateIdentifiersInsteadOfKeepingTheLastOne()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => QuestionSet.Create(
            Probability("frustration"),
            Probability("frustration")));

        Assert.Contains("frustration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsANullQuestion()
    {
        Assert.Throws<ArgumentException>(() => QuestionSet.Create(Probability("first"), null!));
    }

    [Fact]
    public void Constructor_RejectsANullSequence()
    {
        Assert.Throws<ArgumentNullException>(() => new QuestionSet(null!));
    }

    [Fact]
    public void HoldsQuestionsOfDifferentAnswerTypes()
    {
        QuestionSet set = QuestionSet.Create(
            new ProbabilityQuestion(new QuestionId("urgent"), "Is it urgent?"),
            new ScoreQuestion(new QuestionId("frustration"), "How frustrated?", 0, 10),
            new ChoiceQuestion<Department>(new QuestionId("routing"), "Which department?", [Department.Billing, Department.Sales]),
            new UnknownQuestion(new QuestionId("ranking"), "Rank them", "ranking"));

        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void TryGet_FindsAQuestionByIdentifier()
    {
        QuestionSet set = QuestionSet.Create(Probability("urgent"));

        Assert.True(set.TryGet(new QuestionId("urgent"), out Question? question));
        Assert.Equal("urgent", question.Id.Value);
        Assert.False(set.TryGet(new QuestionId("missing"), out _));
    }

    [Fact]
    public void Get_ThrowsWhenTheQuestionIsMissing()
    {
        QuestionSet set = QuestionSet.Create(Probability("urgent"));

        Assert.True(set.Contains(new QuestionId("urgent")));
        Assert.Throws<KeyNotFoundException>(() => set.Get(new QuestionId("missing")));
    }

    [Fact]
    public void Empty_ContainsNothing()
    {
        Assert.Empty(QuestionSet.Empty);
    }

    [Fact]
    public void IsEnumerableInOrder()
    {
        QuestionSet set = QuestionSet.Create(Probability("a"), Probability("b"));

        List<string> ids = [];

        foreach (Question question in set)
        {
            ids.Add(question.Id.Value);
        }

        Assert.Equal(["a", "b"], ids);
    }

    private static ProbabilityQuestion Probability(string id) => new(new QuestionId(id), $"Prompt for {id}");
}
