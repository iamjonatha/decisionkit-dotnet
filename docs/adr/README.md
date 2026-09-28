# Architecture Decision Records

An ADR records a decision that is expensive to reverse, together with the context that made
it necessary and the consequences accepted along with it. Read them before proposing an
architectural change.

Records are immutable. A decision is changed by adding a new record that supersedes the old
one, never by editing history.

## Writing a new record

1. Copy [`0000-template.md`](0000-template.md) to `NNNN-short-title.md`, using the next
   free number.
2. Write it in English, in the imperative, and keep it short.
3. Open it as a pull request in `Proposed` status; it becomes `Accepted` when merged.
4. Add it to the index below.

## Index

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-provider-independent-core.md) | Keep the core provider-independent | Accepted |
| [0002](0002-target-frameworks.md) | Target net8.0 and net10.0 | Accepted |
| [0003](0003-dependency-policy.md) | Restrict shipping dependencies to Microsoft-maintained packages | Accepted |
| [0004](0004-dependency-injection-placement.md) | Keep dependency injection for every package in DecisionKit.Extensions | Accepted |
| [0005](0005-answer-type-inferred-from-question.md) | Infer the answer type from the question | Accepted |
| [0006](0006-error-model.md) | Report failures with a shallow exception hierarchy and a category | Accepted |
| [0007](0007-time-abstraction.md) | Use the BCL TimeProvider as the only clock abstraction | Accepted |
| [0008](0008-jev-wire-boundary.md) | Keep the JEV wire format behind dedicated DTOs and source-generated JSON | Accepted |
| [0009](0009-provider-specific-question-types.md) | Keep provider-specific question types in the provider package | Accepted |
| [0010](0010-retry-is-opt-in.md) | Make retrying opt-in for the JEV provider | Accepted |
| [0011](0011-configuration-binding-shape.md) | Bind configuration onto a separate mutable settings type | Accepted |
