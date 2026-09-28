# The JEV protocol

This page records the JEV wire contract as DecisionKit implements it, and where each
statement comes from. It describes the payload and everything around it: the endpoint, the
credential, the headers, the timeouts, and what happens when a call is repeated.

## Sources

Everything below was verified against three independent sources that agree with each other:

| Source | What it settles |
| --- | --- |
| <https://docs.typesafe.ai/api.md> | The published reference: endpoint, request and response shape, the three question types, the error envelope, limits. |
| The official Python SDK | The request and response schemas, declared as Pydantic models, including which members are required. |
| [`saibimajdi/typesafeai-dotnet-sdk`](https://github.com/saibimajdi/typesafeai-dotnet-sdk) | A second implementation of the same contract, cited by the founding analysis as a benchmark. |

Where a statement is not covered by any of them, it says so explicitly and is marked
**convention** — a choice DecisionKit made, not a rule the protocol imposes.

## The call

JEV evaluates a **state** against a set of **questions**, and answers each one independently.
There is one evaluation endpoint, `POST /v1/systemone`, and a model listing endpoint,
`GET /v1/models`. The version is a path segment: a base URL must not already contain it.

## Request

```json
{
  "state": "Customer: I was charged twice for the same order and nobody has replied.",
  "model": "jev-latest",
  "questions": {
    "is_urgent": {
      "type": "noul",
      "instructions": "Whether the ticket needs attention today"
    },
    "department": {
      "type": "choice",
      "instructions": "Which team should handle this ticket",
      "criteria": { "Sales": null, "Billing": null, "Support": null }
    },
    "frustration": {
      "type": "score",
      "instructions": "How frustrated the customer appears",
      "criteria": [
        "Calm, just stating facts",
        "Frustrated but civil",
        "Very angry, strong language"
      ]
    }
  }
}
```

All three members are required.

### `state`

What the questions are asked about. It may be a string, an object or an array — the service
imposes no schema on it.

Before v1 this member was called `document`. It is not accepted any more; sending `document`
fails validation. DecisionKit never emits it, and a test asserts so.

DecisionKit maps `DecisionInput` onto it:

| Input | `state` |
| --- | --- |
| Text only | The text, as a bare JSON string. |
| Properties only | The properties, as a JSON object. |
| Both | The properties as an object, with the text added under `text`. **Convention.** |
| Neither | Rejected with `jev_state_required`. |

If the properties already contain a member named `text`, the request is rejected with
`jev_state_ambiguous` rather than letting one value silently replace the other.

### `model`

Which model to evaluate with. `jev-latest` is an alias; the response reports the concrete
version it resolved to. DecisionKit sends `JevMappingOptions.Model`, which defaults to
`jev-latest`.

### `questions`

A map. **The key is a name the caller chooses**, and it is the name the answer comes back
under. It is not sent to the model and has no effect on the evaluation. DecisionKit uses
`Question.Id` as the key.

Every question carries a `type` and `instructions`. `instructions` is required for all three
types and may be a string, an object or an array; DecisionKit sends `Question.Prompt` as a
string.

`criteria` depends on the type:

| Type | Domain type | `criteria` |
| --- | --- | --- |
| `noul` | `ProbabilityQuestion` | Optional. An object with `true` and `false` descriptions. DecisionKit does not emit it yet. |
| `choice` | `ChoiceQuestion<TOption>` | **Required.** A map from option name to a description, or `null`. At most 255 options. |
| `score` | `JevScoreQuestion` | **Required.** An ordered array of 2 to 10 level descriptions. |

There are exactly three types. A question of any other type is sent as an `UnknownQuestion`,
whose `ProviderType` becomes the `type` and whose `RawDefinition` becomes the `criteria`.

#### Why `noul`

`noul` is JEV's own name for its boolean-like primitive. The answer is not a verdict with a
confidence attached: it is the probability that the answer is yes. DecisionKit calls the
question a `ProbabilityQuestion` and the answer a `ProbabilityAnswer`, and the name `noul`
stays inside `DecisionKit.Jev`.

#### Choice options

DecisionKit names an option by its own text, culture-invariantly. That text is what the model
sees, so an option whose `ToString()` is an opaque code will be judged on that code. Two
options that produce the same text are rejected with `jev_choice_options_ambiguous`, because
the answer could not be resolved back to one of them.

Per-option descriptions are supported by the protocol but not exposed yet; DecisionKit sends
`null` for every option.

#### Score rubrics

A JEV score is not a number on a scale the caller invents. It is a position across levels the
caller describes, and the rubric is mandatory. `ScoreQuestion` from the core describes a
numeric range and cannot express one, so it is **rejected** with `jev_question_unsupported`
and a message naming `JevScoreQuestion`. See
[ADR-0009](../adr/0009-provider-specific-question-types.md).

### What DecisionKit does not send

| Domain member | What happens | Why |
| --- | --- | --- |
| `DecisionOptions.Language` | **Rejected**, `jev_option_unsupported`. | JEV has no language member. Dropping it would change what the caller asked for. |
| `DecisionRequest.IdempotencyKey` | **Rejected**, `jev_option_unsupported`. | JEV has no idempotency mechanism. A retry would be evaluated again. |
| `DecisionOptions.IncludeExplanations` | Ignored. | JEV returns no rationale in any form. The domain intends this for a provider that cannot explain itself. |
| `DecisionRequest.ClientRequestId` | Not sent; echoed on the result. | Correlation, not instruction. There is no member for it. |
| `DecisionRequest.Metadata` | Not sent. | Annotation, not instruction. There is no member for it. |
| `Question.Metadata` | Not sent. | Same. It remains on the question the caller holds. |

## Response

```json
{
  "model": "jev-1.13.0",
  "answers": {
    "is_urgent": { "type": "noul", "noul": 0.97 },
    "department": {
      "type": "choice",
      "choice": "Billing",
      "confidence": 0.78,
      "probabilities": { "Sales": 0.02, "Billing": 0.93, "Support": 0.05 }
    },
    "frustration": {
      "type": "score",
      "score": 1.42,
      "confidence": 0.81,
      "legend": { "0": "Calm, just stating facts", "1": "Frustrated but civil", "2": "Very angry, strong language" },
      "probabilities": { "0": 0.12, "1": 0.34, "2": 0.54 }
    }
  },
  "usage": { "input_tokens": 392, "output_tokens": 65 }
}
```

`answers` is keyed by the names the request chose. **A question may be absent.** That is not
an error: the result simply does not contain it, and `DecisionResult.TryGet` reports so.
DecisionKit never fills in a default.

An answer for a question that was not asked **is** an error — it cannot be interpreted
without the question that defines its shape.

### `noul`

Carries `noul`, a probability from 0 to 1. **It carries no `confidence`, by design.** The
probability is the whole answer: `0.5` is the model reporting genuine uncertainty about the
world, not low confidence in a verdict. `JevAnswers.TryGetConfidence` returns `false` for a
noul answer, and that is correct rather than a gap.

### `choice`

Carries `choice`, the selected option echoed exactly as the request spelled it, and usually
`probabilities` and `confidence`. An option the question never offered is rejected with
`jev_answer_unmappable` rather than guessed at.

The distribution reaches the domain typed, as `Choice<TOption>.Distribution`, and is not
repeated in metadata. An answer carrying only a distribution and no selection is valid and
maps to a `Choice<TOption>` without a selection.

### `score`

Carries `score`, a **probability-weighted fractional position** over the level indices
`0 .. n-1`. It falls between two levels far more often than it lands on one; `1.42` means
"between the second and third level, closer to the third". It is not an index and must not be
rounded to one without deciding, explicitly, that rounding is what the application wants.

`legend` and `probabilities` are keyed by the level index written as a string.

### `confidence`

Derived from `probabilities`, but **the formula is not published**. DecisionKit treats it as
opaque: it is carried, never recomputed, never compared across answer types. Compare it only
against itself.

### `usage`

Only `input_tokens` and `output_tokens`, both nullable. They reach `DecisionResult.Usage`
under `DecisionUsageKeys.InputTokens` and `DecisionUsageKeys.OutputTokens`.

### What the response does not contain

No explanation or rationale, in any form, for any answer type. No timestamp. No request
identifier in the body — the service reports it in the `x-typesafe-request-id` response
header, which the transport layer passes to `ResponseMapper.ToDomain`.

Because there is no timestamp, `DecisionMetadata.Timestamp` is the moment the mapper read the
response, taken from a `TimeProvider` ([ADR-0007](../adr/0007-time-abstraction.md)).

### Answer metadata

Three things JEV reports have no place in the provider-neutral domain. They travel as answer
metadata and are read back through `JevAnswers`:

| Key | Reader | Present on |
| --- | --- | --- |
| `jev.confidence` | `JevAnswers.TryGetConfidence` | `choice`, `score` |
| `jev.legend` | `JevAnswers.TryGetLegend` | `score` |
| `jev.probabilities` | `JevAnswers.TryGetLevelProbabilities` | `score` |
| `jev.raw` | `JevAnswers.TryGetRawPayload` | Any, when `PreserveRawPayloads` is on |

### Unrecognized answers

An answer whose `type` is not one of the three becomes an `UnknownAnswer` carrying the
provider type and the exact JSON it arrived as. Members the DTO does not model are preserved
through `[JsonExtensionData]` rather than dropped.

`DecisionOptions.UnknownTypeHandling` decides what that means:

- `Preserve` (default) — the answer reaches the caller as data.
- `Fail` — the whole response is rejected.

## Errors

Application errors:

```json
{ "detail": { "error_type": "invalid_request_error", "message": "..." } }
```

Framework errors, raised in front of the application, send a bare string instead:

```json
{ "detail": "Not Found" }
```

Both are read. `JevErrorResponse.Detail` is a `JsonNode` precisely so that neither shape is
lost.

### The category comes from the status code

**`error_type` is undocumented and open-ended. DecisionKit never switches on it.** Branching
on it would silently misclassify anything the service adds later. The value is reported as
`DecisionError.Code` and under `jev.error_type`, for a caller with a specific reason to look
at it.

| Status | `DecisionErrorCategory` | Retry |
| --- | --- | --- |
| 400 | `Validation` | Not retryable |
| 401 | `Authentication` | Not retryable |
| 403 | `Authorization` | Not retryable |
| 404 | `Validation` | Not retryable |
| 408 | `Timeout` | Retryable |
| 422 | `Validation` | Not retryable |
| 429 | `RateLimit` | Retryable |
| 500, 501, … | `ProviderError` | Unknown |
| 502, 503, 504 | `ProviderError` | Retryable |
| **529** | `ProviderError` | Retryable |
| No response | `Transport` | Retryable |

404 is classified as `Validation` rather than `ProviderError` on purpose: reaching it means
the caller addressed an endpoint that does not exist, which is a configuration mistake and
will not fix itself on a retry.

529 is not an HTTP standard code. JEV uses it to say it is overloaded, which is transient.

A `retry-after` reported by the service always wins over the table above.

## Transport

### Endpoint

The base address defaults to `https://api.typesafe.ai`. `JevEndpoint` validates it at
construction and rejects a relative URI, a scheme other than `http` or `https`, plain `http`
against anything but a loopback host, embedded userinfo, a query string, a fragment, and a
base address that already contains the `v1` segment. The version is a path segment the
library appends, so a base URL carrying one would address `/v1/v1/systemone`.

A trailing slash is added when missing, so that a proxy prefix such as
`https://gateway.internal/jev` keeps its path instead of losing it to relative resolution.

A bad base address throws `ArgumentException`, not `DecisionException`: it is a programming
mistake, and [ADR-0006](../adr/0006-error-model.md) keeps those outside the decision error
model.

### Credential

The key travels as `Authorization: Bearer <key>`. `IJevCredentialProvider` is asked for it on
**every** call rather than once at construction, so a rotated key takes effect on the next
call without rebuilding the provider. `StaticJevCredentialProvider` holds a fixed key;
`DelegateJevCredentialProvider` calls back, and deliberately does not cache — a provider that
wants caching owns that decision.

`JevApiKey` is a value object whose `ToString` returns `<redacted>` and whose value is
`internal`. A missing key fails with `jev_credential_missing` before anything is sent.

### Headers

| Header | Direction | Meaning |
| --- | --- | --- |
| `Authorization` | Request | `Bearer` plus the key |
| `x-typesafe-request-id` | Response | The identifier the service stamped on the call; surfaces as `DecisionMetadata.ProviderRequestId` and on `DecisionError` |
| `retry-after-ms` | Response | Retry delay in milliseconds |
| `Retry-After` | Response | Retry delay in seconds, or an HTTP date |

`retry-after-ms` wins when both are present, because it is the more precise of the two. The
HTTP-date form is resolved against the injected `TimeProvider`, and a delay already in the
past is clamped to zero.

### Timeouts and cancellation

Three independent budgets, all created from the `TimeProvider`:

| Budget | Default | Bounds |
| --- | --- | --- |
| `AttemptTimeout` | 30 s | One HTTP call |
| `OperationTimeout` | 60 s | One `DecideAsync` call, end to end, across every attempt and wait |
| `RetryDelayTimeout` | 30 s | One wait between two attempts |

Any of them may be `Timeout.InfiniteTimeSpan`; anything else must be positive. When a budget
expires the call fails with `jev_timeout`, categorized as `Timeout`, marked retryable, and
carrying `jev.timeout_scope` set to `attempt` or `operation`. `RetryDelayTimeout` is the
exception: exceeding it is not a timeout but an abandoned retry, so the failure reported is
the one that was about to be retried.

Cancellation is not a failure. When the caller's token is cancelled the call throws
`OperationCanceledException`, never a `DecisionException`.

### Failures without a status

| Situation | Code | Category | Retryable |
| --- | --- | --- | --- |
| No key available | `jev_credential_missing` | `Authentication` | No |
| Budget expired | `jev_timeout` | `Timeout` | Yes |
| Service unreachable | `jev_transport_failure` | `Transport` | Yes |

An error body that cannot be read as JEV does **not** replace the status: a gateway answering
HTML to a `503` still produces a `ProviderError`. An unreadable *success* body is different,
and surfaces as `jev_payload_unreadable`.

### Logging

Every log message is source-generated and takes scalars only: provider, operation, HTTP
status, elapsed milliseconds, attempt number and the provider request identifier. No payload
and no credential is ever passed to the logger, which makes the rule checkable by reading
`Client/JevLog.cs`.

| Event | Level | Written when |
| --- | --- | --- |
| 1000 | Debug | An evaluation starts |
| 1001 | Information | The service answered |
| 1002 | Warning | The service refused |
| 1003 | Debug | The caller cancelled |
| 1004 | Warning | A budget expired |
| 1005 | Warning | The service could not be reached |
| 1006 | Information | A retry is scheduled |
| 1007 | Warning | Every permitted attempt failed |
| 1008 | Warning | An operation is being repeated without deduplication |

## Resilience

Retrying is **off** by default. `JevProviderOptions.Retry` is `JevRetryPolicy.None`, which
makes exactly one attempt. JEV has no idempotency mechanism, so a repeated call is a second
evaluation that is billed again and may answer differently; turning that on is the
application's decision, recorded in [ADR-0010](../adr/0010-retry-is-opt-in.md).

`JevRetryPolicy.Standard` is the ready-made alternative: three attempts, 500 ms initial
delay, doubling, capped at 10 seconds, 20 % jitter.

```csharp
JevProviderOptions options = new()
{
    Retry = JevRetryPolicy.Standard,
};
```

### Policy

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxAttempts` | 1 | Attempts in total, the first one included |
| `MaxElapsedTime` | infinite | Attempts plus waits, before retrying stops |
| `InitialDelay` | 500 ms | Wait before the second attempt |
| `BackoffFactor` | 2 | Multiplier applied to each wait; 1 keeps it constant |
| `MaxDelay` | 10 s | Ceiling for a computed wait |
| `Jitter` | 0.2 | Fraction of a computed wait randomly removed |
| `RespectRetryAfter` | `true` | A delay the service asked for replaces the backoff |
| `RetryUnknownFailures` | `false` | Repeat a failure the provider gave no signal about |
| `RetryableStatusCodes` | empty | Statuses this deployment repeats regardless |
| `NonRetryableStatusCodes` | empty | Statuses this deployment never repeats |
| `Idempotency` | `Disabled` | The only value JEV accepts |

Jitter only ever shortens a wait, so a delay never exceeds `MaxDelay`, and it is not applied
to a delay the service asked for. A `Retry-After` is obeyed exactly or not at all: a wait
longer than `RetryDelayTimeout` abandons the retry instead of being truncated.

### Classification

A failure is repeated when it classifies as retryable, in this order:

1. `NonRetryableStatusCodes` contains the status: **not retryable**, whatever else says.
2. `RetryableStatusCodes` contains the status: **retryable**.
3. Otherwise the `DecisionRetryHint` the [error mapping](#the-category-comes-from-the-status-code)
   produced decides — a `Retry-After` header alone makes a failure retryable.
4. `Unknown` means the provider said nothing. It is repeated only when
   `RetryUnknownFailures` is on.

Nothing in the chain reads an error message.

### What a retried failure looks like

A failure that survived more than one attempt carries `jev.retry_attempts` with the number of
attempts made. Its absence means the call was made once, so a caller can tell one failed
request from a retried one that never recovered. Everything else about the error — category,
code, request identifier, status — is the last attempt's, unchanged.

### Idempotency

`DecisionRequest.IdempotencyKey` is refused before anything is sent, with
`jev_option_unsupported`: the service offers no way to honour it, and dropping it silently
would let a caller believe a retry is deduplicated. `Capabilities.SupportsIdempotencyKeys` is
`false` for the same reason.

`JevRetryPolicy.Idempotency` therefore accepts only `DecisionIdempotencyPolicy.Disabled`;
`ExplicitOnly` and `Automatic` throw `ArgumentOutOfRangeException` while the options are being
built. When a call is actually repeated, event 1008 is written once for the operation, stating
that the repeated request is evaluated and charged again.

## Limits

| Limit | Value |
| --- | --- |
| Score levels | 2 to 10 |
| Choice options | 255 |

## Recorded payloads

`tests/DecisionKit.Jev.Tests/Payloads` holds the payloads the contract tests run against,
recorded from the published reference examples. `JevWireContractTests` compares what the
mappers produce against `request.json` member by member, and reads `response.json` back into
the domain.

A failure there means the wire contract moved. The fix is to re-record the payload against
the reference — never to relax the assertion.
