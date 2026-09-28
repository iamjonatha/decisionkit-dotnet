# The JEV provider

How to use `DecisionKit.Jev` in an application. For the wire contract itself — payloads,
headers, status codes and every mapping rule — see the
[JEV wire protocol reference](jev-protocol.md).

## Construct it

```csharp
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;

JevDecisionProvider provider = new(httpClient, new StaticJevCredentialProvider(apiKey));
```

Two things are true of that constructor and stay true of every overload:

- the provider **never owns** the `HttpClient`, so pooling, handlers and lifetime stay yours;
- the credential is asked for on **every call**, so a rotated key takes effect immediately.

The full overload takes options, a logger and a `TimeProvider`:

```csharp
JevDecisionProvider provider = new(
    httpClient,
    credentials,
    new JevProviderOptions
    {
        Endpoint = new JevEndpoint(new Uri("https://gateway.internal/jev/")),
        AttemptTimeout = TimeSpan.FromSeconds(10),
        OperationTimeout = TimeSpan.FromSeconds(30),
        Retry = JevRetryPolicy.Standard,
    },
    logger,
    timeProvider);
```

In a host, do not construct it by hand. `AddJevProvider` wires all five arguments from the
container — see the [dependency injection guide](../guides/dependency-injection.md).

## Credentials

| Type | Use it when |
| --- | --- |
| `StaticJevCredentialProvider` | The key is fixed for the lifetime of the process |
| `DelegateJevCredentialProvider` | The key comes from a vault, a cache or a refresh loop |
| `IJevCredentialProvider` | You need something neither of the above covers |

```csharp
IJevCredentialProvider credentials = new DelegateJevCredentialProvider(
    async ct => new JevApiKey(await vault.GetSecretAsync("jev-key", ct)));
```

`JevApiKey.ToString()` does not return the key. It is a value type built to be hard to leak:
see [handling secrets](../guides/secrets.md).

## Scores are a rubric, not a range

This is the one place where JEV is stricter than the domain, and the one that surprises people.

A plain `ScoreQuestion` declares a numeric range. JEV does not score against a range; it scores
against described levels, and the description is mandatory — it is what the model is actually
judging against. So a `ScoreQuestion` is rejected with a `DecisionValidationException`, and the
JEV-specific question carries the rubric:

```csharp
using DecisionKit.Jev.Questions;

JevScoreQuestion frustration = new(
    new QuestionId("frustration"),
    "How frustrated is the customer?",
    [
        "Calm and cooperative.",
        "Mildly annoyed.",
        "Clearly irritated.",
        "Angry and threatening to leave.",
        "Already announced they are cancelling.",
    ]);
```

Between 2 and 10 levels. Minimum is `0` and Maximum is `Levels.Count - 1`, so the answer is a
position across the rubric rather than a pick of one level — the value is probability-weighted
across the levels, not snapped to one. It comes back as a plain `ScoreAnswer`, so nothing
downstream has to know the question was provider-specific:

```csharp
Score score = result.Get(frustration).Value;

if (frustration.TryGetLevel((int)Math.Round(score.Value), out string description))
{
    logger.LogInformation("Frustration: {Value:0.0} — closest level: {Description}", score.Value, description);
}
```

The reasoning is in [ADR-0009](../adr/0009-provider-specific-question-types.md). Provider-specific
question types live in the provider package, never in `Core`.

`ChoiceQuestion<TOption>` and `ProbabilityQuestion` need no JEV equivalent and work directly.

## Options become strings

An option travels as its own culture-invariant text, produced by `JevOptionLabel`:

```csharp
string? label = JevOptionLabel.For(Department.Billing); // "Billing"
bool same = JevOptionLabel.Matches(Department.Billing, "Billing");
```

That label is what the model reads. `Department.Billing` is a good option name; `Dept_017` is a
bad one, and no amount of prompt engineering fixes it. A choice question may declare up to 255
options, and two options that produce the same label are rejected as ambiguous.

## JEV extras on an answer

JEV returns more than the domain models. None of it is discarded; it is reachable through
`JevAnswers` without casting or string keys:

```csharp
using DecisionKit.Jev.Protocol;

Answer answer = result.Get(routing);

if (JevAnswers.TryGetConfidence(answer, out double confidence)) { }
if (JevAnswers.TryGetLegend(answer, out IReadOnlyDictionary<string, object?> legend)) { }
if (JevAnswers.TryGetLevelProbabilities(answer, out IReadOnlyDictionary<string, object?> levels)) { }
if (JevAnswers.TryGetRawPayload(answer, out string payload)) { }
```

Raw payload capture is controlled by `JevMappingOptions.PreserveRawPayloads`, or by
`Jev:PreserveRawPayloads` in configuration. It keeps the provider's original JSON on the
answer, which is invaluable when debugging and is a privacy decision when logged: the payload
contains whatever you sent.

## Timeouts

Three timeouts, and they mean different things:

| Setting | Bounds |
| --- | --- |
| `AttemptTimeout` | One HTTP attempt |
| `OperationTimeout` | The whole call, retries and delays included |
| `RetryDelayTimeout` | Any single wait between two attempts |

A wait longer than `RetryDelayTimeout` abandons the retry and reports the failure that was
being retried, rather than being shortened: a delay the service asked for means nothing if it
is not honoured.

A timeout produces `DecisionErrorCategory.Timeout` and a `DecisionTransientException`. A
cancellation you requested produces `OperationCanceledException`. They are not the same event
and the library never conflates them.

`JevProtocol.TimeoutScopeProperty` on the error says which of the three expired.

## Retrying

**Retrying is off by default.** JEV has no idempotency mechanism, so a repeated call is a
second evaluation: it is billed again and it may answer differently. Only the application knows
whether that is acceptable. [ADR-0010](../adr/0010-retry-is-opt-in.md) records the decision.

```csharp
JevRetryPolicy policy = new()
{
    MaxAttempts = 4,
    InitialDelay = TimeSpan.FromMilliseconds(500),
    BackoffFactor = 2,
    MaxDelay = TimeSpan.FromSeconds(5),
    RespectRetryAfter = true,
};
```

`MaxAttempts` is what switches retrying on — one attempt is not a retry. `JevRetryPolicy.Standard`
is a ready-made policy; `JevRetryPolicy.None` is the default.

**Leave `Jitter` at its default.** It is what stops every client in a fleet from retrying in
lockstep after the same outage.

**A server-supplied delay wins.** With `RespectRetryAfter`, a `Retry-After` header overrides the
computed backoff, because the server knows something the client does not. `RetryDelayTimeout`
bounds how far you will let it push you.

**Classification comes from the error, not from the policy.** The error mapper has already
judged the failure and recorded a `DecisionRetryHint`. `RetryableStatusCodes` and
`NonRetryableStatusCodes` are **empty by default**: they are a per-deployment override, for
the case where a gateway in front of JEV misreports a status. The non-retryable list is checked
first, so listing a code in both refuses it.

```csharp
JevRetryPolicy behindAGateway = new()
{
    MaxAttempts = 4,
    NonRetryableStatusCodes = new HashSet<int> { 429 },
};
```

**Idempotency cannot be pretended into existence.** `JevRetryPolicy.Idempotency` accepts only
`DecisionIdempotencyPolicy.Disabled`; anything else throws `ArgumentOutOfRangeException` where
you set it, rather than quietly doing nothing at run time. An idempotency key is only honest
when the service deduplicates on it, and JEV does not.

`samples/Resilience` prints the backoff table, the header precedence and the classification
rules, and needs no credential to run.

## Capabilities

```csharp
if (!provider.Capabilities.SupportsAll(questions))
{
    // Ask something else, or pick another provider.
}
```

`Capabilities.Supports(typeof(ChoiceQuestion<>))` answers for a question type. Checking is
cheaper than a round trip that ends in a validation error.

## Failure codes

Every failure the provider raises carries a stable `Code`. Match on it, never on `Message`.
The constants live on `JevProtocol`:

| Constant | Raised when |
| --- | --- |
| `CredentialMissingCode` | No API key was supplied |
| `InputRequiredCode` | The request has no input and JEV requires one |
| `AmbiguousInputCode` | The input is ambiguous on the wire |
| `UnsupportedQuestionCode` | The question type has no JEV representation |
| `InvalidQuestionDefinitionCode` | The question is malformed for JEV |
| `TooManyOptionsCode` | More than 255 choice options |
| `AmbiguousOptionsCode` | Two options produce the same label |
| `UnmappableAnswerCode` | The answer does not fit the question that was asked |
| `PayloadUnreadableCode` | The response could not be parsed |
| `TransportFailureCode` | The call did not complete |
| `TimeoutCode` | A timeout expired |

The error's `Properties` carry `JevProtocol.StatusCodeProperty` (a boxed `int`),
`ErrorTypeProperty`, `TimeoutScopeProperty` and `RetryAttemptsProperty` where they apply.

## Logging and privacy

The provider logs through the logger you give it. It never logs the API key, and it does not
log request or response bodies. `ProviderRequestId` — taken from the `x-typesafe-request-id`
header — is logged and surfaced on both the result metadata and the error, and it is what to
quote when asking TypeSafe about a specific call.
