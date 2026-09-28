using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Questions;
using DecisionKit.Questions;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Translates a domain question into the JEV wire question.
/// </summary>
/// <remarks>
/// <para>
/// The translation is explicit and total: every question type the domain models has an arm here,
/// and a type this package cannot express is rejected with a validation failure instead of being
/// sent as something it is not.
/// </para>
/// <para>
/// A choice question is matched through <see cref="IChoiceQuestion"/> rather than through
/// reflection, because C# cannot pattern match an open generic type and reflection would be hostile
/// to trimming.
/// </para>
/// <para>
/// JEV has nowhere to carry <see cref="Question.Metadata"/>. Metadata annotates a question for the
/// application's own benefit rather than instructing the provider, so it is left behind rather than
/// invented into the payload, and it remains on the question the caller holds.
/// </para>
/// </remarks>
public sealed class QuestionMapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionMapper"/> class with the default
    /// mapping settings.
    /// </summary>
    public QuestionMapper()
        : this(JevMappingOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public QuestionMapper(JevMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    /// <summary>
    /// Gets the mapper that uses the default mapping settings.
    /// </summary>
    public static QuestionMapper Default { get; } = new();

    /// <summary>
    /// Gets the mapping settings this mapper uses.
    /// </summary>
    public JevMappingOptions Options { get; }

    /// <summary>
    /// Translates a domain question into its JEV wire form.
    /// </summary>
    /// <param name="question">The question to translate.</param>
    /// <returns>
    /// The wire question. Its identifier is not part of it: JEV keys questions by a name the caller
    /// chooses, which <see cref="RequestMapper"/> supplies from <see cref="Question.Id"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">
    /// The question cannot be expressed in the JEV protocol.
    /// </exception>
    public JevQuestion ToWire(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question switch
        {
            ProbabilityQuestion probability => ToNoul(probability),
            JevScoreQuestion score => ToScore(score),
            ScoreQuestion score => throw RubricRequired(score),
            UnknownQuestion unknown => ToPassThrough(unknown),
            IChoiceQuestion choice => ToChoice(question, choice),
            _ => throw Unsupported(question),
        };
    }

    private static JevQuestion ToNoul(ProbabilityQuestion question) => new()
    {
        Type = JevQuestionTypes.Noul,
        Instructions = JsonValue.Create(question.Prompt),
    };

    private static JevQuestion ToScore(JevScoreQuestion question)
    {
        JsonArray levels = [];

        foreach (string level in question.Levels)
        {
            levels.Add((JsonNode?)JsonValue.Create(level));
        }

        return new JevQuestion
        {
            Type = JevQuestionTypes.Score,
            Instructions = JsonValue.Create(question.Prompt),
            Criteria = levels,
        };
    }

    private JevQuestion ToChoice(Question question, IChoiceQuestion choice) => new()
    {
        Type = JevQuestionTypes.Choice,
        Instructions = JsonValue.Create(question.Prompt),
        Criteria = ToCriteria(choice),
    };

    private JevQuestion ToPassThrough(UnknownQuestion question) => new()
    {
        Type = question.ProviderType,
        Instructions = JsonValue.Create(question.Prompt),
        Criteria = ToCriteria(question),
    };

    private JsonObject ToCriteria(IChoiceQuestion choice)
    {
        if (choice.OptionValues.Count > JevProtocol.MaximumChoiceOptions)
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.TooManyOptionsCode,
                string.Create(CultureInfo.InvariantCulture, $"A choice question offers {choice.OptionValues.Count} options, but JEV accepts at most {JevProtocol.MaximumChoiceOptions}."),
                choice.Id);
        }

        JsonObject criteria = [];

        foreach (object option in choice.OptionValues)
        {
            if (JevOptionLabel.For(option) is not { } label)
            {
                throw JevMappingErrors.CannotSend(
                    Options,
                    JevProtocol.AmbiguousOptionsCode,
                    "An option of a choice question has no textual value, so JEV could neither offer it nor report a selection that identifies it.",
                    choice.Id);
            }

            if (criteria.ContainsKey(label))
            {
                throw JevMappingErrors.CannotSend(
                    Options,
                    JevProtocol.AmbiguousOptionsCode,
                    string.Create(CultureInfo.InvariantCulture, $"Two options of a choice question both travel as '{label}', so an answer could not be resolved back to one of them."),
                    choice.Id);
            }

            criteria[label] = null;
        }

        return criteria;
    }

    private JsonNode? ToCriteria(UnknownQuestion question)
    {
        if (question.RawDefinition is not { } definition)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(definition);
        }
        catch (JsonException exception)
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.InvalidQuestionDefinitionCode,
                "The raw definition of an unknown question is not valid JSON.",
                question.Id,
                exception);
        }
    }

    private DecisionException RubricRequired(ScoreQuestion question) =>
        JevMappingErrors.CannotSend(
            Options,
            JevProtocol.UnsupportedQuestionCode,
            string.Create(CultureInfo.InvariantCulture, $"JEV scores against a rubric of described levels rather than a numeric range from {question.Minimum} to {question.Maximum}, and the rubric is mandatory. Ask this question as a {nameof(JevScoreQuestion)}, which carries one."),
            question.Id);

    private DecisionException Unsupported(Question question) =>
        JevMappingErrors.CannotSend(
            Options,
            JevProtocol.UnsupportedQuestionCode,
            string.Create(CultureInfo.InvariantCulture, $"Question type {question.GetType()} has no JEV representation."),
            question.Id);
}
