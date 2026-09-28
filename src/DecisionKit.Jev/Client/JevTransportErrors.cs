using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Errors;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Builds the failures the transport raises, for the failures that never reach the service.
/// </summary>
/// <remarks>
/// A failure the service reported is classified by <see cref="ErrorMapper"/> from its status code.
/// The failures here are the other kind: the call never got an answer, so there is no status code
/// to classify from and the transport states the category itself.
/// </remarks>
internal static class JevTransportErrors
{
    /// <summary>
    /// The timeout scope reported when a single HTTP attempt ran out of time.
    /// </summary>
    internal const string AttemptScope = "attempt";

    /// <summary>
    /// The timeout scope reported when the whole operation ran out of time.
    /// </summary>
    internal const string OperationScope = "operation";

    internal static DecisionException CredentialMissing(JevMappingOptions options) =>
        DecisionException.FromError(
            new DecisionError(
                DecisionErrorCategory.Authentication,
                "The credential provider offered no API key, so the request was not sent. JEV rejects an unauthenticated call, and sending one anyway would report the failure as coming from the service.")
            {
                Code = JevProtocol.CredentialMissingCode,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.NotRetryable,
            });

    internal static DecisionException TimedOut(
        JevMappingOptions options,
        string scope,
        TimeSpan budget,
        Exception innerException) =>
        DecisionException.FromError(
            new DecisionError(
                DecisionErrorCategory.Timeout,
                string.Create(CultureInfo.InvariantCulture, $"The JEV call exceeded its {scope} budget of {budget.TotalSeconds:0.###} seconds."))
            {
                Code = JevProtocol.TimeoutCode,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.Retryable,
                Properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [JevProtocol.TimeoutScopeProperty] = scope,
                },
            },
            innerException);

    internal static DecisionException Unreachable(
        JevMappingOptions options,
        Uri endpoint,
        Exception innerException) =>
        DecisionException.FromError(
            new DecisionError(
                DecisionErrorCategory.Transport,
                string.Create(CultureInfo.InvariantCulture, $"The JEV service at '{Describe(endpoint)}' could not be reached."))
            {
                Code = JevProtocol.TransportFailureCode,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.Retryable,
            },
            innerException);

    /// <summary>
    /// Describes an endpoint without its user information, which a URI can carry and which must
    /// never reach a message.
    /// </summary>
    internal static string Describe(Uri endpoint) =>
        endpoint.GetComponents(
            UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path,
            UriFormat.UriEscaped);
}
