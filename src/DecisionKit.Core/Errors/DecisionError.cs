using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Internal;

namespace DecisionKit.Errors;

/// <summary>
/// The structured description of one decision failure.
/// </summary>
/// <remarks>
/// <para>
/// Everything a provider reports about a failure — its own error code, the request identifier it
/// assigned, the page documenting the code, whether the call may be repeated — is carried here and
/// stays readable by the caller. It is never flattened into an exception message, because a message
/// is written for a human reading a log, while this is read by code deciding what to do next.
/// </para>
/// <para>
/// An error is delivered to the caller by <see cref="DecisionException"/>. It is also useful on its
/// own, for example when a provider records a failure it then translates.
/// </para>
/// </remarks>
public sealed class DecisionError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionError"/> class.
    /// </summary>
    /// <param name="category">The category the failure belongs to.</param>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public DecisionError(DecisionErrorCategory category, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Category = category;
        Message = message.Trim();
    }

    /// <summary>
    /// Gets the category the failure belongs to.
    /// </summary>
    public DecisionErrorCategory Category { get; }

    /// <summary>
    /// Gets the human-readable description of the failure.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the error code the provider reported, when it reported one.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Gets the name of the provider that produced the failure.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Gets the request identifier the provider assigned, which is what provider support will ask
    /// for.
    /// </summary>
    public string? ProviderRequestId { get; init; }

    /// <summary>
    /// Gets the page documenting the reported error code, when the provider links one.
    /// </summary>
    public Uri? DocumentationUrl { get; init; }

    /// <summary>
    /// Gets what the provider said about repeating the call. Defaults to
    /// <see cref="DecisionRetryHint.Unknown"/>.
    /// </summary>
    public DecisionRetryHint Retry { get; init; }

    /// <summary>
    /// Gets the remaining diagnostic data the provider supplied.
    /// </summary>
    /// <remarks>
    /// This is the documented place for provider detail that has no provider-neutral counterpart,
    /// so that mapping a failure never has to discard it. The collection is copied on assignment.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Properties
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;

    /// <inheritdoc />
    public override string ToString() => Code is null
        ? string.Create(CultureInfo.InvariantCulture, $"{Category}: {Message}")
        : string.Create(CultureInfo.InvariantCulture, $"{Category} [{Code}]: {Message}");
}
