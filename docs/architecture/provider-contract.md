# Provider contract

Everything a provider must implement, and everything an application may rely on, lives in
`DecisionKit.Core`. This page is the reference for that contract. The questions, answers and
results it carries are described in [the domain model](domain-model.md).

## 1. Map

```text
DecisionRequest ──> IDecisionProvider ──> DecisionResult
      │                    │                    │
      │                    └── Capabilities     └── Answer, Metadata, Usage
      │
      ├── QuestionSet        what is being asked
      ├── DecisionInput      what it is about
      ├── DecisionOptions    how it should be answered
      ├── RequestId          correlation
      ├── IdempotencyKey     repeatability
      └── Metadata           provider-specific extras

                       failure
                          │
                          v
                    DecisionException ── DecisionError ── DecisionRetryHint
```

## 2. IDecisionProvider

```csharp
public interface IDecisionProvider
{
    string Name { get; }
    DecisionProviderCapabilities Capabilities { get; }
    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
```

The interface is small on purpose. It carries a name for diagnostics, a capability
declaration, and one operation. Endpoints, credentials, retry budgets and serialization are
construction details of an implementation and never appear here.

Three properties hold:

- **Nothing transport-shaped crosses it.** No `HttpRequestMessage`, no URL, no header. The
  same request must be answerable by a second provider, by a fake, and by a decorator that
  makes no network call.
- **Cancellation is part of the contract.** The token outranks any retry policy, and a
  cancelled call throws `OperationCanceledException` without a further attempt.
- **Implementations are safe for concurrent use**, because they are normally registered as
  singletons. Per-request state belongs to the request.

Because the interface is this small, cross-cutting behaviour is added by wrapping it —
logging, metrics, retry, caching — and never by editing a provider.

## 3. DecisionRequest

| Member | Meaning |
| --- | --- |
| `Questions` | The `QuestionSet` to evaluate, in order. At least one. |
| `Input` | What the questions are asked about. Defaults to `DecisionInput.Empty`. |
| `Options` | Provider-neutral semantics. Defaults to `DecisionOptions.Default`. |
| `ClientRequestId` | The caller's correlation identifier, echoed into `DecisionMetadata`. |
| `IdempotencyKey` | Marks the request as safely repeatable. |
| `Metadata` | Provider-specific extras the domain does not model. Copied on assignment. |

A request must ask at least one question. An empty `QuestionSet` is a legitimate
intermediate value while questions are assembled, but sending it asks nothing, so
`DecisionRequest` rejects it. This is the boundary the domain model deliberately left to the
request type.

`ClientRequestId` and `IdempotencyKey` are nullable, and an uninitialized value is rejected
rather than silently treated as absent: `default(RequestId)` is a bug, `null` is a choice.

## 4. DecisionInput

Providers disagree about what they evaluate. Some take text, some take structured state,
some take both. `DecisionInput` holds that disagreement in one place instead of spreading it
across every question type.

```csharp
DecisionInput.Empty
DecisionInput.FromText("The customer asked for a refund twice.")
DecisionInput.FromProperties(new Dictionary<string, object?> { ["tier"] = "gold" })
DecisionInput.Create(text, properties)
```

An input is optional: a self-contained question needs none.

## 5. Options, and what does not belong in them

`DecisionOptions` carries semantics only:

| Option | Meaning |
| --- | --- |
| `Language` | The BCP 47 tag the provider should answer in. |
| `IncludeExplanations` | Whether the provider is asked to justify its answers. |
| `UnknownTypeHandling` | `Preserve` (default) or `Fail` for types the provider does not recognize. |

The separation the architecture requires is three-way:

| Kind | Lives in | Example |
| --- | --- | --- |
| Domain options | `DecisionKit.Core` | Answer language |
| Transport options | The provider package | Timeout, endpoint, headers |
| Provider options | The provider package | A JEV-specific switch |

The test is one sentence: if the setting would be meaningless for a second provider, it does
not belong in `DecisionOptions`. This is what keeps `Timeout = 30 seconds` from becoming
part of the decision domain.

## 6. Capabilities

`DecisionProviderCapabilities` lets a caller ask instead of guess:

- `SupportedQuestionTypes`, with `Supports<TQuestion>()`, `Supports(Type)` and
  `SupportsAll(QuestionSet)`
- `SupportsIdempotencyKeys`
- `SupportsUsageReporting`
- `SupportsRequestMetadata`
- `SupportsBatching`
- `SupportsExplanations`

Question-type membership is by **exact type**. Declaring a base type does not declare every
type derived from it, because a derived type carries meaning the provider has not been
taught.

A generic question type is the exception, and it has to be. `ChoiceQuestion<TOption>` is
closed by the *caller*, over an option type the provider has never seen, so a provider
cannot enumerate `ChoiceQuestion<Department>`, `ChoiceQuestion<string>` and every other form
an application might use. A provider therefore declares the **generic type definition**:

```csharp
SupportedQuestionTypes = [typeof(ProbabilityQuestion), typeof(ChoiceQuestion<>)]
```

`typeof(ChoiceQuestion<>)` declares every closed form of it. A closed form still means only
itself: declaring `ChoiceQuestion<Department>` does not declare `ChoiceQuestion<string>`.

Without this, a caller learns that a provider drops idempotency keys by noticing that
nothing changed — which is indistinguishable from a bug.

## 7. Failure

The full rationale is [ADR-0006](../adr/0006-error-model.md). The contract is:

```text
DecisionException          every decision failure
    ├── DecisionValidationException       change the request
    ├── DecisionAuthenticationException   fix the credentials
    ├── DecisionTransientException        wait, maybe try again
    └── DecisionProviderException         log, alert, escalate
```

Each one exposes `Error`, a `DecisionError` carrying the finer
`DecisionErrorCategory`, the provider's error code, the provider request identifier, the
documentation link, a `DecisionRetryHint` and a property bag. Diagnostics are data, not
prose inside a message.

`DecisionException.FromError` is the single place that maps a category onto the hierarchy,
so no provider hard-codes the mapping.

Two things stay outside it:

- A cancelled call throws `OperationCanceledException`.
- A malformed call — a null question set, a duplicate identifier — keeps throwing
  `ArgumentException` and its relatives. That is a bug to fix, not a failure to handle.

## 8. Rules that hold across the contract

1. A request is provider-neutral and transport-free.
2. A request asks at least one question.
3. Cancellation is contractual and outranks every retry policy.
4. Retryability is stated by the provider in a `DecisionRetryHint`, never inferred by the
   caller from a status code.
5. Provider diagnostics are preserved as structured data.
6. Capabilities are declared, not discovered by trial.
7. Unrecognized types are preserved by default; failing on them is an explicit choice.
