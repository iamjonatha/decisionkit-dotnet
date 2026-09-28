# Architecture overview

This document describes the intended shape of DecisionKit. It is normative: a change that
contradicts it needs an ADR in [`docs/adr/`](../adr/), not just a pull request.

## 1. The problem

A provider SDK usually fuses five concerns into one surface: the vendor protocol, HTTP
transport, serialization, resilience policy, and the domain model the application actually
thinks in. The application then models its decisions in the vendor's shape, and replacing
the vendor means rewriting the domain.

DecisionKit separates them so that the decision domain is the stable part and the provider
is the replaceable part.

## 2. Layers

```text
Application
    |
    v
DecisionKit.Core            domain + IDecisionProvider abstraction
    ^         ^        ^
    |         |        |
  Jev    Extensions  Testing
```

The dependency arrow only ever points at `Core`. `Core` compiles, and its tests pass,
with no knowledge that TypeSafe exists.

| Layer | Contains | Must not contain |
| --- | --- | --- |
| `DecisionKit.Core` | Questions, answers, results, probability and score value objects, `IDecisionProvider`, provider-neutral options, error contracts, validation | Any URL, header, credential, DTO, endpoint name or protocol JSON |
| `DecisionKit.Jev` | JEV protocol DTOs, mappers, HTTP client, authentication, error mapping, retry policy, idempotency | Domain redefinition; anything a second provider would need to copy |
| `DecisionKit.Extensions` | `IServiceCollection` registration, `IOptions` binding, `IHttpClientFactory` wiring, logging integration | Domain logic; protocol logic |
| `DecisionKit.Testing` | Fake and deterministic providers, canned results, request capture, error injection | HTTP; any real provider call |

## 3. Core concepts

```text
Question<TAnswer>
       |
       v
IDecisionProvider
       |
       v
DecisionResult
       |
       +--> Answer<T>
       +--> Probability
       +--> Metadata
```

A question carries the type of its own answer. `Question<ChoiceAnswer<Department>>` cannot
be read as a `ScoreAnswer` without an explicit conversion, and the result lookup
`result.Get(question)` infers the answer type from the question rather than from a string.

`QuestionId` is a validated value object, not a bare string, so that duplicate detection
and result correlation are deterministic. A question set rejects duplicate identifiers;
`last wins` is never acceptable.

## 4. Request flow

```text
Application
    |  DecisionRequest (input, questions, provider-neutral options)
    v
IDecisionProvider
    v
JevDecisionProvider ── validate ── map ── serialize ── HttpClient ── TypeSafe JEV
                                                                          |
Application <── DecisionResult <── map <── deserialize <── HttpResponse <──┘
```

Two rules govern the two mapping steps:

- **Domain is not the wire DTO.** Protocol DTOs live in the provider package and evolve on
  the provider's schedule, not on the domain's.
- **Mapping is explicit.** Named mappers, no reflection-based convention magic.

## 5. Forward compatibility

The provider will ship a question or answer type before DecisionKit models it. The parser
follows one principle:

> recognize what you know, preserve what you do not.

```text
known   -> typed model
unknown -> UnknownAnswer / raw JSON, with provider type and metadata retained
unknown -> discarded          <- never
```

The raw fallback is a safety net, not the primary API. A single unrecognized answer must
never fail the whole response.

## 6. Failure model

Failures are distinguishable, because callers react to them differently:

```text
validation | authentication | authorization | rate limit | timeout
cancellation | transport | serialization | provider error | unknown response
```

Diagnostic data the provider supplies — error code, message, request identifier,
documentation link, retry hint — is preserved through the mapping. It is never flattened
into a generic exception message.

## 7. Resilience as semantics

Retry, timeout and idempotency are properties of the operation, not of the HTTP client.

- Errors are classified as retryable, non-retryable or unknown, and the classification is
  configurable because the semantics belong to the provider.
- The retry budget covers attempt count, total elapsed time, initial delay, exponential
  backoff, maximum delay, jitter and `Retry-After`.
- Three timeouts are distinguished: per attempt, per operation, and per retry delay.
- The caller's cancellation token outranks the retry policy. A cancelled operation performs
  no further attempt.
- Retrying a non-idempotent POST is an explicit, documented choice. `IdempotencyKey` is a
  first-class concept with a policy of `Disabled`, `ExplicitOnly` or `Automatic`.

Every retry emits a structured diagnostic event.

## 8. Extensibility through decoration

`IDecisionProvider` is designed to be wrapped:

```text
LoggingProvider -> MetricsProvider -> RetryingProvider -> CachingProvider -> JevProvider
```

New cross-cutting behaviour is added by composition, never by editing the provider.

## 9. Two API levels

- **Simple API** for create question → execute → read result. Minimum types, minimum
  configuration.
- **Advanced API** for custom providers, transport control, retry tuning, raw protocol
  access, diagnostics and AOT scenarios.

Advanced configuration never leaks into the simple path.

## 10. Non-functional constraints

| Constraint | Decision |
| --- | --- |
| Target frameworks | `net8.0` and `net10.0` |
| Serialization | `System.Text.Json`, source-generated metadata where useful |
| Trimming and AOT | Designed for both; reflection kept minimal |
| Threading | Providers are reusable across concurrent requests; no mutable per-request state in a singleton |
| Immutability | Configuration objects are immutable after construction |
| Public surface | A type is public only when a consumer must use it |
| Dependencies | `Core` has zero package references; other packages use Microsoft-maintained packages only |

## 11. Out of scope

The first release deliberately excludes: a UI or dashboard, persistence, a visual workflow
engine, application authentication, a general business rules engine, a local model, a
provider-independent probabilistic engine, analytics, account or billing management, and
SDKs for other languages.

Advanced decision mathematics — weighted and composite scores, entropy, variance,
confidence thresholds, routing, policy evaluation — is explicitly excluded from `Core` and
reserved for a future `DecisionKit.Decisions` package that consumes `DecisionResult` and
has no knowledge of any provider.

## 12. Direction

```text
                DecisionKit.Core
                      |
        +-------------+-------------+
        |             |             |
       JEV       Local rules     Other AI
        |             |             |
        +-------------+-------------+
                      |
               DecisionResult
                      |
             DecisionKit.Decisions
                      |
          +-----------+-----------+
          |           |           |
       scoring     routing     policies
```

The goal is not "a .NET wrapper for JEV" but "strongly typed decision infrastructure for
.NET, with JEV as the first production provider". Every architectural decision is judged
against that sentence.
