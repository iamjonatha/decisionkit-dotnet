# 0010. Make retrying opt-in for the JEV provider

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** @iamjonatha

## Context

The JEV provider has a transport that reports a transient failure honestly: a 429,
a 503, a 529, a connection that never opened and an attempt that ran out of time all arrive
as a `DecisionTransientException` carrying a `DecisionRetryHint`. Nothing acts on that hint
yet, so today every transient failure reaches the caller.

The obvious next step is to repeat the call, and the obvious default is to repeat it. Most
HTTP client libraries ship a retry policy that is on out of the box, and a caller who does
not think about resilience gets some anyway.

That default is wrong here, for one reason that is specific to this service and one that is
not.

The specific reason: JEV documents no idempotency mechanism. There is no header, no request
member and no documented deduplication window. `DecisionProviderCapabilities` already says so
with `SupportsIdempotencyKeys = false`, and `RequestMapper` already refuses a request that
carries an `IdempotencyKey` rather than dropping it. A repeated call is therefore a second
evaluation in every sense: it is a second inference, it is billed a second time, and it can
return a different answer from the first.

The general reason: a decision call is not a database read. A retry that a caller did not ask
for turns one budgeted, observable unit of work into an unbounded number of them, and the
place where that shows up is the invoice, not the logs.

Against that, a JEV call genuinely does fail transiently, and a caller who wants three
attempts with exponential backoff should not have to write the loop, the jitter and the
`Retry-After` parsing themselves.

## Decision

The library implements a full retry policy and turns it off by default.

`JevProviderOptions.Retry` defaults to `JevRetryPolicy.None`, which makes exactly one
attempt. `JevRetryPolicy.Standard` is provided, tuned for an interactive call — three
attempts, 500 ms initial delay, doubling, capped at 10 seconds, 20 % jitter — so that turning
retrying on is one assignment rather than a research project.

`JevRetryPolicy.Idempotency` exists, is typed as the provider-neutral
`DecisionIdempotencyPolicy`, and accepts only `Disabled`. `ExplicitOnly` and `Automatic` throw
`ArgumentOutOfRangeException` with a message naming the reason. The property is not decoration:
it is where a reader looks to find out what this provider can promise, and it fails at
configuration time rather than letting a deployment believe a retried call is deduplicated.

When retrying is on and a call is actually repeated, the first retry of each operation writes
one warning stating that the repeated request is evaluated and charged again. It is written
once per operation, not once per attempt, because it describes the operation.

Three budgets bound an operation, and they are independent:

| Budget | Bounds | Enforced by |
| --- | --- | --- |
| `AttemptTimeout` | one HTTP exchange | `JevTransport` |
| `OperationTimeout` | every attempt and every wait | `JevRetryRunner` |
| `RetryDelayTimeout` | one wait between attempts | `JevRetryRunner` |

A wait longer than `RetryDelayTimeout` abandons the retry rather than being shortened.
Honouring half of a `Retry-After` is not honouring it, and arriving early at a service that
asked for room is worse than not arriving.

Classification reads the `DecisionRetryHint` the error mapper produced, and a deployment can
override it per status code through `RetryableStatusCodes` and `NonRetryableStatusCodes`. A
failure the provider said nothing about — `DecisionRetryability.Unknown` — is not repeated
unless `RetryUnknownFailures` says so, because silence is not consent.

## Consequences

### Positive

- Upgrading to this version changes no observable behaviour. A deployment that was making one
  call per decision keeps making one.
- Spend stays predictable by default, and becomes unpredictable only where somebody decided
  it should.
- The idempotency constraint is discovered while configuring the provider, with a message that
  says what to do instead, rather than inferred from a duplicated charge.
- The seam is clean: the transport makes one attempt and knows HTTP, the runner makes several
  and knows time. Each is tested without the other.
- Every wait goes through `TimeProvider`, so the retry suite runs in virtual time and costs
  milliseconds.

### Negative

- A caller who expected resilience by default gets none, and finds out from a failure rather
  than from a setting. The README, the provider reference and this record are the mitigation;
  they are not as loud as a default would be.
- Two knobs govern how long a wait may be — `JevRetryPolicy.MaxDelay` for the computed backoff
  and `JevProviderOptions.RetryDelayTimeout` for any wait including a requested one. The
  distinction is real but has to be read.

### Neutral

- `DecisionIdempotencyPolicy` is added to the core although JEV can only honour one of its
  three values. It is provider-neutral vocabulary, like `DecisionRetryability`, and a future
  provider that documents deduplication will use the rest of it.

## Alternatives considered

**Retry by default, like most HTTP clients.** Rejected. Those clients retry requests the
protocol defines as idempotent. There is no such definition here, and the cost of being wrong
is charged to the user.

**Refuse to retry at all, since nothing can be deduplicated.** Rejected. It is a real
constraint but not the library's decision to make. A caller who knows their prompt is cheap
and their deadline is soft is entitled to three attempts.

**Let `ExplicitOnly` mean "retry only a request carrying an `IdempotencyKey`".** Rejected. The
mapper refuses such a request, deliberately, because JEV cannot honour the key. Accepting the
key as a local safe-to-repeat marker would give one type two meanings, one of which only
applies to this provider.

**Put the retry loop in the transport.** Rejected. It would merge two budgets and two
concerns into one type, and there would be no way to test the attempt without also testing
the loop around it.

**Build on `Microsoft.Extensions.Http.Resilience`.** Rejected, and already foreclosed:
[ADR-0003](0003-dependency-policy.md) states that retry, backoff and idempotency are
implemented in-house. The package pulls in Polly, which is not Microsoft-maintained, and it
operates on `HttpResponseMessage`, below the point where a failure has become a classified
`DecisionError`. The hosting integration can still let an application wrap the client
with its own handler.
