using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Translates a failed JEV call into a domain error.
/// </summary>
/// <remarks>
/// <para>
/// What kind of failure occurred is decided by the HTTP status code, never by the <c>error_type</c>
/// the body carries. The set of error types is neither documented nor closed, so branching on it
/// would silently misclassify anything the service adds later. The value is still reported, under
/// <see cref="JevProtocol.ErrorTypeProperty"/>, for a caller who has a reason to look at it.
/// </para>
/// <para>
/// The <c>detail</c> member is polymorphic: an object for an error the application raised, a bare
/// string for one raised by the framework in front of it. Both are read, and a body that is neither
/// still produces an error, because a failure must never be lost to the shape of its own
/// description.
/// </para>
/// <para>
/// Status 529 is not an HTTP standard code. The service uses it to say it is overloaded, which is
/// transient, so it is classified as a provider error that is worth retrying rather than lumped in
/// with the permanent ones.
/// </para>
/// </remarks>
public sealed class ErrorMapper
{
    private const int OverloadedStatusCode = 529;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorMapper"/> class with the default mapping
    /// settings.
    /// </summary>
    public ErrorMapper()
        : this(JevMappingOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ErrorMapper(JevMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    /// <summary>
    /// Gets the mapper that uses the default mapping settings.
    /// </summary>
    public static ErrorMapper Default { get; } = new();

    /// <summary>
    /// Gets the mapping settings this mapper uses.
    /// </summary>
    public JevMappingOptions Options { get; }

    /// <summary>
    /// Translates a failed call into a domain error.
    /// </summary>
    /// <param name="body">The error body the service returned, or <see langword="null"/> when it returned none.</param>
    /// <param name="context">What is known about the failure besides its body.</param>
    /// <returns>The domain error.</returns>
    public DecisionError ToDomain(JevErrorResponse? body, JevErrorContext context)
    {
        (string? errorType, string? message) = Read(body?.Detail);

        DecisionErrorCategory category = Categorize(context.StatusCode);

        return new DecisionError(category, message ?? Describe(context.StatusCode))
        {
            Code = errorType,
            ProviderName = Options.ProviderName,
            ProviderRequestId = context.RequestId,
            Retry = ToRetry(category, context),
            Properties = ToProperties(errorType, context),
        };
    }

    /// <summary>
    /// Translates a failed call into the exception that reports it.
    /// </summary>
    /// <param name="body">The error body the service returned, or <see langword="null"/> when it returned none.</param>
    /// <param name="context">What is known about the failure besides its body.</param>
    /// <returns>
    /// The exception. Its runtime type follows the category, so a caller can catch
    /// <see cref="DecisionTransientException"/> without inspecting the error.
    /// </returns>
    public DecisionException ToException(JevErrorResponse? body, JevErrorContext context) =>
        DecisionException.FromError(ToDomain(body, context));

    private static (string? ErrorType, string? Message) Read(JsonNode? detail) => detail switch
    {
        JsonObject reported => (Text(reported["error_type"]), Text(reported["message"])),
        JsonValue text => (null, Text(text)),
        JsonArray reported when reported.Count > 0 => (null, reported.ToJsonString()),
        _ => (null, null),
    };

    private static string? Text(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        string? text = node.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? node.GetValue<string>()
            : node.ToJsonString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static DecisionErrorCategory Categorize(int? statusCode) => statusCode switch
    {
        null => DecisionErrorCategory.Transport,
        400 or 404 or 422 => DecisionErrorCategory.Validation,
        401 => DecisionErrorCategory.Authentication,
        403 => DecisionErrorCategory.Authorization,
        408 => DecisionErrorCategory.Timeout,
        429 => DecisionErrorCategory.RateLimit,
        >= 500 => DecisionErrorCategory.ProviderError,
        _ => DecisionErrorCategory.Unknown,
    };

    private static DecisionRetryHint ToRetry(DecisionErrorCategory category, JevErrorContext context)
    {
        if (context.RetryAfter is { } delay)
        {
            return DecisionRetryHint.After(delay);
        }

        return category switch
        {
            DecisionErrorCategory.RateLimit or DecisionErrorCategory.Timeout or DecisionErrorCategory.Transport =>
                DecisionRetryHint.Retryable,
            DecisionErrorCategory.ProviderError =>
                context.StatusCode is OverloadedStatusCode or 502 or 503 or 504
                    ? DecisionRetryHint.Retryable
                    : DecisionRetryHint.Unknown,
            DecisionErrorCategory.Validation or DecisionErrorCategory.Authentication or DecisionErrorCategory.Authorization =>
                DecisionRetryHint.NotRetryable,
            _ => DecisionRetryHint.Unknown,
        };
    }

    private static string Describe(int? statusCode) => statusCode switch
    {
        null => "The JEV call failed before the service replied.",
        OverloadedStatusCode => "JEV is overloaded and declined to evaluate the request.",
        _ => string.Create(CultureInfo.InvariantCulture, $"JEV replied with status {statusCode} and no description of what went wrong."),
    };

    private static Dictionary<string, object?> ToProperties(string? errorType, JevErrorContext context)
    {
        Dictionary<string, object?> properties = new(StringComparer.Ordinal);

        if (errorType is not null)
        {
            properties[JevProtocol.ErrorTypeProperty] = errorType;
        }

        if (context.StatusCode is { } statusCode)
        {
            properties[JevProtocol.StatusCodeProperty] = statusCode;
        }

        return properties;
    }
}
