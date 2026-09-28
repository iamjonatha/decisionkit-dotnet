# 0008. Keep the JEV wire format behind dedicated DTOs and source-generated JSON

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** @iamjonatha

## Context

`DecisionKit.Jev` translates the domain onto the JEV protocol. Two questions had to be answered
before a single mapper could be written.

**What serializes?** The cheapest thing to do is to decorate the domain types —
`ProbabilityAnswer`, `ChoiceQuestion<TOption>`, `DecisionResult` — with
`System.Text.Json` attributes and hand them to the serializer. It is also the decision that
is hardest to reverse: the wire format would become part of the public API of
`DecisionKit.Core`, a provider changing a property name would be a breaking change of the
domain, and a second provider with a different payload would have nowhere to put its own
shape. ADR-0001 keeps the core provider-independent, and a core that knows what JSON looks
like is not provider-independent.

**How does it serialize?** `src/Directory.Build.props` enables the trim and AOT analyzers
with `TreatWarningsAsErrors`, so every reflection-based `System.Text.Json` entry point is a
build error, not a runtime surprise. Serialization has to be resolved at compile time.

**Is the payload known?** Yes, and it was confirmed before a line of it was written. The
founding analysis names only the JEV boolean-like primitive — "Noul" — which is not enough
to build against. The protocol is documented publicly at <https://docs.typesafe.ai/api.md>,
and two independent SDKs implement it: the official Python SDK, whose request and response
schemas are declared as Pydantic models, and the community .NET SDK
[`saibimajdi/typesafeai-dotnet-sdk`](https://github.com/saibimajdi/typesafeai-dotnet-sdk),
cited by the founding analysis as a benchmark. All three agree. The payloads under
`tests/DecisionKit.Jev.Tests/Payloads` are recorded from the published reference examples,
and `docs/providers/jev-protocol.md` records the contract member by member with its
citations.

That still leaves the format outside our control. Whatever carries it has to be cheap to
re-record when the service moves.

## Decision

The JEV payload is modelled by dedicated data transfer objects in
`DecisionKit.Jev.Models`, and nothing else on either side of the boundary knows about JSON.

- Every wire type is a `sealed class` with `init` properties and an explicit
  `[JsonPropertyName]` on every member. Renaming a C# property cannot silently change the
  payload; only editing the attribute can.
- Wire types are flat, not polymorphic: a `type` discriminator selects which optional
  members carry meaning. A polymorphic hierarchy must reject a discriminator it has never
  seen, and a provider is expected to ship new question and answer types before DecisionKit
  models them.
- `JevAnswer` and `JevUsage` carry `[JsonExtensionData]`, so a property this version does
  not model is preserved rather than dropped.
- The three members whose shape the protocol leaves open — `state`, `instructions` and
  `criteria` — are typed as `JsonNode`, because each may be a string, an object or an array
  and narrowing them would reject valid payloads.
- `JevErrorResponse.Detail` is a `JsonNode` for the same reason: the service sends an object
  for an application error and a bare string for a framework one.
- Serialization goes through a source-generated `JsonSerializerContext`
  (`JevJsonContext`) and is exposed by the `JevJsonSerialization` facade, which only ever
  calls the `JsonTypeInfo<T>` overloads.
- Translation is done by explicit, hand-written mappers. No `AutoMapper`, no convention, no
  reflection over domain types.
- No domain type carries a `System.Text.Json` attribute. A contract test enforces this by
  scanning the `DecisionKit.Core` assembly, so the rule survives the next contributor.

`System.Text.Json` ships in `net8.0` and `net10.0`, so `DecisionKit.Jev` takes **no** package
reference for any of this and keeps a single project reference to `DecisionKit.Core`.
ADR-0003 requires a record for a dependency addition; there is no dependency to add.

## Consequences

### Positive

- The wire format can change without touching `DecisionKit.Core`, and the core can evolve
  without renegotiating the payload.
- The whole JEV contract is readable in one directory instead of being inferred from
  attributes scattered across the domain.
- Source-generated metadata keeps the package trim- and AOT-clean with the analyzers on and
  warnings as errors.
- A second provider is unblocked: it brings its own DTOs and its own mappers, and shares the
  domain.
- The mappers are the single place where a wire-level surprise is handled, which is what
  makes "an unrecognized answer does not fail the response" implementable at all.

### Negative

- The payload is written twice: once as a DTO and once as a domain type, with a mapper in
  between. That is roughly a thousand lines that a decorated domain would not need.
- Every new question or answer type has to be added in three places — DTO, mapper, test —
  and forgetting the mapper is a silent no-op rather than a compile error. The contract
  tests exist to make it loud.
- Source generation means the context has to list every serializable type explicitly. A type
  that is not listed fails at build time, which is the intended failure, but it is one more
  step.

### Neutral

- The DTOs are internal in spirit but public in fact, because a caller that captures a raw
  payload for diagnostics needs to be able to read it back.

## Alternatives considered

**Serialize the domain types directly.** Rejected. It publishes the wire format as part of
the core's public API, makes a provider's rename a breaking change of the domain, and leaves
a second provider with nowhere to put a different shape. It is the cheapest decision now and
the most expensive one to reverse later.

**A polymorphic DTO hierarchy with `[JsonDerivedType]`.** Rejected. It fails on the first
type name the provider adds, which is exactly the case the mapper must survive: an unrecognized
answer has to arrive as data, not as an exception.

**Reflection-based `System.Text.Json` with a naming policy.** Rejected twice over: the AOT
and trim analyzers make it a build error, and a naming policy turns a C# rename into a
silent payload change.

**A mapping library.** Rejected. It adds a dependency ADR-0003 would not allow, it moves
mapping errors from compile time to run time, and the mapping here is not mechanical — it is
where unknown types, out-of-range values and retry hints are decided.
