# 0001. Keep the core provider-independent

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

The project's starting point was an analysis of existing TypeSafe JEV SDKs for .NET. Those
SDKs are usable, but they place the protocol, the HTTP transport, the retry policy and the
domain model in a single package. As a result the application's decision model takes the
shape of the vendor's wire format, the public surface is large, and replacing or adding a
provider is not possible without rewriting application code.

The project could have been a cleaner JEV SDK. That would have been simpler, but it would
have competed on the same axis — number of endpoints and helpers — and inherited the same
coupling.

## Decision

`DecisionKit.Core` models decisions and nothing else. It contains no provider URL, header,
credential, DTO, endpoint name or protocol JSON, and it has zero package references. JEV is
one implementation of `IDecisionProvider`, shipped in a separate package that depends on
`Core`. The dependency direction is one-way and is enforced by review.

A JEV feature that cannot be expressed provider-neutrally stays in `DecisionKit.Jev` behind
a JEV-specific API. It does not widen `IDecisionProvider`.

## Consequences

### Positive

- The application's domain model survives a provider change.
- `Core` is testable with no network, no credentials and no vendor account.
- The public surface of `Core` stays small and can stabilize quickly.
- Local rule engines, mocks and future providers are first-class, not workarounds.

### Negative

- Two mapping layers exist where an SDK would have none, which is more code and more tests.
- A provider capability that has no neutral expression is reachable only through a
  provider-specific API, so the "swap the provider" promise has documented limits.
- Contributors must understand the layering before they can add a feature.

### Neutral

- The project is positioned as decision infrastructure rather than as a vendor SDK, which
  changes how the README and the package descriptions are written.

## Alternatives considered

### A single JEV SDK package

Rejected. It reproduces the coupling the project exists to remove, and it makes the domain
model a function of the vendor's schema.

### A core that references the provider for convenience

Rejected. Once `Core` can name a JEV type, the boundary stops being checkable and erodes
one pull request at a time.
