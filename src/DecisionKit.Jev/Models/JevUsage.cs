using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DecisionKit.Jev.Models;

/// <summary>
/// What a System One evaluation consumed.
/// </summary>
/// <remarks>
/// The service documents two metrics. Anything else it reports is kept in
/// <see cref="Extensions"/> and surfaces as a provider-specific metric, so a new billing dimension
/// does not need a new release to be visible.
/// </remarks>
public sealed class JevUsage
{
    /// <summary>
    /// Gets the number of tokens the request consumed.
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public double? InputTokens { get; init; }

    /// <summary>
    /// Gets the number of tokens the answers consumed.
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public double? OutputTokens { get; init; }

    /// <summary>
    /// Gets or sets every usage metric the service sent that this version of the DTO does not
    /// model.
    /// </summary>
    /// <remarks>
    /// Extension data is the one member that cannot be <c>init</c>-only:
    /// <c>System.Text.Json</c> assigns it while the object is being read.
    /// </remarks>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json assigns extension data through the setter.")]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
