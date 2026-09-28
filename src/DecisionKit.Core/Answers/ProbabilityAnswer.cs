using System;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Answers;

/// <summary>
/// Represents the answer to a <see cref="Questions.ProbabilityQuestion"/>.
/// </summary>
public sealed class ProbabilityAnswer : Answer<Probability>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProbabilityAnswer"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <param name="probability">The probability the provider returned.</param>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is uninitialized.</exception>
    public ProbabilityAnswer(QuestionId questionId, Probability probability)
        : base(questionId, probability)
    {
    }
}
