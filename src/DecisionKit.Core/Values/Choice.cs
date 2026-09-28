using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DecisionKit.Values;

/// <summary>
/// Represents the outcome of a choice question: a selected option, a probability distribution over
/// the available options, or both.
/// </summary>
/// <typeparam name="TOption">The type of the options.</typeparam>
/// <remarks>
/// A provider that returns a distribution is telling the caller something a single selection cannot
/// express. DecisionKit therefore never collapses a distribution into a selection on the caller's
/// behalf; <see cref="TryGetMostLikely"/> exists so that collapsing is an explicit, visible act.
/// </remarks>
public sealed class Choice<TOption> : IEquatable<Choice<TOption>>
    where TOption : notnull
{
    private static readonly OptionProbability<TOption>[] s_noDistribution = [];

    private readonly TOption? _selection;

    internal Choice(TOption? selection, bool hasSelection, IReadOnlyList<OptionProbability<TOption>> distribution)
    {
        _selection = selection;
        HasSelection = hasSelection;
        Distribution = distribution;
    }

    /// <summary>
    /// Gets a value indicating whether the provider committed to one option.
    /// </summary>
    public bool HasSelection { get; }

    /// <summary>
    /// Gets the selected option.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The provider returned a distribution without committing to an option. Use
    /// <see cref="TryGetSelection"/> or <see cref="TryGetMostLikely"/> instead.
    /// </exception>
    public TOption Selection => HasSelection
        ? _selection!
        : throw new InvalidOperationException("The choice does not carry a selected option. Inspect Distribution, or call TryGetMostLikely to collapse the distribution explicitly.");

    /// <summary>
    /// Gets the probability distribution over the options, in the order the provider returned it.
    /// </summary>
    /// <remarks>
    /// The probabilities are reported as received. DecisionKit does not renormalize them, because a
    /// distribution that does not sum to one is diagnostic information, not noise to be hidden.
    /// </remarks>
    public IReadOnlyList<OptionProbability<TOption>> Distribution { get; }

    /// <summary>
    /// Gets a value indicating whether the provider returned a distribution.
    /// </summary>
    public bool HasDistribution => Distribution.Count > 0;

    /// <summary>
    /// Attempts to read the selected option.
    /// </summary>
    /// <param name="selection">The selected option when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the choice carries a selected option; otherwise <see langword="false"/>.</returns>
    public bool TryGetSelection([MaybeNullWhen(false)] out TOption selection)
    {
        selection = HasSelection ? _selection! : default;
        return HasSelection;
    }

    /// <summary>
    /// Attempts to read the option with the highest probability in the distribution.
    /// </summary>
    /// <param name="mostLikely">The highest ranked entry when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the choice carries a distribution; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Ties are resolved by taking the first entry in provider order. This method is the documented
    /// way to reduce a distribution to a single option; it never mutates the choice.
    /// </remarks>
    public bool TryGetMostLikely(out OptionProbability<TOption> mostLikely)
    {
        if (Distribution.Count == 0)
        {
            mostLikely = default;
            return false;
        }

        OptionProbability<TOption> best = Distribution[0];

        for (int index = 1; index < Distribution.Count; index++)
        {
            if (Distribution[index].Probability > best.Probability)
            {
                best = Distribution[index];
            }
        }

        mostLikely = best;
        return true;
    }

    /// <summary>
    /// Attempts to read the probability the provider assigned to a specific option.
    /// </summary>
    /// <param name="option">The option to look up.</param>
    /// <param name="probability">The probability when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the option appears in the distribution; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public bool TryGetProbability(TOption option, out Probability probability)
    {
        ArgumentNullException.ThrowIfNull(option);

        foreach (OptionProbability<TOption> entry in Distribution)
        {
            if (EqualityComparer<TOption>.Default.Equals(entry.Option, option))
            {
                probability = entry.Probability;
                return true;
            }
        }

        probability = default;
        return false;
    }

    internal static IReadOnlyList<OptionProbability<TOption>> Empty => s_noDistribution;

    /// <inheritdoc />
    public bool Equals(Choice<TOption>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return HasSelection == other.HasSelection
            && EqualityComparer<TOption?>.Default.Equals(_selection, other._selection)
            && DistributionEquals(other.Distribution);
    }

    private bool DistributionEquals(IReadOnlyList<OptionProbability<TOption>> other)
    {
        if (Distribution.Count != other.Count)
        {
            return false;
        }

        for (int index = 0; index < Distribution.Count; index++)
        {
            if (!Distribution[index].Equals(other[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Choice<TOption>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(HasSelection);
        hash.Add(_selection);

        foreach (OptionProbability<TOption> entry in Distribution)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => HasSelection
        ? _selection!.ToString() ?? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $"distribution of {Distribution.Count} option(s)");
}

/// <summary>
/// Creates <see cref="Choice{TOption}"/> instances.
/// </summary>
/// <remarks>
/// The factory methods live on a non-generic type so that the option type is inferred from the
/// arguments: <c>Choice.Selected(Department.Billing)</c> rather than
/// <c>Choice&lt;Department&gt;.Selected(Department.Billing)</c>.
/// </remarks>
public static class Choice
{
    /// <summary>
    /// Creates a choice that carries a single selected option and no distribution.
    /// </summary>
    /// <typeparam name="TOption">The type of the option.</typeparam>
    /// <param name="option">The selected option.</param>
    /// <returns>The created choice.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public static Choice<TOption> Selected<TOption>(TOption option)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(option);

        return new Choice<TOption>(option, hasSelection: true, Choice<TOption>.Empty);
    }

    /// <summary>
    /// Creates a choice that carries a probability distribution and no selected option.
    /// </summary>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <returns>The created choice.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="distribution"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="distribution"/> is empty or contains the same option more than once.
    /// </exception>
    public static Choice<TOption> Distributed<TOption>(IEnumerable<OptionProbability<TOption>> distribution)
        where TOption : notnull
    {
        List<OptionProbability<TOption>> entries = ValidateDistribution(distribution, nameof(distribution));

        return new Choice<TOption>(default, hasSelection: false, entries);
    }

    /// <summary>
    /// Creates a choice that carries both a probability distribution and the option the provider
    /// committed to.
    /// </summary>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <param name="selection">The selected option, which must appear in the distribution.</param>
    /// <returns>The created choice.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="distribution"/> or <paramref name="selection"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="distribution"/> is empty, contains the same option more than once, or does
    /// not contain <paramref name="selection"/>.
    /// </exception>
    public static Choice<TOption> Distributed<TOption>(IEnumerable<OptionProbability<TOption>> distribution, TOption selection)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(selection);

        List<OptionProbability<TOption>> entries = ValidateDistribution(distribution, nameof(distribution));

        if (!ContainsOption(entries, selection))
        {
            throw new ArgumentException("The selected option must appear in the distribution.", nameof(selection));
        }

        return new Choice<TOption>(selection, hasSelection: true, entries);
    }

    private static bool ContainsOption<TOption>(IReadOnlyList<OptionProbability<TOption>> entries, TOption option)
        where TOption : notnull
    {
        foreach (OptionProbability<TOption> entry in entries)
        {
            if (EqualityComparer<TOption>.Default.Equals(entry.Option, option))
            {
                return true;
            }
        }

        return false;
    }

    private static List<OptionProbability<TOption>> ValidateDistribution<TOption>(
        IEnumerable<OptionProbability<TOption>> distribution,
        string parameterName)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(distribution);

        List<OptionProbability<TOption>> entries = [.. distribution];

        if (entries.Count == 0)
        {
            throw new ArgumentException("A distribution must contain at least one option.", parameterName);
        }

        HashSet<TOption> seen = new(EqualityComparer<TOption>.Default);

        foreach (OptionProbability<TOption> entry in entries)
        {
            if (entry.IsEmpty)
            {
                throw new ArgumentException("A distribution cannot contain an uninitialized entry.", parameterName);
            }

            if (!seen.Add(entry.Option))
            {
                throw new ArgumentException("A distribution cannot assign two probabilities to the same option.", parameterName);
            }
        }

        return entries;
    }
}
