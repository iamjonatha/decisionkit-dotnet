# Roadmap

What exists today, and what is being considered next. This is a direction, not a commitment,
and there are no dates.

## Today

The first release is feature-complete for what it claims.

| Area | State |
| --- | --- |
| Decision domain model | Questions, answers, values, results, typed lookup |
| Provider abstraction | `IDecisionProvider`, capabilities, the failure model |
| TypeSafe JEV provider | Protocol, HTTP transport, authentication, resilience |
| Hosting integration | `IServiceCollection`, configuration, `IHttpClientFactory`, logging |
| Testing support | Fake and deterministic providers, virtual time, assertions |
| Quality gates | Public API tracking, package validation, Native AOT, benchmarks |

`net8.0` and `net10.0`, with an identical public surface on both.

## Stability

The public API is unstable until `1.0.0`: it may change in any minor release while the major
version is `0`. Every public symbol is tracked in `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`, so a change to the surface is visible in the diff of the pull
request that makes it, and package validation fails a build that breaks a released contract.

## Next

In rough priority order. Each item is gated by its own decision record before any code is
written, and each one that lands must leave the existing API working.

1. **Telemetry.** An optional `DecisionKit.Telemetry` package: `ActivitySource`, metrics,
   tracing, latency histograms.
2. **Capability model extensions.** More of what a provider can declare, so a caller has to
   guess less.
3. **Provider decorators as first-class components.** Caching, metrics and logging, shipped
   rather than described.
4. **Batching** — only for a provider that supports it natively. A loop is not a batch.
5. **Streaming** through `IAsyncEnumerable<DecisionEvent>`, introduced without breaking the
   existing API.
6. **Analyzers.** Duplicate question identifiers, provider mismatch, incompatible answer
   usage, retry configured on an operation that is not safe to repeat.
7. **`DecisionKit.Decisions`.** Weighted and composite scores, entropy, variance, confidence
   thresholds, routing, policy evaluation — consuming `DecisionResult` and knowing no
   provider.
8. **A caching decorator.**
9. **Additional providers**, which is also the real test of whether
   [ADR-0001](adr/0001-provider-independent-core.md) held.

## What is deliberately not planned

- **A prompt-engineering surface.** The question is the prompt. A second, untyped way to talk
  to a provider would undo the point of the library.
- **A provider-agnostic model registry.** Model names are provider vocabulary and belong in
  the provider package.
- **Synchronous overloads.** Every provider call is remote.
- **Optional parameters on public API.** Overloads or an options object, always.

## Influencing it

Open an issue describing the problem before proposing the API. An item on this list moves
because someone has a use case it blocks, not because it is next in the numbering.

Anything that would put provider knowledge into `DecisionKit.Core` is out of scope by
construction — see [ADR-0001](adr/0001-provider-independent-core.md).
