using System;
using System.Threading;
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
using Microsoft.Extensions.Time.Testing;

namespace DecisionKit.Testing.Tests.Providers;

public sealed class FakeDecisionProviderTests
{
    private static readonly ProbabilityQuestion s_frustration = new(new QuestionId("frustration"), "Is the customer frustrated?");
    private static readonly ScoreQuestion s_urgency = new(new QuestionId("urgency"), "How urgent is this?", 0, 10);

    [Fact]
    public async Task AConfiguredAnswerIsReturnedAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.8);

        DecisionResult result = await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.Equal(0.8, result.Get(s_frustration).Value.Value);
        Assert.Equal("fake", result.Metadata.ProviderName);
    }

    [Fact]
    public async Task AScoreIsConfiguredOnTheQuestionsOwnScaleAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_urgency, 7.5);

        DecisionResult result = await provider.DecideAsync(Request(s_urgency), TestContext.Current.CancellationToken);

        Score score = result.Get(s_urgency).Value;

        Assert.Equal(7.5, score.Value);
        Assert.Equal(0, score.Minimum);
        Assert.Equal(10, score.Maximum);
    }

    [Fact]
    public async Task AChoiceIsConfiguredByOptionAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(TicketRouter.RoutingQuestion, Department.Technical);

        DecisionResult result = await provider.DecideAsync(
            Request(TicketRouter.RoutingQuestion),
            TestContext.Current.CancellationToken);

        Assert.Equal(Department.Technical, result.Get(TicketRouter.RoutingQuestion).Value.Selection);
    }

    [Fact]
    public async Task AChoiceCanAlsoBeConfiguredAsADistributionAsync()
    {
        Choice<Department> distribution = Choice.Distributed<Department>(
        [
            new OptionProbability<Department>(Department.Billing, new Probability(0.7)),
            new OptionProbability<Department>(Department.Sales, new Probability(0.3)),
        ]);

        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(TicketRouter.RoutingQuestion, distribution);

        DecisionResult result = await provider.DecideAsync(
            Request(TicketRouter.RoutingQuestion),
            TestContext.Current.CancellationToken);

        Assert.False(result.Get(TicketRouter.RoutingQuestion).Value.HasSelection);
    }

    [Fact]
    public async Task AnUnconfiguredQuestionIsReportedRatherThanSilentlyOmittedAsync()
    {
        FakeDecisionProvider provider = new();

        DecisionValidationException exception = await Assert.ThrowsAsync<DecisionValidationException>(
            () => provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken));

        Assert.Equal("fake_answer_not_configured", exception.Error.Code);
        Assert.Equal("fake", exception.Error.ProviderName);
    }

    [Fact]
    public async Task AQuestionCanBeLeftDeliberatelyUnansweredAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(s_frustration, 0.2)
            .ReturnsNothing(s_urgency);

        DecisionResult result = await provider.DecideAsync(
            Request(s_frustration, s_urgency),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.False(result.Contains(s_urgency.Id));
    }

    [Fact]
    public async Task ConfiguringAnAnswerCancelsAPreviousOmissionAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .ReturnsNothing(s_frustration)
            .Returns(s_frustration, 0.4);

        DecisionResult result = await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.Equal(0.4, result.Get(s_frustration).Value.Value);
    }

    [Fact]
    public void AnAnswerForAnotherQuestionIsRejected()
    {
        FakeDecisionProvider provider = new();
        ProbabilityAnswer foreign = new(s_urgency.Id, new Probability(0.5));

        Assert.Throws<ArgumentException>(() => provider.Returns(s_frustration, foreign));
    }

    [Fact]
    public async Task AnInjectedErrorIsDeliveredAsTheMatchingExceptionAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(s_frustration, 0.5)
            .Fails(new DecisionError(DecisionErrorCategory.RateLimit, "Too many requests.")
            {
                Retry = DecisionRetryHint.After(TimeSpan.FromSeconds(5)),
            });

        DecisionTransientException exception = await Assert.ThrowsAsync<DecisionTransientException>(
            () => provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(5), exception.Retry.RetryAfter);
    }

    [Fact]
    public async Task AFailureBudgetLetsARetryPolicyBeTestedAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(s_frustration, 0.9)
            .FailsTimes(2, new DecisionError(DecisionErrorCategory.Timeout, "The provider timed out."));

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<DecisionTransientException>(
                () => provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken));
        }

        DecisionResult result = await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.Equal(0.9, result.Get(s_frustration).Value.Value);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void CancellationIsNotAnInjectableFailure()
    {
        FakeDecisionProvider provider = new();

        Assert.Throws<ArgumentException>(
            () => provider.Fails(new DecisionError(DecisionErrorCategory.Canceled, "Cancelled.")));
    }

    [Fact]
    public async Task SucceedsCancelsAConfiguredFailureAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(s_frustration, 0.1)
            .Fails(DecisionErrorCategory.Transport, "The socket was reset.")
            .Succeeds();

        DecisionResult result = await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.Equal(0.1, result.Get(s_frustration).Value.Value);
    }

    [Fact]
    public async Task SimulatedLatencyRunsOnTheSuppliedClockAsync()
    {
        FakeTimeProvider clock = new();
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.3);
        provider.TimeProvider = clock;
        provider.Latency = TimeSpan.FromSeconds(30);

        Task<DecisionResult> pending = provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(30));
        DecisionResult result = await pending;

        Assert.Equal(TimeSpan.FromSeconds(30), result.Metadata.Latency);
    }

    [Fact]
    public async Task ACallCancelledWhileWaitingThrowsOperationCanceledExceptionAsync()
    {
        FakeTimeProvider clock = new();
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.3);
        provider.TimeProvider = clock;
        provider.Latency = TimeSpan.FromSeconds(30);

        using CancellationTokenSource cancellation = new();
        Task<DecisionResult> pending = provider.DecideAsync(Request(s_frustration), cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task ACallCancelledBeforeItStartsIsNotRecordedAsync()
    {
        FakeDecisionProvider provider = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.DecideAsync(Request(s_frustration), cancellation.Token));

        Assert.True(provider.Calls.IsEmpty);
    }

    [Fact]
    public async Task EveryRequestIsCapturedInOrderAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.5);
        RequestId first = RequestId.New();
        RequestId second = RequestId.New();

        await provider.DecideAsync(
            new DecisionRequest(QuestionSet.Create(s_frustration)) { ClientRequestId = first },
            TestContext.Current.CancellationToken);

        await provider.DecideAsync(
            new DecisionRequest(QuestionSet.Create(s_frustration)) { ClientRequestId = second },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, provider.CallCount);
        Assert.Equal(first, provider.Calls[0].Request.ClientRequestId);
        Assert.Equal(second, provider.LastRequest!.ClientRequestId);
        Assert.Equal(0, provider.Calls[0].Ordinal);
        Assert.Equal(1, provider.Calls[1].Ordinal);
    }

    [Fact]
    public void CapabilitiesReportTheQuestionTypesTheFakeWasTaught()
    {
        FakeDecisionProvider provider = new();

        Assert.False(provider.Capabilities.Supports<ProbabilityQuestion>());

        provider.Returns(s_frustration, 0.5);

        Assert.True(provider.Capabilities.Supports<ProbabilityQuestion>());
        Assert.False(provider.Capabilities.Supports<ScoreQuestion>());
    }

    [Fact]
    public void ExplicitCapabilitiesAreNotOverwrittenByConfiguration()
    {
        FakeDecisionProvider provider = new()
        {
            Capabilities = new DecisionProviderCapabilities { SupportsIdempotencyKeys = false },
        };

        provider.Returns(s_frustration, 0.5);

        Assert.False(provider.Capabilities.Supports<ProbabilityQuestion>());
        Assert.False(provider.Capabilities.SupportsIdempotencyKeys);
    }

    [Fact]
    public async Task ConfiguredUsageIsReportedOnTheResultAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.5);
        provider.Usage = new DecisionUsage([new(DecisionUsageKeys.InputTokens, 42)]);

        DecisionResult result = await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        Assert.True(result.Usage.TryGetMetric(DecisionUsageKeys.InputTokens, out double tokens));
        Assert.Equal(42, tokens);
    }

    [Fact]
    public async Task TheClientRequestIdentifierReachesTheResultMetadataAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.5);
        provider.ProviderRequestId = "provider-123";
        RequestId clientRequestId = RequestId.New();

        DecisionResult result = await provider.DecideAsync(
            new DecisionRequest(QuestionSet.Create(s_frustration)) { ClientRequestId = clientRequestId },
            TestContext.Current.CancellationToken);

        Assert.Equal(clientRequestId, result.Metadata.ClientRequestId);
        Assert.Equal("provider-123", result.Metadata.ProviderRequestId);
    }

    [Fact]
    public async Task ResetForgetsTheArrangedScenarioAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(s_frustration, 0.5);
        await provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken);

        provider.Reset();

        Assert.Equal(0, provider.CallCount);
        Assert.Null(provider.LastRequest);
        await Assert.ThrowsAsync<DecisionValidationException>(
            () => provider.DecideAsync(Request(s_frustration), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANullRequestIsRejectedAsync()
    {
        FakeDecisionProvider provider = new();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.DecideAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ANegativeLatencyIsRejected()
    {
        FakeDecisionProvider provider = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Latency = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void AnEmptyNameIsRejected() => Assert.Throws<ArgumentException>(() => new FakeDecisionProvider(" "));

    private static DecisionRequest Request(params Question[] questions) => new(QuestionSet.Create(questions));
}
