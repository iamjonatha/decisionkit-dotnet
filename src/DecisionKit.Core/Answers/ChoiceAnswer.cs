using System;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Answers;

/// <summary>
/// Represents the answer to a <see cref="Questions.ChoiceQuestion{TOption}"/>.
/// </summary>
/// <typeparam name="TOption">The type of the options the question offered.</typeparam>
public sealed class ChoiceAnswer<TOption> : Answer<Choice<TOption>>
    where TOption : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChoiceAnswer{TOption}"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <param name="choice">The choice the provider returned.</param>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is uninitialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="choice"/> is <see langword="null"/>.</exception>
    public ChoiceAnswer(QuestionId questionId, Choice<TOption> choice)
        : base(questionId, choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
    }
}
