using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;

namespace DecisionKit.Extensions.Configuration;

/// <summary>
/// The configurable shape of <see cref="DecisionKit.Jev.Resilience.JevRetryPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every value is nullable, and nothing means "not configured" rather than "zero". That is the
/// whole reason this type exists next to the policy it produces: a configuration binder writes
/// default values into every property it can see, so binding straight onto the policy would turn
/// an unmentioned setting into a deliberate zero.
/// </para>
/// <para>
/// A value left unset keeps the library default. The policy itself validates, so an impossible
/// combination is reported while the application starts rather than on the first failed call.
/// </para>
/// </remarks>
public sealed class JevRetrySettings
{
    /// <summary>
    /// Gets or sets how many attempts one operation may make in total, the first one included.
    /// </summary>
    /// <remarks>
    /// Leaving this unset leaves retrying off, which is the library default. This is the single
    /// switch that turns the whole section on.
    /// </remarks>
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets how long attempts and waits together may take before the operation stops
    /// retrying.
    /// </summary>
    public TimeSpan? MaxElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets how long the caller waits before the second attempt.
    /// </summary>
    public TimeSpan? InitialDelay { get; set; }

    /// <summary>
    /// Gets or sets the factor each wait is multiplied by to produce the next one.
    /// </summary>
    public double? BackoffFactor { get; set; }

    /// <summary>
    /// Gets or sets the longest wait the backoff may grow to.
    /// </summary>
    public TimeSpan? MaxDelay { get; set; }

    /// <summary>
    /// Gets or sets the fraction of a computed wait that is randomized away, from 0 to 1.
    /// </summary>
    public double? Jitter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a delay the service asked for replaces the computed
    /// backoff.
    /// </summary>
    public bool? RespectRetryAfter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a failure the provider gave no retry signal for is
    /// repeated.
    /// </summary>
    public bool? RetryUnknownFailures { get; set; }

    /// <summary>
    /// Gets the HTTP status codes this deployment repeats regardless of what the provider
    /// signalled.
    /// </summary>
    public IList<int> RetryableStatusCodes { get; } = [];

    /// <summary>
    /// Gets the HTTP status codes this deployment never repeats, whatever the provider signalled.
    /// </summary>
    public IList<int> NonRetryableStatusCodes { get; } = [];

    /// <summary>
    /// Gets or sets what the policy does about idempotency when it repeats a call.
    /// </summary>
    /// <remarks>
    /// JEV accepts <see cref="DecisionIdempotencyPolicy.Disabled"/> only. The setting is
    /// configurable anyway so that the constraint is discovered while configuring the application
    /// rather than inferred from a repeated call being charged twice.
    /// </remarks>
    public DecisionIdempotencyPolicy? Idempotency { get; set; }

    /// <summary>
    /// Gets a value indicating whether anything in this section was configured.
    /// </summary>
    internal bool IsConfigured =>
        MaxAttempts.HasValue
        || MaxElapsedTime.HasValue
        || InitialDelay.HasValue
        || BackoffFactor.HasValue
        || MaxDelay.HasValue
        || Jitter.HasValue
        || RespectRetryAfter.HasValue
        || RetryUnknownFailures.HasValue
        || RetryableStatusCodes.Count > 0
        || NonRetryableStatusCodes.Count > 0
        || Idempotency.HasValue;
}
