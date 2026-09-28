using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Serialization;

/// <summary>
/// Converts between the provider-neutral metadata bags of the domain and the JSON values JEV
/// carries.
/// </summary>
/// <remarks>
/// <para>
/// Domain metadata is typed as <see cref="object"/>, and JSON has four scalar shapes. This type is
/// the single, explicit translation between the two, so that no mapper has to guess and no
/// reflection-based serializer call is needed for a caller-supplied value.
/// </para>
/// <para>
/// Everything that is not a JSON scalar, object or array is normalized to a string using the
/// invariant culture: a <see cref="DateTimeOffset"/>, a <see cref="Guid"/>, a
/// <see cref="TimeSpan"/> and an enumeration all travel as text. A value that cannot be represented
/// at all is rejected rather than silently dropped.
/// </para>
/// </remarks>
internal static class JevJsonValues
{
    internal static JsonObject? ToJsonObject(IReadOnlyDictionary<string, object?> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Count == 0)
        {
            return null;
        }

        JsonObject target = [];

        foreach (KeyValuePair<string, object?> entry in source)
        {
            target[entry.Key] = ToJsonNode(entry.Key, entry.Value);
        }

        return target;
    }

    internal static IReadOnlyDictionary<string, object?> ToMetadata(JsonObject? source)
    {
        if (source is null || source.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, object?> target = new(source.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNode?> entry in source)
        {
            target[entry.Key] = ToValue(entry.Value);
        }

        return target;
    }

    internal static IReadOnlyDictionary<string, object?> ToMetadata(IReadOnlyDictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, object?> target = new(source.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> entry in source)
        {
            target[entry.Key] = ToValue(entry.Value);
        }

        return target;
    }

    internal static object? ToValue(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.Number => ToNumber(node.AsValue()),
            JsonValueKind.Object => ToMetadata(node.AsObject()),
            JsonValueKind.Array => ToList(node.AsArray()),
            _ => null,
        };
    }

    internal static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long integer) ? integer : (object)element.GetDouble(),
        JsonValueKind.Object or JsonValueKind.Array => ToValue(JsonNode.Parse(element.GetRawText())),
        _ => null,
    };

    private static IReadOnlyDictionary<string, object?> EmptyMetadata { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    private static JsonNode? ToJsonNode(string key, object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode node:
                return node.DeepClone();
            case string text:
                return JsonValue.Create(text);
            case IReadOnlyDictionary<string, object?> nested:
                return ToNestedObject(key, nested);
            case IEnumerable sequence:
                return ToJsonArray(key, sequence);
            default:
                return ToScalar(key, value);
        }
    }

    private static JsonObject ToNestedObject(string key, IReadOnlyDictionary<string, object?> source)
    {
        JsonObject target = [];

        foreach (KeyValuePair<string, object?> entry in source)
        {
            target[entry.Key] = ToJsonNode(string.Create(CultureInfo.InvariantCulture, $"{key}.{entry.Key}"), entry.Value);
        }

        return target;
    }

    private static JsonArray ToJsonArray(string key, IEnumerable source)
    {
        JsonArray target = [];

        foreach (object? item in source)
        {
            target.Add(ToJsonNode(key, item));
        }

        return target;
    }

    private static JsonValue ToScalar(string key, object value)
    {
        // Only the four JSON scalar shapes are produced, so that a JsonNode written inside a
        // source-generated payload never needs a converter the JEV context does not declare.
        return value switch
        {
            bool flag => JsonValue.Create(flag),
            byte or sbyte or short or ushort or int or uint or long =>
                JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            ulong or float or double or decimal =>
                JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            char character => JsonValue.Create(character.ToString()),
            DateTimeOffset timestamp => JsonValue.Create(timestamp.ToString("O", CultureInfo.InvariantCulture)),
            DateTime timestamp => JsonValue.Create(timestamp.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly date => JsonValue.Create(date.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly time => JsonValue.Create(time.ToString("O", CultureInfo.InvariantCulture)),
            TimeSpan duration => JsonValue.Create(duration.ToString("c", CultureInfo.InvariantCulture)),
            Guid identifier => JsonValue.Create(identifier.ToString("d", CultureInfo.InvariantCulture)),
            Uri uri => JsonValue.Create(uri.ToString()),
            Enum member => JsonValue.Create(member.ToString()),
            _ => throw Unrepresentable(key, value),
        };
    }

    private static object ToNumber(JsonValue value) =>
        value.TryGetValue(out long integer) ? integer : (object)value.GetValue<double>();

    internal static bool TryGetDouble(JsonNode? node, out double value)
    {
        if (node is JsonValue candidate && candidate.GetValueKind() == JsonValueKind.Number)
        {
            return candidate.TryGetValue(out value);
        }

        value = 0;
        return false;
    }

    private static List<object?> ToList(JsonArray source)
    {
        List<object?> items = new(source.Count);

        foreach (JsonNode? item in source)
        {
            items.Add(ToValue(item));
        }

        return items;
    }

    private static DecisionException Unrepresentable(string key, object value) =>
        DecisionException.FromError(
            new DecisionError(
                DecisionErrorCategory.Validation,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Metadata entry '{key}' holds a value of type {value.GetType()}, which has no JSON representation. Convert it to a string, a number, a boolean, a dictionary or a sequence before sending it."))
            {
                Code = JevProtocol.UnrepresentableMetadataCode,
                ProviderName = JevProtocol.ProviderName,
            });
}
