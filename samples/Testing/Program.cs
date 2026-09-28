using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Providers;
using DecisionKit.Samples.Testing;
using DecisionKit.Testing.Assertions;
using DecisionKit.Testing.Providers;
using DecisionKit.Testing.Recording;

const string Ticket =
    "I have been charged twice for the same month and nobody has replied to my last two emails.";

// This sample is shaped like a test suite, because that is what the package is for. Every check
// below is a real assertion: DecisionAssert throws when it fails, so the process exits non-zero and
// continuous integration notices.
await StatedAnswersAsync();
await AssertingWhatWasAskedAsync();
await InjectedFailureAsync();
await FailingThenSucceedingAsync();
await RecordingARealProviderAsync();

Console.WriteLine("All scenarios passed.");

static async Task StatedAnswersAsync()
{
    // The fake answers exactly what the test states, and nothing else. An unanswered question is a
    // deliberate scenario, not an accident.
    FakeDecisionProvider provider = new FakeDecisionProvider()
        .Returns(TicketTriage.Routing, Department.Billing)
        .Returns(TicketTriage.Frustration, 8.5);

    (Department team, bool escalate) = await new TicketTriage(provider).TriageAsync(Ticket, CancellationToken.None);

    Assert(team == Department.Billing, "The ticket should be routed to billing.");
    Assert(escalate, "A frustration of 8.5 out of 10 should escalate.");

    Console.WriteLine("Stated answers: ok");
}

static async Task AssertingWhatWasAskedAsync()
{
    FakeDecisionProvider provider = new FakeDecisionProvider()
        .Returns(TicketTriage.Routing, Department.Security)
        .Returns(TicketTriage.Frustration, 2.0);

    _ = await new TicketTriage(provider).TriageAsync(Ticket, CancellationToken.None);

    // Asserting the request matters as much as asserting the answer: it is the only way to catch a
    // question that was silently dropped or asked twice.
    RecordedDecisionCall call = DecisionAssert.CalledOnce(provider.Calls);

    DecisionAssert.AskedExactly(call.Request, "routing", "frustration");
    Assert(call.Request.Input.Text == Ticket, "The ticket text should reach the provider unchanged.");

    Console.WriteLine("Asserted request: ok");
}

static async Task InjectedFailureAsync()
{
    // Error handling is behaviour, so it is tested like behaviour. No HTTP status code is faked and
    // no handler is stubbed: the failure is stated in the domain's own vocabulary.
    FakeDecisionProvider provider = new FakeDecisionProvider()
        .Fails(DecisionErrorCategory.RateLimit, "Too many requests.");

    (Department team, bool escalate) = await new TicketTriage(provider).TriageAsync(Ticket, CancellationToken.None);

    Assert(team == Department.Support, "An unavailable provider should fall back to support.");
    Assert(escalate, "A fallback decision should be escalated to a human.");

    Console.WriteLine("Injected failure: ok");
}

static async Task FailingThenSucceedingAsync()
{
    DecisionError transient = new(DecisionErrorCategory.Transport, "The connection was reset.")
    {
        ProviderName = "fake",
        Retry = DecisionRetryHint.Retryable,
    };

    FakeDecisionProvider provider = new FakeDecisionProvider()
        .Returns(TicketTriage.Routing, Department.Billing)
        .Returns(TicketTriage.Frustration, 1.0)
        .FailsTimes(1, transient);

    TicketTriage triage = new(provider);

    (Department first, _) = await triage.TriageAsync(Ticket, CancellationToken.None);
    (Department second, bool escalate) = await triage.TriageAsync(Ticket, CancellationToken.None);

    Assert(first == Department.Support, "The first call fails and falls back.");
    Assert(second == Department.Billing, "The second call succeeds.");
    Assert(!escalate, "A frustration of 1.0 out of 10 should not escalate.");

    DecisionAssert.CalledTimes(provider.Calls, 2);

    Console.WriteLine("Failing then succeeding: ok");
}

static async Task RecordingARealProviderAsync()
{
    // A recorder wraps any provider, including a real one, so that an integration test can assert
    // what was asked without the fake deciding the answers.
    DeterministicDecisionProvider inner = new() { Seed = 42 };
    RecordingDecisionProvider provider = new(inner);

    _ = await new TicketTriage(provider).TriageAsync(Ticket, CancellationToken.None);
    _ = await new TicketTriage(provider).TriageAsync(Ticket, CancellationToken.None);

    DecisionAssert.CalledTimes(provider.Calls, 2);
    Assert(provider.Calls[0].Request.Questions.Count == 2, "Both questions travel in one request.");

    Console.WriteLine("Recording a real provider: ok");
}

static void Assert(bool condition, string because)
{
    if (!condition)
    {
        throw new DecisionAssertionException(because);
    }
}
