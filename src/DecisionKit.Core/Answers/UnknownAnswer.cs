using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Internal;

namespace DecisionKit.Answers;

/// <summary>
/// Represents an answer whose shape DecisionKit does not model yet.
/// </summary>
/// <remarks>
/// A provider will ship a new answer type before DecisionKit supports it. When that happens the
/// response must not fail: the unrecognized answer is preserved here, with everything the provider
/// sent, so that the caller can still reach the data and the rest of the result stays usable.
/// This is a safety net, not the primary API.
/// </remarks>
public sealed class UnknownAnswer : Answer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownAnswer"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <param name="providerType">The answer type name as the provider reported it.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="questionId"/> is uninitialized, or <paramref name="providerType"/> is empty
    /// or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <see langword="null"/>.</exception>
    public UnknownAnswer(QuestionId questionId, string providerType)
        : base(questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        ProviderType = providerType.Trim();
    }

    /// <summary>
    /// Gets the answer type name as the provider reported it.
    /// </summary>
    public string ProviderType { get; }

    /// <summary>
    /// Gets the payload exactly as the provider sent it, when the provider was configured to
    /// preserve it.
    /// </summary>
    public string? RawPayload { get; init; }

    /// <summary>
    /// Gets the properties the provider returned that DecisionKit could not map to a known member.
    /// </summary>
    public IReadOnlyDictionary<string, object?> UnknownProperties
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;

    /// <inheritdoc />
    public override Type? ValueType => null;
}
