using System;
using System.Collections.Generic;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Questions;

/// <summary>
/// The non-generic view of a choice question.
/// </summary>
/// <remarks>
/// <para>
/// Mappers, decorators and test doubles receive questions as <see cref="Question"/>, and C# offers
/// no way to pattern match an open generic type: there is no <c>case ChoiceQuestion&lt;?&gt;</c>.
/// Without this interface every such component has to use reflection to read the options of a
/// choice question or to build its answer, which is fragile and hostile to trimming.
/// </para>
/// <para>
/// This interface is implemented by <see cref="ChoiceQuestion{TOption}"/> alone. It is a view over
/// an existing question, not an extension point: implementing it on another type does not make that
/// type a choice question.
/// </para>
/// </remarks>
public interface IChoiceQuestion
{
    /// <summary>
    /// Gets the identifier that correlates this question with its answer.
    /// </summary>
    QuestionId Id { get; }

    /// <summary>
    /// Gets the type of the options this question offers.
    /// </summary>
    Type OptionType { get; }

    /// <summary>
    /// Gets the options this question offers, as objects, in declaration order.
    /// </summary>
    /// <remarks>
    /// The typed <see cref="ChoiceQuestion{TOption}.Options"/> stays the primary API. This
    /// projection exists for code that does not know the option type at compile time.
    /// </remarks>
    IReadOnlyList<object> OptionValues { get; }

    /// <summary>
    /// Creates the answer that selects one of the offered options.
    /// </summary>
    /// <param name="selection">The selected option.</param>
    /// <returns>
    /// The created answer. Its runtime type is <see cref="ChoiceAnswer{TOption}"/> closed over
    /// <see cref="OptionType"/>, so a caller holding the typed question can read it back through
    /// <see cref="Results.DecisionResult.Get{TAnswer}(Question{TAnswer})"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="selection"/> is not of type <see cref="OptionType"/>, or is not one of the
    /// options this question offers.
    /// </exception>
    Answer CreateAnswer(object selection);

    /// <summary>
    /// Creates the answer described by an untyped outcome.
    /// </summary>
    /// <param name="outcome">The selection, the distribution and the metadata to carry.</param>
    /// <returns>
    /// The created answer. Its runtime type is <see cref="ChoiceAnswer{TOption}"/> closed over
    /// <see cref="OptionType"/>.
    /// </returns>
    /// <remarks>
    /// A provider that reports a distribution is saying something a single selection cannot express,
    /// so the distribution is never collapsed on the caller's behalf. See
    /// <see cref="Choice{TOption}.TryGetMostLikely"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="outcome"/> names an option that is not of type <see cref="OptionType"/> or
    /// that this question does not offer, assigns two probabilities to the same option, or selects
    /// an option that does not appear in its own distribution.
    /// </exception>
    Answer CreateAnswer(ChoiceOutcome outcome);
}
