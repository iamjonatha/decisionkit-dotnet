# Type safety

The answer knows its own type because the question decided it. This page explains the
mechanism, what it prevents, and where the type system deliberately stops.

The decision behind it is recorded in
[ADR-0005](../adr/0005-answer-type-inferred-from-question.md).

## The mechanism

Every question derives from `Question<TAnswer>`, where `TAnswer` is the answer it produces:

```csharp
public abstract class Question<TAnswer> : Question where TAnswer : Answer
```

`DecisionResult.Get` is generic over the same parameter:

```csharp
public TAnswer Get<TAnswer>(Question<TAnswer> question) where TAnswer : Answer
```

So the question instance carries the type through the call:

```csharp
ChoiceAnswer<Department> answer = result.Get(TicketQuestions.Routing);
Department team = answer.Value.Selection;
```

`ChoiceQuestion<Department>` derives from `Question<ChoiceAnswer<Department>>`, the compiler
infers `TAnswer`, and `Value` is a `Choice<Department>`. There is no cast anywhere, and there
is nothing to keep in sync: changing the question's option type is a compile error at every
call site that reads it.

## What this prevents

```csharp
// Does not compile: the answer is ChoiceAnswer<Department>, not ScoreAnswer.
ScoreAnswer wrong = result.Get(TicketQuestions.Routing);

// Does not compile: Selection is a Department.
string team = result.Get(TicketQuestions.Routing).Value.Selection;

// Does not compile: Department.Finance is not an option, and does not exist.
Department team = Department.Finance;
```

The failure mode this replaces is the one every stringly typed client has: a dictionary of
`object`, a cast that is right today, and a rename that silently breaks it.

## The three value types

| Value | Shape | Notes |
| --- | --- | --- |
| `Choice<TOption>` | `Selection`, `Distribution` | A provider may return a selection, a distribution, or both |
| `Score` | `Value`, `Minimum`, `Maximum` | `Normalize()` maps it to 0..1; `HasScale` says whether a scale is known |
| `Probability` | `Value` | Always 0..1, validated on construction |

`Choice<TOption>` is the only one where a provider can legitimately give you less than you
asked for, so it asks you to say what you want:

```csharp
Choice<Department> choice = result.Get(TicketQuestions.Routing).Value;

if (choice.TryGetSelection(out Department team))
{
    // The provider committed to one option.
}

if (choice.TryGetProbability(Department.Billing, out Probability billing))
{
    // The provider gave a distribution and Billing is in it.
}

if (choice.TryGetMostLikely(out OptionProbability<Department> best))
{
    // Highest-probability option, when a distribution exists.
}
```

`Selection` throws when there is no selection. `HasSelection` and `HasDistribution` let you
check first; the `TryGet` methods do both in one step.

## Identifiers are types too

`QuestionId` and `RequestId` are structs, not strings. They validate on construction, so an
empty or malformed identifier fails where it is created rather than where it is used.

```csharp
QuestionId id = new("routing");                  // throws on an invalid value
RequestId.TryCreate(candidate, out RequestId r); // for untrusted input
```

`QuestionSet` rejects duplicate identifiers, so two questions cannot silently collide into one
answer.

## Where the type system stops

Three seams are deliberately untyped, because the alternative is worse.

**Metadata and input properties.** `IReadOnlyDictionary<string, object?>` on
`DecisionInput`, `Question`, `Answer`, `DecisionMetadata` and `DecisionError`. This is
provider-shaped data; typing it would mean `Core` knowing a provider.

**Unknown question and answer types.** When a provider ships a feature this library does not
model, `UnknownQuestion` and `UnknownAnswer` carry the provider's own type name and the raw
payload:

```csharp
UnknownAnswer answer = result.Get(sentiment);

string? payload = answer.RawPayload;
object? intensity = answer.UnknownProperties["intensity"];
```

You lose compile-time checking on that answer and nothing else. The alternative — refusing the
answer until a release catches up — loses the answer entirely. Unknown is never discarded.

**The non-generic mapping seam.** A provider maps answers without knowing `TOption` at compile
time. `IChoiceQuestion` exposes what it needs:

```csharp
public interface IChoiceQuestion
{
    QuestionId Id { get; }
    Type OptionType { get; }
    IReadOnlyList<object> OptionValues { get; }

    Answer CreateAnswer(object selection);
    Answer CreateAnswer(ChoiceOutcome outcome);
}
```

The question builds its own typed answer, so the provider never calls `MakeGenericType` or
`Activator.CreateInstance`. That is what keeps the whole library trim-safe and AOT-safe — see
[AOT and trimming](aot-and-trimming.md).

## Reading by identifier

The untyped overloads exist for code that genuinely does not know the question at compile
time — a generic logger, an audit trail, a dynamic rule engine:

```csharp
Answer answer = result.Get("routing");
Type? valueType = answer.ValueType;

foreach (Answer each in result.Answers)
{
    logger.LogInformation("{Question} = {Value}", each.QuestionId, each);
}
```

Prefer the typed overload everywhere else. If you find yourself casting the result of
`Get(string)`, the typed question you need is already in scope.
