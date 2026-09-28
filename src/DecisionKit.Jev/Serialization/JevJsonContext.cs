using System.Text.Json.Serialization;
using DecisionKit.Jev.Models;

namespace DecisionKit.Jev.Serialization;

/// <summary>
/// The source-generated metadata for every JEV wire type.
/// </summary>
/// <remarks>
/// <para>
/// All JEV serialization goes through this context. Nothing in the package calls a reflection-based
/// <see cref="System.Text.Json.JsonSerializer"/> overload, which is what keeps the package trimmable
/// and AOT-compatible instead of merely claiming to be.
/// </para>
/// <para>
/// The naming policy is declared here so that a member without an explicit
/// <see cref="JsonPropertyNameAttribute"/> still lands on a snake_case name. Every DTO member is
/// annotated anyway; the policy is the safety net, not the contract.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(JevRequest))]
[JsonSerializable(typeof(JevQuestion))]
[JsonSerializable(typeof(JevResponse))]
[JsonSerializable(typeof(JevAnswer))]
[JsonSerializable(typeof(JevErrorResponse))]
internal sealed partial class JevJsonContext : JsonSerializerContext
{
}
