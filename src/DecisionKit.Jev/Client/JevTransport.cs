using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using Microsoft.Extensions.Logging;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Sends one JEV call and turns whatever comes back into either a response or a typed failure.
/// </summary>
/// <remarks>
/// <para>
/// This is the only type in the package that knows HTTP exists. Everything above it works in wire
/// models and domain types, which is what lets the mapping layer be tested without a network and
/// what keeps the status-code table in exactly one place.
/// </para>
/// <para>
/// The type holds no per-call state, so one instance serves every request in flight.
/// </para>
/// </remarks>
internal sealed class JevTransport
{
    private readonly HttpClient _http;
    private readonly IJevCredentialProvider _credentials;
    private readonly JevProviderOptions _options;
    private readonly ErrorMapper _errors;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;

    internal JevTransport(
        HttpClient httpClient,
        IJevCredentialProvider credentials,
        JevProviderOptions options,
        ILogger logger,
        TimeProvider timeProvider)
    {
        _http = httpClient;
        _credentials = credentials;
        _options = options;
        _errors = new ErrorMapper(options.Mapping);
        _logger = logger;
        _time = timeProvider;
    }

    private string Provider => _options.Mapping.ProviderName;

    /// <summary>
    /// Makes one attempt at evaluating a request against the service.
    /// </summary>
    /// <param name="request">The wire request to send.</param>
    /// <param name="cancellationToken">The token bounding the operation this attempt belongs to.</param>
    /// <returns>The completed exchange.</returns>
    /// <remarks>
    /// One attempt, never more. Repeating a failed call belongs to
    /// <see cref="DecisionKit.Jev.Resilience.JevRetryRunner"/>, which is also what owns the
    /// operation budget; the budget enforced here bounds this attempt alone. That split is why a
    /// timed-out attempt is reported as retryable while a timed-out operation is not repeated.
    /// </remarks>
    /// <exception cref="DecisionException">The attempt failed.</exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was cancelled, either by the caller or by its own budget.
    /// </exception>
    internal async Task<JevExchange> EvaluateAsync(JevRequest request, CancellationToken cancellationToken)
    {
        long start = _time.GetTimestamp();

        using CancellationTokenSource attemptTimeout = new(_options.AttemptTimeout, _time);
        using CancellationTokenSource attempt =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptTimeout.Token);

        try
        {
            return await ExchangeAsync(request, attempt.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The operation token is still live, so what ran out is this attempt's own budget, or
            // the one the HttpClient enforces. Either way the operation may still have time left,
            // which is what makes this failure worth retrying.
            throw TimedOut(exception, attemptTimeout, start);
        }
        catch (HttpRequestException exception)
        {
            JevLog.EvaluationUnreachable(_logger, Provider, JevTransportErrors.Describe(_options.Endpoint.Evaluate), Elapsed(start));

            throw JevTransportErrors.Unreachable(_options.Mapping, _options.Endpoint.Evaluate, exception);
        }
    }

    private async Task<JevExchange> ExchangeAsync(JevRequest request, CancellationToken cancellationToken)
    {
        long start = _time.GetTimestamp();

        using HttpRequestMessage message = await CreateMessageAsync(request, cancellationToken).ConfigureAwait(false);
        using HttpResponseMessage response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        string? requestId = JevResponseHeaders.ReadRequestId(response);
        int statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            JevLog.EvaluationFailed(_logger, Provider, statusCode, Elapsed(start), requestId);

            throw await FailureAsync(response, requestId, cancellationToken).ConfigureAwait(false);
        }

        using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        JevResponse payload = await JevJsonSerialization.DeserializeResponseAsync(body, cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            double elapsed = Elapsed(start);

            JevLog.EvaluationSucceeded(_logger, Provider, statusCode, elapsed, requestId);
        }

        return new JevExchange(payload, requestId, statusCode);
    }

    private async Task<HttpRequestMessage> CreateMessageAsync(JevRequest request, CancellationToken cancellationToken)
    {
        JevApiKey apiKey = await _credentials.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);

        if (apiKey.Value is not { } secret)
        {
            throw JevTransportErrors.CredentialMissing(_options.Mapping);
        }

        HttpRequestMessage message = new(HttpMethod.Post, _options.Endpoint.Evaluate)
        {
            Content = JsonContent.Create(request, JevJsonSerialization.RequestTypeInfo),
        };

        message.Headers.Authorization = new AuthenticationHeaderValue(JevProtocol.AuthenticationScheme, secret);

        return message;
    }

    private async Task<DecisionException> FailureAsync(
        HttpResponseMessage response,
        string? requestId,
        CancellationToken cancellationToken)
    {
        JevErrorContext context = new(
            (int)response.StatusCode,
            JevResponseHeaders.ReadRetryAfter(response, _time),
            requestId);

        return _errors.ToException(await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false), context);
    }

    /// <summary>
    /// Reads the error body, or gives up on it.
    /// </summary>
    /// <remarks>
    /// A failing response is the one place where an unreadable body must not become the reported
    /// failure: the status code already says what went wrong, and replacing a 503 with a parse
    /// error would hide it. A gateway that answers with HTML is exactly this case.
    /// </remarks>
    private static async Task<JevErrorResponse?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            return await JevJsonSerialization.DeserializeErrorAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (DecisionException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private DecisionException TimedOut(
        OperationCanceledException exception,
        CancellationTokenSource attemptTimeout,
        long start)
    {
        // Neither budget of ours fired means the cancellation came from the HttpClient's own
        // timeout. It bounds the same thing an attempt timeout does, and is reported as one.
        TimeSpan budget = attemptTimeout.IsCancellationRequested ? _options.AttemptTimeout : _http.Timeout;

        JevLog.EvaluationTimedOut(_logger, Provider, JevTransportErrors.AttemptScope, Elapsed(start));

        return JevTransportErrors.TimedOut(_options.Mapping, JevTransportErrors.AttemptScope, budget, exception);
    }

    private double Elapsed(long start) => _time.GetElapsedTime(start).TotalMilliseconds;
}
