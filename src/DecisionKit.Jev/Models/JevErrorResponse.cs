using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// The body of an unsuccessful System One response, exactly as it travels on the wire.
/// </summary>
/// <remarks>
/// <para>
/// The service reports what went wrong under a single <c>detail</c> member, and that member is
/// polymorphic: an object for an error the application raised, and a bare string for one raised by
/// the framework in front of it, such as <c>{"detail":"Not Found"}</c>. It is kept as JSON here so
/// that both forms survive, and is interpreted by
/// <see cref="DecisionKit.Jev.Protocol.ErrorMapper"/>.
/// </para>
/// <para>
/// There is no retry member and no category member. What a failure means comes from the HTTP
/// status code, and when to try again comes from the response headers.
/// </para>
/// </remarks>
public sealed class JevErrorResponse
{
    /// <summary>
    /// Gets what the service reported, either an object carrying <c>error_type</c> and
    /// <c>message</c>, or a bare string.
    /// </summary>
    [JsonPropertyName("detail")]
    public JsonNode? Detail { get; init; }
}
