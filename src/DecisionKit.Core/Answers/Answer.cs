using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Internal;

namespace DecisionKit.Answers;

/// <summary>
/// Represents the provider's answer to one question of a decision request.
/// </summary>
/// <remarks>
/// Every answer is correlated with its question through <see cref="QuestionId"/>. Answers are
/// immutable: a provider builds them once while mapping the response and never mutates them
/// afterwards.
/// </remarks>
public abstract class Answer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Answer"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is uninitialized.</exception>
    protected Answer(QuestionId questionId)
    {
        if (questionId.IsEmpty)
        {
            throw new ArgumentException("An answer must reference an initialized question identifier.", nameof(questionId));
        }

        QuestionId = questionId;
    }

    /// <summary>
    /// Gets the identifier of the question this answer belongs to.
    /// </summary>
    public QuestionId QuestionId { get; }

    /// <summary>
    /// Gets the provider-neutral metadata attached to this answer.
    /// </summary>
    /// <remarks>
    /// The collection is copied on assignment, so the answer cannot be changed through the
    /// dictionary the caller supplied.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;

    /// <summary>
    /// Gets the runtime type of the value this answer carries, or <see langword="null"/> when the
    /// answer carries no typed value.
    /// </summary>
    public abstract Type? ValueType { get; }
}

/// <summary>
/// Represents an answer that carries a typed value.
/// </summary>
/// <typeparam name="TValue">The type of the value the provider returned.</typeparam>
public abstract class Answer<TValue> : Answer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Answer{TValue}"/> class.
    /// </summary>
    /// <param name="questionId">The identifier of the question this answer belongs to.</param>
    /// <param name="value">The value the provider returned.</param>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is uninitialized.</exception>
    protected Answer(QuestionId questionId, TValue value)
        : base(questionId)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the value the provider returned.
    /// </summary>
    public TValue Value { get; }

    /// <inheritdoc />
    public sealed override Type? ValueType => typeof(TValue);
}
