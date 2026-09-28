using System;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Assertions;
using DecisionKit.Testing.Providers;
using DecisionKit.Testing.Recording;
using DecisionKit.Testing.Tests.Fixtures;
using DecisionKit.Values;

namespace DecisionKit.Testing.Tests.Assertions;

public sealed class DecisionAssertTests
{
    private static readonly ProbabilityQuestion s_frustration = new(new QuestionId("frustration"), "Is the customer frustrated?");
    private static readonly ScoreQuestion s_urgency = new(new QuestionId("urgency"), "How urgent is this?", 0, 10);

    [Fact]
    public void AnsweredReturnsTheAnswer()
    {
        DecisionResult result = Result(new ProbabilityAnswer(s_frustration.Id, new Probability(0.8)));

        Assert.Equal(0.8, DecisionAssert.Answered(result, s_frustration).Value.Value);
    }

    [Fact]
    public void AnsweredNamesTheMissingQuestion()
    {
        DecisionResult result = Result();

        DecisionAssertionException exception = Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.Answered(result, s_frustration));

        Assert.Contains("frustration", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nothing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotAnsweredHoldsWhenTheQuestionWasSkipped()
    {
        DecisionResult result = Result();

        DecisionAssert.NotAnswered(result, s_frustration);

        Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.NotAnswered(Result(new ProbabilityAnswer(s_frustration.Id, new Probability(0.1))), s_frustration));
    }

    [Fact]
    public void HasProbabilityComparesWithinATolerance()
    {
        DecisionResult result = Result(new ProbabilityAnswer(s_frustration.Id, new Probability(0.8)));

        DecisionAssert.HasProbability(result, s_frustration, 0.8);
        DecisionAssert.HasProbability(result, s_frustration, 0.81, 0.02);

        Assert.Throws<DecisionAssertionException>(() => DecisionAssert.HasProbability(result, s_frustration, 0.5));
    }

    [Fact]
    public void HasScoreComparesWithinATolerance()
    {
        DecisionResult result = Result(new ScoreAnswer(s_urgency.Id, s_urgency.CreateScore(7)));

        DecisionAssert.HasScore(result, s_urgency, 7);
        DecisionAssert.HasScore(result, s_urgency, 7.05, 0.1);

        DecisionAssertionException exception = Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.HasScore(result, s_urgency, 2));

        Assert.Contains("urgency", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasSelectionComparesTheSelectedOption()
    {
        DecisionResult result = Result(TicketRouter.RoutingQuestion.CreateAnswer(Department.Sales));

        DecisionAssert.HasSelection(result, TicketRouter.RoutingQuestion, Department.Sales);

        Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.HasSelection(result, TicketRouter.RoutingQuestion, Department.Billing));
    }

    [Fact]
    public void HasSelectionExplainsADistributionWithNoSelection()
    {
        Choice<Department> distribution = Choice.Distributed<Department>(
        [
            new OptionProbability<Department>(Department.Billing, new Probability(0.6)),
        ]);

        DecisionResult result = Result(new ChoiceAnswer<Department>(TicketRouter.RoutingQuestion.Id, distribution));

        DecisionAssertionException exception = Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.HasSelection(result, TicketRouter.RoutingQuestion, Department.Billing));

        Assert.Contains("distribution", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasAnswerCountComparesTheNumberOfAnswers()
    {
        DecisionResult result = Result(new ProbabilityAnswer(s_frustration.Id, new Probability(0.8)));

        DecisionAssert.HasAnswerCount(result, 1);

        Assert.Throws<DecisionAssertionException>(() => DecisionAssert.HasAnswerCount(result, 2));
    }

    [Fact]
    public void AskedFindsTheQuestion()
    {
        DecisionRequest request = new(QuestionSet.Create(s_frustration, s_urgency));

        Assert.Same(s_urgency, DecisionAssert.Asked(request, "urgency"));

        DecisionAssertionException exception = Assert.Throws<DecisionAssertionException>(
            () => DecisionAssert.Asked(request, "routing"));

        Assert.Contains("frustration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AskedExactlyComparesContentAndOrder()
    {
        DecisionRequest request = new(QuestionSet.Create(s_frustration, s_urgency));

        DecisionAssert.AskedExactly(request, "frustration", "urgency");

        Assert.Throws<DecisionAssertionException>(() => DecisionAssert.AskedExactly(request, "urgency", "frustration"));
        Assert.Throws<DecisionAssertionException>(() => DecisionAssert.AskedExactly(request, "frustration"));
    }

    [Fact]
    public async Task CalledOnceReturnsTheOnlyCallAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.5);
        await provider.DecideAsync(new DecisionRequest(QuestionSet.Create(s_frustration)), TestContext.Current.CancellationToken);

        RecordedDecisionCall call = DecisionAssert.CalledOnce(provider.Calls);

        Assert.Equal(0, call.Ordinal);
    }

    [Fact]
    public void CalledTimesCountsTheCalls()
    {
        DecisionCallLog log = new();

        DecisionAssert.CalledTimes(log, 0);

        log.Record(new DecisionRequest(QuestionSet.Create(s_frustration)), DateTimeOffset.UnixEpoch);

        DecisionAssertionException exception = Assert.Throws<DecisionAssertionException>(() => DecisionAssert.CalledTimes(log, 3));

        Assert.Contains("3 provider call(s)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionAssert.Answered(null!, s_frustration));
        Assert.Throws<ArgumentNullException>(() => DecisionAssert.HasAnswerCount(null!, 0));
        Assert.Throws<ArgumentNullException>(() => DecisionAssert.CalledTimes(null!, 0));
    }

    [Fact]
    public void ANegativeToleranceIsRejected()
    {
        DecisionResult result = Result(new ProbabilityAnswer(s_frustration.Id, new Probability(0.8)));

        Assert.Throws<ArgumentOutOfRangeException>(() => DecisionAssert.HasProbability(result, s_frustration, 0.8, -1));
    }

    private static DecisionResult Result(params Answer[] answers) =>
        new(answers, new DecisionMetadata("fake", DateTimeOffset.UnixEpoch));
}
