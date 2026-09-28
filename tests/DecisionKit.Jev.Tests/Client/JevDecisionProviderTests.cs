using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Questions;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DecisionKit.Jev.Tests.Client;

public sealed class JevDecisionProviderTests
{
    private const string Secret = "ts_live_do_not_log_this_value";

    [Fact]
    public void Constructor_RejectsEveryMissingCollaborator()
    {
        using HttpClient http = new();
        StaticJevCredentialProvider credentials = new(Secret);

        Assert.Throws<ArgumentNullException>(() => new JevDecisionProvider(null!, credentials));
        Assert.Throws<ArgumentNullException>(() => new JevDecisionProvider(http, null!));
        Assert.Throws<ArgumentNullException>(() => new JevDecisionProvider(http, credentials, null!));
        Assert.Throws<ArgumentNullException>(
            () => new JevDecisionProvider(http, credentials, JevProviderOptions.Default, null!));
        Assert.Throws<ArgumentNullException>(() => new JevDecisionProvider(
            http,
            credentials,
            JevProviderOptions.Default,
            NullLogger<JevDecisionProvider>.Instance,
            null!));
    }

    [Fact]
    public void Name_IsTheConfiguredProviderName()
    {
        using JevFixture fixture = JevFixture.Answering();

        Assert.Equal(JevProtocol.ProviderName, fixture.Provider.Name);
    }

    [Fact]
    public void Capabilities_DeclareTheQuestionsJevCanBeAsked()
    {
        using JevFixture fixture = JevFixture.Answering();
        DecisionProviderCapabilities capabilities = fixture.Provider.Capabilities;

        Assert.True(capabilities.Supports<ProbabilityQuestion>());
        Assert.True(capabilities.Supports<ChoiceQuestion<Department>>());
        Assert.True(capabilities.Supports<JevScoreQuestion>());

        // A bare numeric range has no JEV form: the protocol requires a rubric.
        Assert.False(capabilities.Supports<ScoreQuestion>());
    }

    [Fact]
    public void Capabilities_DeclareWhatTheProtocolCannotCarry()
    {
        using JevFixture fixture = JevFixture.Answering();
        DecisionProviderCapabilities capabilities = fixture.Provider.Capabilities;

        Assert.False(capabilities.SupportsIdempotencyKeys);
        Assert.False(capabilities.SupportsRequestMetadata);
        Assert.False(capabilities.SupportsExplanations);
        Assert.True(capabilities.SupportsBatching);
        Assert.True(capabilities.SupportsUsageReporting);
    }

    [Fact]
    public async Task DecideAsync_PostsTheRequestToTheEvaluationEndpoint()
    {
        using JevFixture fixture = JevFixture.Answering();

        await fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);

        StubHttpRequest sent = fixture.Handler.LastRequest;

        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", sent.Uri.AbsoluteUri);
        Assert.Equal("application/json", sent.ContentType);
        Assert.Contains("\"questions\"", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_AuthenticatesWithABearerToken()
    {
        using JevFixture fixture = JevFixture.Answering();

        await fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);

        StubHttpRequest sent = fixture.Handler.LastRequest;

        Assert.Equal(JevProtocol.AuthenticationScheme, sent.AuthorizationScheme);
        Assert.Equal(Secret, sent.AuthorizationParameter);
    }

    [Fact]
    public async Task DecideAsync_AsksForTheKeyOnEveryCall()
    {
        int lookups = 0;
        using JevFixture fixture = JevFixture.AnsweringWith(new DelegateJevCredentialProvider(() =>
        {
            Interlocked.Increment(ref lookups);

            return new JevApiKey(Secret);
        }));

        await fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);
        await fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(2, lookups);
    }

    [Fact]
    public async Task DecideAsync_ProducesATypedResult()
    {
        using JevFixture fixture = JevFixture.Answering();

        DecisionResult result = await fixture.Provider.DecideAsync(
            JevScenario.CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new Probability(0.97), result.Get(JevScenario.UrgentQuestion).Value);
        Assert.Equal(Department.Billing, result.Get(JevScenario.TeamQuestion).Value.Selection);
        Assert.Equal(1.42, result.Get(JevScenario.FrustrationQuestion).Value.Value, 6);
        Assert.Equal("jev-1.13.0", result.Metadata.ModelVersion);
    }

    [Fact]
    public async Task DecideAsync_ReportsWhatTheCallCost()
    {
        using JevFixture fixture = JevFixture.Answering();

        DecisionResult result = await fixture.Provider.DecideAsync(
            JevScenario.CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(392, result.Usage.Metrics[DecisionUsageKeys.InputTokens]);
        Assert.Equal(65, result.Usage.Metrics[DecisionUsageKeys.OutputTokens]);
    }

    [Fact]
    public async Task DecideAsync_RecordsTheIdentifierTheServiceStampedOnTheCall()
    {
        using JevFixture fixture = JevFixture.AnsweringWithHeaders((JevProtocol.RequestIdHeader, "req_2c9f"));

        DecisionResult result = await fixture.Provider.DecideAsync(
            JevScenario.CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("req_2c9f", result.Metadata.ProviderRequestId);
    }

    [Fact]
    public async Task DecideAsync_RefusesARequestItCannotExpressWithoutSendingAnything()
    {
        using JevFixture fixture = JevFixture.Answering();

        DecisionRequest request = new(JevScenario.CreateQuestions())
        {
            Input = DecisionInput.FromText("I was charged twice."),
            Options = new DecisionOptions { Language = "en-GB" },
        };

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => fixture.Provider.DecideAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(JevProtocol.UnsupportedOptionCode, error.Error.Code);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task DecideAsync_ReportsAMissingCredentialAsAnAuthenticationFailure()
    {
        using JevFixture fixture = JevFixture.AnsweringWith(new DelegateJevCredentialProvider(() => JevApiKey.None));

        DecisionException error = await Assert.ThrowsAsync<DecisionAuthenticationException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(JevProtocol.CredentialMissingCode, error.Error.Code);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(401, DecisionErrorCategory.Authentication)]
    [InlineData(403, DecisionErrorCategory.Authorization)]
    [InlineData(422, DecisionErrorCategory.Validation)]
    [InlineData(429, DecisionErrorCategory.RateLimit)]
    [InlineData(529, DecisionErrorCategory.ProviderError)]
    public async Task DecideAsync_ClassifiesAFailureFromItsStatusCode(int statusCode, DecisionErrorCategory expected)
    {
        using JevFixture fixture = JevFixture.Failing((HttpStatusCode)statusCode, RecordedPayload.Read("error.json"));

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(expected, error.Category);
    }

    [Fact]
    public async Task DecideAsync_CarriesTheRetryDelayTheServiceAskedFor()
    {
        using JevFixture fixture = JevFixture.FailingWithHeaders(
            HttpStatusCode.TooManyRequests,
            RecordedPayload.Read("error.json"),
            (JevProtocol.RetryAfterMillisecondsHeader, "1500"));

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromMilliseconds(1500), error.Retry.RetryAfter);
    }

    [Fact]
    public async Task DecideAsync_ReportsTheStatusEvenWhenTheErrorBodyIsNotJev()
    {
        using JevFixture fixture = JevFixture.Failing(
            HttpStatusCode.ServiceUnavailable,
            "<html><body>503 Service Unavailable</body></html>");

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        // A gateway's HTML must not replace the outage with a parse error.
        Assert.Equal(DecisionErrorCategory.ProviderError, error.Category);
        Assert.NotEqual(JevProtocol.PayloadUnreadableCode, error.Error.Code);
    }

    [Fact]
    public async Task DecideAsync_ReportsAnUnreadableSuccessBodyAsASerializationFailure()
    {
        using JevFixture fixture = JevFixture.AnsweringBody("{ this is not json");

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(DecisionErrorCategory.Serialization, error.Category);
        Assert.Equal(JevProtocol.PayloadUnreadableCode, error.Error.Code);
    }

    [Fact]
    public async Task DecideAsync_ReportsAnUnreachableServiceAsARetryableTransportFailure()
    {
        using JevFixture fixture = JevFixture.From(StubHttpMessageHandler.Unreachable());

        DecisionException error = await Assert.ThrowsAsync<DecisionTransientException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(DecisionErrorCategory.Transport, error.Category);
        Assert.Equal(JevProtocol.TransportFailureCode, error.Error.Code);
        Assert.Equal(DecisionRetryability.Retryable, error.Retry.Retryability);
    }

    [Fact]
    public async Task DecideAsync_RefusesAnAlreadyCancelledCall()
    {
        using JevFixture fixture = JevFixture.Answering();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Provider.DecideAsync(JevScenario.CreateRequest(), cancellation.Token));

        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task DecideAsync_AbortsTheCallInFlightWhenTheCallerCancels()
    {
        TaskCompletionSource arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using JevFixture fixture = JevFixture.From(Pending(arrived));

        using CancellationTokenSource cancellation = new();
        Task<DecisionResult> pending = fixture.Provider.DecideAsync(JevScenario.CreateRequest(), cancellation.Token);

        await arrived.Task;
        await cancellation.CancelAsync();

        // Cancellation is not a decision failure: it leaves the DecisionException hierarchy alone,
        // which the compiler already guarantees for the declared type below.
        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.NotNull(canceled);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task DecideAsync_FailsWithATimeoutWhenTheAttemptBudgetRunsOut()
    {
        FakeTimeProvider time = new();
        TaskCompletionSource arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using JevFixture fixture = JevFixture.From(
            Pending(arrived),
            new JevProviderOptions
            {
                AttemptTimeout = TimeSpan.FromSeconds(5),
                OperationTimeout = TimeSpan.FromMinutes(5),
            },
            time);

        Task<DecisionResult> pending = fixture.Provider.DecideAsync(
            JevScenario.CreateRequest(),
            TestContext.Current.CancellationToken);

        await arrived.Task;
        time.Advance(TimeSpan.FromSeconds(6));

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(() => pending);

        Assert.Equal(DecisionErrorCategory.Timeout, error.Category);
        Assert.Equal(JevProtocol.TimeoutCode, error.Error.Code);
        Assert.Equal("attempt", error.Error.Properties[JevProtocol.TimeoutScopeProperty]);
    }

    [Fact]
    public async Task DecideAsync_FailsWithATimeoutWhenTheOperationBudgetRunsOut()
    {
        FakeTimeProvider time = new();
        TaskCompletionSource arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using JevFixture fixture = JevFixture.From(
            Pending(arrived),
            new JevProviderOptions
            {
                AttemptTimeout = TimeSpan.FromMinutes(5),
                OperationTimeout = TimeSpan.FromSeconds(5),
            },
            time);

        Task<DecisionResult> pending = fixture.Provider.DecideAsync(
            JevScenario.CreateRequest(),
            TestContext.Current.CancellationToken);

        await arrived.Task;
        time.Advance(TimeSpan.FromSeconds(6));

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(() => pending);

        Assert.Equal("operation", error.Error.Properties[JevProtocol.TimeoutScopeProperty]);
    }

    [Fact]
    public async Task DecideAsync_NeverLetsTheCredentialReachALogOrAFailure()
    {
        RecordingLogger<JevDecisionProvider> logger = new();
        using JevFixture success = JevFixture.AnsweringWith(logger);

        await success.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken);

        using JevFixture failure = JevFixture.FailingWith(
            HttpStatusCode.Unauthorized,
            RecordedPayload.Read("error.json"),
            logger);

        DecisionException error = await Assert.ThrowsAnyAsync<DecisionException>(
            () => failure.Provider.DecideAsync(JevScenario.CreateRequest(), TestContext.Current.CancellationToken));

        Assert.NotEmpty(logger.Entries);

        foreach (string message in logger.Entries)
        {
            Assert.DoesNotContain(Secret, message, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, Describe(error.Error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_IsSafeToCallConcurrently()
    {
        using JevFixture fixture = JevFixture.Answering();

        IEnumerable<Task<DecisionResult>> calls = Enumerable
            .Range(0, 24)
            .Select(_ => fixture.Provider.DecideAsync(
                JevScenario.CreateRequest(),
                TestContext.Current.CancellationToken));

        DecisionResult[] results = await Task.WhenAll(calls);

        Assert.All(results, result => Assert.Equal("jev-1.13.0", result.Metadata.ModelVersion));
        Assert.Equal(24, fixture.Handler.Requests.Count);
    }

    /// <summary>
    /// A handler that never answers, so that a timeout or a cancellation has something to interrupt.
    /// </summary>
    private static StubHttpMessageHandler Pending(TaskCompletionSource arrived) =>
        new(async (_, token) =>
        {
            arrived.TrySetResult();

            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);

            return StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        });

    private static string Describe(DecisionError error)
    {
        IEnumerable<string> properties = error.Properties.Select(
            entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}={entry.Value}"));

        return string.Join('|', [error.Message, error.Code, error.ProviderName, .. properties]);
    }

    /// <summary>
    /// A provider wired to a stub handler, so that every test states only what the service answers.
    /// </summary>
    private sealed class JevFixture : IDisposable
    {
        private readonly HttpClient _http;

        private JevFixture(
            StubHttpMessageHandler handler,
            JevProviderOptions options,
            TimeProvider time,
            IJevCredentialProvider credentials,
            ILogger<JevDecisionProvider> logger)
        {
            Handler = handler;
            _http = new HttpClient(handler, disposeHandler: false);
            Provider = new JevDecisionProvider(_http, credentials, options, logger, time);
        }

        public StubHttpMessageHandler Handler { get; }

        public JevDecisionProvider Provider { get; }

        public static JevFixture Answering() => AnsweringBody(RecordedPayload.Read("response.json"));

        public static JevFixture AnsweringBody(string body) =>
            From(StubHttpMessageHandler.Always(HttpStatusCode.OK, body));

        public static JevFixture AnsweringWithHeaders(params (string Name, string Value)[] headers) =>
            From(StubHttpMessageHandler.Always(HttpStatusCode.OK, RecordedPayload.Read("response.json"), headers));

        public static JevFixture AnsweringWith(IJevCredentialProvider credentials) =>
            new(
                StubHttpMessageHandler.Always(HttpStatusCode.OK, RecordedPayload.Read("response.json")),
                JevProviderOptions.Default,
                TimeProvider.System,
                credentials,
                NullLogger<JevDecisionProvider>.Instance);

        public static JevFixture AnsweringWith(ILogger<JevDecisionProvider> logger) =>
            new(
                StubHttpMessageHandler.Always(HttpStatusCode.OK, RecordedPayload.Read("response.json")),
                JevProviderOptions.Default,
                TimeProvider.System,
                new StaticJevCredentialProvider(Secret),
                logger);

        public static JevFixture Failing(HttpStatusCode status, string body) =>
            From(StubHttpMessageHandler.Always(status, body));

        public static JevFixture FailingWithHeaders(
            HttpStatusCode status,
            string body,
            params (string Name, string Value)[] headers) =>
            From(StubHttpMessageHandler.Always(status, body, headers));

        public static JevFixture FailingWith(HttpStatusCode status, string body, ILogger<JevDecisionProvider> logger) =>
            new(
                StubHttpMessageHandler.Always(status, body),
                JevProviderOptions.Default,
                TimeProvider.System,
                new StaticJevCredentialProvider(Secret),
                logger);

        public static JevFixture From(StubHttpMessageHandler handler) =>
            From(handler, JevProviderOptions.Default, TimeProvider.System);

        public static JevFixture From(StubHttpMessageHandler handler, JevProviderOptions options, TimeProvider time) =>
            new(handler, options, time, new StaticJevCredentialProvider(Secret), NullLogger<JevDecisionProvider>.Instance);

        public void Dispose()
        {
            _http.Dispose();
            Handler.Dispose();
        }
    }
}
