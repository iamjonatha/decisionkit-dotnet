using System;
using System.Collections.Generic;
using DecisionKit.Internal;

namespace DecisionKit.Providers;

/// <summary>
/// What a decision is about: the text, the state, or both, that the questions are asked against.
/// </summary>
/// <remarks>
/// <para>
/// Providers disagree about what they evaluate. Some take a piece of text, some take a structured
/// state object, some take both. Modelling the input as a closed type of its own keeps that
/// disagreement out of <see cref="DecisionRequest"/> and out of every question type.
/// </para>
/// <para>
/// An input is optional. A question can be self-contained, and a request that carries only
/// questions is valid.
/// </para>
/// </remarks>
public sealed class DecisionInput
{
    private DecisionInput(string? text, IReadOnlyDictionary<string, object?> properties)
    {
        Text = text;
        Properties = properties;
    }

    /// <summary>
    /// Gets the input that carries nothing.
    /// </summary>
    public static DecisionInput Empty { get; } = new(null, MetadataSnapshot.Empty);

    /// <summary>
    /// Gets the text the questions are asked against, when the decision is about text.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the state the questions are asked against, when the decision is about structured data.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets a value indicating whether this input carries neither text nor state.
    /// </summary>
    public bool IsEmpty => Text is null && Properties.Count == 0;

    /// <summary>
    /// Creates an input from text.
    /// </summary>
    /// <param name="text">The text the questions are asked against.</param>
    /// <returns>The created input.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static DecisionInput FromText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new DecisionInput(text, MetadataSnapshot.Empty);
    }

    /// <summary>
    /// Creates an input from structured state.
    /// </summary>
    /// <param name="properties">The state the questions are asked against.</param>
    /// <returns>The created input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="properties"/> is <see langword="null"/>.</exception>
    public static DecisionInput FromProperties(IReadOnlyDictionary<string, object?> properties) =>
        new(null, MetadataSnapshot.Create(properties));

    /// <summary>
    /// Creates an input from text and structured state.
    /// </summary>
    /// <param name="text">The text the questions are asked against, or <see langword="null"/>.</param>
    /// <param name="properties">The state the questions are asked against.</param>
    /// <returns>The created input.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="properties"/> is <see langword="null"/>.</exception>
    public static DecisionInput Create(string? text, IReadOnlyDictionary<string, object?> properties)
    {
        if (text is not null && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("An input text cannot be empty or whitespace. Pass null instead.", nameof(text));
        }

        return new DecisionInput(text, MetadataSnapshot.Create(properties));
    }
}
