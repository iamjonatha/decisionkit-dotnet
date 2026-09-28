# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org) measurements for the paths that run on every
decision. They exist to justify an optimization, or to refuse one.

BenchmarkDotNet is a non-Microsoft dependency. It is allowed under the same exception as the
test tooling: it never reaches a shipping package.

## Running

```bash
dotnet run --project benchmarks/DecisionKit.Benchmarks -c Release -- --filter '*'
```

A full run takes several minutes. Narrow it while iterating:

```bash
dotnet run --project benchmarks/DecisionKit.Benchmarks -c Release -- --filter '*JevProtocol*'
dotnet run --project benchmarks/DecisionKit.Benchmarks -c Release -- --list flat
```

## What is measured

| Class | Paths |
| --- | --- |
| `DomainBenchmarks` | Question set construction, request construction, typed and untyped answer lookup, answer enumeration |
| `JevProtocolBenchmarks` | Domain to wire, wire to JSON, JSON to wire, wire to domain, and the full round trip without the network |

The transport is deliberately absent. A network call is measured in milliseconds and would
bury everything these numbers exist to show.

## Reading the numbers

CI runs these with `--job Dry`, which proves they still execute but measures nothing useful.
Timings from a shared runner are noise; take measurements on a quiet machine, and compare
against a baseline you produced the same way.

Allocation counts are the more durable signal. A change that adds allocations to
`MapResponseToDomain` costs every caller on every decision.
