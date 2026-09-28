using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Serialization;

/// <summary>
/// Reads and writes the JEV wire format.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place in the package that turns a JEV type into text and back. It exposes the
/// source-generated <see cref="JsonTypeInfo{T}"/> for each wire type so that the transport layer can
/// hand it to <c>HttpClient</c> without ever reaching a reflection-based overload.
/// </para>
/// <para>
/// A malformed payload is reported as a <see cref="DecisionException"/> categorized as
/// <see cref="DecisionErrorCategory.Serialization"/>. The error carries the JSON path and position,
/// which are structural, and never the payload itself.
/// </para>
/// </remarks>
public static class JevJsonSerialization
{
    /// <summary>
    /// Gets the serialization metadata for a JEV request.
    /// </summary>
    public static JsonTypeInfo<JevRequest> RequestTypeInfo => JevJsonContext.Default.JevRequest;

    /// <summary>
    /// Gets the serialization metadata for a JEV response.
    /// </summary>
    public static JsonTypeInfo<JevResponse> ResponseTypeInfo => JevJsonContext.Default.JevResponse;

    /// <summary>
    /// Gets the serialization metadata for a single JEV question.
    /// </summary>
    public static JsonTypeInfo<JevQuestion> QuestionTypeInfo => JevJsonContext.Default.JevQuestion;

    /// <summary>
    /// Gets the serialization metadata for a single JEV answer.
    /// </summary>
    public static JsonTypeInfo<JevAnswer> AnswerTypeInfo => JevJsonContext.Default.JevAnswer;

    /// <summary>
    /// Gets the serialization metadata for a JEV error body.
    /// </summary>
    public static JsonTypeInfo<JevErrorResponse> ErrorTypeInfo => JevJsonContext.Default.JevErrorResponse;

    /// <summary>
    /// Writes a JEV request as JSON.
    /// </summary>
    /// <param name="request">The request to write.</param>
    /// <returns>The JSON payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public static string Serialize(JevRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(request, RequestTypeInfo);
    }

    /// <summary>
    /// Writes a JEV response as JSON.
    /// </summary>
    /// <param name="response">The response to write.</param>
    /// <returns>The JSON payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    public static string Serialize(JevResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(response, ResponseTypeInfo);
    }

    /// <summary>
    /// Writes a single JEV answer as JSON.
    /// </summary>
    /// <param name="answer">The answer to write.</param>
    /// <returns>The JSON payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static string Serialize(JevAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        return JsonSerializer.Serialize(answer, AnswerTypeInfo);
    }

    /// <summary>
    /// Writes a JEV error body as JSON.
    /// </summary>
    /// <param name="error">The error body to write.</param>
    /// <returns>The JSON payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static string Serialize(JevErrorResponse error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return JsonSerializer.Serialize(error, ErrorTypeInfo);
    }

    /// <summary>
    /// Reads a JEV request from JSON.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The request the payload describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">The payload is not a readable JEV request.</exception>
    public static JevRequest DeserializeRequest(string json) => Read(json, RequestTypeInfo, "request");

    /// <summary>
    /// Reads a JEV response from JSON.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The response the payload describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">The payload is not a readable JEV response.</exception>
    public static JevResponse DeserializeResponse(string json) => Read(json, ResponseTypeInfo, "response");

    /// <summary>
    /// Reads a JEV error body from JSON.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The error body the payload describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">The payload is not a readable JEV error body.</exception>
    public static JevErrorResponse DeserializeError(string json) => Read(json, ErrorTypeInfo, "error body");

    /// <summary>
    /// Reads a JEV response from a stream of JSON.
    /// </summary>
    /// <param name="json">The stream holding the payload.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The response the payload describes.</returns>
    /// <remarks>
    /// This is the overload the transport uses, so that a response body is never materialized as a
    /// string on the way to being parsed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">The payload is not a readable JEV response.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static ValueTask<JevResponse> DeserializeResponseAsync(Stream json, CancellationToken cancellationToken) =>
        ReadAsync(json, ResponseTypeInfo, "response", cancellationToken);

    /// <summary>
    /// Reads a JEV error body from a stream of JSON.
    /// </summary>
    /// <param name="json">The stream holding the payload.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The error body the payload describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">The payload is not a readable JEV error body.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static ValueTask<JevErrorResponse> DeserializeErrorAsync(Stream json, CancellationToken cancellationToken) =>
        ReadAsync(json, ErrorTypeInfo, "error body", cancellationToken);

    private static async ValueTask<T> ReadAsync<T>(
        Stream json,
        JsonTypeInfo<T> typeInfo,
        string what,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(json);

        T? value;

        try
        {
            value = await JsonSerializer.DeserializeAsync(json, typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw DecisionException.FromError(UnreadableError(what, exception), exception);
        }

        return value ?? throw DecisionException.FromError(EmptyError(what));
    }

    private static T Read<T>(string json, JsonTypeInfo<T> typeInfo, string what)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(json);

        T? value;

        try
        {
            value = JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException exception)
        {
            throw DecisionException.FromError(UnreadableError(what, exception), exception);
        }

        return value ?? throw DecisionException.FromError(EmptyError(what));
    }

    private static DecisionError UnreadableError(string what, JsonException exception)
    {
        Dictionary<string, object?> properties = new(StringComparer.Ordinal);

        if (exception.Path is { } path)
        {
            properties["json_path"] = path;
        }

        if (exception.LineNumber is { } line)
        {
            properties["json_line"] = line;
        }

        if (exception.BytePositionInLine is { } position)
        {
            properties["json_position"] = position;
        }

        return new DecisionError(
            DecisionErrorCategory.Serialization,
            string.Create(CultureInfo.InvariantCulture, $"The JEV {what} could not be read as JSON."))
        {
            Code = JevProtocol.PayloadUnreadableCode,
            ProviderName = JevProtocol.ProviderName,
            Properties = properties,
        };
    }

    private static DecisionError EmptyError(string what) =>
        new(
            DecisionErrorCategory.Serialization,
            string.Create(CultureInfo.InvariantCulture, $"The JEV {what} payload is the JSON literal null."))
        {
            Code = JevProtocol.PayloadUnreadableCode,
            ProviderName = JevProtocol.ProviderName,
        };
}
