using System;
using DecisionKit.Answers;
using DecisionKit.Identifiers;

namespace DecisionKit.Questions;

/// <summary>
/// Asks the provider how likely a statement is, and expects a probability in return.
/// </summary>
/// <remarks>
/// This is the provider-neutral shape of the question type some protocols expose under a vendor
/// specific name. The vendor name belongs to the provider package; the domain only knows that the
/// answer is a probability.
/// </remarks>
public sealed class ProbabilityQuestion : Question<ProbabilityAnswer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProbabilityQuestion"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, or <paramref name="prompt"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is <see langword="null"/>.</exception>
    public ProbabilityQuestion(QuestionId id, string prompt)
        : base(id, prompt)
    {
    }
}
