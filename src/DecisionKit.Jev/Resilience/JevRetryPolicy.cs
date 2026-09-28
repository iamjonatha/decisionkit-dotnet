using System;
using System.Collections.Generic;
using System.Threading;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Resilience;

/// <summary>
/// Decides whether a failed JEV call is repeated, and how long the caller waits first.
/// </summary>
/// <remarks>
/// <para>
/// Retrying is off by default. A JEV evaluation is not a read: the service has no idempotency
/// mechanism, so a repeated call is a second evaluation that is billed again and can answer
/// differently. Turning that on is a decision the application makes with its own knowledge of cost
/// and of what a stale answer is worth, which is why it is opt-in rather than a default that costs
/// money quietly. Start from <see cref="Standard"/> when the decision is yes.
/// </para>
/// <para>
/// The policy holds no clock and performs no waiting. It answers two questions — may this failure
/// be repeated, and after how long — and the resilience layer does the rest. That is what makes it
/// testable without time passing.
/// </para>
/// <para>
/// Instances are immutable and every value is validated on assignment.
/// </para>
/// </remarks>
public sealed class JevRetryPolicy
{
    private static readonly IReadOnlySet<int> s_noStatusCodes = new HashSet<int>();

    /// <summary>
    /// Gets the policy that never repeats a call. This is the default.
    /// </summary>
    public static JevRetryPolicy None { get; } = new();

    /// <summary>
    /// Gets a policy that repeats a retryable failure twice, backing off from half a second and
    /// giving up after ten.
    /// </summary>
    /// <remarks>
    /// These numbers suit an interactive call that a person is waiting on. A background job can
    /// afford more attempts and a longer ceiling, and should say so rather than inherit these.
    /// </remarks>
    public static JevRetryPolicy Standard { get; } = new() { MaxAttempts = 3 };

    /// <summary>
    /// Gets how many attempts one operation may make in total, the first one included.
    /// </summary>
    /// <value>The default is 1, which means no retry.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxAttempts
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 1;

    /// <summary>
    /// Gets how long the attempts and the waits between them may take together before the operation
    /// stops retrying.
    /// </summary>
    /// <value>The default is <see cref="Timeout.InfiniteTimeSpan"/>, which defers to
    /// <see cref="Client.JevProviderOptions.OperationTimeout"/>.</value>
    /// <remarks>
    /// This bounds the retrying, not the operation. Exhausting it reports the failure that was
    /// being retried, because that is what actually went wrong; exhausting the operation timeout
    /// reports a timeout, because the caller ran out of patience instead.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative, and is not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan MaxElapsedTime
    {
        get;
        init
        {
            field = ValidateBudget(value, nameof(MaxElapsedTime));
        }
    } = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Gets how long the caller waits before the second attempt.
    /// </summary>
    /// <value>The default is 500 milliseconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or infinite.</exception>
    public TimeSpan InitialDelay
    {
        get;
        init
        {
            field = ValidateDelay(value, nameof(InitialDelay));
        }
    } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets the factor each wait is multiplied by to produce the next one.
    /// </summary>
    /// <value>The default is 2, which doubles the wait every time.</value>
    /// <remarks>A factor of 1 keeps the wait constant.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1, or is not finite.</exception>
    public double BackoffFactor
    {
        get;
        init
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(BackoffFactor), value, "A backoff factor must be a finite number.");
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1d);
            field = value;
        }
    } = 2d;

    /// <summary>
    /// Gets the longest wait the backoff may grow to.
    /// </summary>
    /// <value>The default is 10 seconds.</value>
    /// <remarks>
    /// A delay the service asked for is not capped here. Truncating it would ignore an explicit
    /// instruction; it is bounded by
    /// <see cref="Client.JevProviderOptions.RetryDelayTimeout"/> instead, which abandons the retry
    /// rather than shortening the wait.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or infinite.</exception>
    public TimeSpan MaxDelay
    {
        get;
        init
        {
            field = ValidateDelay(value, nameof(MaxDelay));
        }
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the fraction of a computed wait that is randomized away, from 0 to 1.
    /// </summary>
    /// <value>The default is 0.2.</value>
    /// <remarks>
    /// Jitter only ever shortens a wait, so a delay stays within <see cref="MaxDelay"/>. It exists
    /// to break up the synchronized retry storm that a shared outage otherwise produces, and it
    /// applies to computed waits only, never to one the service asked for.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the range 0 to 1.</exception>
    public double Jitter
    {
        get;
        init
        {
            if (!double.IsFinite(value) || value < 0d || value > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(Jitter), value, "Jitter must be a finite fraction between 0 and 1.");
            }

            field = value;
        }
    } = 0.2d;

    /// <summary>
    /// Gets a value indicating whether a delay the service asked for replaces the computed backoff.
    /// </summary>
    /// <value>The default is <see langword="true"/>.</value>
    public bool RespectRetryAfter { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a failure the provider gave no retry signal for is repeated.
    /// </summary>
    /// <value>The default is <see langword="false"/>.</value>
    /// <remarks>
    /// <see cref="DecisionRetryability.Unknown"/> means the provider said nothing, not that it said
    /// yes. Repeating such a failure is a deployment's choice, and the default is the cautious one.
    /// </remarks>
    public bool RetryUnknownFailures { get; init; }

    /// <summary>
    /// Gets the HTTP status codes this deployment repeats regardless of what the provider signalled.
    /// </summary>
    /// <remarks>
    /// This is the escape hatch for a deployment that knows something the library does not, such as
    /// a gateway in front of the service that returns a status the mapper reads as permanent. The
    /// collection is copied on assignment.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlySet<int> RetryableStatusCodes
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = new HashSet<int>(value);
        }
    } = s_noStatusCodes;

    /// <summary>
    /// Gets the HTTP status codes this deployment never repeats, whatever the provider signalled.
    /// </summary>
    /// <remarks>
    /// Checked before <see cref="RetryableStatusCodes"/>, so listing a code in both refuses it. The
    /// collection is copied on assignment.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlySet<int> NonRetryableStatusCodes
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = new HashSet<int>(value);
        }
    } = s_noStatusCodes;

    /// <summary>
    /// Gets what this policy does about idempotency when it repeats a call.
    /// </summary>
    /// <value>
    /// <see cref="DecisionIdempotencyPolicy.Disabled"/>, which is the only value JEV accepts.
    /// </value>
    /// <remarks>
    /// JEV documents no idempotency mechanism and
    /// <see cref="DecisionKit.Providers.DecisionProviderCapabilities.SupportsIdempotencyKeys"/> says
    /// so, which is why a request carrying a key is refused before it is sent. The setting is
    /// exposed anyway, and rejects the other two values, so that the constraint is discovered while
    /// configuring the provider rather than inferred from a retried call being charged twice.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not
    /// <see cref="DecisionIdempotencyPolicy.Disabled"/>.</exception>
    public DecisionIdempotencyPolicy Idempotency
    {
        get;
        init
        {
            if (value != DecisionIdempotencyPolicy.Disabled)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Idempotency),
                    value,
                    "JEV has no idempotency mechanism, so only DecisionIdempotencyPolicy.Disabled can be honoured. Deduplicate before calling if a repeated evaluation is unacceptable.");
            }

            field = value;
        }
    } = DecisionIdempotencyPolicy.Disabled;

    /// <summary>
    /// Gets a value indicating whether this policy can repeat anything at all.
    /// </summary>
    public bool IsEnabled => MaxAttempts > 1;

    /// <summary>
    /// States whether a failure may be repeated under this policy.
    /// </summary>
    /// <param name="error">The failure to classify.</param>
    /// <returns>
    /// The classification. <see cref="DecisionRetryability.Unknown"/> means the provider gave no
    /// signal and this deployment configured none either.
    /// </returns>
    /// <remarks>
    /// The configured status-code sets win over the provider's own signal, and the signal wins over
    /// anything the library could infer. Nothing here looks at an error message.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionRetryability Classify(DecisionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (StatusCodeOf(error) is { } statusCode)
        {
            if (NonRetryableStatusCodes.Contains(statusCode))
            {
                return DecisionRetryability.NotRetryable;
            }

            if (RetryableStatusCodes.Contains(statusCode))
            {
                return DecisionRetryability.Retryable;
            }
        }

        return error.Retry.Retryability;
    }

    /// <summary>
    /// Computes how long to wait before the next attempt.
    /// </summary>
    /// <param name="completedAttempts">How many attempts have already failed. The first is 1.</param>
    /// <param name="retryAfter">The delay the service asked for, when it asked for one.</param>
    /// <returns>The wait, which is never negative.</returns>
    /// <remarks>
    /// The result includes <see cref="Jitter"/>, so two calls with the same arguments do not
    /// generally agree. Set the jitter to zero when an exact value is needed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="completedAttempts"/> is less than 1.</exception>
    public TimeSpan GetDelay(int completedAttempts, TimeSpan? retryAfter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(completedAttempts, 1);

        if (RespectRetryAfter && retryAfter is { } requested)
        {
            return requested < TimeSpan.Zero ? TimeSpan.Zero : requested;
        }

        return Jittered(Backoff(completedAttempts));
    }

    private TimeSpan Backoff(int completedAttempts)
    {
        double growth = Math.Pow(BackoffFactor, completedAttempts - 1);
        double milliseconds = InitialDelay.TotalMilliseconds * growth;

        return double.IsFinite(milliseconds) && milliseconds < MaxDelay.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(milliseconds)
            : MaxDelay;
    }

    private TimeSpan Jittered(TimeSpan delay)
    {
        if (Jitter == 0d || delay == TimeSpan.Zero)
        {
            return delay;
        }

        double kept = 1d - (Jitter * Random.Shared.NextDouble());

        return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * kept);
    }

    private static int? StatusCodeOf(DecisionError error) =>
        error.Properties.TryGetValue(JevProtocol.StatusCodeProperty, out object? value) && value is int statusCode
            ? statusCode
            : null;

    private static TimeSpan ValidateBudget(TimeSpan value, string propertyName)
    {
        if (value == Timeout.InfiniteTimeSpan || value > TimeSpan.Zero)
        {
            return value;
        }

        throw new ArgumentOutOfRangeException(
            propertyName,
            value,
            "A budget must be positive, or Timeout.InfiniteTimeSpan to leave the retrying unbounded.");
    }

    private static TimeSpan ValidateDelay(TimeSpan value, string propertyName)
    {
        if (value >= TimeSpan.Zero && value < TimeSpan.MaxValue)
        {
            return value;
        }

        throw new ArgumentOutOfRangeException(
            propertyName,
            value,
            "A delay must be zero or positive, and must be finite.");
    }
}
