using System;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Values;

namespace DecisionKit.Questions;

/// <summary>
/// Asks the provider for a numeric evaluation on an explicit scale.
/// </summary>
/// <remarks>
/// The scale is part of the question, not a convention shared informally between the caller and the
/// provider. Every <see cref="ScoreAnswer"/> produced for this question is expected to carry the
/// same scale.
/// </remarks>
public sealed class ScoreQuestion : Question<ScoreAnswer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScoreQuestion"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <param name="minimum">The smallest value the scale can produce.</param>
    /// <param name="maximum">The largest value the scale can produce.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, or <paramref name="prompt"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A bound is not finite, or <paramref name="minimum"/> is not smaller than
    /// <paramref name="maximum"/>.
    /// </exception>
    public ScoreQuestion(QuestionId id, string prompt, double minimum, double maximum)
        : base(id, prompt)
    {
        ValidateScale(minimum, maximum);

        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// Gets the smallest value the scale can produce.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the largest value the scale can produce.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Creates a <see cref="Score"/> on this question's scale.
    /// </summary>
    /// <param name="value">The measured value.</param>
    /// <returns>The created score.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is not finite or falls outside the scale.
    /// </exception>
    public Score CreateScore(double value) => new(value, Minimum, Maximum);

    private static void ValidateScale(double minimum, double maximum)
    {
        if (double.IsNaN(minimum) || double.IsInfinity(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "A score bound must be a finite number.");
        }

        if (double.IsNaN(maximum) || double.IsInfinity(maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "A score bound must be a finite number.");
        }

        if (minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "The minimum of a score scale must be strictly smaller than its maximum.");
        }
    }
}
