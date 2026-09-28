# Contributing to DecisionKit

Thanks for considering a contribution. This document is the contract between contributors
and maintainers. Everything here is enforced by review, and where possible by tooling.

---

## 1. Language policy

**English is the only language of this project.** No exceptions.

| Artifact | Language |
| --- | --- |
| Source code, identifiers, XML documentation | English |
| README, official documentation, ADRs, samples | English |
| Commit messages | English |
| Pull request titles, descriptions, review comments | English |
| Issue titles and descriptions | English |
| Log messages, exception messages, diagnostics | English |
| Code comments | English |

Non-English contributions are closed with a request to resubmit in English. There is no
exception.

---

## 2. Commit conventions

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:** `feat`, `fix`, `perf`, `refactor`, `docs`, `test`, `build`, `ci`, `chore`, `revert`.

**Scopes:** `core`, `jev`, `extensions`, `testing`, `docs`, `build`, `ci`, `samples`.

**Rules:**

- Subject in English, imperative mood, lower case, no trailing period, max 72 characters.
- Body explains *why*, not *what*. The diff already says what.
- Breaking changes use `!` after the scope and a `BREAKING CHANGE:` footer.
- Reference issues with `Closes #123` / `Refs #123`.
- **Never add `Co-authored-by` trailers.** Not for pair programming, not for AI assistants,
  not for rebased work. Authorship is the commit author field and nothing else.
- Never add tool attribution footers of any kind (`Generated with`, `Signed-off-by` is
  allowed only if a DCO is later adopted, which it currently is not).

Good:

```
feat(core): add value-based QuestionId identifier

String identifiers allowed silent typos and made result lookup unverifiable.
A validated value object makes duplicate detection and lookup deterministic.

Closes #12
```

Bad:

```
Aggiunto QuestionId
fix stuff
feat(core): added the QuestionId class.

Co-authored-by: Someone <someone@example.com>
```

---

## 3. Branching and pull requests

- `main` is always releasable. Direct pushes are disabled.
- Work happens on `feat/<short-slug>`, `fix/<short-slug>`, `docs/<short-slug>`.
- One logical change per pull request. Split unrelated refactors out.
- Rebase onto `main` before requesting review. Merge commits into feature branches are discouraged.
- Pull requests are squash-merged; the squash subject must itself be a valid Conventional Commit.

---

## 4. Architectural rules

These are non-negotiable and a pull request that violates them is rejected regardless of quality.

1. **`DecisionKit.Core` never depends on a provider.** No JEV URLs, headers, API keys,
   DTOs, endpoint names or protocol JSON. Core must compile and its tests must pass with
   no knowledge that TypeSafe exists.
2. **Dependency direction is one-way:** `DecisionKit.Jev` → `DecisionKit.Core`. Never the reverse.
3. **Domain types are not wire DTOs.** Protocol DTOs live in the provider package and are mapped explicitly.
4. **Mapping is explicit.** No reflection-based or convention-based magic mapping.
5. **Unknown data is preserved, never discarded.** Known shapes map to typed models; unknown
   shapes map to the raw/unknown fallback. `unknown -> discard` is a bug.
6. **Every public asynchronous method accepts a `CancellationToken`** and propagates it to
   the transport. Never create a detached token that defeats caller cancellation.
7. **No synchronous API performs I/O.**
8. **No secret ever reaches a log, an exception message, or a diagnostic dump.** That includes
   API keys, `Authorization` headers and request payloads.
9. **Retry is a semantic decision, not an HTTP detail.** A retried non-idempotent POST must be an
   explicit, documented choice.
10. **A type is `public` only if a real consumer must use it.** Everything else is `internal`.

---

## 5. Coding standards

- Target frameworks: `net8.0` and `net10.0`. Code must compile cleanly on both.
- Nullable reference types are enabled everywhere. Warnings are errors.
- File-scoped namespaces. Namespace must match folder structure.
- `using` directives outside the namespace, `System` first.
- Naming: PascalCase for types and members, `camelCase` for locals and parameters,
  `_camelCase` for private instance fields, `s_camelCase` for private static fields,
  `TPascalCase` for type parameters, `I` prefix for interfaces, `Async` suffix for
  asynchronous methods. All of this is enforced by `.editorconfig`.
- Prefer immutable types: `readonly record struct`, `sealed` classes, `readonly` fields.
- Keep cyclomatic complexity low. If a method needs a comment to explain its flow, split it.
- Comment only non-obvious logic, protocol quirks and deliberate workarounds. Do not narrate code.
- Every public member that is not self-evident carries XML documentation.
- No `try`/`catch` that only rethrows or swallows. Let errors reach the boundary that can decide.
- No optional parameters on public API. Use overloads or an options object, so that adding
  a parameter is never a silent binary break.

Run before pushing:

```bash
dotnet format DecisionKit.slnx --verify-no-changes
dotnet build DecisionKit.slnx -c Release
dotnet test --solution DecisionKit.slnx -c Release
```

`dotnet test` runs in Microsoft.Testing.Platform mode, configured in `global.json`, which
is why the solution is passed with `--solution`.

---

## 6. Dependency policy

- Shipping packages may reference **only** Microsoft-maintained framework and
  `Microsoft.Extensions.*` packages.
- `DecisionKit.Core` targets **zero** package references.
- Third-party packages are allowed only in test and benchmark projects, and only for the
  test framework, coverage collection and BenchmarkDotNet.
- All versions are declared centrally in `Directory.Packages.props`. No inline `Version=`
  attributes in project files.
- Adding any new dependency requires an ADR in `docs/adr/`.

---

## 7. Testing requirements

- Every behavioural change ships with tests. Bug fixes start with a failing test.
- Tests are deterministic. No real clocks, no real network, no real delays; retry and backoff
  are tested through injected time abstractions.
- Provider tests never call the live TypeSafe service. Protocol behaviour is covered by
  contract tests against recorded fixtures.
- Test naming: `MethodOrScenario_ExpectedBehaviour`.
- Arrange/Act/Assert, one behaviour per test.

---

## 8. Documentation requirements

A change is not done until the documentation matches it:

- New or changed public API → XML documentation.
- New capability → a guide in `docs/guides/`.
- Architectural decision → an ADR in `docs/adr/` using the existing template.
- User-visible change → a `CHANGELOG.md` entry under `## [Unreleased]`.

---

## 9. Pull request checklist

- [ ] Everything is in English, including the commit messages.
- [ ] No `Co-authored-by` or tool attribution trailers.
- [ ] Commits follow Conventional Commits.
- [ ] `dotnet format DecisionKit.slnx --verify-no-changes` passes.
- [ ] `dotnet build -c Release` is warning-free on both target frameworks.
- [ ] `dotnet test -c Release` passes.
- [ ] Core has no provider-specific code or dependency.
- [ ] Cancellation is honoured end to end.
- [ ] Unknown provider data is preserved.
- [ ] No secret is logged or embedded in an exception.
- [ ] Public API additions are documented and intentional.
- [ ] `CHANGELOG.md` is updated.

---

## 10. Releasing

See [`docs/guides/releasing.md`](docs/guides/releasing.md).

---

## 11. Code of conduct

Participation is governed by [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).
