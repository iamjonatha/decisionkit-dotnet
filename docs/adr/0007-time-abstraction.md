# 0007. Use the BCL TimeProvider as the only clock abstraction

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

`DecisionKit.Testing` exists so that application code can be tested without a provider, and
two of its behaviours are about time rather than about answers:

- A fake provider must be able to simulate latency, so that a test can prove a timeout, a
  cancellation or a deadline is honoured without actually waiting.
- A recorded call must carry a timestamp, so that a test can assert ordering and elapsed
  time between calls.

A test that waits for real time is slow and flaky, and a library that reads
`DateTimeOffset.UtcNow` internally cannot be tested for either behaviour. Something has to
stand between the library and the clock.

The same question will be asked again by retry and backoff, which need a clock they
can fast-forward, and in every provider that reports latency in `DecisionMetadata`. The
answer should be decided once, here, rather than reinvented per package.

ADR-0003 restricts shipping dependencies to Microsoft-maintained packages and requires a new
record for every dependency added. This record is that requirement being met.

## Decision

`TimeProvider`, from the base class library, is the clock abstraction for the whole
repository. DecisionKit defines no clock interface of its own.

- Any type that needs the current instant or a delay exposes a settable
  `TimeProvider TimeProvider` property that defaults to `TimeProvider.System`.
- Delays are `Task.Delay(delay, TimeProvider, cancellationToken)`, never
  `Task.Delay(delay, cancellationToken)`.
- Timestamps are `TimeProvider.GetUtcNow()`, never `DateTimeOffset.UtcNow`.

`TimeProvider` ships in `net8.0` and `net10.0`, so `DecisionKit.Testing` takes **no** package
reference for any of this. It keeps a single project reference to `DecisionKit.Core`.

Fast-forwarding a clock needs a controllable implementation, and Microsoft publishes one:
`Microsoft.Extensions.TimeProvider.Testing`, which contains `FakeTimeProvider`. That package
is referenced by `DecisionKit.Testing.Tests` only, as test tooling. It is **not** referenced
by any shipping package and is never exposed in a public signature, so it is not a
transitive dependency of anyone who installs `DecisionKit.Testing`.

Consumers who want virtual-time tests add that package to their own test project, exactly as
they would for any other library that accepts a `TimeProvider`.

## Consequences

### Positive

- `DecisionKit.Testing` ships with zero package dependencies, like `DecisionKit.Core`.
- `FakeDecisionProvider.Latency` is free: a test sets a latency of thirty seconds, advances
  a `FakeTimeProvider` by thirty seconds, and the call completes instantly.
- The abstraction is the one the rest of the ecosystem already uses. A consumer who already
  injects a `TimeProvider` for their own code injects the same instance here.
- Retry and provider latency reporting inherit the decision without reopening it.

### Negative

- A consumer who wants virtual time must add `Microsoft.Extensions.TimeProvider.Testing`
  themselves. This is documented in the testing guide, but it is one more step than a
  bundled clock would be.
- `TimeProvider` is an abstract class, not an interface, so it cannot be mocked by a
  proxy-based mocking library that only handles interfaces. `FakeTimeProvider` covers every
  case we have, and hand-writing a subclass is possible when it does not.

### Neutral

- `DeterministicDecisionProvider` does not read a clock at all: its `Timestamp` is a fixed
  value defaulting to `DateTimeOffset.UnixEpoch`, because a snapshot containing the current
  time never matches twice.

## Alternatives considered

**Define `IDecisionClock` in `DecisionKit.Core`.** Rejected. It is a worse `TimeProvider`:
it cannot create timers, it does not compose with `Task.Delay`, and every consumer would
have to adapt their existing `TimeProvider` to it. Inventing an abstraction that the
framework already ships is how a library becomes hard to integrate.

**Write our own controllable `TimeProvider` inside `DecisionKit.Testing`.** Rejected. It is
roughly a hundred and fifty lines duplicating `FakeTimeProvider`, including timer scheduling
and auto-advance, and every bug in it would be ours. The gain — saving consumers one package
reference in their test project — does not pay for it.

**Reference `Microsoft.Extensions.TimeProvider.Testing` from `DecisionKit.Testing` and
re-export `FakeTimeProvider`.** Rejected. It forces the package on every consumer, including
those who never fast-forward anything, and it ties our shipping surface to the versioning of
a package we do not control. Test tooling belongs in test projects.

**Accept a `Func<DateTimeOffset>` where a timestamp is needed.** Rejected. It covers the
timestamp half and not the delay half, so latency simulation would still need something
else, and two clock abstractions is worse than one.
