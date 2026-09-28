using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using Microsoft.Extensions.Logging;

namespace DecisionKit.Jev.Resilience;

/// <summary>
/// Runs one logical JEV operation, repeating the attempt when the policy allows it.
/// </summary>
/// <remarks>
/// <para>
/// This is where an operation becomes more than one call. It owns the operation budget, which
/// spans every attempt and every wait between them, and it owns the waiting itself. The transport
/// below it stays a single exchange with a single attempt budget, which is what keeps the two
/// testable apart.
/// </para>
/// <para>
/// Every wait goes through the injected <see cref="TimeProvider"/>, so a test drives the backoff
/// without a clock ever advancing on its own.
/// </para>
/// </remarks>
internal sealed class JevRetryRunner
{
    private readonly JevProviderOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;

    internal JevRetryRunner(JevProviderOptions options, ILogger logger, TimeProvider timeProvider)
    {
        _options = options;
        _logger = logger;
        _time = timeProvider;
    }

    private JevRetryPolicy Policy => _options.Retry;

    private string Provider => _options.Mapping.ProviderName;

    /// <summary>
    /// Runs an attempt until it succeeds, until the policy stops allowing another one, or until a
    /// budget runs out.
    /// </summary>
    /// <typeparam name="TResult">What one attempt produces.</typeparam>
    /// <param name="attempt">The attempt, which receives the token bounding the whole operation.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The result of the attempt that succeeded.</returns>
    /// <exception cref="DecisionException">Every permitted attempt failed.</exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the operation.</exception>
    internal async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> attempt,
        CancellationToken cancellationToken)
    {
        long start = _time.GetTimestamp();

        using CancellationTokenSource operationTimeout = new(_options.OperationTimeout, _time);
        using CancellationTokenSource operation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationTimeout.Token);

        try
        {
            return await RunAsync(attempt, start, operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            JevLog.EvaluationTimedOut(_logger, Provider, JevTransportErrors.OperationScope, Elapsed(start));

            throw JevTransportErrors.TimedOut(
                _options.Mapping,
                JevTransportErrors.OperationScope,
                _options.OperationTimeout,
                exception);
        }
        catch (OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                double elapsed = Elapsed(start);

                JevLog.EvaluationCanceled(_logger, Provider, elapsed);
            }

            throw;
        }
    }

    private async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> attempt,
        long start,
        CancellationToken cancellationToken)
    {
        bool warned = false;

        for (int completed = 1; ; completed++)
        {
            try
            {
                return await attempt(cancellationToken).ConfigureAwait(false);
            }
            catch (DecisionException failure)
            {
                if (NextDelay(failure.Error, completed, start) is not { } wait)
                {
                    if (completed == 1)
                    {
                        throw;
                    }

                    JevLog.RetriesExhausted(_logger, Provider, completed, Elapsed(start), failure.Error.Code);

                    throw Annotate(failure, completed);
                }

                warned = WarnOnce(warned);

                LogScheduled(completed, wait, failure.Error.Code);

                await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Decides how long to wait before the next attempt, or that there will not be one.
    /// </summary>
    private TimeSpan? NextDelay(DecisionError error, int completedAttempts, long start)
    {
        JevRetryPolicy policy = Policy;

        if (completedAttempts >= policy.MaxAttempts || !IsRetryable(policy.Classify(error)))
        {
            return null;
        }

        TimeSpan delay = policy.GetDelay(completedAttempts, error.Retry.RetryAfter);

        return WithinBudgets(delay, start) ? delay : null;
    }

    private bool IsRetryable(DecisionRetryability retryability) => retryability switch
    {
        DecisionRetryability.Retryable => true,
        DecisionRetryability.Unknown => Policy.RetryUnknownFailures,
        _ => false,
    };

    /// <summary>
    /// States whether a wait fits in the budgets that bound the retrying.
    /// </summary>
    /// <remarks>
    /// A wait longer than <see cref="JevProviderOptions.RetryDelayTimeout"/> abandons the retry
    /// instead of being shortened. When the service asked for that wait, waiting less would ignore
    /// the instruction and arrive early; when the backoff produced it, the caller has already said
    /// it will not wait that long.
    /// </remarks>
    private bool WithinBudgets(TimeSpan delay, long start)
    {
        if (_options.RetryDelayTimeout != Timeout.InfiniteTimeSpan && delay > _options.RetryDelayTimeout)
        {
            return false;
        }

        TimeSpan budget = Policy.MaxElapsedTime;

        return budget == Timeout.InfiniteTimeSpan || _time.GetElapsedTime(start) + delay <= budget;
    }

    private bool WarnOnce(bool warned)
    {
        if (!warned)
        {
            JevLog.RetryNotDeduplicated(_logger, Provider);
        }

        return true;
    }

    private void LogScheduled(int completedAttempts, TimeSpan wait, string? code)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        double delay = wait.TotalMilliseconds;

        JevLog.RetryScheduled(_logger, Provider, completedAttempts + 1, Policy.MaxAttempts, delay, code);
    }

    /// <summary>
    /// Restates a failure with the number of attempts it took, so that the caller can tell one
    /// failed call from a retried one that never recovered.
    /// </summary>
    private static DecisionException Annotate(DecisionException failure, int attempts)
    {
        DecisionError error = failure.Error;

        Dictionary<string, object?> properties = new(error.Properties, StringComparer.Ordinal)
        {
            [JevProtocol.RetryAttemptsProperty] = attempts,
        };

        return DecisionException.FromError(
            new DecisionError(error.Category, error.Message)
            {
                Code = error.Code,
                ProviderName = error.ProviderName,
                ProviderRequestId = error.ProviderRequestId,
                DocumentationUrl = error.DocumentationUrl,
                Retry = error.Retry,
                Properties = properties,
            },
            failure);
    }

    private double Elapsed(long start) => _time.GetElapsedTime(start).TotalMilliseconds;
}
