using System;
using System.Globalization;

namespace DecisionKit.Errors;

/// <summary>
/// What the provider said about repeating a failed call.
/// </summary>
/// <remarks>
/// This is a hint, not a policy. It records what the provider reported, including the delay it
/// asked for. Deciding whether to actually retry, how often, and with which backoff belongs to the
/// resilience layer, which reads this hint instead of inferring retryability from a status code.
/// </remarks>
public readonly struct DecisionRetryHint : IEquatable<DecisionRetryHint>
{
    private DecisionRetryHint(DecisionRetryability retryability, TimeSpan? retryAfter)
    {
        Retryability = retryability;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets whether the call can be attempted again.
    /// </summary>
    public DecisionRetryability Retryability { get; }

    /// <summary>
    /// Gets the delay the provider asked the caller to wait before the next attempt, when it asked
    /// for one.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the hint used when the provider gave no retry signal.
    /// </summary>
    public static DecisionRetryHint Unknown => default;

    /// <summary>
    /// Gets a hint stating that the call can be attempted again, with no delay requested.
    /// </summary>
    public static DecisionRetryHint Retryable { get; } = new(DecisionRetryability.Retryable, null);

    /// <summary>
    /// Gets a hint stating that repeating the call cannot succeed.
    /// </summary>
    public static DecisionRetryHint NotRetryable { get; } = new(DecisionRetryability.NotRetryable, null);

    /// <summary>
    /// Creates a hint stating that the call can be attempted again after a delay.
    /// </summary>
    /// <param name="retryAfter">The delay the provider asked for.</param>
    /// <returns>The created hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryAfter"/> is negative.</exception>
    public static DecisionRetryHint After(TimeSpan retryAfter)
    {
        if (retryAfter.Ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter), retryAfter, "A retry delay cannot be negative.");
        }

        return new DecisionRetryHint(DecisionRetryability.Retryable, retryAfter);
    }

    /// <inheritdoc />
    public bool Equals(DecisionRetryHint other) => Retryability == other.Retryability && RetryAfter == other.RetryAfter;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DecisionRetryHint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Retryability, RetryAfter);

    /// <inheritdoc />
    public override string ToString() =>
        RetryAfter is { } delay
            ? string.Create(CultureInfo.InvariantCulture, $"{Retryability} after {delay}")
            : Retryability.ToString();

    /// <summary>Determines whether two hints are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both hints carry the same signal.</returns>
    public static bool operator ==(DecisionRetryHint left, DecisionRetryHint right) => left.Equals(right);

    /// <summary>Determines whether two hints are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the hints carry different signals.</returns>
    public static bool operator !=(DecisionRetryHint left, DecisionRetryHint right) => !left.Equals(right);
}
