using System;
using System.Threading;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;

namespace DecisionKit.Jev.Client;

/// <summary>
/// The settings that govern how DecisionKit talks to the JEV service.
/// </summary>
/// <remarks>
/// <para>
/// These are transport settings: where to send a request and how long to wait for it. What a
/// request <em>means</em> lives in <see cref="JevMappingOptions"/>, reachable here as
/// <see cref="Mapping"/>, and what a decision means lives in
/// <see cref="DecisionKit.Providers.DecisionOptions"/> on the request itself. Keeping the three
/// apart is what lets a caller change a timeout without touching a model name, and change a model
/// name without touching a question.
/// </para>
/// <para>
/// Instances are immutable, and every value is validated on assignment, so an options object that
/// exists is an options object that can be used.
/// </para>
/// </remarks>
public sealed class JevProviderOptions
{
    /// <summary>
    /// Gets the settings used when the caller supplies none: the public service, the default
    /// mapping, and the default timeouts.
    /// </summary>
    public static JevProviderOptions Default { get; } = new();

    /// <summary>
    /// Gets the address the service is reached at.
    /// </summary>
    /// <value>The default is <see cref="JevEndpoint.Default"/>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public JevEndpoint Endpoint
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = JevEndpoint.Default;

    /// <summary>
    /// Gets the settings that govern the translation between the domain and the protocol.
    /// </summary>
    /// <value>The default is <see cref="JevMappingOptions.Default"/>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public JevMappingOptions Mapping
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = JevMappingOptions.Default;

    /// <summary>
    /// Gets how long one HTTP attempt may take before it is abandoned.
    /// </summary>
    /// <value>The default is 30 seconds.</value>
    /// <remarks>
    /// This bounds a single call to the service, including reading its response. It exists
    /// separately from <see cref="OperationTimeout"/> so that a retry policy can give up on one
    /// slow attempt without giving up on the operation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative, and is not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan AttemptTimeout
    {
        get;
        init
        {
            field = Validate(value, nameof(AttemptTimeout));
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets how long the whole operation may take before it is abandoned, across every attempt.
    /// </summary>
    /// <value>The default is 60 seconds.</value>
    /// <remarks>
    /// This is the budget the caller actually cares about. It is deliberately not derived from
    /// <see cref="AttemptTimeout"/>: how long a caller is willing to wait for an answer has nothing
    /// to do with how long one network round trip is allowed to take.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative, and is not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan OperationTimeout
    {
        get;
        init
        {
            field = Validate(value, nameof(OperationTimeout));
        }
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets how long the provider is willing to wait between two attempts.
    /// </summary>
    /// <value>The default is 30 seconds.</value>
    /// <remarks>
    /// This is the third budget, and the only one that bounds time spent not calling anything. A
    /// wait longer than this abandons the retry and reports the failure that was being retried,
    /// rather than being shortened: a delay the service asked for means nothing if it is not
    /// honoured, and arriving early would be worse than not arriving.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative, and is not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan RetryDelayTimeout
    {
        get;
        init
        {
            field = Validate(value, nameof(RetryDelayTimeout));
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the policy that decides whether a failed call is repeated.
    /// </summary>
    /// <value>The default is <see cref="JevRetryPolicy.None"/>, which never repeats one.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public JevRetryPolicy Retry
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = JevRetryPolicy.None;

    private static TimeSpan Validate(TimeSpan value, string propertyName)
    {
        if (value == Timeout.InfiniteTimeSpan || value > TimeSpan.Zero)
        {
            return value;
        }

        throw new ArgumentOutOfRangeException(
            propertyName,
            value,
            "A timeout must be positive, or Timeout.InfiniteTimeSpan to wait indefinitely.");
    }
}
