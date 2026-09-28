# 0004. Keep dependency injection for every package in DecisionKit.Extensions

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

`DecisionKit.Core` must not require `Microsoft.Extensions.DependencyInjection`, so the
registration helpers live somewhere else. Two placements are reasonable.

One package per provider — `AddJevProvider` shipped inside `DecisionKit.Jev` — keeps each
provider self-contained, but it forces every consumer of the provider to take a dependency
on the DI and `IHttpClientFactory` stack even when hosting it manually.

One integration package — `DecisionKit.Extensions` referencing both `Core` and `Jev` —
keeps the hosting concern in a single place, at the cost of that package knowing about a
provider.

## Decision

`DecisionKit.Extensions` owns all hosting integration: `IServiceCollection` registration,
`IOptions` binding, `IHttpClientFactory` wiring and logging setup, for `Core` and for the
JEV provider alike. It references both `DecisionKit.Core` and `DecisionKit.Jev`.

This does not weaken [ADR-0001](0001-provider-independent-core.md): the constraint is that
`Core` knows nothing about a provider. `Extensions` is an application-facing convenience
layer and is allowed to know about both.

When a second provider ships, this decision is revisited: the expected outcome is one
`DecisionKit.<Provider>.Extensions` package per provider, with `DecisionKit.Extensions`
reduced to provider-neutral registration.

## Consequences

### Positive

- One obvious place for an application to wire everything up.
- `DecisionKit.Jev` stays usable without any DI dependency, by direct construction.
- The number of packages stays small while there is only one provider.

### Negative

- `DecisionKit.Extensions` pulls in `DecisionKit.Jev` even for an application that uses a
  custom provider only.
- The decision has a known expiry date: a second provider forces a repackaging.

### Neutral

- The repackaging will be a breaking change for the `Extensions` package and must land
  before `1.0.0`, or be scheduled as a major version.

## Alternatives considered

### Registration helpers inside each provider package

Deferred rather than rejected. It is the right shape once more than one provider exists,
but today it would add a DI dependency to the provider package for no benefit.

### A separate `DecisionKit.Jev.DependencyInjection` package now

Rejected. It doubles the package count to solve a problem the project does not yet have.
