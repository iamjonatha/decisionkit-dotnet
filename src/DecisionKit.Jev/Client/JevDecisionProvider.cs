using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Questions;
using DecisionKit.Jev.Resilience;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Answers decision requests with the TypeSafe JEV service.
/// </summary>
/// <remarks>
/// <para>
/// The provider owns nothing expensive. It takes an <see cref="HttpClient"/> rather than creating
/// one, because deciding how a client is pooled, how long its connections live and which handlers
/// wrap it belongs to the application, not to a library. It never disposes the client it was given,
/// for the same reason.
/// </para>
/// <para>
/// It holds no mutable state either, so one instance is safe to register as a singleton and to
/// call from any number of requests at once. Everything that varies between calls lives on the
/// request or on the call stack.
/// </para>
/// <para>
/// Three steps happen on every call, in this order: the request is translated to the wire format,
/// which is where a request JEV cannot express is rejected before a byte is sent; the call is made,
/// once or several times depending on <see cref="JevProviderOptions.Retry"/>; the response is
/// translated back. A failure at any step is a
/// <see cref="DecisionKit.Errors.DecisionException"/>, and a cancellation is an
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public sealed class JevDecisionProvider : IDecisionProvider
{
    private static readonly DecisionProviderCapabilities s_capabilities = new()
    {
        SupportedQuestionTypes = [typeof(ProbabilityQuestion), typeof(ChoiceQuestion<>), typeof(JevScoreQuestion)],

        // JEV accepts no idempotency key, so the provider refuses one rather than dropping it.
        SupportsIdempotencyKeys = false,
        SupportsUsageReporting = true,

        // The protocol has nowhere to carry request metadata, which is left on the request instead.
        SupportsRequestMetadata = false,
        SupportsBatching = true,
        SupportsExplanations = false,
    };

    private readonly RequestMapper _requests;
    private readonly ResponseMapper _responses;
    private readonly JevTransport _transport;
    private readonly JevRetryRunner _retries;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JevDecisionProvider"/> class with the default
    /// settings, no logging and the system clock.
    /// </summary>
    /// <param name="httpClient">The client the provider sends through and never disposes.</param>
    /// <param name="credentials">The source of the API key.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public JevDecisionProvider(HttpClient httpClient, IJevCredentialProvider credentials)
        : this(httpClient, credentials, JevProviderOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JevDecisionProvider"/> class with no logging
    /// and the system clock.
    /// </summary>
    /// <param name="httpClient">The client the provider sends through and never disposes.</param>
    /// <param name="credentials">The source of the API key.</param>
    /// <param name="options">The transport settings.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public JevDecisionProvider(HttpClient httpClient, IJevCredentialProvider credentials, JevProviderOptions options)
        : this(httpClient, credentials, options, NullLogger<JevDecisionProvider>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JevDecisionProvider"/> class with the system
    /// clock.
    /// </summary>
    /// <param name="httpClient">The client the provider sends through and never disposes.</param>
    /// <param name="credentials">The source of the API key.</param>
    /// <param name="options">The transport settings.</param>
    /// <param name="logger">The log the provider reports calls to.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public JevDecisionProvider(
        HttpClient httpClient,
        IJevCredentialProvider credentials,
        JevProviderOptions options,
        ILogger<JevDecisionProvider> logger)
        : this(httpClient, credentials, options, logger, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JevDecisionProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The client the provider sends through and never disposes.</param>
    /// <param name="credentials">The source of the API key.</param>
    /// <param name="options">The transport settings.</param>
    /// <param name="logger">The log the provider reports calls to.</param>
    /// <param name="timeProvider">The clock that measures timeouts and stamps results.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public JevDecisionProvider(
        HttpClient httpClient,
        IJevCredentialProvider credentials,
        JevProviderOptions options,
        ILogger<JevDecisionProvider> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Options = options;
        _logger = logger;
        _requests = new RequestMapper(options.Mapping);
        _responses = new ResponseMapper(options.Mapping, timeProvider);
        _transport = new JevTransport(httpClient, credentials, options, logger, timeProvider);
        _retries = new JevRetryRunner(options, logger, timeProvider);
    }

    /// <inheritdoc />
    public string Name => Options.Mapping.ProviderName;

    /// <inheritdoc />
    /// <remarks>
    /// A plain <see cref="ScoreQuestion"/> is absent on purpose: JEV scores against a rubric of
    /// described levels rather than a numeric range, so the question has to be a
    /// <see cref="JevScoreQuestion"/>, which carries one.
    /// </remarks>
    public DecisionProviderCapabilities Capabilities => s_capabilities;

    /// <summary>
    /// Gets the settings this provider was built with.
    /// </summary>
    public JevProviderOptions Options { get; }

    /// <inheritdoc />
    public async Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        JevRequest wire = _requests.ToWire(request);

        JevLog.EvaluationStarting(_logger, Name, wire.Model ?? Options.Mapping.Model, wire.Questions.Count);

        JevExchange exchange = await _retries
            .ExecuteAsync(token => _transport.EvaluateAsync(wire, token), cancellationToken)
            .ConfigureAwait(false);

        return _responses.ToDomain(request, exchange.Response, exchange.RequestId);
    }
}
