# 0002. Target net8.0 and net10.0

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** @iamjonatha

## Context

The library must be adoptable by applications on the current LTS release and by
applications that have not yet migrated to it. At the time of writing, `net10.0` is the
current LTS and `net8.0` is the previous LTS, still in support.

The analysis left the target open and asked that `netstandard2.0` be added only against a
concrete legacy requirement, because it would block modern API, AOT and type-safety work.

## Decision

Shipping packages multi-target `net8.0` and `net10.0`. Test projects build and run against
both. `netstandard2.0` and .NET Framework are not targeted.

Samples target `net10.0` only, because they demonstrate usage rather than compatibility.

`net8.0` is dropped when it leaves support, in a release that bumps the minor version and
is announced in the changelog.

## Consequences

### Positive

- Covers both supported LTS releases with one package.
- Keeps access to modern `System.Text.Json`, AOT and trimming features.
- Multi-targeting catches accidental use of an API that exists only in the newer runtime.

### Negative

- Build time and CI time roughly double.
- Code that differs per framework needs `#if` guards, which must be kept rare and small.

### Neutral

- Consumers on .NET Framework are not served. That is a deliberate scope decision, not an
  oversight.

## Alternatives considered

### `net10.0` only

Rejected. It excludes applications still on the previous LTS, which is most of the
realistic audience during the project's first year.

### Adding `netstandard2.0`

Rejected. No concrete consumer requires it, and it would constrain the API surface, the
AOT story and the serialization strategy in exchange for a hypothetical benefit.
