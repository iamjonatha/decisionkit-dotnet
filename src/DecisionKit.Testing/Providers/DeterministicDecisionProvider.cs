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

namespace DecisionKit.Testing.Providers;

/// <summary>
/// A provider that derives its answers from the request, so that the same request always produces
/// the same result.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FakeDecisionProvider"/> requires a test to state every answer. That is the right
/// trade for a unit test, and the wrong one for a snapshot test, a demo, a sample application or a
/// load test, where what matters is that the answers are plausible, varied and stable.
/// </para>
/// <para>
/// Answers are derived from the question identifier, the input text and <see cref="Seed"/> through
/// a fixed hash, never from a random number generator and never from the wall clock. The same
/// request therefore produces the same answers on every machine, in every process and in every run,
/// which is what makes a recorded snapshot meaningful.
/// </para>
/// <para>
/// Answers are plausible, not correct. This provider is not a model.
/// </para>
/// </remarks>
public sealed class DeterministicDecisionProvider : IDecisionProvider
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const int RoundedDigits = 4;

    private static readonly DecisionProviderCapabilities s_capabilities = new()
    {
        SupportedQuestionTypes =
        [
            typeof(ProbabilityQuestion),
            typeof(ScoreQuestion),
            typeof(ChoiceQuestion<>),
            typeof(UnknownQuestion),
        ],
        SupportsIdempotencyKeys = true,
        SupportsRequestMetadata = true,
        SupportsBatching = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicDecisionProvider"/> class named
    /// <c>deterministic</c>.
    /// </summary>
    public DeterministicDecisionProvider()
        : this("deterministic")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicDecisionProvider"/> class.
    /// </summary>
    /// <param name="name">The provider name reported in results and errors.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public DeterministicDecisionProvider(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public DecisionProviderCapabilities Capabilities => s_capabilities;

    /// <summary>
    /// Gets the seed that shifts every derived answer.
    /// </summary>
    /// <remarks>
    /// Two providers with different seeds answer the same request differently, which is how a test
    /// covers a second scenario without inventing a second set of questions.
    /// </remarks>
    public ulong Seed { get; init; }

    /// <summary>
    /// Gets the timestamp reported in result metadata. Defaults to
    /// <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    /// <remarks>
    /// The clock is fixed rather than read, because a snapshot that contains the current time is a
    /// snapshot that never matches twice.
    /// </remarks>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UnixEpoch;

    /// <inheritdoc />
    /// <exception cref="DecisionValidationException">
    /// The request asks a question type this provider cannot derive an answer for.
    /// </exception>
    public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        List<Answer> answers = new(request.Questions.Count);

        foreach (Question question in request.Questions)
        {
            answers.Add(CreateAnswer(question, request.Input.Text));
        }

        DecisionMetadata metadata = new(Name, Timestamp)
        {
            ClientRequestId = request.ClientRequestId,
            ProviderRequestId = string.Create(CultureInfo.InvariantCulture, $"{DeriveRequestHash(request):x16}"),
        };

        return Task.FromResult(new DecisionResult(answers, metadata));
    }

    private static ulong Hash(ulong seed, string? qualifier, string value)
    {
        ulong hash = FnvOffsetBasis;

        for (int shift = 0; shift < 64; shift += 8)
        {
            hash = Fold(hash, (byte)(seed >> shift));
        }

        if (qualifier is not null)
        {
            hash = Fold(hash, qualifier);
        }

        return Fold(hash, value);
    }

    private static ulong Fold(ulong hash, string value)
    {
        foreach (char character in value)
        {
            hash = Fold(hash, (byte)character);
            hash = Fold(hash, (byte)(character >> 8));
        }

        return hash;
    }

    private static ulong Fold(ulong hash, byte value) => (hash ^ value) * FnvPrime;

    /// <summary>
    /// Projects a hash onto the half-open interval <c>[0.0, 1.0)</c>, using the 53 bits a double
    /// can represent exactly.
    /// </summary>
    private static double ToUnitInterval(ulong hash) => (hash >> 11) * (1.0 / 9007199254740992.0);

    private static double Round(double value) => Math.Round(value, RoundedDigits, MidpointRounding.AwayFromZero);

    private Answer CreateAnswer(Question question, string? inputText)
    {
        ulong hash = Hash(Seed, inputText, question.Id.Value);

        switch (question)
        {
            case ProbabilityQuestion probability:
                return new ProbabilityAnswer(probability.Id, new Probability(Round(ToUnitInterval(hash))));

            case ScoreQuestion score:
                return new ScoreAnswer(score.Id, score.CreateScore(DeriveScore(hash, score)));

            case IChoiceQuestion choice:
                return choice.CreateAnswer(choice.OptionValues[(int)(hash % (ulong)choice.OptionValues.Count)]);

            case UnknownQuestion unknown:
                return new UnknownAnswer(unknown.Id, unknown.ProviderType);

            default:
                throw DecisionException.FromError(new DecisionError(
                    DecisionErrorCategory.Validation,
                    string.Create(CultureInfo.InvariantCulture, $"This provider cannot derive an answer for question type {question.GetType()}."))
                {
                    Code = "unsupported_question_type",
                    ProviderName = Name,
                    Retry = DecisionRetryHint.NotRetryable,
                });
        }
    }

    private static double DeriveScore(ulong hash, ScoreQuestion question)
    {
        double value = question.Minimum + (ToUnitInterval(hash) * (question.Maximum - question.Minimum));

        return Math.Clamp(Round(value), question.Minimum, question.Maximum);
    }

    private ulong DeriveRequestHash(DecisionRequest request)
    {
        ulong hash = Hash(Seed, request.Input.Text, Name);

        foreach (Question question in request.Questions)
        {
            hash = Fold(hash, question.Id.Value);
        }

        return hash;
    }
}
