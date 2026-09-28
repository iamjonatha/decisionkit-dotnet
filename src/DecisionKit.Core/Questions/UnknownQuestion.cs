using System;
using DecisionKit.Answers;
using DecisionKit.Identifiers;

namespace DecisionKit.Questions;

/// <summary>
/// Represents a question type DecisionKit does not model yet.
/// </summary>
/// <remarks>
/// A configuration-driven caller may need to send a question type that a newer provider supports
/// and DecisionKit does not. Such a question is passed through with the provider type name intact,
/// and its answer comes back as an <see cref="UnknownAnswer"/>.
/// </remarks>
public sealed class UnknownQuestion : Question<UnknownAnswer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownQuestion"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <param name="providerType">The question type name the provider expects.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, or a string argument is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">A string argument is <see langword="null"/>.</exception>
    public UnknownQuestion(QuestionId id, string prompt, string providerType)
        : base(id, prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        ProviderType = providerType.Trim();
    }

    /// <summary>
    /// Gets the question type name the provider expects.
    /// </summary>
    public string ProviderType { get; }

    /// <summary>
    /// Gets the provider-specific definition of the question, serialized by the caller.
    /// </summary>
    /// <remarks>
    /// The value is opaque to <c>DecisionKit.Core</c>. The provider package decides how to place it
    /// on the wire.
    /// </remarks>
    public string? RawDefinition { get; init; }
}
