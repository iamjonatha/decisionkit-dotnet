# Testing

`DecisionKit.Testing` lets you test the code that *uses* a decision provider without calling
one. It contains test doubles, request capture, error injection and assertion helpers. It
makes no HTTP call, reads no configuration and never touches the network.

It is a normal package, meant to be referenced from test projects:

```xml
<PackageReference Include="DecisionKit.Testing" Version="0.1.0" />
```

It depends on `DecisionKit.Core` and on nothing else.

## The code under test

Everything below tests this class, which is the shape application code normally takes: it
owns its questions, builds a request, and turns the answers into a decision of its own.

```csharp
public sealed class TicketRouter
{
    private readonly IDecisionProvider _provider;

    public TicketRouter(IDecisionProvider provider) => _provider = provider;

    public static ChoiceQuestion<Department> RoutingQuestion { get; } = new(
        new QuestionId("routing"),
        "Which department should handle this ticket?",
        [Department.Billing, Department.Technical, Department.Sales]);

    public static ScoreQuestion UrgencyQuestion { get; } = new(
        new QuestionId("urgency"), "How urgent is this ticket?", 0, 10);

    public async Task<TicketRouting> RouteAsync(string ticket, CancellationToken cancellationToken)
    {
        DecisionRequest request = new(QuestionSet.Create(RoutingQuestion, UrgencyQuestion))
        {
            Input = DecisionInput.FromText(ticket),
        };

        DecisionResult result = await _provider.DecideAsync(request, cancellationToken);

        return new TicketRouting(
            result.Get(RoutingQuestion).Value.Selection,
            result.Get(UrgencyQuestion).Value.Value >= 7);
    }
}
```

Note what it does *not* do: it does not name a provider, does not read a key and does not
know whether the answer came from a model or from a hash. That is what makes it testable.

## FakeDecisionProvider

The fake answers with whatever you told it to answer. You teach it one question at a time,
and the configuration is fluent.

```csharp
FakeDecisionProvider provider = new FakeDecisionProvider()
    .Returns(TicketRouter.RoutingQuestion, Department.Billing)
    .Returns(TicketRouter.UrgencyQuestion, 8.0);

TicketRouting routing = await new TicketRouter(provider).RouteAsync(
    "I was charged twice this month.", cancellationToken);

Assert.Equal(Department.Billing, routing.Department);
Assert.True(routing.IsUrgent);
```

`Returns` has an overload per question type, so the value you pass is checked at compile
time: a `double` for a probability or a score, an option for a choice, a `Probability`,
`Score` or `Choice<TOption>` when you want to build the value yourself.

```csharp
provider
    .Returns(frustration, new Probability(0.84))
    .Returns(urgency, urgency.CreateScore(7))
    .Returns(routing, Choice.Distributed<Department>(
    [
        new OptionProbability<Department>(Department.Billing, new Probability(0.7)),
        new OptionProbability<Department>(Department.Technical, new Probability(0.3)),
    ]));
```

### An unconfigured question is an error

If the code under test asks something the fake was not taught, the call fails with a
`DecisionValidationException` carrying the code `fake_answer_not_configured`. This is
deliberate: a silently missing answer turns into a `KeyNotFoundException` three frames away,
or worse, into a test that passes for the wrong reason.

When the omission is the point of the test — a provider that skips a question it cannot
answer — say so:

```csharp
provider.ReturnsNothing(TicketRouter.UrgencyQuestion);
```

### Capabilities are derived

`Capabilities` reports exactly the question types the fake has been taught, and declares
every optional feature supported. Code that checks capabilities before calling therefore
works against the fake without extra setup:

```csharp
Assert.True(provider.Capabilities.SupportsAll(request.Questions));
```

Assign `Capabilities` yourself when the test is *about* a capability being missing.

## Asserting the request

The fake records every call. This is how you test the half of your code that builds the
request, which is usually the half that breaks.

```csharp
RecordedDecisionCall call = DecisionAssert.CalledOnce(provider.Calls);

DecisionAssert.AskedExactly(call.Request, "routing", "urgency");
Assert.Equal("I was charged twice this month.", call.Request.Input.Text);
```

`Calls` is a `DecisionCallLog`: an ordered, thread-safe list of `RecordedDecisionCall`, each
with its `Ordinal`, `Request` and `Timestamp`. Use `Only()` when exactly one call is
expected and `Last` for the most recent one.

To capture calls made to a *different* provider — the deterministic one, or a real one in an
integration test — wrap it:

```csharp
RecordingDecisionProvider provider = new(inner);
```

## Injecting failures

Every error path in your code deserves a test, and the fake is how you reach it without
waiting for a provider to rate-limit you.

```csharp
provider.Fails(DecisionErrorCategory.RateLimit, "Too many requests.");

await Assert.ThrowsAsync<DecisionTransientException>(
    () => router.RouteAsync("...", cancellationToken));
```

Pass a full `DecisionError` when the test asserts on the diagnostics — the provider error
code, the retry hint, the documentation link.

For retry tests, fail a fixed number of times and then succeed:

```csharp
provider.FailsTimes(2, error);   // call 3 succeeds
```

`Succeeds()` clears the injected failure. `Reset()` clears answers, omissions, capabilities,
failures and the call log, keeping the name, the clock and the latency.

Cancellation is not injectable, by design: a cancelled call throws
`OperationCanceledException` and must come from the token, as ADR-0006 requires. Configuring
a `Canceled` error throws at configuration time.

## Simulating latency

`Latency` delays every call. To keep the test fast, delay it on a clock you control.
`TimeProvider` is the abstraction (see [ADR-0007](../adr/0007-time-abstraction.md)), and the
controllable implementation lives in a Microsoft package you add to your own test project:

```xml
<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.10.0" />
```

```csharp
FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);

FakeDecisionProvider provider = new FakeDecisionProvider
{
    TimeProvider = clock,
    Latency = TimeSpan.FromSeconds(30),
}.Returns(TicketRouter.UrgencyQuestion, 5.0);

Task<DecisionResult> pending = provider.DecideAsync(request, cancellationToken);

Assert.False(pending.IsCompleted);
clock.Advance(TimeSpan.FromSeconds(30));

await pending;
```

Thirty simulated seconds, no real waiting. The same clock timestamps the recorded calls and
the result metadata.

## DeterministicDecisionProvider

The fake requires a test to state every answer, which is right for a unit test and wrong for
a snapshot test, a sample application, a demo or a load test. There, what matters is that
answers are plausible, varied and stable.

```csharp
DeterministicDecisionProvider provider = new();

TicketRouting first = await new TicketRouter(provider).RouteAsync(ticket, cancellationToken);
TicketRouting second = await new TicketRouter(provider).RouteAsync(ticket, cancellationToken);

Assert.Equal(first, second);
```

Answers are derived from the question identifier, the input text and `Seed` through a fixed
hash — never from a random number generator, never from the wall clock. The same request
produces the same answers on every machine and in every run. Change `Seed` to get a
different, equally stable set of answers.

The timestamp is fixed at `DateTimeOffset.UnixEpoch` by default, so a recorded snapshot
matches twice.

Answers are plausible, not correct. This provider is not a model: do not assert that a
billing complaint routes to billing.

## Assertion helpers

`DecisionAssert` throws `DecisionAssertionException` with a message naming the question, the
expected value and what was actually there. It depends on no test framework, so it works
with xUnit, NUnit, MSTest or TUnit.

| Helper | Checks |
| --- | --- |
| `Answered<TAnswer>(result, question)` | The question was answered; returns the typed answer |
| `NotAnswered(result, question)` | The question was deliberately skipped |
| `HasProbability(result, question, expected[, tolerance])` | A probability, compared with a tolerance |
| `HasScore(result, question, expected[, tolerance])` | A score, compared with a tolerance |
| `HasSelection(result, question, expected)` | A choice that selected a specific option |
| `HasAnswerCount(result, expected)` | The number of answers in the result |
| `Asked(request, questionId)` | The request contains the question; returns it |
| `AskedExactly(request, ids...)` | The request contains exactly these questions, in order |
| `CalledOnce(calls)` | Exactly one call was made; returns it |
| `CalledTimes(calls, expected)` | The number of calls |

These are a convenience, not a requirement. Every one of them is a few lines over the public
API, and your own framework's assertions are fine too.

## Choosing a double

| Situation | Use |
| --- | --- |
| A unit test of application logic | `FakeDecisionProvider` |
| A test of an error path, a timeout or a retry | `FakeDecisionProvider` with `Fails`, `FailsTimes` and `Latency` |
| A sample, a demo, a snapshot or a load test | `DeterministicDecisionProvider` |
| Capturing what a real provider was asked | `RecordingDecisionProvider` around it |
| Verifying a provider implementation | Write a real integration test; none of these are a substitute |

## Testing a provider you wrote

The doubles here test *callers*. If you are implementing `IDecisionProvider`, test it
against the contract in [the provider contract](../architecture/provider-contract.md):
argument validation, cancellation, capability honesty, error mapping and answer typing. A
fake cannot tell you whether your HTTP mapping is right.
