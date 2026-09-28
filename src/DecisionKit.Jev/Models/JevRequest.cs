using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// The body of a System One evaluation request, exactly as it travels on the wire.
/// </summary>
/// <remarks>
/// <para>
/// This is a data transfer object, not a domain type. It exists so that a change in the TypeSafe
/// payload never reaches <c>DecisionKit.Core</c>, and so that the wire contract can be read in one
/// place instead of being inferred from serializer attributes scattered over the domain.
/// </para>
/// <para>
/// Every member is documented at
/// <see href="https://docs.typesafe.ai/api">the TypeSafe API reference</see>. All three members are
/// required by the service.
/// </para>
/// </remarks>
public sealed class JevRequest
{
    /// <summary>
    /// Gets the content to evaluate. A JSON string, object or array.
    /// </summary>
    /// <remarks>
    /// The field was called <c>document</c> before API v1 and a request carrying that name now
    /// fails validation.
    /// </remarks>
    [JsonPropertyName("state")]
    public JsonNode? State { get; init; }

    /// <summary>
    /// Gets the model that handles the request, such as <c>jev-latest</c>.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Gets the questions to evaluate, keyed by the identifier the caller chose.
    /// </summary>
    /// <remarks>
    /// The key is how an answer is correlated with its question. It is not sent to the model and
    /// plays no part in inference, so it can be any name that suits the calling code.
    /// </remarks>
    [JsonPropertyName("questions")]
    public IReadOnlyDictionary<string, JevQuestion> Questions { get; init; } =
        new Dictionary<string, JevQuestion>(StringComparer.Ordinal);
}
