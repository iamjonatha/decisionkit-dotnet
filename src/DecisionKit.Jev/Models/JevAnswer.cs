using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// One answer of a System One response, exactly as it travels on the wire.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="JevQuestion"/> the shape is flat: <see cref="Type"/> selects which of the
/// optional members carry meaning. Anything the service sends that this version does not model is
/// kept in <see cref="Extensions"/> rather than discarded, which is what lets an unrecognized
/// answer reach the caller as data instead of as a failure.
/// </para>
/// <para>
/// The answer's identifier is the key it is stored under in <see cref="JevResponse.Answers"/> and
/// is never a member of the answer object itself.
/// </para>
/// </remarks>
public sealed class JevAnswer
{
    /// <summary>
    /// Gets the answer type: <c>noul</c>, <c>choice</c> or <c>score</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the probability that the answer to a <c>noul</c> question is yes, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// A <c>noul</c> answer carries no confidence. The probability is the whole answer: a value
    /// near 0.5 is the model reporting genuine uncertainty, not low confidence in a verdict.
    /// </remarks>
    [JsonPropertyName("noul")]
    public double? Noul { get; init; }

    /// <summary>
    /// Gets the option a <c>choice</c> question resolved to, echoed exactly as the request spelled
    /// it.
    /// </summary>
    [JsonPropertyName("choice")]
    public string? Choice { get; init; }

    /// <summary>
    /// Gets the value a <c>score</c> question resolved to, as a position across the levels of the
    /// rubric.
    /// </summary>
    /// <remarks>
    /// The value is probability-weighted, so it falls between two levels far more often than it
    /// lands on one.
    /// </remarks>
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    /// <summary>
    /// Gets how sure the model is of a <c>choice</c> or <c>score</c> answer, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// The service derives this from <see cref="Probabilities"/> but does not publish the formula,
    /// so DecisionKit treats it as an opaque value and never recomputes it.
    /// </remarks>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    /// <summary>
    /// Gets the full distribution behind a <c>choice</c> or <c>score</c> answer.
    /// </summary>
    /// <remarks>
    /// The keys are option names for a <c>choice</c>, and level indices written as strings, such as
    /// <c>"0"</c>, for a <c>score</c>. The shape is kept as JSON rather than as a typed dictionary
    /// so that an entry of an unexpected kind is skipped rather than failing the whole response.
    /// </remarks>
    [JsonPropertyName("probabilities")]
    public JsonObject? Probabilities { get; init; }

    /// <summary>
    /// Gets the rubric a <c>score</c> answer was measured against, keyed by level index written as
    /// a string.
    /// </summary>
    /// <remarks>
    /// This echoes the <c>criteria</c> the request sent, so an answer can be explained without the
    /// question at hand.
    /// </remarks>
    [JsonPropertyName("legend")]
    public JsonObject? Legend { get; init; }

    /// <summary>
    /// Gets or sets every property the service sent that this version of the DTO does not model.
    /// </summary>
    /// <remarks>
    /// Extension data is the one member that cannot be <c>init</c>-only:
    /// <c>System.Text.Json</c> assigns it while the object is being read.
    /// </remarks>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json assigns extension data through the setter.")]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
