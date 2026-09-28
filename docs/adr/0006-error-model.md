# 0006. Report failures with a shallow exception hierarchy and a category

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

The founding analysis sketched one exception per failure kind: validation, authentication,
authorization, rate limit, timeout, transport, serialization, provider error and unknown
response. It also warned, in the same section, that the hierarchy had to be judged against
.NET practice so that it would not become "an unnecessary proliferation of exceptions", and
left the alternative of a result type open.

Both halves of that warning matter. Callers must be able to tell a bad request from an
expired credential from a rate limit, because they react differently. They must not have to
write nine `catch` blocks to do it, and a library that defines nine public exception types
in its first release has to keep every one of them forever.

There is a second requirement that neither shape solves on its own. Whatever a provider
reports about a failure — its own error code, the request identifier it assigned, the page
documenting the code, the delay it asked for — has to survive the mapping. Flattening it
into an exception message means the only way to act on it is to parse English prose.

## Decision

Failures are reported by throwing, and the delivery mechanism is separated from the
classification.

**Classification** is `DecisionErrorCategory`, which has a member for every kind the
analysis listed, plus `Unknown`. **Diagnostics** are `DecisionError`, which carries the
category, the message, the provider error code, the provider name, the provider request
identifier, the documentation link, a `DecisionRetryHint` and a property bag for whatever is
left. **Delivery** is a hierarchy of four exceptions under `DecisionException`, one per
reaction a caller can have:

| Exception | Categories | What the caller does |
| --- | --- | --- |
| `DecisionValidationException` | `Validation` | Changes the request |
| `DecisionAuthenticationException` | `Authentication`, `Authorization` | Fixes the identity configuration |
| `DecisionTransientException` | `RateLimit`, `Timeout`, `Transport` | Waits and may try again |
| `DecisionProviderException` | `ProviderError`, `Serialization`, `UnknownResponse`, `Unknown` | Logs, alerts, escalates |

Every `DecisionException` exposes its `Error`, so the finer distinction is one property
away and never requires a new exception type.

`DecisionException.FromError` is the single mapping from a category to the exception that
carries it. A provider builds a `DecisionError` and calls it; no provider hard-codes the
table above, and two providers cannot disagree about it.

Two things stay outside the hierarchy:

- **Cancellation** throws `OperationCanceledException`. It is what `CancellationToken`,
  `Task`, ASP.NET Core and every existing `catch` in the caller's code already expect.
  `DecisionErrorCategory.Canceled` exists so a cancelled call can still be *recorded* and
  logged consistently, and `FromError` refuses to wrap it.
- **Argument exceptions** stay as they are. A null question set or a duplicate identifier is
  a programming error caught before any provider is involved, and turning it into a
  `DecisionException` would tell the caller to handle a bug at run time instead of fixing it.

## Consequences

### Positive

- `catch (DecisionException)` catches every decision failure; four narrower types cover the
  four reactions; the category covers the rest without new types.
- Provider diagnostics survive to the caller as data, ready for structured logging.
- Retryability is something the provider states in `DecisionRetryHint`, not something a
  resilience policy infers from a status code. A retry layer reads it instead of re-deriving it.
- Adding a failure kind later is an enum member, not a public exception type, so it is not a
  breaking change.

### Negative

- The category and the exception type overlap, and a reader has to learn that the type
  answers "what do I do" while the category answers "what happened".
- Callers who want to treat, say, a rate limit differently from a timeout must read
  `Category` rather than catch a distinct type.

### Neutral

- `DecisionError` is usable on its own, before any exception exists. A provider that
  records a failure and translates it later needs no extra type.

## Alternatives considered

**One exception per category, as the analysis sketched.** Rejected. Nine public types is a
permanent surface for a distinction callers rarely act on, and it makes every new failure
kind a breaking addition. The information is preserved as a category instead.

**A single `DecisionException` with only a category.** Rejected. It forces every caller to
switch on an enum to tell a malformed request from an outage, which .NET callers do not
expect and which loses the readability of a typed `catch`.

**A result type, `DecisionOutcome<T>` or similar.** Rejected for the primary API. It would
make failure impossible to ignore, but it does not compose with the ecosystem the library
has to live in: `IDecisionProvider` is designed to be decorated, and middleware, logging
scopes and ASP.NET Core exception handling all work in exceptions. A result-shaped API also
tends to grow a `.Value` that throws, which is the original design with extra steps. The
`Try` pattern is already available on `DecisionResult` where a caller wants no exception.
