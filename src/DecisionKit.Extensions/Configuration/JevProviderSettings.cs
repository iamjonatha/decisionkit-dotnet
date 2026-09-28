using System;
using System.Diagnostics;
using System.Globalization;

namespace DecisionKit.Extensions.Configuration;

/// <summary>
/// The configurable shape of a JEV provider registration.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DecisionKit.Jev.Client.JevProviderOptions"/> is immutable and validates every value
/// as it is assigned, which is what makes an options object that exists an options object that can
/// be used. A configuration binder needs the opposite: a type it can create empty and write into
/// one property at a time. This type is that shape, and nothing more. It is translated into the
/// validated one while the application starts.
/// </para>
/// <para>
/// Every value is optional, and an unset value keeps the library default rather than becoming a
/// zero.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class JevProviderSettings
{
    /// <summary>
    /// Gets or sets the API key requests are authenticated with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place a credential appears in this package, and it is here because
    /// configuration is where a deployment already keeps one: a user-secrets entry, an environment
    /// variable, a mounted file, a key vault provider. The value is never logged and never appears
    /// in <see cref="ToString"/> or in a validation message.
    /// </para>
    /// <para>
    /// Leave it unset when the key comes from somewhere the configuration system cannot reach, and
    /// register a credential provider instead. A registration that has neither fails at startup.
    /// </para>
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the address the service is reached at, without the version segment.
    /// </summary>
    /// <remarks>The default is the public service.</remarks>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the name this provider reports on results and failures.
    /// </summary>
    /// <remarks>
    /// The default is the name the provider was registered under, so two registrations are told
    /// apart in a log without configuring anything.
    /// </remarks>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the model requested when a request does not name one.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an answer keeps the JSON it was read from.
    /// </summary>
    /// <remarks>
    /// This is a diagnostic switch. It costs memory proportional to the response and is off by
    /// default; turn it on while investigating a surprising answer, and turn it off afterwards.
    /// </remarks>
    public bool? PreserveRawPayloads { get; set; }

    /// <summary>
    /// Gets or sets how long one HTTP attempt may take before it is abandoned.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; set; }

    /// <summary>
    /// Gets or sets how long the whole operation may take, across every attempt.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// Gets or sets how long the provider is willing to wait between two attempts.
    /// </summary>
    public TimeSpan? RetryDelayTimeout { get; set; }

    /// <summary>
    /// Gets the settings that decide whether a failed call is repeated.
    /// </summary>
    /// <remarks>
    /// Retrying is off until <see cref="JevRetrySettings.MaxAttempts"/> is set to more than one.
    /// </remarks>
    public JevRetrySettings Retry { get; } = new();

    /// <summary>
    /// Returns a description of these settings with the API key removed.
    /// </summary>
    /// <returns>
    /// The values that are safe to show. The key is replaced by a fixed placeholder, so a
    /// diagnostic dump of a bound options object cannot leak it.
    /// </returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"JevProviderSettings {{ Endpoint = {Endpoint ?? "<default>"}, Model = {Model ?? "<default>"}, ApiKey = {(string.IsNullOrWhiteSpace(ApiKey) ? "<none>" : "<redacted>")} }}");
}
