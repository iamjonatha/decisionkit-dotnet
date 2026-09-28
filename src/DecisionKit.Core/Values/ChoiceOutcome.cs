using System;
using System.Collections.Generic;
using DecisionKit.Internal;

namespace DecisionKit.Values;

/// <summary>
/// Describes the outcome of a choice question without naming the option type.
/// </summary>
/// <remarks>
/// <para>
/// A mapper or a test double holds a choice question as <see cref="Questions.IChoiceQuestion"/>,
/// which deliberately hides the option type. It still has to describe everything a provider
/// returned — a selection, a distribution, or both, plus the metadata that came with them — and
/// <see cref="Choice{TOption}"/> cannot be built without the type. This is the untyped description
/// of that outcome; the question turns it into a typed <see cref="Choice{TOption}"/>.
/// </para>
/// <para>
/// An outcome carries at least one of a selection and a distribution, because an answer that
/// carries neither says nothing.
/// </para>
/// </remarks>
public sealed class ChoiceOutcome
{
    private static readonly OptionProbability[] s_noDistribution = [];

    private ChoiceOutcome(object? selection, bool hasSelection, IReadOnlyList<OptionProbability> distribution)
    {
        Selection = selection;
        HasSelection = hasSelection;
        Distribution = distribution;
    }

    /// <summary>
    /// Gets a value indicating whether the outcome commits to one option.
    /// </summary>
    public bool HasSelection { get; }

    /// <summary>
    /// Gets the selected option, or <see langword="null"/> when the outcome carries only a
    /// distribution.
    /// </summary>
    public object? Selection { get; }

    /// <summary>
    /// Gets the probabilities assigned to the options, in provider order.
    /// </summary>
    public IReadOnlyList<OptionProbability> Distribution { get; }

    /// <summary>
    /// Gets a value indicating whether the outcome carries a distribution.
    /// </summary>
    public bool HasDistribution => Distribution.Count > 0;

    /// <summary>
    /// Gets the metadata to attach to the answer built from this outcome.
    /// </summary>
    /// <remarks>The collection is copied on assignment.</remarks>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get;
        private init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;

    /// <summary>
    /// Creates a copy of this outcome that carries the given metadata.
    /// </summary>
    /// <param name="metadata">The metadata to attach to the answer built from the outcome.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
    public ChoiceOutcome WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ChoiceOutcome(Selection, HasSelection, Distribution) { Metadata = metadata };
    }

    /// <summary>
    /// Creates an outcome that commits to one option.
    /// </summary>
    /// <param name="selection">The selected option.</param>
    /// <returns>The created outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    public static ChoiceOutcome FromSelection(object selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new ChoiceOutcome(selection, hasSelection: true, s_noDistribution);
    }

    /// <summary>
    /// Creates an outcome that carries a distribution and no selected option.
    /// </summary>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <returns>The created outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="distribution"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="distribution"/> is empty.</exception>
    public static ChoiceOutcome FromDistribution(IReadOnlyList<OptionProbability> distribution)
    {
        RequireEntries(distribution);

        return new ChoiceOutcome(null, hasSelection: false, distribution);
    }

    /// <summary>
    /// Creates an outcome that carries both a distribution and the option the provider committed to.
    /// </summary>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <param name="selection">The selected option.</param>
    /// <returns>The created outcome.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="distribution"/> or <paramref name="selection"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="distribution"/> is empty.</exception>
    public static ChoiceOutcome Create(IReadOnlyList<OptionProbability> distribution, object selection)
    {
        RequireEntries(distribution);
        ArgumentNullException.ThrowIfNull(selection);

        return new ChoiceOutcome(selection, hasSelection: true, distribution);
    }

    private static void RequireEntries(IReadOnlyList<OptionProbability> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        if (distribution.Count == 0)
        {
            throw new ArgumentException("A distribution must contain at least one option.", nameof(distribution));
        }
    }
}
