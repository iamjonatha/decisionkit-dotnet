using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Samples.CustomProvider;

/// <summary>
/// A provider backed by an in-house rules engine instead of a remote service.
/// </summary>
/// <remarks>
/// <para>
/// Writing a provider means answering four questions, and nothing else:
/// </para>
/// <list type="number">
/// <item><description>What is it called? <see cref="Name"/> appears in results and errors.</description></item>
/// <item><description>What can it do? <see cref="Capabilities"/> is declared, never guessed by the caller.</description></item>
/// <item><description>How does it answer? <see cref="DecideAsync"/> returns one answer per question it handled.</description></item>
/// <item><description>How does it fail? Every failure is a <see cref="DecisionException"/> carrying a <see cref="DecisionError"/>.</description></item>
/// </list>
/// <para>
/// There is no base class to inherit and no registration to perform. A provider is whatever
/// implements the interface.
/// </para>
/// </remarks>
public sealed class KeywordDecisionProvider : IDecisionProvider
{
    private static readonly DecisionProviderCapabilities s_capabilities = new()
    {
        // Membership is by exact type, except for an open generic, which declares every closed
        // form of itself. Anything not listed here is something this provider refuses.
        SupportedQuestionTypes =
        [
            typeof(ProbabilityQuestion),
            typeof(ScoreQuestion),
            typeof(ChoiceQuestion<>),
        ],
        SupportsIdempotencyKeys = false,
        SupportsUsageReporting = true,
        SupportsBatching = true,
    };

    private readonly IReadOnlyDictionary<string, double> _weights;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeywordDecisionProvider"/> class.
    /// </summary>
    /// <param name="weights">How much each keyword contributes, between 0 and 1.</param>
    /// <param name="timeProvider">The clock used to stamp results.</param>
    public KeywordDecisionProvider(IReadOnlyDictionary<string, double> weights, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _weights = weights;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string Name => "keyword";

    /// <inheritdoc />
    public DecisionProviderCapabilities Capabilities => s_capabilities;

    /// <inheritdoc />
    public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check the token before doing work, so that a caller that already gave up pays nothing.
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Input.Text is not { Length: > 0 } text)
        {
            throw Fail(DecisionErrorCategory.Validation, "This provider needs input text to score.", "input_required");
        }

        double weight = Score(text);
        List<Answer> answers = new(request.Questions.Count);

        foreach (Question question in request.Questions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            answers.Add(Answer(question, weight));
        }

        DecisionMetadata metadata = new(Name, _timeProvider.GetUtcNow())
        {
            ClientRequestId = request.ClientRequestId,
            ModelVersion = "keyword/1",
        };

        DecisionResult result = new(answers, metadata)
        {
            Usage = new DecisionUsage(new Dictionary<string, double>
            {
                [DecisionUsageKeys.EvaluatedQuestions] = request.Questions.Count,
            }),
        };

        return Task.FromResult(result);
    }

    private double Score(string text)
    {
        double total = 0;

        foreach (KeyValuePair<string, double> weight in _weights)
        {
            if (text.Contains(weight.Key, StringComparison.OrdinalIgnoreCase))
            {
                total += weight.Value;
            }
        }

        return Math.Clamp(total, 0, 1);
    }

    private Answer Answer(Question question, double weight) => question switch
    {
        ProbabilityQuestion probability =>
            new ProbabilityAnswer(probability.Id, new Probability(weight)),

        // A score answer carries the scale its question declared, so the number stays meaningful.
        ScoreQuestion score =>
            new ScoreAnswer(score.Id, score.CreateScore(score.Minimum + (weight * (score.Maximum - score.Minimum)))),

        // IChoiceQuestion is the non-generic view a provider needs: it can build a correctly typed
        // ChoiceAnswer<TOption> without the provider ever knowing what TOption is.
        IChoiceQuestion choice =>
            choice.CreateAnswer(choice.OptionValues[(int)Math.Round(weight * (choice.OptionValues.Count - 1))]),

        // Refusing is a first-class outcome. Saying so beats returning a plausible answer that the
        // provider did not actually compute.
        _ => throw Fail(
            DecisionErrorCategory.Validation,
            string.Create(CultureInfo.InvariantCulture, $"This provider cannot answer '{question.GetType().Name}'."),
            "unsupported_question_type"),
    };

    private DecisionException Fail(DecisionErrorCategory category, string message, string code) =>
        DecisionException.FromError(new DecisionError(category, message)
        {
            Code = code,
            ProviderName = Name,
            Retry = DecisionRetryHint.NotRetryable,
        });
}
