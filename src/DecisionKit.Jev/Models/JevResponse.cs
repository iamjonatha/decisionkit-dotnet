using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// The body of a successful System One response, exactly as it travels on the wire.
/// </summary>
/// <remarks>
/// A response carries no identifier of its own: the request identifier is the
/// <c>x-typesafe-request-id</c> response header, which belongs to the transport rather than to the
/// payload. It also carries no timestamp, so the time of a decision is observed by the client.
/// </remarks>
public sealed class JevResponse
{
    /// <summary>
    /// Gets the model that answered, resolved to a concrete version such as <c>jev-1.13.0</c> even
    /// when the request asked for an alias.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Gets the answers, keyed by the identifier the request used for the question.
    /// </summary>
    /// <remarks>
    /// A question may be absent: the service does not promise an answer for everything it was
    /// asked.
    /// </remarks>
    [JsonPropertyName("answers")]
    public IReadOnlyDictionary<string, JevAnswer> Answers { get; init; } =
        new Dictionary<string, JevAnswer>(StringComparer.Ordinal);

    /// <summary>
    /// Gets what the evaluation consumed.
    /// </summary>
    [JsonPropertyName("usage")]
    public JevUsage? Usage { get; init; }
}
