# 0005. Infer the answer type from the question

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

A decision result holds answers of different shapes: a probability, a score, a choice over a
typed option set, and whatever the provider invents next. The application has to read them
back.

The obvious API is a string lookup with a caller-supplied type argument:

```csharp
var score = result.Get<ScoreAnswer>("frustration");
```

It compiles for every combination of identifier and type, including the wrong ones. The
identifier is unchecked, the type argument is unchecked against the identifier, and both
failures surface at run time, in production, on the unhappy path.

The founding analysis asked for the opposite: the question should determine the type of its
answer, and the string lookup should exist only as a documented escape hatch for callers
whose questions come from configuration or from a database.

## Decision

The answer type is part of the question type.

- `Question<TAnswer>` carries the answer type. `ScoreQuestion` is a
  `Question<ScoreAnswer>`, `ChoiceQuestion<TOption>` is a `Question<ChoiceAnswer<TOption>>`.
- `DecisionResult.Get(question)` infers `TAnswer` from the question. There is no type
  argument to get wrong, and no identifier to mistype.
- `DecisionResult.Get(string)` and `Get(QuestionId)` return an untyped `Answer`. They are
  the documented dynamic path, not the default one.
- `DecisionResult` exposes **no** indexer taking a question. A C# indexer cannot be generic,
  so `result[question]` would have to return an untyped `Answer` and would silently throw
  away the type information the question carries. `Get(question)` is the only ergonomic
  typed path, and the ergonomic gap against an indexer is four characters.
- Generic nesting stops at one level in application code. `ChoiceQuestion<Department>`
  reads well; deeper nesting would not.

Three failure modes are distinguished when reading a result:

| Situation | Behaviour |
| --- | --- |
| No answer for the question | `KeyNotFoundException` |
| An answer of a different shape | `InvalidOperationException` naming both types |
| Either of the above, without throwing | `TryGet` returns `false` |

Returning `false` from `TryGet` for a shape mismatch is deliberate: the caller asked whether
a usable answer is present, and an answer of the wrong shape is not usable.

## Consequences

### Positive

- The compiler rejects reading a score question as a choice answer.
- Renaming a question identifier is a single-site change, because call sites reference the
  question object instead of repeating the string.
- The dynamic path still exists, and is visible in review precisely because it looks
  different from the typed path.

### Negative

- Questions must be held somewhere the reading code can reach, usually as static fields or
  as members of a request definition. That is a real constraint on application structure.
- The exception type differs between "missing" and "wrong shape". Callers who do not care
  must catch two types or use `TryGet`.

### Neutral

- `Answer` remains a class hierarchy rather than a discriminated union. C# has no unions
  today; when it does, the hierarchy is the thing that would be revisited.

## Alternatives considered

### `Get<TAnswer>(string id)`

Rejected. It is the pattern the analysis explicitly argued against: two unchecked inputs,
both failing at run time.

### A non-generic indexer, `result[question]`

Rejected. It returns `Answer`, so every call site needs a cast, and a cast is exactly the
mistake the design exists to prevent.

### One `Answer` type with a discriminator enum and nullable value members

Rejected. Every consumer would have to check the discriminator by hand, nothing would stop
it from reading the wrong member, and adding a question type would mean widening a type that
every provider already depends on.
