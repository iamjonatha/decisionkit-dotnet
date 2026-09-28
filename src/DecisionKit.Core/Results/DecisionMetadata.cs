using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Internal;

namespace DecisionKit.Results;

/// <summary>
/// Describes how a decision result was produced.
/// </summary>
/// <remarks>
/// Three correlation identifiers are kept apart on purpose: the client request identifier the
/// caller generated, the request identifier the provider assigned, and the trace identifier of the
/// distributed trace. Collapsing them into one field makes production incidents harder to
/// investigate, not easier.
/// </remarks>
public sealed class DecisionMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionMetadata"/> class.
    /// </summary>
    /// <param name="providerName">The name of the provider that produced the result.</param>
    /// <param name="timestamp">The moment the result was produced.</param>
    /// <exception cref="ArgumentException"><paramref name="providerName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="providerName"/> is <see langword="null"/>.</exception>
    public DecisionMetadata(string providerName, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        ProviderName = providerName.Trim();
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the name of the provider that produced the result.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the moment the result was produced.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the request identifier the caller generated, when one was supplied.
    /// </summary>
    public RequestId? ClientRequestId { get; init; }

    /// <summary>
    /// Gets the request identifier the provider assigned, when the provider reports one.
    /// </summary>
    public string? ProviderRequestId { get; init; }

    /// <summary>
    /// Gets the trace identifier of the distributed trace the call belonged to, when one existed.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the model or protocol version the provider used, when the provider exposes one.
    /// </summary>
    public string? ModelVersion { get; init; }

    /// <summary>
    /// Gets the time the call took, measured by the client.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned latency is negative.</exception>
    public TimeSpan? Latency
    {
        get;
        init
        {
            if (value is { Ticks: < 0 })
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Latency cannot be negative.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the provider-specific metadata that has no provider-neutral counterpart.
    /// </summary>
    /// <remarks>
    /// This bag is the documented place for data that would otherwise force a provider concept into
    /// the shared domain. The collection is copied on assignment.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Properties
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;
}
