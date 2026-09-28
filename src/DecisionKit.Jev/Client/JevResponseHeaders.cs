using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Reads the parts of a JEV answer that travel outside the body.
/// </summary>
/// <remarks>
/// Two things the caller needs are not in the payload at all: the identifier the service assigned
/// the call, and how long it wants the caller to wait before trying again. Both are headers, so
/// this is the only place that knows they exist; everything downstream receives plain values.
/// </remarks>
internal static class JevResponseHeaders
{
    internal static string? ReadRequestId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(JevProtocol.RequestIdHeader, out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the delay the service asked for, preferring its millisecond header over the standard
    /// one because it is the more precise of the two.
    /// </summary>
    internal static TimeSpan? ReadRetryAfter(HttpResponseMessage response, TimeProvider timeProvider) =>
        ReadRetryAfterMilliseconds(response) ?? ReadStandardRetryAfter(response, timeProvider);

    private static TimeSpan? ReadRetryAfterMilliseconds(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(JevProtocol.RetryAfterMillisecondsHeader, out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (string value in values)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) &&
                double.IsFinite(milliseconds))
            {
                return NonNegative(TimeSpan.FromMilliseconds(milliseconds));
            }
        }

        return null;
    }

    private static TimeSpan? ReadStandardRetryAfter(HttpResponseMessage response, TimeProvider timeProvider)
    {
        if (response.Headers.RetryAfter is not { } retryAfter)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return NonNegative(delta);
        }

        // The date form is absolute, so it only means anything against the clock the rest of the
        // library reads, which is the injected one and not DateTimeOffset.UtcNow.
        return retryAfter.Date is { } date ? NonNegative(date - timeProvider.GetUtcNow()) : null;
    }

    private static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
