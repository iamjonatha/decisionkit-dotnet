using Microsoft.Extensions.Logging;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Every log message the JEV transport writes.
/// </summary>
/// <remarks>
/// <para>
/// Declaring them in one place is what makes the safety rule checkable: <b>no message here takes a
/// credential or a payload.</b> Every parameter is a scalar that describes the call — who, where,
/// how long, what status — and a reviewer can confirm that by reading one short file instead of
/// auditing every call site.
/// </para>
/// <para>
/// The messages are source-generated, so formatting costs nothing when the level is disabled.
/// </para>
/// </remarks>
internal static partial class JevLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Evaluating a decision with {Provider} using model {Model} for {QuestionCount} question(s).")]
    internal static partial void EvaluationStarting(ILogger logger, string provider, string model, int questionCount);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "{Provider} answered with status {StatusCode} in {ElapsedMilliseconds} ms (request {ProviderRequestId}).")]
    internal static partial void EvaluationSucceeded(
        ILogger logger,
        string provider,
        int statusCode,
        double elapsedMilliseconds,
        string? providerRequestId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "{Provider} failed with status {StatusCode} in {ElapsedMilliseconds} ms (request {ProviderRequestId}).")]
    internal static partial void EvaluationFailed(
        ILogger logger,
        string provider,
        int statusCode,
        double elapsedMilliseconds,
        string? providerRequestId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "The call to {Provider} was cancelled by the caller after {ElapsedMilliseconds} ms.")]
    internal static partial void EvaluationCanceled(ILogger logger, string provider, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "The call to {Provider} exceeded its {TimeoutScope} budget after {ElapsedMilliseconds} ms.")]
    internal static partial void EvaluationTimedOut(
        ILogger logger,
        string provider,
        string timeoutScope,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "{Provider} at {Endpoint} could not be reached after {ElapsedMilliseconds} ms.")]
    internal static partial void EvaluationUnreachable(
        ILogger logger,
        string provider,
        string endpoint,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Retrying {Provider} as attempt {Attempt} of {MaxAttempts} in {DelayMilliseconds} ms after error {ErrorCode}.")]
    internal static partial void RetryScheduled(
        ILogger logger,
        string provider,
        int attempt,
        int maxAttempts,
        double delayMilliseconds,
        string? errorCode);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Warning,
        Message = "{Provider} still failed with error {ErrorCode} after {Attempts} attempts in {ElapsedMilliseconds} ms.")]
    internal static partial void RetriesExhausted(
        ILogger logger,
        string provider,
        int attempts,
        double elapsedMilliseconds,
        string? errorCode);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Warning,
        Message = "The call to {Provider} is being repeated without idempotency: the service cannot deduplicate it, so the request is evaluated and charged again.")]
    internal static partial void RetryNotDeduplicated(ILogger logger, string provider);
}
