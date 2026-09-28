using System;
using System.Collections.Generic;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Internal;

namespace DecisionKit.Questions;

/// <summary>
/// Represents one question of a decision request.
/// </summary>
/// <remarks>
/// This non-generic base exists so that a <see cref="QuestionSet"/> can hold questions of different
/// answer types. Application code should work with <see cref="Question{TAnswer}"/> and its
/// concrete subclasses, because those carry the answer type.
/// </remarks>
public abstract class Question
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Question"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, or <paramref name="prompt"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is <see langword="null"/>.</exception>
    protected Question(QuestionId id, string prompt)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A question must carry an initialized identifier.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Id = id;
        Prompt = prompt.Trim();
    }

    /// <summary>
    /// Gets the identifier that correlates this question with its answer.
    /// </summary>
    public QuestionId Id { get; }

    /// <summary>
    /// Gets the human-readable question text sent to the provider.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Gets the provider-neutral metadata attached to this question.
    /// </summary>
    /// <remarks>
    /// The collection is copied on assignment, so the question cannot be changed through the
    /// dictionary the caller supplied.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;

    /// <summary>
    /// Gets the type of answer this question expects.
    /// </summary>
    public abstract Type AnswerType { get; }
}

/// <summary>
/// Represents a question that knows the type of its own answer.
/// </summary>
/// <typeparam name="TAnswer">The type of answer this question expects.</typeparam>
/// <remarks>
/// The answer type is part of the question type, so that
/// <see cref="Results.DecisionResult.Get{TAnswer}(Question{TAnswer})"/> infers the answer type from
/// the question instead of from a string supplied at the call site.
/// </remarks>
public abstract class Question<TAnswer> : Question
    where TAnswer : Answer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Question{TAnswer}"/> class.
    /// </summary>
    /// <param name="id">The identifier that correlates this question with its answer.</param>
    /// <param name="prompt">The human-readable question text sent to the provider.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is uninitialized, or <paramref name="prompt"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is <see langword="null"/>.</exception>
    protected Question(QuestionId id, string prompt)
        : base(id, prompt)
    {
    }

    /// <inheritdoc />
    public sealed override Type AnswerType => typeof(TAnswer);
}
