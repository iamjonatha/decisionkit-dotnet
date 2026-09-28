using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// One question of a System One request, exactly as it travels on the wire.
/// </summary>
/// <remarks>
/// <para>
/// The shape is flat rather than polymorphic: <see cref="Type"/> selects what
/// <see cref="Criteria"/> means. A polymorphic hierarchy would have to reject a type name it has
/// never seen, and a provider is expected to ship new question types before DecisionKit models
/// them.
/// </para>
/// <para>
/// The question's identifier is the key it is stored under in
/// <see cref="JevRequest.Questions"/> and is never a member of the question object itself.
/// </para>
/// </remarks>
public sealed class JevQuestion
{
    /// <summary>
    /// Gets the question type: <c>noul</c>, <c>choice</c> or <c>score</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets what the model should evaluate. A JSON string, object or array.
    /// </summary>
    /// <remarks>
    /// The API reference marks this as required for all three question types.
    /// </remarks>
    [JsonPropertyName("instructions")]
    public JsonNode? Instructions { get; init; }

    /// <summary>
    /// Gets the rubric, whose shape depends on <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>noul</c>: an optional object with <c>true</c> and <c>false</c> members describing what a
    /// yes and a no mean.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>choice</c>: a required object mapping each option to a description, or to
    /// <see langword="null"/> when the option needs no further detail.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>score</c>: a required array of level descriptions, in ascending order.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("criteria")]
    public JsonNode? Criteria { get; init; }
}
