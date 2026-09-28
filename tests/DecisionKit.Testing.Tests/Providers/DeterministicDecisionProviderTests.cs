using System;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Providers;
using DecisionKit.Testing.Tests.Fixtures;
using DecisionKit.Values;

namespace DecisionKit.Testing.Tests.Providers;

public sealed class DeterministicDecisionProviderTests
{
    private static readonly ProbabilityQuestion s_frustration = new(new QuestionId("frustration"), "Is the customer frustrated?");
    private static readonly ScoreQuestion s_urgency = new(new QuestionId("urgency"), "How urgent is this?", 0, 10);

    [Fact]
    public async Task TheSameRequestAlwaysProducesTheSameAnswersAsync()
    {
        DeterministicDecisionProvider provider = new();
        DecisionRequest request = Request("A duplicate charge.", s_frustration, s_urgency);

        DecisionResult first = await provider.DecideAsync(request, TestContext.Current.CancellationToken);
        DecisionResult second = await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(first.Get(s_frustration).Value, second.Get(s_frustration).Value);
        Assert.Equal(first.Get(s_urgency).Value, second.Get(s_urgency).Value);
        Assert.Equal(first.Metadata.ProviderRequestId, second.Metadata.ProviderRequestId);
    }

    [Fact]
    public async Task TwoInstancesAgreeAsync()
    {
        DecisionRequest request = Request("A duplicate charge.", s_frustration);

        DecisionResult first = await new DeterministicDecisionProvider().DecideAsync(request, TestContext.Current.CancellationToken);
        DecisionResult second = await new DeterministicDecisionProvider().DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(first.Get(s_frustration).Value, second.Get(s_frustration).Value);
    }

    [Fact]
    public async Task TheSeedShiftsEveryAnswerAsync()
    {
        DecisionRequest request = Request("A duplicate charge.", s_frustration);

        DecisionResult unseeded = await new DeterministicDecisionProvider()
            .DecideAsync(request, TestContext.Current.CancellationToken);

        DecisionResult seeded = await new DeterministicDecisionProvider { Seed = 42 }
            .DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(unseeded.Get(s_frustration).Value, seeded.Get(s_frustration).Value);
    }

    [Fact]
    public async Task TheInputChangesTheAnswerAsync()
    {
        DeterministicDecisionProvider provider = new();

        DecisionResult billing = await provider.DecideAsync(
            Request("I was charged twice.", s_frustration),
            TestContext.Current.CancellationToken);

        DecisionResult crash = await provider.DecideAsync(
            Request("The application crashes.", s_frustration),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(billing.Get(s_frustration).Value, crash.Get(s_frustration).Value);
    }

    [Fact]
    public async Task AScoreStaysOnItsQuestionsScaleAsync()
    {
        DeterministicDecisionProvider provider = new();

        for (int index = 0; index < 50; index++)
        {
            ScoreQuestion question = new(new QuestionId($"urgency-{index}"), "How urgent is this?", -5, 5);

            Score score = (await provider.DecideAsync(Request("A ticket.", question), TestContext.Current.CancellationToken))
                .Get(question)
                .Value;

            Assert.InRange(score.Value, -5, 5);
            Assert.Equal(-5, score.Minimum);
            Assert.Equal(5, score.Maximum);
        }
    }

    [Fact]
    public async Task AProbabilityStaysInItsIntervalAsync()
    {
        DeterministicDecisionProvider provider = new();

        for (int index = 0; index < 50; index++)
        {
            ProbabilityQuestion question = new(new QuestionId($"likely-{index}"), "How likely is this?");

            Probability probability = (await provider.DecideAsync(Request("A ticket.", question), TestContext.Current.CancellationToken))
                .Get(question)
                .Value;

            Assert.InRange(probability.Value, 0.0, 1.0);
        }
    }

    [Fact]
    public async Task AChoiceIsAnsweredWithAnOfferedOptionAsync()
    {
        DeterministicDecisionProvider provider = new();

        DecisionResult result = await provider.DecideAsync(
            Request("A ticket.", TicketRouter.RoutingQuestion),
            TestContext.Current.CancellationToken);

        ChoiceAnswer<Department> answer = result.Get(TicketRouter.RoutingQuestion);

        Assert.True(answer.Value.HasSelection);
        Assert.Contains(answer.Value.Selection, TicketRouter.RoutingQuestion.Options);
    }

    [Fact]
    public async Task AnUnknownQuestionComesBackAsAnUnknownAnswerAsync()
    {
        UnknownQuestion question = new(new QuestionId("novel"), "Something new.", "vendor.novel");

        DecisionResult result = await new DeterministicDecisionProvider()
            .DecideAsync(Request("A ticket.", question), TestContext.Current.CancellationToken);

        UnknownAnswer answer = result.Get(question);

        Assert.Equal("vendor.novel", answer.ProviderType);
    }

    [Fact]
    public async Task AQuestionTypeItCannotDeriveIsReportedAsAValidationFailureAsync()
    {
        DeterministicDecisionProvider provider = new();

        DecisionValidationException exception = await Assert.ThrowsAsync<DecisionValidationException>(
            () => provider.DecideAsync(Request("A ticket.", new CustomQuestion()), TestContext.Current.CancellationToken));

        Assert.Equal("unsupported_question_type", exception.Error.Code);
        Assert.Equal("deterministic", exception.Error.ProviderName);
    }

    [Fact]
    public void ItDeclaresEveryClosedFormOfAChoiceQuestion()
    {
        DeterministicDecisionProvider provider = new();

        Assert.True(provider.Capabilities.Supports<ChoiceQuestion<Department>>());
        Assert.True(provider.Capabilities.Supports<ChoiceQuestion<string>>());
        Assert.True(provider.Capabilities.Supports<ProbabilityQuestion>());
    }

    [Fact]
    public async Task TheTimestampIsFixedSoThatASnapshotMatchesTwiceAsync()
    {
        DeterministicDecisionProvider provider = new() { Timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z", null) };

        DecisionResult result = await provider.DecideAsync(
            Request("A ticket.", s_frustration),
            TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z", null), result.Metadata.Timestamp);
    }

    [Fact]
    public async Task ANullRequestIsRejectedAsync()
    {
        DeterministicDecisionProvider provider = new();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.DecideAsync(null!, TestContext.Current.CancellationToken));
    }

    private static DecisionRequest Request(string input, params Question[] questions) =>
        new(QuestionSet.Create(questions)) { Input = DecisionInput.FromText(input) };

    private sealed class CustomQuestion : Question<ProbabilityAnswer>
    {
        public CustomQuestion()
            : base(new QuestionId("custom"), "A question type DecisionKit does not model.")
        {
        }
    }
}
