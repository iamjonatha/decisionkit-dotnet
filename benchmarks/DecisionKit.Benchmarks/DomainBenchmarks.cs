using BenchmarkDotNet.Attributes;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Benchmarks;

/// <summary>
/// The domain operations an application performs on every single decision.
/// </summary>
/// <remarks>
/// These are the paths that run whether or not a network call happens, so they are the ones where a
/// regression is paid for on every request. Nothing here should allocate more than the answers it
/// produces.
/// </remarks>
[MemoryDiagnoser]
public class DomainBenchmarks
{
    private readonly ChoiceQuestion<Department> _routing = new(
        new QuestionId("routing"),
        "Which team should handle this ticket?",
        [Department.Billing, Department.Support, Department.Security]);

    private readonly ScoreQuestion _frustration = new(
        new QuestionId("frustration"),
        "How frustrated is the customer?",
        minimum: 0,
        maximum: 10);

    private readonly ProbabilityQuestion _churn = new(
        new QuestionId("churn"),
        "How likely is this customer to cancel?");

    private QuestionSet _questions = null!;
    private DecisionResult _result = null!;

    [GlobalSetup]
    public void Setup()
    {
        _questions = QuestionSet.Create(_routing, _frustration, _churn);

        _result = new DecisionResult(
            [
                _routing.CreateAnswer(Department.Billing),
                new Answers.ScoreAnswer(_frustration.Id, _frustration.CreateScore(8.5)),
                new Answers.ProbabilityAnswer(_churn.Id, new Probability(0.34)),
            ],
            new DecisionMetadata("benchmark", System.DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// Building the question set, which validates identifiers and rejects duplicates.
    /// </summary>
    [Benchmark]
    public QuestionSet BuildQuestionSet() => QuestionSet.Create(_routing, _frustration, _churn);

    /// <summary>
    /// Building the request an application sends.
    /// </summary>
    [Benchmark]
    public DecisionRequest BuildRequest() => new(_questions)
    {
        Input = DecisionInput.FromText("I have been charged twice for the same month."),
        ClientRequestId = RequestId.New(),
    };

    /// <summary>
    /// The typed lookup, which is how application code is expected to read an answer.
    /// </summary>
    [Benchmark(Baseline = true)]
    public Department TypedLookup() => _result.Get(_routing).Value.Selection;

    /// <summary>
    /// The untyped lookup, for comparison. It costs a cast at the call site, not here.
    /// </summary>
    [Benchmark]
    public Answers.Answer UntypedLookup() => _result.Get("routing");

    /// <summary>
    /// Reading every answer, as a logging or auditing path would.
    /// </summary>
    [Benchmark]
    public int EnumerateAnswers()
    {
        int count = 0;

        foreach (Answers.Answer answer in _result.Answers)
        {
            count += answer.QuestionId.Value.Length;
        }

        return count;
    }
}

/// <summary>
/// The team a support ticket can be routed to.
/// </summary>
public enum Department
{
    Billing,
    Support,
    Security,
}
