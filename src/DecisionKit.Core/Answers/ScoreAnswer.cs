using System;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Answers;

/// <summary>
/// Represents the answer to a <see cref="Questions.ScoreQuestion"/>.
/// </summary>
public sealed class ScoreAnswer : Answer<Score>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScoreAnswer"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <param name="score">The score the provider returned, together with the scale it was measured on.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="questionId"/> is uninitialized, or <paramref name="score"/> carries no scale.
    /// </exception>
    public ScoreAnswer(QuestionId questionId, Score score)
        : base(questionId, score)
    {
        if (!score.HasScale)
        {
            throw new ArgumentException("A score answer must carry an initialized score.", nameof(score));
        }
    }
}
