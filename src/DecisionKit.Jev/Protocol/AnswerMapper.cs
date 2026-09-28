using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Questions;
using DecisionKit.Jev.Serialization;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Values;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Translates a JEV wire answer into the domain answer the question asked for.
/// </summary>
/// <remarks>
/// <para>
/// The question drives the translation, not the payload. JEV reports an answer under the key the
/// caller chose for the question, so the expected shape is always known before the answer is read,
/// and an answer that does not fit it is reported as a failure rather than reinterpreted.
/// </para>
/// <para>
/// Three things JEV reports have no place in the provider-neutral domain: how sure the model was,
/// the rubric a score was measured against, and the per-level distribution behind a score. They
/// reach the caller as answer metadata under the keys <see cref="JevProtocol"/> declares, and
/// <see cref="JevAnswers"/> reads them back.
/// </para>
/// </remarks>
public sealed class AnswerMapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnswerMapper"/> class with the default mapping
    /// settings.
    /// </summary>
    public AnswerMapper()
        : this(JevMappingOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnswerMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public AnswerMapper(JevMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    /// <summary>
    /// Gets the mapper that uses the default mapping settings.
    /// </summary>
    public static AnswerMapper Default { get; } = new();

    /// <summary>
    /// Gets the mapping settings this mapper uses.
    /// </summary>
    public JevMappingOptions Options { get; }

    /// <summary>
    /// Translates a wire answer into the answer its question asked for.
    /// </summary>
    /// <param name="question">The question the answer responds to.</param>
    /// <param name="answer">The wire answer.</param>
    /// <returns>The domain answer.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionException">
    /// The answer cannot be mapped onto the question that asked for it.
    /// </exception>
    public Answer ToDomain(Question question, JevAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);

        if (question is UnknownQuestion || !IsKnownType(answer.Type))
        {
            return ToUnknown(question.Id, answer);
        }

        RequireMatchingType(question, answer);

        return question switch
        {
            ProbabilityQuestion probability => ToProbability(probability, answer),
            JevScoreQuestion score => ToScore(score, answer),
            IChoiceQuestion choice => ToChoice(choice, answer),
            _ => throw JevMappingErrors.CannotRead(
                Options,
                string.Create(CultureInfo.InvariantCulture, $"Question type {question.GetType()} has no JEV representation, so its answer could not be read."),
                question.Id),
        };
    }

    private static bool IsKnownType(string? type) =>
        type is JevQuestionTypes.Noul or JevQuestionTypes.Choice or JevQuestionTypes.Score;

    private static string ExpectedType(Question question) => question switch
    {
        ProbabilityQuestion => JevQuestionTypes.Noul,
        JevScoreQuestion => JevQuestionTypes.Score,
        IChoiceQuestion => JevQuestionTypes.Choice,
        _ => string.Empty,
    };

    private static UnknownAnswer ToUnknown(QuestionId questionId, JevAnswer answer)
    {
        return new UnknownAnswer(questionId, answer.Type ?? JevProtocol.UnnamedAnswerType)
        {
            RawPayload = JevJsonSerialization.Serialize(answer),
            Metadata = Describe(answer, includeProbabilities: true, includeRawPayload: false),
        };
    }

    private void RequireMatchingType(Question question, JevAnswer answer)
    {
        string expected = ExpectedType(question);

        if (expected.Length != 0 && !string.Equals(answer.Type, expected, StringComparison.Ordinal))
        {
            throw JevMappingErrors.CannotRead(
                Options,
                string.Create(CultureInfo.InvariantCulture, $"The question expects a '{expected}' answer, but JEV reported a '{answer.Type}' one."),
                question.Id);
        }
    }

    private ProbabilityAnswer ToProbability(ProbabilityQuestion question, JevAnswer answer)
    {
        if (answer.Noul is not { } value)
        {
            throw Missing(question.Id, JevQuestionTypes.Noul);
        }

        if (!Probability.TryCreate(value, out Probability probability))
        {
            throw OutOfRange(question.Id, value, "a probability between 0 and 1");
        }

        return new ProbabilityAnswer(question.Id, probability) { Metadata = Describe(answer) };
    }

    private ScoreAnswer ToScore(JevScoreQuestion question, JevAnswer answer)
    {
        if (answer.Score is not { } value)
        {
            throw Missing(question.Id, JevQuestionTypes.Score);
        }

        if (!Score.TryCreate(value, question.Minimum, question.Maximum, out Score score))
        {
            throw OutOfRange(
                question.Id,
                value,
                string.Create(CultureInfo.InvariantCulture, $"a position between {question.Minimum} and {question.Maximum}, one per rubric level"));
        }

        return new ScoreAnswer(question.Id, score) { Metadata = Describe(answer) };
    }

    private Answer ToChoice(IChoiceQuestion question, JevAnswer answer)
    {
        ChoiceOutcome outcome = ToOutcome(question, answer);

        return question.CreateAnswer(outcome.WithMetadata(Describe(answer, includeProbabilities: false)));
    }

    private ChoiceOutcome ToOutcome(IChoiceQuestion question, JevAnswer answer)
    {
        List<OptionProbability> distribution = ToDistribution(question, answer.Probabilities);

        if (answer.Choice is not { } label)
        {
            return distribution.Count > 0
                ? ChoiceOutcome.FromDistribution(distribution)
                : throw Missing(question.Id, JevQuestionTypes.Choice);
        }

        object selection = Resolve(question, label);

        return distribution.Count > 0
            ? ChoiceOutcome.Create(distribution, selection)
            : ChoiceOutcome.FromSelection(selection);
    }

    private List<OptionProbability> ToDistribution(IChoiceQuestion question, JsonObject? probabilities)
    {
        if (probabilities is null)
        {
            return [];
        }

        List<OptionProbability> distribution = new(probabilities.Count);

        foreach (KeyValuePair<string, JsonNode?> entry in probabilities)
        {
            object option = Resolve(question, entry.Key);

            if (!JevJsonValues.TryGetDouble(entry.Value, out double value) || !Probability.TryCreate(value, out Probability probability))
            {
                throw JevMappingErrors.CannotRead(
                    Options,
                    string.Create(CultureInfo.InvariantCulture, $"JEV assigned option '{entry.Key}' a probability of {entry.Value?.ToJsonString() ?? "null"}, which is not a probability between 0 and 1."),
                    question.Id);
            }

            distribution.Add(new OptionProbability(option, probability));
        }

        return distribution;
    }

    private object Resolve(IChoiceQuestion question, string label)
    {
        foreach (object option in question.OptionValues)
        {
            if (JevOptionLabel.Matches(option, label))
            {
                return option;
            }
        }

        throw JevMappingErrors.CannotRead(
            Options,
            string.Create(CultureInfo.InvariantCulture, $"JEV named the option '{label}', which the question never offered."),
            question.Id);
    }

    private Dictionary<string, object?> Describe(JevAnswer answer) =>
        Describe(answer, includeProbabilities: true, includeRawPayload: Options.PreserveRawPayloads);

    private Dictionary<string, object?> Describe(JevAnswer answer, bool includeProbabilities) =>
        Describe(answer, includeProbabilities, includeRawPayload: Options.PreserveRawPayloads);

    private static Dictionary<string, object?> Describe(JevAnswer answer, bool includeProbabilities, bool includeRawPayload)
    {
        Dictionary<string, object?> metadata = new(StringComparer.Ordinal);

        if (answer.Confidence is { } confidence)
        {
            metadata[JevProtocol.ConfidenceMetadataKey] = confidence;
        }

        if (answer.Legend is { } legend)
        {
            metadata[JevProtocol.LegendMetadataKey] = JevJsonValues.ToMetadata(legend);
        }

        if (includeProbabilities && answer.Probabilities is { } probabilities)
        {
            metadata[JevProtocol.ProbabilitiesMetadataKey] = JevJsonValues.ToMetadata(probabilities);
        }

        foreach (KeyValuePair<string, object?> entry in JevJsonValues.ToMetadata(answer.Extensions))
        {
            metadata[entry.Key] = entry.Value;
        }

        if (includeRawPayload)
        {
            metadata[JevProtocol.RawPayloadMetadataKey] = JevJsonSerialization.Serialize(answer);
        }

        return metadata;
    }

    private DecisionException Missing(QuestionId questionId, string type) =>
        JevMappingErrors.CannotRead(
            Options,
            string.Create(CultureInfo.InvariantCulture, $"JEV reported a '{type}' answer without the value that carries its result."),
            questionId);

    private DecisionException OutOfRange(QuestionId questionId, double value, string expected) =>
        JevMappingErrors.CannotRead(
            Options,
            string.Create(CultureInfo.InvariantCulture, $"JEV reported {value}, which is not {expected}."),
            questionId);
}
