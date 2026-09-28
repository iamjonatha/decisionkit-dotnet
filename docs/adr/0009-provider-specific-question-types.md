# 0009. Keep provider-specific question types in the provider package

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** @iamjonatha

## Context

JEV does not score against a numeric range. A `score` question is answered against an
ordered rubric of two to ten levels, each described in words by the caller, and the rubric is
mandatory: the service rejects a score question that does not carry one. The answer is a
probability-weighted position across those levels, so a value of `1.42` means "between the
second and the third level, closer to the third".

`DecisionKit.Core` models a score question as a prompt and a numeric range — `ScoreQuestion`
with a `Minimum` and a `Maximum`. That is the right provider-neutral shape, and it is not
convertible to a rubric: there is no honest way to invent three sentences from `1..5`.

So a question type the domain does not have had to exist somewhere. Three places were
possible: add the rubric to `ScoreQuestion` in the core, add a new `RubricQuestion` to the
core, or define the type in `DecisionKit.Jev`.

A related constraint made this a real decision rather than a formality: the concrete question
types in the core are `sealed`, deliberately, so that a question type is a closed set the
mappers can exhaust. `Question<TAnswer>`, however, is `abstract` with a `protected`
constructor, so another assembly can define its own question type without the core opening
up.

## Decision

A question type that only one provider can express is defined in that provider's package.

`JevScoreQuestion` lives in `DecisionKit.Jev.Questions`, derives from
`Question<ScoreAnswer>`, and carries the rubric as an `IReadOnlyList<string>` validated at
construction. Its `Minimum` is `0` and its `Maximum` is `Levels.Count - 1`, so the answer is
an ordinary `ScoreAnswer` and application code reads it exactly as it reads any other score.

A plain `ScoreQuestion` sent to JEV is **rejected**, with `jev_question_unsupported` and a
message naming `JevScoreQuestion`. It is not silently converted into a rubric, and it is not
sent without one.

`DecisionProviderCapabilities.Supports` matches an exact type or a generic type definition,
so a JEV provider reports `Supports<JevScoreQuestion>() == true` and
`Supports<ScoreQuestion>() == false`. A caller can therefore find out before sending.

The same rule covers the other direction. JEV reports a confidence, a rubric legend and a
per-level distribution that the domain does not model; those travel as answer metadata under
keys declared by `JevProtocol` and are read back through `JevAnswers`, rather than being
added to `Answer`.

## Consequences

### Positive

- The provider-neutral domain stays provider-neutral. One service's vocabulary — rubrics,
  nouls, confidence — does not reach the types every other provider has to implement.
- The rejection is loud and actionable. A caller is told which type to use instead, at
  mapping time, rather than discovering at the service that a payload was invalid.
- The rubric is validated once, in the constructor, so an invalid question cannot be built
  and then fail later.
- Nothing in the core had to be unsealed or widened.

### Negative

- Code that targets JEV is not entirely provider-agnostic: swapping providers means
  revisiting every `JevScoreQuestion`. This is honest rather than accidental — the question
  genuinely cannot be asked of a provider that has no rubric — but it is a real coupling.
- A second provider with its own rubric-like concept will define its own type, and the two
  will not be interchangeable. If that happens twice, it is evidence that the domain should
  grow a neutral abstraction, and this record should be superseded rather than worked
  around.

### Neutral

- `ScoreQuestion` remains the right type for any provider that does score against a range.
  It is not deprecated and is not JEV's concern.

## Alternatives considered

**Add the rubric to `ScoreQuestion`.** Rejected. It puts an optional, JEV-shaped member on a
type every provider sees, and leaves every other provider to decide what to do with a rubric
it cannot honour — which is exactly the ambiguity this record exists to avoid.

**Add a neutral `RubricQuestion` to the core.** Rejected for now, on evidence. One provider
is not a pattern. Inventing the neutral abstraction before a second provider exists means
guessing at what it has in common with the first, and the guess is expensive to correct once
it is public.

**Carry the rubric in `Question.Metadata`.** Rejected. Metadata is an untyped annotation bag;
a mandatory, validated, ordered list of two to ten descriptions is not an annotation. It
would move a construction-time error to mapping time and give the caller no compiler help.

**Send a `ScoreQuestion` with a generated rubric.** Rejected outright. Fabricating level
descriptions from a numeric range changes what the model is asked to judge, silently, and the
result would be unattributable to anything the caller wrote.
