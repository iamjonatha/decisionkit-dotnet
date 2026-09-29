using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Providers;
using DecisionKit.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DecisionKit.Jev.Tests.Resilience;

/// <summary>
/// Exercises retrying through the provider, because the interesting part is not the policy in
/// isolation but what actually reaches the network and when.
/// </summary>
/// <remarks>
/// Nothing here sleeps. Every wait goes through a <see cref="FakeTimeProvider"/> that only moves
/// when a test moves it, so a backoff of an hour costs the suite nothing and a test that hangs
/// fails instead of passing slowly.
/// </remarks>
public sealed class JevRetryTests
{
    private const string Secret = "ts_live_do_not_log_this_value";

    [Fact]
    public async Task Decide_DoesNotRepeatAnythingByDefault()
    {
        using RetryFixture fixture = RetryFixture.From(JevProviderOptions.Default, _ => Unavailable());

        await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_RepeatsATransientFailureAndReturnsTheAnswer()
    {
        using RetryFixture fixture = RetryFixture.From(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(200), Jitter = 0d }),
            attempt => attempt == 1 ? Unavailable() : Answer());

        DecisionResult result = await fixture.DecideAsync();

        Assert.Equal(2, fixture.Attempts);
        Assert.Equal(JevProtocol.ProviderName, result.Metadata.ProviderName);
    }

    [Fact]
    public async Task Decide_DoesNotRepeatAFailureTheCallerHasToFixFirst()
    {
        using RetryFixture fixture = RetryFixture.From(
            Retrying(JevRetryPolicy.Standard),
            _ => Respond(HttpStatusCode.UnprocessableEntity));

        await Assert.ThrowsAsync<DecisionValidationException>(() => fixture.DecideAsync());

        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_StopsAtTheAttemptCeilingAndSaysHowManyItMade()
    {
        using RetryFixture fixture = RetryFixture.From(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(100), Jitter = 0d }),
            _ => Unavailable());

        DecisionException failure = await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.Equal(3, fixture.Attempts);
        Assert.Equal(3, Assert.IsType<int>(failure.Error.Properties[JevProtocol.RetryAttemptsProperty]));

        // The reported failure is still the one the service sent, not a summary that replaces it.
        Assert.Equal(503, Assert.IsType<int>(failure.Error.Properties[JevProtocol.StatusCodeProperty]));
    }

    [Fact]
    public async Task Decide_LeavesASingleFailureExactlyAsItWas()
    {
        using RetryFixture fixture = RetryFixture.From(JevProviderOptions.Default, _ => Unavailable());

        DecisionException failure = await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.DoesNotContain(JevProtocol.RetryAttemptsProperty, failure.Error.Properties.Keys);
    }

    [Fact]
    public async Task Decide_WaitsAsLongAsTheServiceAskedRatherThanAsLongAsTheBackoffSays()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(30),
            Jitter = 0d,
        };

        using RetryFixture fixture = RetryFixture.From(
            Retrying(policy),
            attempt => attempt == 1
                ? Respond(HttpStatusCode.TooManyRequests, (JevProtocol.RetryAfterMillisecondsHeader, "1500"))
                : Answer());

        DateTimeOffset started = fixture.Time.GetUtcNow();

        await fixture.DecideAsync();

        Assert.Equal(2, fixture.Attempts);
        Assert.InRange(fixture.Time.GetUtcNow() - started, TimeSpan.FromMilliseconds(1500), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Decide_AbandonsARetryItWouldHaveToWaitTooLongFor()
    {
        JevProviderOptions options = new()
        {
            RetryDelayTimeout = TimeSpan.FromSeconds(2),
            Retry = new JevRetryPolicy { MaxAttempts = 5 },
        };

        using RetryFixture fixture = RetryFixture.From(
            options,
            _ => Respond(HttpStatusCode.TooManyRequests, (JevProtocol.RetryAfterMillisecondsHeader, "600000")));

        await Assert.ThrowsAsync<DecisionTransientException>(() => fixture.DecideAsync());

        // Shortening the wait would ignore the instruction, so the retry is dropped instead.
        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_StopsRetryingOnceTheElapsedBudgetIsSpent()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 10,
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffFactor = 1d,
            Jitter = 0d,
            MaxElapsedTime = TimeSpan.FromMilliseconds(2500),
        };

        using RetryFixture fixture = RetryFixture.From(Retrying(policy), _ => Unavailable());

        DecisionException failure = await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.Equal(3, fixture.Attempts);
        Assert.Equal(3, Assert.IsType<int>(failure.Error.Properties[JevProtocol.RetryAttemptsProperty]));
    }

    [Fact]
    public async Task Decide_LeavesAFailureTheProviderSaidNothingAboutAlone()
    {
        using RetryFixture fixture = RetryFixture.From(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(100), Jitter = 0d }),
            _ => Respond(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<DecisionProviderException>(() => fixture.DecideAsync());

        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_RepeatsAnUnexplainedFailureWhenTheDeploymentAsksItTo()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            Jitter = 0d,
            RetryUnknownFailures = true,
        };

        using RetryFixture fixture = RetryFixture.From(
            Retrying(policy),
            attempt => attempt == 1 ? Respond(HttpStatusCode.InternalServerError) : Answer());

        await fixture.DecideAsync();

        Assert.Equal(2, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_RepeatsAStatusTheDeploymentDeclaredRetryable()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            Jitter = 0d,
            RetryableStatusCodes = new HashSet<int> { 422 },
        };

        using RetryFixture fixture = RetryFixture.From(
            Retrying(policy),
            attempt => attempt == 1 ? Respond(HttpStatusCode.UnprocessableEntity) : Answer());

        await fixture.DecideAsync();

        Assert.Equal(2, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_RefusesAStatusTheDeploymentDeclaredFinal()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            Jitter = 0d,
            NonRetryableStatusCodes = new HashSet<int> { 503 },
        };

        using RetryFixture fixture = RetryFixture.From(Retrying(policy), _ => Unavailable());

        await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_RepeatsACallThatNeverReachedTheService()
    {
        using RetryFixture fixture = RetryFixture.Throwing(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(100), Jitter = 0d }));

        await fixture.DecideAsync();

        Assert.Equal(2, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_MakesNoFurtherAttemptOnceTheCallerCancels()
    {
        using RetryFixture fixture = RetryFixture.From(
            Retrying(new JevRetryPolicy { MaxAttempts = 5, InitialDelay = TimeSpan.FromMinutes(1), Jitter = 0d }),
            _ => Unavailable());

        using CancellationTokenSource caller =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Task<DecisionResult> pending = fixture.Provider.DecideAsync(JevScenario.CreateRequest(), caller.Token);

        await YieldUntilAsync(() => fixture.Attempts == 1);

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(1, fixture.Attempts);
    }

    [Fact]
    public async Task Decide_ReportsTheOperationBudgetWhenTheRetryingOutlastsIt()
    {
        JevProviderOptions options = new()
        {
            OperationTimeout = TimeSpan.FromSeconds(1),
            RetryDelayTimeout = Timeout.InfiniteTimeSpan,
            Retry = new JevRetryPolicy
            {
                MaxAttempts = 10,
                InitialDelay = TimeSpan.FromMilliseconds(400),
                BackoffFactor = 1d,
                Jitter = 0d,
            },
        };

        using RetryFixture fixture = RetryFixture.From(options, _ => Unavailable());

        DecisionTransientException failure =
            await Assert.ThrowsAsync<DecisionTransientException>(() => fixture.DecideAsync());

        Assert.Equal(JevProtocol.TimeoutCode, failure.Error.Code);
        Assert.Equal("operation", failure.Error.Properties[JevProtocol.TimeoutScopeProperty]);
    }

    [Fact]
    public async Task Decide_ReportsEveryRetryAndWarnsOnceThatNothingIsDeduplicated()
    {
        RecordingLogger<JevDecisionProvider> logger = new();

        using RetryFixture fixture = RetryFixture.Logging(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(100), Jitter = 0d }),
            logger);

        await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.Equal(2, logger.Entries.Count(entry => entry.Contains(" 1006 ", StringComparison.Ordinal)));
        Assert.Single(logger.Entries, entry => entry.Contains(" 1007 ", StringComparison.Ordinal));

        // The warning states a property of the operation, not of an attempt, so it is written once.
        Assert.Single(logger.Entries, entry => entry.Contains(" 1008 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Decide_NeverWritesTheCredentialWhileRetrying()
    {
        RecordingLogger<JevDecisionProvider> logger = new();

        using RetryFixture fixture = RetryFixture.Logging(
            Retrying(new JevRetryPolicy { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(100), Jitter = 0d }),
            logger);

        await Assert.ThrowsAnyAsync<DecisionException>(() => fixture.DecideAsync());

        Assert.DoesNotContain(logger.Entries, entry => entry.Contains(Secret, StringComparison.Ordinal));
    }

    private static JevProviderOptions Retrying(JevRetryPolicy policy) => new() { Retry = policy };

    private static HttpResponseMessage Answer() =>
        StubHttpMessageHandler.Respond(HttpStatusCode.OK, RecordedPayload.Read("response.json"));

    private static HttpResponseMessage Unavailable() => Respond(HttpStatusCode.ServiceUnavailable);

    private static HttpResponseMessage Respond(HttpStatusCode status, params (string Name, string Value)[] headers) =>
        StubHttpMessageHandler.Respond(status, RecordedPayload.Read("error.json"), headers);

    /// <summary>
    /// Lets the operation run without letting any time pass, which is how a test reaches the point
    /// just before a backoff without accidentally completing it.
    /// </summary>
    private static async Task YieldUntilAsync(Func<bool> condition)
    {
        for (int step = 0; step < 10_000 && !condition(); step++)
        {
            await Task.Yield();
        }
    }

    private sealed class RetryFixture : IDisposable
    {
        private const int QuietTurns = 4;

        private const int MaxTurns = 512;

        private static readonly TimeSpan s_step = TimeSpan.FromMilliseconds(25);

        private static readonly TimeSpan s_budget = TimeSpan.FromSeconds(60);

        private readonly HttpClient _http;

        private RetryFixture(
            StubHttpMessageHandler handler,
            JevProviderOptions options,
            ILogger<JevDecisionProvider> logger)
        {
            Handler = handler;
            Time = new FakeTimeProvider();
            _http = new HttpClient(handler, disposeHandler: false);
            Provider = new JevDecisionProvider(_http, new StaticJevCredentialProvider(Secret), options, logger, Time);
        }

        public StubHttpMessageHandler Handler { get; }

        public FakeTimeProvider Time { get; }

        public JevDecisionProvider Provider { get; }

        public int Attempts => Handler.Requests.Count;

        public static RetryFixture From(JevProviderOptions options, Func<int, HttpResponseMessage> responses) =>
            new(Scripted(responses), options, NullLogger<JevDecisionProvider>.Instance);

        public static RetryFixture Logging(JevProviderOptions options, ILogger<JevDecisionProvider> logger) =>
            new(Scripted(_ => Unavailable()), options, logger);

        /// <summary>
        /// Fails the first attempt the way an unreachable service does, then answers.
        /// </summary>
        public static RetryFixture Throwing(JevProviderOptions options)
        {
            int attempts = 0;

            StubHttpMessageHandler handler = new((_, _) => Interlocked.Increment(ref attempts) == 1
                ? throw new HttpRequestException("The name could not be resolved.")
                : Task.FromResult(Answer()));

            return new RetryFixture(handler, options, NullLogger<JevDecisionProvider>.Instance);
        }

        /// <summary>
        /// Runs a decision to completion, moving the clock forward in small steps until it settles.
        /// </summary>
        /// <remarks>
        /// The clock is only ever nudged, never jumped to a deadline, so a test cannot accidentally
        /// skip past a budget it meant to stay inside. The virtual budget is bounded so that a
        /// deadlock fails the test rather than hanging the suite.
        /// </remarks>
        public async Task<DecisionResult> DecideAsync()
        {
            Task<DecisionResult> pending =
                Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);

            for (TimeSpan advanced = TimeSpan.Zero; advanced < s_budget; advanced += s_step)
            {
                if (await SettlesAsync(pending))
                {
                    return await pending;
                }

                Time.Advance(s_step);
            }

            Assert.True(
                await SettlesAsync(pending),
                "The decision never settled, so something is waiting on a clock that is not the fake one.");

            return await pending;
        }

        /// <summary>
        /// Gives the decision every chance to move before the clock is allowed to.
        /// </summary>
        /// <remarks>
        /// A single yield is not enough. The pump would outrun the provider, advance the fake clock
        /// past the moment at which the provider later registers its delay, and leave behind a timer
        /// that can never fire — which is indistinguishable from a deadlock. Moving the clock is only
        /// safe once the decision has settled, or once the thread pool has nothing left to run and the
        /// decision is therefore waiting on the clock and on nothing else.
        /// </remarks>
        private static async Task<bool> SettlesAsync(Task pending)
        {
            int quiet = 0;

            for (int turn = 0; turn < MaxTurns && quiet < QuietTurns; turn++)
            {
                if (pending.IsCompleted)
                {
                    return true;
                }

                await Task.Yield();

                quiet = ThreadPool.PendingWorkItemCount == 0 ? quiet + 1 : 0;
            }

            return pending.IsCompleted;
        }

        public void Dispose()
        {
            _http.Dispose();
            Handler.Dispose();
        }

        private static StubHttpMessageHandler Scripted(Func<int, HttpResponseMessage> responses)
        {
            int attempts = 0;

            return new StubHttpMessageHandler((_, _) => Task.FromResult(responses(Interlocked.Increment(ref attempts))));
        }
    }
}
