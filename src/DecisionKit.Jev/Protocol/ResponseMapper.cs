using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Serialization;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Translates a JEV wire response into a domain decision result.
/// </summary>
/// <remarks>
/// <para>
/// The response is read against the request that produced it. JEV keys its answers by the names the
/// caller gave its questions, so the mapper walks the answers and finds the question each one
/// belongs to; an answer for a question that was never asked is a failure, because it cannot be
/// interpreted without one.
/// </para>
/// <para>
/// A question may go unanswered. That is not an error and it is not filled in with a default: the
/// result simply does not contain it, and <see cref="DecisionResult.TryGet(QuestionId, out Answer)"/>
/// reports so.
/// </para>
/// <para>
/// JEV puts no timestamp in the response body, so the result is stamped with the time the mapper
/// read it, taken from a <see cref="TimeProvider"/> the caller can substitute in tests.
/// </para>
/// </remarks>
public sealed class ResponseMapper
{
    private readonly AnswerMapper _answers;
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseMapper"/> class with the default mapping
    /// settings and the system clock.
    /// </summary>
    public ResponseMapper()
        : this(AnswerMapper.Default, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ResponseMapper(JevMappingOptions options)
        : this(new AnswerMapper(options), TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <param name="timeProvider">The clock that stamps the result.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ResponseMapper(JevMappingOptions options, TimeProvider timeProvider)
        : this(new AnswerMapper(options), timeProvider)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseMapper"/> class over an existing answer
    /// mapper.
    /// </summary>
    /// <param name="answerMapper">The mapper that translates each answer.</param>
    /// <param name="timeProvider">The clock that stamps the result.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ResponseMapper(AnswerMapper answerMapper, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(answerMapper);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _answers = answerMapper;
        _time = timeProvider;
    }

    /// <summary>
    /// Gets the mapper that uses the default mapping settings and the system clock.
    /// </summary>
    public static ResponseMapper Default { get; } = new();

    /// <summary>
    /// Gets the mapping settings this mapper uses.
    /// </summary>
    public JevMappingOptions Options => _answers.Options;

    /// <summary>
    /// Translates a wire response into the result of the request that produced it.
    /// </summary>
    /// <param name="request">The request the response answers.</param>
    /// <param name="response">The wire response.</param>
    /// <returns>The decision result.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionKit.Errors.DecisionException">
    /// The response cannot be mapped onto the request.
    /// </exception>
    public DecisionResult ToDomain(DecisionRequest request, JevResponse response) =>
        ToDomain(request, response, providerRequestId: null);

    /// <summary>
    /// Translates a wire response into the result of the request that produced it, recording the
    /// identifier the service assigned the call.
    /// </summary>
    /// <param name="request">The request the response answers.</param>
    /// <param name="response">The wire response.</param>
    /// <param name="providerRequestId">
    /// The identifier the service reported for the call, which travels in a response header rather
    /// than in the body, or <see langword="null"/> when it is unknown.
    /// </param>
    /// <returns>The decision result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> or <paramref name="response"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DecisionKit.Errors.DecisionException">
    /// The response cannot be mapped onto the request.
    /// </exception>
    public DecisionResult ToDomain(DecisionRequest request, JevResponse response, string? providerRequestId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        return new DecisionResult(ToDomain(request, response.Answers), ToMetadata(request, response, providerRequestId))
        {
            Usage = ToUsage(response.Usage),
        };
    }

    private static DecisionUsage ToUsage(JevUsage? usage)
    {
        if (usage is null)
        {
            return DecisionUsage.Empty;
        }

        Dictionary<string, double> metrics = new(StringComparer.Ordinal);

        if (usage.InputTokens is { } input)
        {
            metrics[DecisionUsageKeys.InputTokens] = input;
        }

        if (usage.OutputTokens is { } output)
        {
            metrics[DecisionUsageKeys.OutputTokens] = output;
        }

        foreach (KeyValuePair<string, object?> entry in JevJsonValues.ToMetadata(usage.Extensions))
        {
            if (entry.Value is double measure)
            {
                metrics[entry.Key] = measure;
            }
            else if (entry.Value is long count)
            {
                metrics[entry.Key] = count;
            }
        }

        return metrics.Count == 0 ? DecisionUsage.Empty : new DecisionUsage(metrics);
    }

    private DecisionMetadata ToMetadata(DecisionRequest request, JevResponse response, string? providerRequestId) =>
        new(Options.ProviderName, _time.GetUtcNow())
        {
            ClientRequestId = request.ClientRequestId,
            ProviderRequestId = providerRequestId,
            ModelVersion = response.Model,
        };

    private List<Answer> ToDomain(DecisionRequest request, IReadOnlyDictionary<string, JevAnswer> answers)
    {
        List<Answer> mapped = new(answers.Count);

        foreach (KeyValuePair<string, JevAnswer> entry in answers)
        {
            if (entry.Value is null)
            {
                throw JevMappingErrors.CannotRead(
                    Options,
                    string.Create(CultureInfo.InvariantCulture, $"JEV reported the literal null as the answer to question '{entry.Key}'."));
            }

            if (!QuestionId.TryCreate(entry.Key, out QuestionId questionId) ||
                !request.Questions.TryGet(questionId, out Question? question))
            {
                throw JevMappingErrors.CannotRead(
                    Options,
                    string.Create(CultureInfo.InvariantCulture, $"JEV answered a question named '{entry.Key}', which the request never asked."));
            }

            Answer answer = _answers.ToDomain(question, entry.Value);

            if (answer is UnknownAnswer unknown && request.Options.UnknownTypeHandling == UnknownTypeHandling.Fail)
            {
                throw JevMappingErrors.CannotRead(
                    Options,
                    string.Create(CultureInfo.InvariantCulture, $"JEV reported an answer of type '{unknown.ProviderType}', which DecisionKit does not model, and the request asked to fail on anything unrecognized."),
                    questionId);
            }

            mapped.Add(answer);
        }

        return mapped;
    }
}
