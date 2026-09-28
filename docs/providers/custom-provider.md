# Writing a provider

A provider is whatever implements `IDecisionProvider`. There is no base class to inherit, no
attribute to apply and no registry to join.

`samples/CustomProvider` is a complete, runnable implementation backed by an in-house rules
engine. This page is the contract behind it. The full normative version is
[the provider contract](../architecture/provider-contract.md).

## The interface

```csharp
public interface IDecisionProvider
{
    string Name { get; }

    DecisionProviderCapabilities Capabilities { get; }

    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
```

Three members, four questions to answer: what is it called, what can it do, how does it
answer, and how does it fail.

## 1. Name

```csharp
public string Name => "keyword";
```

It appears in `DecisionResult.Metadata.ProviderName` and in `DecisionError.ProviderName`. Pick
something short and stable; it will end up in logs and in dashboards.

## 2. Capabilities

Declare them. Never make the caller guess, and never let the caller find out by getting an
error.

```csharp
private static readonly DecisionProviderCapabilities s_capabilities = new()
{
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
```

Membership is by exact type, except that an **open generic declares every closed form of
itself** — `ChoiceQuestion<>` covers `ChoiceQuestion<Department>` and every other option type.
Anything not listed is something the provider refuses.

Callers check with `Capabilities.Supports(type)` and `Capabilities.SupportsAll(questionSet)`.

## 3. Answer

```csharp
public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    List<Answer> answers = new(request.Questions.Count);

    foreach (Question question in request.Questions)
    {
        cancellationToken.ThrowIfCancellationRequested();
        answers.Add(Answer(question));
    }

    DecisionMetadata metadata = new(Name, _timeProvider.GetUtcNow())
    {
        ClientRequestId = request.ClientRequestId,
        ModelVersion = "keyword/1",
    };

    return Task.FromResult(new DecisionResult(answers, metadata));
}
```

Rules that are not negotiable:

- **Check the token before doing work.** A caller that already gave up should pay nothing.
- **Take the clock from a `TimeProvider`.** Never call `DateTimeOffset.UtcNow`. That is what
  makes a provider testable with virtual time — see [ADR-0007](../adr/0007-time-abstraction.md).
- **Echo `ClientRequestId`** onto the metadata so the caller can correlate.
- **Do not answer a question you were not asked**, and do not invent an answer you did not
  compute.

Answering fewer questions than were asked is allowed: `DecisionResult` does not require one
answer per question, and the caller finds out with `TryGet`.

## 4. Build typed answers without knowing the type

This is the part that looks hard and is not. A provider does not know `TOption` at compile
time, and must not use `MakeGenericType` to find out. `IChoiceQuestion` is the seam:

```csharp
private Answer Answer(Question question, double weight) => question switch
{
    ProbabilityQuestion probability =>
        new ProbabilityAnswer(probability.Id, new Probability(weight)),

    ScoreQuestion score =>
        new ScoreAnswer(score.Id, score.CreateScore(score.Minimum + (weight * (score.Maximum - score.Minimum)))),

    IChoiceQuestion choice =>
        choice.CreateAnswer(choice.OptionValues[(int)Math.Round(weight * (choice.OptionValues.Count - 1))]),

    _ => throw Fail(DecisionErrorCategory.Validation, $"Cannot answer '{question.GetType().Name}'.", "unsupported_question_type"),
};
```

The question builds its own answer. The generic instantiation already exists because the caller
created the question, so nothing is constructed reflectively and the provider stays trim-safe
and AOT-safe. `CreateAnswer(ChoiceOutcome)` is the overload to use when you have a
distribution rather than a single selection.

Use `ScoreQuestion.CreateScore` rather than constructing a `Score` directly: it applies the
scale the question declared, so the number stays meaningful to whoever reads it.

## 5. Fail

Every failure is a `DecisionException` carrying a `DecisionError`. Build it through
`DecisionException.FromError`, which picks the right exception type from the category:

```csharp
private DecisionException Fail(DecisionErrorCategory category, string message, string code) =>
    DecisionException.FromError(new DecisionError(category, message)
    {
        Code = code,
        ProviderName = Name,
        Retry = DecisionRetryHint.NotRetryable,
    });
```

- **Set `Code`.** It is the stable identifier callers match on. `Message` is for humans.
- **Set `Retry`.** You know whether trying again can help; the caller does not.
- **Never report cancellation as a `DecisionError`.** Let `OperationCanceledException`
  propagate. `FromError` throws if you pass it a `Canceled` category, on purpose.
- **Refusing is a first-class outcome.** Saying "I cannot answer this" beats returning a
  plausible answer you did not compute.

See [error handling](../guides/error-handling.md) for the full category table.

## Keep provider concepts in the provider

If your service has a question shape the domain does not model, add the question type in
**your** package, deriving from `Question<TAnswer>`. `JevScoreQuestion` is the worked example:
it lives in `DecisionKit.Jev`, and it produces an ordinary `ScoreAnswer` so that nothing
downstream has to know it was provider-specific.

Never add a provider concept to `DecisionKit.Core`. That boundary is
[ADR-0001](../adr/0001-provider-independent-core.md), and a provider reference in `Core` is a
blocking defect. [ADR-0009](../adr/0009-provider-specific-question-types.md) covers the
provider-specific question pattern.

## Preserve what you do not understand

If your service returns something the domain has no type for, return an `UnknownAnswer`
carrying the provider's own type name, the raw payload and whatever you could pull out of it.
Do not drop it, and do not throw. Unknown is never discarded.

## Testing it

Your provider is testable without a network, because everything it depends on is injected:

```csharp
FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
KeywordDecisionProvider provider = new(weights, time);
```

Test at minimum: the happy path, cancellation before any work, cancellation mid-flight, an
unsupported question type, and each failure category you can produce. If you ship your provider
as a package, run it against the same expectations `DecisionKit.Testing` sets for the built-in
ones — see the [testing guide](../guides/testing.md).

## Registering it

Nothing in DecisionKit needs to know about your provider for it to work. In a host, register
it like any other service:

```csharp
builder.Services.AddSingleton<IDecisionProvider>(container =>
    new KeywordDecisionProvider(weights, container.GetRequiredService<TimeProvider>()));
```

`AddDecisionKit()` is only needed for the JEV registration helpers.
