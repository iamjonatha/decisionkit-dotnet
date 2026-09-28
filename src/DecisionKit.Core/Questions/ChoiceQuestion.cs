using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Questions;

/// <summary>
/// Asks the provider to choose among a closed set of options.
/// </summary>
/// <typeparam name="TOption">
/// The type of the options. Use an enumeration or a value object when the options are known at
/// compile time, and <see cref="string"/> when they come from configuration or from a database.
/// </typeparam>
public sealed class ChoiceQuestion<TOption> : Question<ChoiceAnswer<TOption>>, IChoiceQuestion
    where TOption : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChoiceQuestion{TOption}"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <param name="options">The options the provider may choose from.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, <paramref name="prompt"/> is empty or whitespace, or
    /// <paramref name="options"/> is empty or contains a duplicate.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="prompt"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public ChoiceQuestion(QuestionId id, string prompt, IEnumerable<TOption> options)
        : base(id, prompt)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = Validate(options);
    }

    /// <summary>
    /// Gets the options the provider may choose from, in declaration order.
    /// </summary>
    public IReadOnlyList<TOption> Options { get; }

    /// <inheritdoc />
    public Type OptionType => typeof(TOption);

    /// <inheritdoc />
    public IReadOnlyList<object> OptionValues => field ??= CreateOptionValues(Options);

    /// <summary>
    /// Creates the answer that selects one of the offered options.
    /// </summary>
    /// <param name="selection">The selected option.</param>
    /// <returns>The created answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="selection"/> is not one of the options this question offers. A provider that
    /// answers with an option that was never offered is reporting something the caller cannot act
    /// on, so it is rejected rather than passed through.
    /// </exception>
    public ChoiceAnswer<TOption> CreateAnswer(TOption selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        RequireOffered(selection, nameof(selection));

        return new ChoiceAnswer<TOption>(Id, Choice.Selected(selection));
    }

    /// <summary>
    /// Creates the answer that carries a probability distribution over the offered options.
    /// </summary>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <returns>The created answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="distribution"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="distribution"/> is empty, contains an uninitialized entry, assigns two
    /// probabilities to the same option, or mentions an option this question does not offer.
    /// </exception>
    public ChoiceAnswer<TOption> CreateAnswer(IReadOnlyList<OptionProbability<TOption>> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        RequireOffered(distribution, nameof(distribution));

        return new ChoiceAnswer<TOption>(Id, Choice.Distributed(distribution));
    }

    /// <summary>
    /// Creates the answer that carries both a probability distribution and the option the provider
    /// committed to.
    /// </summary>
    /// <param name="distribution">The probabilities assigned to the options, in provider order.</param>
    /// <param name="selection">The selected option, which must appear in the distribution.</param>
    /// <returns>The created answer.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="distribution"/> or <paramref name="selection"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="distribution"/> is not a valid distribution for this question, or
    /// <paramref name="selection"/> is not offered or does not appear in the distribution.
    /// </exception>
    public ChoiceAnswer<TOption> CreateAnswer(IReadOnlyList<OptionProbability<TOption>> distribution, TOption selection)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        ArgumentNullException.ThrowIfNull(selection);

        RequireOffered(distribution, nameof(distribution));
        RequireOffered(selection, nameof(selection));

        return new ChoiceAnswer<TOption>(Id, Choice.Distributed(distribution, selection));
    }

    Answer IChoiceQuestion.CreateAnswer(object selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return CreateAnswer(RequireOption(selection, nameof(selection)));
    }

    Answer IChoiceQuestion.CreateAnswer(ChoiceOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new ChoiceAnswer<TOption>(Id, CreateChoice(outcome)) { Metadata = outcome.Metadata };
    }

    private Choice<TOption> CreateChoice(ChoiceOutcome outcome)
    {
        TOption selection = default!;

        if (outcome.HasSelection)
        {
            selection = RequireOption(outcome.Selection!, nameof(outcome));
            RequireOffered(selection, nameof(outcome));
        }

        if (!outcome.HasDistribution)
        {
            return Choice.Selected(selection);
        }

        List<OptionProbability<TOption>> distribution = Convert(outcome.Distribution, nameof(outcome));
        RequireOffered(distribution, nameof(outcome));

        return outcome.HasSelection
            ? Choice.Distributed(distribution, selection)
            : Choice.Distributed(distribution);
    }

    private static List<OptionProbability<TOption>> Convert(IReadOnlyList<OptionProbability> distribution, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        List<OptionProbability<TOption>> typed = new(distribution.Count);

        foreach (OptionProbability entry in distribution)
        {
            if (entry.Option is null)
            {
                throw new ArgumentException("A distribution cannot contain an uninitialized entry.", parameterName);
            }

            if (entry.Option is not TOption option)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"A distribution entry holds an option of type {entry.Option.GetType()}, not {typeof(TOption)}."),
                    parameterName);
            }

            typed.Add(new OptionProbability<TOption>(option, entry.Probability));
        }

        return typed;
    }

    private static List<object> CreateOptionValues(IReadOnlyList<TOption> options)
    {
        List<object> values = new(options.Count);

        foreach (TOption option in options)
        {
            values.Add(option);
        }

        return values;
    }

    private static List<TOption> Validate(IEnumerable<TOption> options)
    {
        List<TOption> materialized = [.. options];

        if (materialized.Count == 0)
        {
            throw new ArgumentException("A choice question must offer at least one option.", nameof(options));
        }

        HashSet<TOption> seen = new(EqualityComparer<TOption>.Default);

        foreach (TOption option in materialized)
        {
            if (option is null)
            {
                throw new ArgumentException("A choice question cannot offer a null option.", nameof(options));
            }

            if (!seen.Add(option))
            {
                throw new ArgumentException("A choice question cannot offer the same option twice.", nameof(options));
            }
        }

        return materialized;
    }

    private bool Offers(TOption option)
    {
        EqualityComparer<TOption> comparer = EqualityComparer<TOption>.Default;

        foreach (TOption candidate in Options)
        {
            if (comparer.Equals(candidate, option))
            {
                return true;
            }
        }

        return false;
    }

    private static TOption RequireOption(object selection, string parameterName)
    {
        if (selection is not TOption option)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"This question offers options of type {typeof(TOption)}, not {selection.GetType()}."),
                parameterName);
        }

        return option;
    }

    private void RequireOffered(TOption option, string parameterName)
    {
        if (!Offers(option))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Question '{Id}' does not offer the option '{option}'."),
                parameterName);
        }
    }

    private void RequireOffered(IReadOnlyList<OptionProbability<TOption>> distribution, string parameterName)
    {
        foreach (OptionProbability<TOption> entry in distribution)
        {
            if (!entry.IsEmpty)
            {
                RequireOffered(entry.Option, parameterName);
            }
        }
    }
}
