using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DecisionKit.Identifiers;

namespace DecisionKit.Questions;

/// <summary>
/// An ordered collection of questions whose identifiers are guaranteed to be unique.
/// </summary>
/// <remarks>
/// Order is preserved because some protocols give it meaning, and lookup by
/// <see cref="QuestionId"/> is indexed because results are correlated by identifier. A duplicate
/// identifier is a programming error and is rejected at construction time: silently keeping the
/// last question would make a result impossible to correlate.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "A question set is a domain concept with uniqueness semantics. Renaming it to QuestionCollection would hide that.")]
public sealed class QuestionSet : IReadOnlyList<Question>
{
    private readonly List<Question> _questions;
    private readonly Dictionary<QuestionId, Question> _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionSet"/> class.
    /// </summary>
    /// <param name="questions">The questions, in the order they should be sent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="questions"/> contains a <see langword="null"/> entry or two questions with
    /// the same identifier.
    /// </exception>
    public QuestionSet(IEnumerable<Question> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        _questions = [.. questions];
        _index = new Dictionary<QuestionId, Question>(_questions.Count);

        foreach (Question question in _questions)
        {
            if (question is null)
            {
                throw new ArgumentException("A question set cannot contain a null question.", nameof(questions));
            }

            if (!_index.TryAdd(question.Id, question))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"The question set contains more than one question with identifier '{question.Id}'."),
                    nameof(questions));
            }
        }
    }

    /// <summary>
    /// Gets an empty question set.
    /// </summary>
    public static QuestionSet Empty { get; } = new([]);

    /// <summary>
    /// Gets the number of questions in the set.
    /// </summary>
    public int Count => _questions.Count;

    /// <summary>
    /// Gets the question at the given position.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The question at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the set.</exception>
    public Question this[int index] => _questions[index];

    /// <summary>
    /// Creates a question set from an argument list.
    /// </summary>
    /// <param name="questions">The questions, in the order they should be sent.</param>
    /// <returns>The created question set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="questions"/> contains a <see langword="null"/> entry or two questions with
    /// the same identifier.
    /// </exception>
    public static QuestionSet Create(params Question[] questions) => new(questions);

    /// <summary>
    /// Determines whether the set contains a question with the given identifier.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <returns><see langword="true"/> when the set contains a matching question.</returns>
    public bool Contains(QuestionId id) => _index.ContainsKey(id);

    /// <summary>
    /// Attempts to find the question with the given identifier.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <param name="question">The matching question when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the set contains a matching question.</returns>
    public bool TryGet(QuestionId id, [NotNullWhen(true)] out Question? question) => _index.TryGetValue(id, out question);

    /// <summary>
    /// Finds the question with the given identifier.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <returns>The matching question.</returns>
    /// <exception cref="KeyNotFoundException">The set contains no question with that identifier.</exception>
    public Question Get(QuestionId id)
    {
        if (_index.TryGetValue(id, out Question? question))
        {
            return question;
        }

        throw new KeyNotFoundException(
            string.Create(CultureInfo.InvariantCulture, $"The question set contains no question with identifier '{id}'."));
    }

    /// <inheritdoc />
    public IEnumerator<Question> GetEnumerator() => _questions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
