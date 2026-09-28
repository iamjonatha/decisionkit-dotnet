using System;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// The settings that govern how DecisionKit translates between its domain and the JEV protocol.
/// </summary>
/// <remarks>
/// The settings are about meaning, not about transport. Nothing here says where to send a request
/// or how to authenticate it.
/// </remarks>
public sealed class JevMappingOptions
{
    /// <summary>
    /// Gets the settings used when the caller supplies none.
    /// </summary>
    public static JevMappingOptions Default { get; } = new();

    /// <summary>
    /// Gets the provider name reported on results and failures.
    /// </summary>
    /// <value>The default is <see cref="JevProtocol.ProviderName"/>.</value>
    /// <exception cref="ArgumentException">The value is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string ProviderName
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    } = JevProtocol.ProviderName;

    /// <summary>
    /// Gets the model requested when the request does not name one.
    /// </summary>
    /// <value>The default is <see cref="JevProtocol.DefaultModel"/>.</value>
    /// <remarks>
    /// The service reports the concrete version it resolved the request to, which reaches the
    /// caller as <see cref="DecisionKit.Results.DecisionMetadata.ModelVersion"/> and can differ from
    /// what was asked for.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string Model
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    } = JevProtocol.DefaultModel;

    /// <summary>
    /// Gets a value indicating whether an answer keeps the JSON it was read from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keeping the raw payload makes a surprising answer diagnosable, because the exact bytes the
    /// service sent are still available next to the interpretation of them. It costs memory
    /// proportional to the response, so it is off by default and worth turning on while
    /// investigating.
    /// </para>
    /// <para>
    /// This governs only what is retained after mapping. An answer of a type this package does not
    /// recognize always keeps its payload, because that payload is the only description of it.
    /// </para>
    /// </remarks>
    public bool PreserveRawPayloads { get; init; }
}
