# 0003. Restrict shipping dependencies to Microsoft-maintained packages

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

A library's dependencies become its consumers' dependencies. Every added package is a
version-conflict risk, a supply-chain surface, and a constraint on trimming and AOT.
The analysis asks for minimal external dependencies in `Core`, `System.Text.Json` for
serialization, and no mandatory OpenTelemetry or Newtonsoft.Json.

Resilience libraries and mapping libraries are the usual candidates for a decision engine.
Both would be convenient and both would be visible in every consumer's dependency graph.

## Decision

- `DecisionKit.Core` has **zero** package references.
- Other shipping packages may reference only Microsoft-maintained framework and
  `Microsoft.Extensions.*` packages.
- Retry, backoff and idempotency are implemented in-house against an injectable time
  abstraction, rather than taken from a third-party resilience library.
- Mapping is hand-written. No mapping library.
- Test and benchmark projects may use third-party tooling — a test framework, a coverage
  collector, BenchmarkDotNet. That is the only exception, and it holds because none of it
  reaches a shipping package.
- All versions are declared centrally in `Directory.Packages.props`; project files carry no
  inline `Version` attribute. `NuGet.config` pins `nuget.org` as the only source, and NuGet
  auditing runs at build time.
- Adding any dependency requires a new ADR.

## Consequences

### Positive

- Consumers inherit almost nothing, so version conflicts are unlikely.
- Retry semantics are ours, which matters because the analysis treats retry as part of the
  operation's meaning rather than as HTTP plumbing.
- Trimming and AOT behaviour depends only on code we control.
- The supply-chain surface is small and auditable.

### Negative

- We write and test backoff, jitter and `Retry-After` handling ourselves.
- We forgo the ecosystem integration that a well-known resilience library would bring.

### Neutral

- Optional integration packages (for example telemetry) may still be added later, as long
  as they are separate packages that consumers opt into.

## Alternatives considered

### Depend on a third-party resilience library

Rejected for the first release. It would impose a transitive dependency on every consumer
to solve a problem whose semantics we need to own anyway.

### Allow third-party packages case by case with no rule

Rejected. Without a default of "no", the dependency graph grows by accretion and nobody is
accountable for it.
