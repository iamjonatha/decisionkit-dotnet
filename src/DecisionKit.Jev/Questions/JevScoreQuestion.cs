using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Protocol;
using DecisionKit.Questions;
using DecisionKit.Values;

namespace DecisionKit.Jev.Questions;

/// <summary>
/// A score question carrying the rubric JEV requires.
/// </summary>
/// <remarks>
/// <para>
/// JEV does not score against a numeric range. It scores against an ordered list of levels that
/// the caller describes in words, and the rubric is mandatory: the service rejects a score question
/// that does not carry one. <see cref="ScoreQuestion"/> describes a range and cannot express that,
/// so this type exists to supply what the protocol needs.
/// </para>
/// <para>
/// It is deliberately a provider-specific type in the provider's own package. The alternative —
/// teaching <c>DecisionKit.Core</c> what a JEV rubric is — would put one provider's vocabulary in
/// the domain every other provider shares.
/// </para>
/// <para>
/// The answer is an ordinary <see cref="ScoreAnswer"/> measured from <see cref="Minimum"/> to
/// <see cref="Maximum"/>, so application code reads the result exactly as it would any other score,
/// and the value is probability-weighted across the levels rather than snapped to one of them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// JevScoreQuestion frustration = new(
///     new QuestionId("frustration"),
///     "How frustrated the customer appears",
///     ["Calm, just stating facts", "Frustrated but civil", "Very angry, strong language"]);
/// </code>
/// </example>
public sealed class JevScoreQuestion : Question<ScoreAnswer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JevScoreQuestion"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">What the model should rate.</param>
    /// <param name="levels">
    /// The rubric, in ascending order. The first entry describes the lowest level.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, <paramref name="prompt"/> is empty or whitespace,
    /// a level is empty or whitespace, or the number of levels is outside the range the service
    /// accepts.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="prompt"/> or <paramref name="levels"/> is <see langword="null"/>.
    /// </exception>
    public JevScoreQuestion(QuestionId id, string prompt, IEnumerable<string> levels)
        : base(id, prompt)
    {
        ArgumentNullException.ThrowIfNull(levels);

        Levels = Validate(levels);
    }

    /// <summary>
    /// Gets the rubric, in ascending order.
    /// </summary>
    public IReadOnlyList<string> Levels { get; }

    /// <summary>
    /// Gets the lowest value an answer to this question can take, which is always zero.
    /// </summary>
    /// <remarks>
    /// A JEV score is a position across the levels, and the levels are indexed from zero.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The lower bound belongs to the question alongside Maximum, and reads as question.Minimum at every call site. Making it static because it happens to be constant would split one concept across two kinds of member.")]
    public double Minimum => 0;

    /// <summary>
    /// Gets the highest value an answer to this question can take, which is the index of the last
    /// level.
    /// </summary>
    public double Maximum => Levels.Count - 1;

    /// <summary>
    /// Builds the answer to this question from a value the provider reported.
    /// </summary>
    /// <param name="value">The position across the levels.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> lies outside <see cref="Minimum"/> and <see cref="Maximum"/>.
    /// </exception>
    public ScoreAnswer CreateAnswer(double value) =>
        new(Id, new Score(value, Minimum, Maximum));

    /// <summary>
    /// Describes the level at an index, when the index names one.
    /// </summary>
    /// <param name="level">The zero-based level index.</param>
    /// <param name="description">The description of that level.</param>
    /// <returns><see langword="true"/> when the index names a level of this rubric.</returns>
    public bool TryGetLevel(int level, out string description)
    {
        if (level < 0 || level >= Levels.Count)
        {
            description = string.Empty;
            return false;
        }

        description = Levels[level];
        return true;
    }

    private static ReadOnlyCollection<string> Validate(IEnumerable<string> levels)
    {
        List<string> validated = [];

        foreach (string level in levels)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                throw new ArgumentException(
                    $"Level {validated.Count} has no description. Every level of a JEV rubric must describe what it means, because the description is what the model rates against.",
                    nameof(levels));
            }

            validated.Add(level.Trim());
        }

        if (validated.Count < JevProtocol.MinimumScoreLevels || validated.Count > JevProtocol.MaximumScoreLevels)
        {
            throw new ArgumentException(
                $"A JEV score question needs between {JevProtocol.MinimumScoreLevels} and {JevProtocol.MaximumScoreLevels} levels, but {validated.Count} were supplied.",
                nameof(levels));
        }

        return validated.AsReadOnly();
    }
}
