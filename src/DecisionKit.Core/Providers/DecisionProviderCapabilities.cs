using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Questions;

namespace DecisionKit.Providers;

/// <summary>
/// What a provider can actually do, declared by the provider instead of guessed by the caller.
/// </summary>
/// <remarks>
/// Without this, a caller discovers that a provider ignores idempotency keys or never reports usage
/// by noticing that the data is missing, which is indistinguishable from a bug. Capabilities make
/// the difference explicit and testable before the call is made.
/// </remarks>
public sealed class DecisionProviderCapabilities
{
    private static readonly HashSet<Type> s_noQuestionTypes = [];

    private readonly HashSet<Type> _supportedQuestionTypes = s_noQuestionTypes;

    /// <summary>
    /// Gets the capabilities of a provider that supports nothing beyond asking questions.
    /// </summary>
    public static DecisionProviderCapabilities None { get; } = new();

    /// <summary>
    /// Gets the question types the provider can evaluate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Membership is by exact type. A provider that supports a base question type does not thereby
    /// support every type derived from it, because a derived type carries meaning the provider has
    /// not been taught.
    /// </para>
    /// <para>
    /// A generic type definition is the one exception. Declaring <c>typeof(ChoiceQuestion&lt;&gt;)</c>
    /// declares every closed form of it, because the option type changes what the question is about
    /// and never what the provider has to be able to do. Listing every closed form is impossible:
    /// the option types belong to the caller, not to the provider.
    /// </para>
    /// <para>
    /// The collection is copied on assignment.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The assigned collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The assigned collection contains a <see langword="null"/> entry or a type that does not
    /// derive from <see cref="Question"/>.
    /// </exception>
    public IReadOnlyCollection<Type> SupportedQuestionTypes
    {
        get => _supportedQuestionTypes;
        init => _supportedQuestionTypes = CreateQuestionTypeSet(value);
    }

    /// <summary>
    /// Gets a value indicating whether the provider honours
    /// <see cref="DecisionRequest.IdempotencyKey"/>.
    /// </summary>
    public bool SupportsIdempotencyKeys { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider reports what a decision consumed.
    /// </summary>
    public bool SupportsUsageReporting { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider forwards
    /// <see cref="DecisionRequest.Metadata"/> instead of dropping it.
    /// </summary>
    public bool SupportsRequestMetadata { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider evaluates several questions in one call rather
    /// than one call per question.
    /// </summary>
    public bool SupportsBatching { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider can explain its answers when
    /// <see cref="DecisionOptions.IncludeExplanations"/> is set.
    /// </summary>
    public bool SupportsExplanations { get; init; }

    /// <summary>
    /// Determines whether the provider can evaluate a question type.
    /// </summary>
    /// <param name="questionType">The question type to check.</param>
    /// <returns>
    /// <see langword="true"/> when the provider declares that exact type, or declares the generic
    /// type definition of it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="questionType"/> is <see langword="null"/>.</exception>
    public bool Supports(Type questionType)
    {
        ArgumentNullException.ThrowIfNull(questionType);

        if (_supportedQuestionTypes.Contains(questionType))
        {
            return true;
        }

        return questionType.IsConstructedGenericType
            && _supportedQuestionTypes.Contains(questionType.GetGenericTypeDefinition());
    }

    /// <summary>
    /// Determines whether the provider can evaluate a question type.
    /// </summary>
    /// <typeparam name="TQuestion">The question type to check.</typeparam>
    /// <returns>
    /// <see langword="true"/> when the provider declares that exact type, or declares the generic
    /// type definition of it.
    /// </returns>
    public bool Supports<TQuestion>()
        where TQuestion : Question => Supports(typeof(TQuestion));

    /// <summary>
    /// Determines whether the provider can evaluate every question in a set.
    /// </summary>
    /// <param name="questions">The questions to check.</param>
    /// <returns><see langword="true"/> when every question has a declared type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <see langword="null"/>.</exception>
    public bool SupportsAll(QuestionSet questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        foreach (Question question in questions)
        {
            if (!Supports(question.GetType()))
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<Type> CreateQuestionTypeSet(IReadOnlyCollection<Type> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        HashSet<Type> types = new(source.Count);

        foreach (Type questionType in source)
        {
            if (questionType is null)
            {
                throw new ArgumentException("A supported question type cannot be null.", nameof(source));
            }

            if (!IsQuestionType(questionType))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Type '{questionType}' is not a question type."),
                    nameof(source));
            }

            types.Add(questionType);
        }

        return types;
    }

    private static bool IsQuestionType(Type candidate)
    {
        // Walking the base chain rather than calling IsAssignableFrom, because a generic type
        // definition such as ChoiceQuestion<> is not assignable to anything.
        for (Type? current = candidate; current is not null; current = current.BaseType)
        {
            if (current == typeof(Question))
            {
                return true;
            }
        }

        return false;
    }
}
