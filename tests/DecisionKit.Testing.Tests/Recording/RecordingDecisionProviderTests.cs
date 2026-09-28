using System;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Providers;
using DecisionKit.Testing.Recording;
using Microsoft.Extensions.Time.Testing;

namespace DecisionKit.Testing.Tests.Recording;

public sealed class RecordingDecisionProviderTests
{
    private static readonly ProbabilityQuestion s_question = new(new QuestionId("q"), "Prompt");

    [Fact]
    public async Task ItRecordsWithoutChangingTheAnswerAsync()
    {
        FakeDecisionProvider inner = new FakeDecisionProvider("inner").Returns(s_question, 0.6);
        RecordingDecisionProvider provider = new(inner);

        DecisionResult result = await provider.DecideAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(0.6, result.Get(s_question).Value.Value);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal("inner", provider.Name);
    }

    [Fact]
    public void ItReportsTheCapabilitiesOfTheProviderItWraps()
    {
        FakeDecisionProvider inner = new FakeDecisionProvider().Returns(s_question, 0.6);
        RecordingDecisionProvider provider = new(inner);

        Assert.Same(inner.Capabilities, provider.Capabilities);
    }

    [Fact]
    public async Task AFailedCallIsStillRecordedAsync()
    {
        FakeDecisionProvider inner = new FakeDecisionProvider()
            .Returns(s_question, 0.6)
            .Fails(DecisionErrorCategory.Timeout, "The provider timed out.");

        RecordingDecisionProvider provider = new(inner);

        await Assert.ThrowsAsync<DecisionTransientException>(
            () => provider.DecideAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task CallsAreTimestampedOnTheSuppliedClockAsync()
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        RecordingDecisionProvider provider = new(new DeterministicDecisionProvider())
        {
            TimeProvider = clock,
        };

        await provider.DecideAsync(Request(), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        await provider.DecideAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeOffset.UnixEpoch, provider.Calls[0].Timestamp);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), provider.Calls[1].Timestamp);
    }

    [Fact]
    public void ANullInnerProviderIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new RecordingDecisionProvider(null!));

    [Fact]
    public async Task ANullRequestIsRejectedAsync()
    {
        RecordingDecisionProvider provider = new(new DeterministicDecisionProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.DecideAsync(null!, TestContext.Current.CancellationToken));
    }

    private static DecisionRequest Request() => new(QuestionSet.Create(s_question));
}
