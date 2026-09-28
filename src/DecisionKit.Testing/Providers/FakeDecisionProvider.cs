using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Recording;
using DecisionKit.Values;

namespace DecisionKit.Testing.Providers;

/// <summary>
/// A provider that answers exactly what a test told it to answer, and records what it was asked.
/// </summary>
/// <remarks>
/// <para>
/// This is the type most tests use. It replaces a real provider without any HTTP, any credential or
/// any network, and it fails loudly rather than quietly when the system under test asks something
/// the test did not anticipate.
/// </para>
/// <para>
/// Configuration is question-first: every <c>Returns</c> overload takes the question object, so the
/// answer type is checked by the compiler and a typo in an identifier cannot go unnoticed.
/// </para>
/// <para>
/// Configure the fake before exercising the system under test. Once configured, concurrent calls
/// are safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// FakeDecisionProvider provider = new FakeDecisionProvider()
///     .Returns(TicketRouter.RoutingQuestion, Department.Billing)
///     .Returns(TicketRouter.UrgencyQuestion, 8.0);
///
/// TicketRouting routing = await new TicketRouter(provider).RouteAsync(ticket, cancellationToken);
///
/// Assert.Equal(1, provider.CallCount);
/// Assert.Equal(Department.Billing, routing.Department);
/// </code>
/// </example>
public sealed class FakeDecisionProvider : IDecisionProvider
{
    private readonly Dictionary<QuestionId, Answer> _answers = [];
    private readonly HashSet<QuestionId> _unanswered = [];
    private readonly HashSet<Type> _configuredQuestionTypes = [];

    private DecisionProviderCapabilities? _capabilities;
    private bool _capabilitiesWereSet;
    private DecisionError? _error;
    private bool _failsEveryCall;
    private int _remainingFailures;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDecisionProvider"/> class named <c>fake</c>.
    /// </summary>
    public FakeDecisionProvider()
        : this("fake")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDecisionProvider"/> class.
    /// </summary>
    /// <param name="name">The provider name reported in results and errors.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public FakeDecisionProvider(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets or sets what this fake declares it can do.
    /// </summary>
    /// <remarks>
    /// Until a test sets this explicitly, the fake reports the question types it has been taught to
    /// answer, and declares that it honours every optional provider feature. Set it to assert how
    /// application code behaves against a provider that supports less.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The assigned capabilities are <see langword="null"/>.</exception>
    public DecisionProviderCapabilities Capabilities
    {
        get => _capabilities ??= BuildCapabilities();
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _capabilities = value;
            _capabilitiesWereSet = true;
        }
    }

    /// <summary>
    /// Gets the log of the calls this fake observed.
    /// </summary>
    public DecisionCallLog Calls { get; } = new();

    /// <summary>
    /// Gets the number of calls this fake observed.
    /// </summary>
    public int CallCount => Calls.Count;

    /// <summary>
    /// Gets the most recent request, or <see langword="null"/> when there was none.
    /// </summary>
    public DecisionRequest? LastRequest => Calls.IsEmpty ? null : Calls.Last.Request;

    /// <summary>
    /// Gets or sets the delay the fake simulates before answering.
    /// </summary>
    /// <remarks>
    /// The delay is awaited through <see cref="TimeProvider"/>, so a controllable clock keeps the
    /// test instant while still exercising timeout and cancellation paths.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The assigned latency is negative.</exception>
    public TimeSpan Latency
    {
        get;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Simulated latency cannot be negative.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the clock used for the simulated latency and for result timestamps.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned provider is <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the usage the fake reports on every result.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned usage is <see langword="null"/>.</exception>
    public DecisionUsage Usage
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = DecisionUsage.Empty;

    /// <summary>
    /// Gets or sets the provider-assigned request identifier the fake reports in result metadata.
    /// </summary>
    public string? ProviderRequestId { get; set; }

    /// <summary>
    /// Configures the answer to a question.
    /// </summary>
    /// <typeparam name="TAnswer">The type of answer the question expects.</typeparam>
    /// <param name="question">The question to answer.</param>
    /// <param name="answer">The answer to return.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="answer"/> belongs to a different question.</exception>
    public FakeDecisionProvider Returns<TAnswer>(Question<TAnswer> question, TAnswer answer)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.QuestionId != question.Id)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The answer belongs to question '{answer.QuestionId}', not to '{question.Id}'."),
                nameof(answer));
        }

        return Configure(question, answer);
    }

    /// <summary>
    /// Configures the probability returned for a question.
    /// </summary>
    /// <param name="question">The question to answer.</param>
    /// <param name="probability">The probability to return.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    public FakeDecisionProvider Returns(ProbabilityQuestion question, Probability probability)
    {
        ArgumentNullException.ThrowIfNull(question);

        return Configure(question, new ProbabilityAnswer(question.Id, probability));
    }

    /// <summary>
    /// Configures the probability returned for a question.
    /// </summary>
    /// <param name="question">The question to answer.</param>
    /// <param name="probability">The probability to return, in the closed interval <c>[0.0, 1.0]</c>.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside the interval.</exception>
    public FakeDecisionProvider Returns(ProbabilityQuestion question, double probability) =>
        Returns(question, new Probability(probability));

    /// <summary>
    /// Configures the score returned for a question.
    /// </summary>
    /// <param name="question">The question to answer.</param>
    /// <param name="score">The score to return.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    public FakeDecisionProvider Returns(ScoreQuestion question, Score score)
    {
        ArgumentNullException.ThrowIfNull(question);

        return Configure(question, new ScoreAnswer(question.Id, score));
    }

    /// <summary>
    /// Configures the score returned for a question, on that question's own scale.
    /// </summary>
    /// <param name="question">The question to answer.</param>
    /// <param name="score">The measured value.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> falls outside the question's scale.</exception>
    public FakeDecisionProvider Returns(ScoreQuestion question, double score)
    {
        ArgumentNullException.ThrowIfNull(question);

        return Returns(question, question.CreateScore(score));
    }

    /// <summary>
    /// Configures the option selected for a question.
    /// </summary>
    /// <typeparam name="TOption">The type of the options the question offers.</typeparam>
    /// <param name="question">The question to answer.</param>
    /// <param name="selection">The option to select.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="selection"/> is not one of the offered options.</exception>
    public FakeDecisionProvider Returns<TOption>(ChoiceQuestion<TOption> question, TOption selection)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(question);

        return Configure(question, question.CreateAnswer(selection));
    }

    /// <summary>
    /// Configures the choice returned for a question, which may be a distribution rather than a
    /// single option.
    /// </summary>
    /// <typeparam name="TOption">The type of the options the question offers.</typeparam>
    /// <param name="question">The question to answer.</param>
    /// <param name="choice">The choice to return.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FakeDecisionProvider Returns<TOption>(ChoiceQuestion<TOption> question, Choice<TOption> choice)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(choice);

        return Configure(question, new ChoiceAnswer<TOption>(question.Id, choice));
    }

    /// <summary>
    /// Configures a question to come back unanswered, so that a test can assert how application
    /// code copes with an incomplete result.
    /// </summary>
    /// <param name="question">The question to leave unanswered.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <see langword="null"/>.</exception>
    public FakeDecisionProvider ReturnsNothing(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        _answers.Remove(question.Id);
        _unanswered.Add(question.Id);
        Teach(question);

        return this;
    }

    /// <summary>
    /// Configures every call to fail.
    /// </summary>
    /// <param name="error">The error to report.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="error"/> is a cancellation.</exception>
    public FakeDecisionProvider Fails(DecisionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        RejectCancellation(error);

        _error = error;
        _failsEveryCall = true;
        _remainingFailures = 0;

        return this;
    }

    /// <summary>
    /// Configures every call to fail with a minimally described error.
    /// </summary>
    /// <param name="category">How the failure should be classified.</param>
    /// <param name="message">What went wrong.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> is empty or whitespace, or <paramref name="category"/> is
    /// <see cref="DecisionErrorCategory.Canceled"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public FakeDecisionProvider Fails(DecisionErrorCategory category, string message) =>
        Fails(new DecisionError(category, message) { ProviderName = Name });

    /// <summary>
    /// Configures the next calls to fail, after which the fake answers normally again.
    /// </summary>
    /// <param name="count">How many calls fail.</param>
    /// <param name="error">The error to report.</param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <remarks>
    /// This is how a retry policy is tested: fail twice, then succeed, and assert that the caller
    /// saw one success and that the log holds three calls.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="error"/> is a cancellation.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public FakeDecisionProvider FailsTimes(int count, DecisionError error)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(error);
        RejectCancellation(error);

        _error = error;
        _failsEveryCall = false;
        _remainingFailures = count;

        return this;
    }

    /// <summary>
    /// Cancels any configured failure, so that the fake answers normally again.
    /// </summary>
    /// <returns>This instance, so that calls can be chained.</returns>
    public FakeDecisionProvider Succeeds()
    {
        _error = null;
        _failsEveryCall = false;
        _remainingFailures = 0;

        return this;
    }

    /// <summary>
    /// Forgets every configured answer, every configured failure and the whole call log.
    /// </summary>
    /// <remarks>
    /// The provider name, the clock and the latency survive, because they describe the fake rather
    /// than the scenario being arranged.
    /// </remarks>
    public void Reset()
    {
        _answers.Clear();
        _unanswered.Clear();
        _configuredQuestionTypes.Clear();
        _capabilities = null;
        _capabilitiesWereSet = false;

        Succeeds();
        Calls.Clear();
    }

    /// <inheritdoc />
    /// <exception cref="DecisionValidationException">
    /// The request asks a question the test did not configure. This is reported rather than
    /// silently omitted, because an unconfigured question means the test does not describe what the
    /// system under test actually does.
    /// </exception>
    public async Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Calls.Record(request, TimeProvider.GetUtcNow());

        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, TimeProvider, cancellationToken).ConfigureAwait(false);
        }

        if (TryTakeFailure(out DecisionError? error))
        {
            throw DecisionException.FromError(error);
        }

        List<Answer> answers = new(request.Questions.Count);

        foreach (Question question in request.Questions)
        {
            if (_answers.TryGetValue(question.Id, out Answer? answer))
            {
                answers.Add(answer);
                continue;
            }

            if (_unanswered.Contains(question.Id))
            {
                continue;
            }

            throw DecisionException.FromError(new DecisionError(
                DecisionErrorCategory.Validation,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The fake provider has no answer configured for question '{question.Id}'. Call Returns to configure one, or ReturnsNothing to leave it deliberately unanswered."))
            {
                Code = "fake_answer_not_configured",
                ProviderName = Name,
                Retry = DecisionRetryHint.NotRetryable,
            });
        }

        DecisionMetadata metadata = new(Name, TimeProvider.GetUtcNow())
        {
            ClientRequestId = request.ClientRequestId,
            ProviderRequestId = ProviderRequestId,
            Latency = Latency,
        };

        return new DecisionResult(answers, metadata) { Usage = Usage };
    }

    private static void RejectCancellation(DecisionError error)
    {
        if (error.Category == DecisionErrorCategory.Canceled)
        {
            throw new ArgumentException(
                "Cancellation is not an injectable failure. Cancel the token passed to DecideAsync instead.",
                nameof(error));
        }
    }

    private FakeDecisionProvider Configure(Question question, Answer answer)
    {
        _answers[question.Id] = answer;
        _unanswered.Remove(question.Id);
        Teach(question);

        return this;
    }

    private void Teach(Question question)
    {
        if (_configuredQuestionTypes.Add(question.GetType()) && !_capabilitiesWereSet)
        {
            _capabilities = null;
        }
    }

    private DecisionProviderCapabilities BuildCapabilities() => new()
    {
        SupportedQuestionTypes = [.. _configuredQuestionTypes],
        SupportsIdempotencyKeys = true,
        SupportsUsageReporting = true,
        SupportsRequestMetadata = true,
        SupportsBatching = true,
        SupportsExplanations = true,
    };

    private bool TryTakeFailure([NotNullWhen(true)] out DecisionError? error)
    {
        DecisionError? configured = _error;

        if (configured is null)
        {
            error = null;
            return false;
        }

        if (_failsEveryCall)
        {
            error = configured;
            return true;
        }

        if (Interlocked.Decrement(ref _remainingFailures) >= 0)
        {
            error = configured;
            return true;
        }

        Interlocked.Increment(ref _remainingFailures);
        error = null;

        return false;
    }
}
