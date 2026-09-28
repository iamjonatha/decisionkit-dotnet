using System;
using System.Collections.Generic;
using DecisionKit.Answers;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Values;

public sealed class ChoiceOutcomeTests
{
    private static readonly ChoiceQuestion<Department> s_question = new(
        new QuestionId("routing"),
        "Which department should handle this?",
        [Department.Sales, Department.Billing]);

    [Fact]
    public void FromSelection_DescribesACommitment()
    {
        ChoiceOutcome outcome = ChoiceOutcome.FromSelection(Department.Billing);

        Assert.True(outcome.HasSelection);
        Assert.False(outcome.HasDistribution);
        Assert.Equal(Department.Billing, outcome.Selection);
    }

    [Fact]
    public void FromDistribution_DescribesSpreadBelief()
    {
        ChoiceOutcome outcome = ChoiceOutcome.FromDistribution(CreateDistribution());

        Assert.False(outcome.HasSelection);
        Assert.True(outcome.HasDistribution);
        Assert.Equal(2, outcome.Distribution.Count);
    }

    [Fact]
    public void Create_DescribesBoth()
    {
        ChoiceOutcome outcome = ChoiceOutcome.Create(CreateDistribution(), Department.Billing);

        Assert.True(outcome.HasSelection);
        Assert.True(outcome.HasDistribution);
    }

    [Fact]
    public void WithMetadata_CopiesTheCallersCollection()
    {
        Dictionary<string, object?> metadata = new(StringComparer.Ordinal) { ["source"] = "jev" };

        ChoiceOutcome outcome = ChoiceOutcome.FromSelection(Department.Sales).WithMetadata(metadata);
        metadata["source"] = "changed";

        Assert.Equal("jev", outcome.Metadata["source"]);
    }

    [Fact]
    public void CreateAnswer_BuildsTheTypedAnswerFromAnUntypedOutcome()
    {
        IChoiceQuestion question = s_question;

        Answer answer = question.CreateAnswer(ChoiceOutcome.Create(CreateDistribution(), Department.Billing));

        ChoiceAnswer<Department> typed = Assert.IsType<ChoiceAnswer<Department>>(answer);
        Assert.True(typed.Value.TryGetSelection(out Department selection));
        Assert.Equal(Department.Billing, selection);
        Assert.Equal(2, typed.Value.Distribution.Count);
    }

    [Fact]
    public void CreateAnswer_KeepsTheDistributionInsteadOfCollapsingIt()
    {
        IChoiceQuestion question = s_question;

        Answer answer = question.CreateAnswer(ChoiceOutcome.FromDistribution(CreateDistribution()));

        ChoiceAnswer<Department> typed = Assert.IsType<ChoiceAnswer<Department>>(answer);
        Assert.False(typed.Value.HasSelection);
        Assert.True(typed.Value.TryGetMostLikely(out OptionProbability<Department> mostLikely));
        Assert.Equal(Department.Billing, mostLikely.Option);
    }

    [Fact]
    public void CreateAnswer_CarriesTheOutcomeMetadata()
    {
        IChoiceQuestion question = s_question;

        Answer answer = question.CreateAnswer(
            ChoiceOutcome.FromSelection(Department.Sales)
                .WithMetadata(new Dictionary<string, object?> { ["explanation"] = "because" }));

        Assert.Equal("because", answer.Metadata["explanation"]);
    }

    [Fact]
    public void CreateAnswer_RejectsAnOptionOfTheWrongType()
    {
        IChoiceQuestion question = s_question;

        Assert.Throws<ArgumentException>(() => question.CreateAnswer(ChoiceOutcome.FromSelection("Billing")));
    }

    [Fact]
    public void CreateAnswer_RejectsAnOptionTheQuestionDoesNotOffer()
    {
        IChoiceQuestion question = s_question;

        Assert.Throws<ArgumentException>(() => question.CreateAnswer(ChoiceOutcome.FromSelection(Department.Technical)));
    }

    [Fact]
    public void CreateAnswer_RejectsADistributionOverOptionsTheQuestionDoesNotOffer()
    {
        IChoiceQuestion question = s_question;
        List<OptionProbability> distribution = [new OptionProbability(Department.Technical, new Probability(1))];

        Assert.Throws<ArgumentException>(() => question.CreateAnswer(ChoiceOutcome.FromDistribution(distribution)));
    }

    [Fact]
    public void FromDistribution_RejectsAnEmptyDistribution()
    {
        Assert.Throws<ArgumentException>(() => ChoiceOutcome.FromDistribution([]));
    }

    [Fact]
    public void Factories_RejectMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ChoiceOutcome.FromSelection(null!));
        Assert.Throws<ArgumentNullException>(() => ChoiceOutcome.FromDistribution(null!));
        Assert.Throws<ArgumentNullException>(() => ChoiceOutcome.Create(null!, Department.Sales));
        Assert.Throws<ArgumentNullException>(() => ChoiceOutcome.Create(CreateDistribution(), null!));
        Assert.Throws<ArgumentNullException>(() => ChoiceOutcome.FromSelection(Department.Sales).WithMetadata(null!));
        Assert.Throws<ArgumentNullException>(() => ((IChoiceQuestion)s_question).CreateAnswer((ChoiceOutcome)null!));
    }

    private static List<OptionProbability> CreateDistribution() =>
    [
        new OptionProbability(Department.Sales, new Probability(0.25)),
        new OptionProbability(Department.Billing, new Probability(0.75)),
    ];
}
