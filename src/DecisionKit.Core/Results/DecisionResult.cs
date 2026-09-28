using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Questions;

namespace DecisionKit.Results;

/// <summary>
/// The answers a provider returned for one decision request, together with the metadata describing
/// how they were produced.
/// </summary>
/// <remarks>
/// <para>
/// The primary way to read a result is <see cref="Get{TAnswer}(Question{TAnswer})"/>: the answer
/// type is inferred from the question, so a score question cannot be read as a choice answer by
/// mistake. <see cref="Get(string)"/> is the documented dynamic escape hatch for callers whose
/// questions come from configuration or from a database.
/// </para>
/// <para>
/// There is deliberately no indexer taking a question. An indexer cannot be generic, so
/// <c>result[question]</c> would have to return an untyped <see cref="Answer"/> and would quietly
/// discard the type safety the question carries.
/// </para>
/// </remarks>
public sealed class DecisionResult
{
    private readonly List<Answer> _answers;
    private readonly ReadOnlyDictionary<QuestionId, Answer> _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionResult"/> class.
    /// </summary>
    /// <param name="answers">The answers, in the order the provider returned them.</param>
    /// <param name="metadata">The metadata describing how the result was produced.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="answers"/> or <paramref name="metadata"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="answers"/> contains a <see langword="null"/> entry or two answers for the
    /// same question.
    /// </exception>
    public DecisionResult(IEnumerable<Answer> answers, DecisionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(metadata);

        _answers = [.. answers];

        Dictionary<QuestionId, Answer> index = new(_answers.Count);

        foreach (Answer answer in _answers)
        {
            if (answer is null)
            {
                throw new ArgumentException("A decision result cannot contain a null answer.", nameof(answers));
            }

            if (!index.TryAdd(answer.QuestionId, answer))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"The decision result contains more than one answer for question '{answer.QuestionId}'."),
                    nameof(answers));
            }
        }

        _index = new ReadOnlyDictionary<QuestionId, Answer>(index);
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the answers, in the order the provider returned them.
    /// </summary>
    public IReadOnlyList<Answer> Answers => _answers;

    /// <summary>
    /// Gets the metadata describing how the result was produced.
    /// </summary>
    public DecisionMetadata Metadata { get; }

    /// <summary>
    /// Gets what the decision consumed.
    /// </summary>
    public DecisionUsage Usage { get; init; } = DecisionUsage.Empty;

    /// <summary>
    /// Gets the number of answers in the result.
    /// </summary>
    public int Count => _answers.Count;

    /// <summary>
    /// Reads the answer to a question, with the answer type inferred from the question.
    /// </summary>
    /// <typeparam name="TAnswer">The type of answer the question expects.</typeparam>
    /// <param name="question">The question to look up.</param>
    /// <returns>The answer to <paramref name="question"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The result contains no answer for that question.</exception>
    /// <exception cref="InvalidOperationException">
    /// The result contains an answer for that question, but of a different type. This means the
    /// provider mapped the question to an answer shape the question does not expect.
    /// </exception>
    public TAnswer Get<TAnswer>(Question<TAnswer> question)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(question);

        Answer answer = Get(question.Id);

        if (answer is TAnswer typed)
        {
            return typed;
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Question '{question.Id}' expects an answer of type {typeof(TAnswer)}, but the result carries {answer.GetType()}."));
    }

    /// <summary>
    /// Attempts to read the answer to a question, with the answer type inferred from the question.
    /// </summary>
    /// <typeparam name="TAnswer">The type of answer the question expects.</typeparam>
    /// <param name="question">The question to look up.</param>
    /// <param name="answer">The answer when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the result contains an answer of the expected type for that
    /// question; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    public bool TryGet<TAnswer>(Question<TAnswer> question, [NotNullWhen(true)] out TAnswer? answer)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(question);

        if (_index.TryGetValue(question.Id, out Answer? candidate) && candidate is TAnswer typed)
        {
            answer = typed;
            return true;
        }

        answer = null;
        return false;
    }

    /// <summary>
    /// Reads an answer by question identifier.
    /// </summary>
    /// <param name="questionId">The identifier to look up.</param>
    /// <returns>The matching answer.</returns>
    /// <exception cref="KeyNotFoundException">The result contains no answer for that identifier.</exception>
    public Answer Get(QuestionId questionId)
    {
        if (_index.TryGetValue(questionId, out Answer? answer))
        {
            return answer;
        }

        throw new KeyNotFoundException(
            string.Create(CultureInfo.InvariantCulture, $"The decision result contains no answer for question '{questionId}'."));
    }

    /// <summary>
    /// Reads an answer by question identifier, for callers whose questions are not known at compile
    /// time.
    /// </summary>
    /// <param name="questionId">The identifier to look up.</param>
    /// <returns>The matching answer, typed only as <see cref="Answer"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="questionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The result contains no answer for that identifier.</exception>
    public Answer Get(string questionId) => Get(new QuestionId(questionId));

    /// <summary>
    /// Attempts to read an answer by question identifier.
    /// </summary>
    /// <param name="questionId">The identifier to look up.</param>
    /// <param name="answer">The matching answer when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the result contains a matching answer.</returns>
    public bool TryGet(QuestionId questionId, [NotNullWhen(true)] out Answer? answer) =>
        _index.TryGetValue(questionId, out answer);

    /// <summary>
    /// Attempts to read an answer by question identifier.
    /// </summary>
    /// <param name="questionId">The identifier to look up.</param>
    /// <param name="answer">The matching answer when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="questionId"/> is a valid identifier and the
    /// result contains a matching answer.
    /// </returns>
    public bool TryGet(string? questionId, [NotNullWhen(true)] out Answer? answer)
    {
        if (!QuestionId.TryCreate(questionId, out QuestionId parsed))
        {
            answer = null;
            return false;
        }

        return TryGet(parsed, out answer);
    }

    /// <summary>
    /// Determines whether the result contains an answer for the given identifier.
    /// </summary>
    /// <param name="questionId">The identifier to look for.</param>
    /// <returns><see langword="true"/> when a matching answer exists.</returns>
    public bool Contains(QuestionId questionId) => _index.ContainsKey(questionId);
}
