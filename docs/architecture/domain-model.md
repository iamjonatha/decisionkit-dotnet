# Domain model

The types that model a decision. They live in `DecisionKit.Core`, have no package references,
and know nothing about HTTP, JSON or any provider.

Read [`overview.md`](overview.md) for the layering rules and
[ADR-0005](../adr/0005-answer-type-inferred-from-question.md) for why results are read the way
they are.

## 1. Map

```text
QuestionSet ──▶ Question ──▶ Question<TAnswer>
                                  │
                                  ├── ProbabilityQuestion   ──▶ ProbabilityAnswer   ──▶ Probability
                                  ├── ScoreQuestion         ──▶ ScoreAnswer         ──▶ Score
                                  ├── ChoiceQuestion<T>     ──▶ ChoiceAnswer<T>     ──▶ Choice<T>
                                  └── UnknownQuestion       ──▶ UnknownAnswer       ──▶ raw payload

DecisionResult ──▶ Answer, DecisionMetadata, DecisionUsage
```

## 2. Identity

| Type | Purpose |
| --- | --- |
| `QuestionId` | Correlates a question with its answer |
| `RequestId` | Correlates a call across application logs, provider logs and traces |
| `IdempotencyKey` | Marks a call as safely repeatable |

All three are validated value objects, not bare strings: non-empty, trimmed, ordinal
equality, `TryCreate` for the non-throwing path, and an `IsEmpty` flag so that a `default`
instance is detectable instead of silently behaving like an empty string.

`RequestId.New()` and `IdempotencyKey.New()` generate values; `QuestionId` has no generator,
because a question identifier is chosen by the author of the question.

## 3. Values

### `Probability`

A `double` constrained to `[0.0, 1.0]`. `NaN` and the infinities are rejected at
construction. `default` is `0.0`, which is a valid probability, so there is no uninitialized
trap. Comparable, equatable, formattable with an explicit culture.

### `Score`

A value plus the scale it was measured on. A bare `7` is meaningless; `Score(7, 0, 10)` is
not. `Normalize()` projects onto `[0.0, 1.0]` and is an explicit call, never an implicit
assumption. A `default` score has no scale, reports `HasScale == false`, and throws on
`Normalize()`.

### `Choice<TOption>` and `OptionProbability<TOption>`

A choice carries a selected option, a distribution over options, or both.

```csharp
Choice.Selected(Department.Billing);
Choice.Distributed(distribution);
Choice.Distributed(distribution, Department.Billing);
```

A distribution is **never** collapsed into a selection automatically. `TryGetMostLikely`
exists so that collapsing is a visible act at the call site. Probabilities are reported as
received and are not renormalized: a distribution that does not sum to one is diagnostic
information about the provider, not noise to hide.

The factory methods sit on the non-generic `Choice` class so that `TOption` is inferred.

## 4. Questions

`Question` is the non-generic base, so a `QuestionSet` can hold questions with different
answer types. `Question<TAnswer>` adds the answer type. Every question has an identifier, a
prompt and an immutable metadata bag.

| Question | Answer | Notes |
| --- | --- | --- |
| `ProbabilityQuestion` | `ProbabilityAnswer` | The provider-neutral shape of the question type some protocols expose under a vendor name |
| `ScoreQuestion` | `ScoreAnswer` | Carries `Minimum` and `Maximum`; `CreateScore(value)` builds a score on that scale |
| `ChoiceQuestion<TOption>` | `ChoiceAnswer<TOption>` | Options are unique, non-empty and ordered; use `string` options for runtime-driven configuration |
| `UnknownQuestion` | `UnknownAnswer` | Carries a provider type name and an opaque definition |

### Reading a choice question without knowing its option type

`ChoiceQuestion<TOption>` is generic, and C# has no `case ChoiceQuestion<?>`. Code that
walks a `QuestionSet` — a provider mapping questions to a wire format, a test double
deriving an answer — holds a `Question` base reference and cannot open the generic without
reflection.

`IChoiceQuestion` is the non-generic view that closes that gap:

```csharp
case IChoiceQuestion choice:
    object option = choice.OptionValues[index];   // the options, as objects
    return choice.CreateAnswer(option);           // a ChoiceAnswer<TOption>, correctly typed
```

It exposes `Id`, `OptionType`, `OptionValues` and `CreateAnswer(object)`, which validates
the runtime type and membership before building the answer. `ChoiceQuestion<TOption>` also
exposes a typed `CreateAnswer(TOption)` for callers that do know the option type.

The interface is deliberately read-only and small: it is the seam a provider needs, not a
second way to model a question.

### `QuestionSet`

Ordered storage plus an indexed lookup by `QuestionId`. A duplicate identifier is rejected at
construction with an `ArgumentException` naming the identifier. `last wins` is never
acceptable, because it makes a result impossible to correlate.

## 5. Answers

`Answer` carries the `QuestionId` and a metadata bag. `Answer<TValue>` adds the typed value
and reports `ValueType`.

`UnknownAnswer` is the forward-compatibility path: it keeps the provider type name, the raw
payload when the provider is configured to preserve it, and every property that could not be
mapped. One unrecognized answer must never fail a whole response.

## 6. Results

`DecisionResult` keeps the answers in provider order, indexes them by identifier, and rejects
two answers for the same question.

```csharp
ScoreAnswer score = result.Get(frustrationQuestion);   // typed, inferred
Answer any = result.Get("frustration");                // dynamic escape hatch
```

`DecisionMetadata` records the provider name, the timestamp, an optional model or protocol
version, an optional latency, and three correlation identifiers that are deliberately kept
apart: the client request identifier, the provider request identifier and the trace
identifier. Provider-specific data that has no neutral counterpart goes in `Properties`.

`DecisionUsage` is a set of named metrics rather than fixed token and cost properties,
because tokens, credits, request units and money are provider concepts and no provider has
all of them. `DecisionUsageKeys` documents the names to use when the concept applies.

## 7. Rules that hold across the model

- Value objects are immutable, equatable and free of hidden state.
- Metadata bags are copied on assignment, so a caller cannot mutate a question, an answer or
  a result after construction.
- Validation happens in the constructor. An instance that exists is valid.
- The domain types throw `ArgumentException`, `ArgumentOutOfRangeException`,
  `ArgumentNullException`, `InvalidOperationException` and `KeyNotFoundException`. The
  provider-facing error contracts described in
  [the provider contract](provider-contract.md) do not replace these: an argument exception
  signals a programming error, not a provider failure.
