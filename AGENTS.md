# AGENTS.md

Operating rules for AI coding agents working in this repository. Human contributors follow
[`CONTRIBUTING.md`](CONTRIBUTING.md); everything there applies here as well. This file adds
the constraints that agents get wrong most often.

## Hard rules

1. **English only.** Code, identifiers, XML documentation, comments, commit messages, pull
   request text, log messages and exception messages. No exceptions, regardless of the
   language the user writes to you in.
2. **Never add a `Co-authored-by` trailer.** Never add `Generated with`, `Assisted by`, or
   any other tool attribution to a commit, pull request or code comment. Commits carry an
   author and nothing else.
3. **Never commit a secret.** No API key, token or credential in source, tests, samples,
   fixtures or documentation. Use placeholders such as `<your-api-key>`.
4. **`DecisionKit.Core` never learns about a provider.** If a change requires Core to know
   a URL, a header, a DTO or an endpoint name, the design is wrong. Stop and say so.
5. **Never discard unknown provider data.** Map what you know, preserve the rest.
6. **Never break cancellation.** Every public asynchronous method takes a
   `CancellationToken` and passes it down to the transport.
7. **Never add a non-Microsoft runtime dependency** without an approved ADR.

## Before you change code

- Read [`docs/architecture/overview.md`](docs/architecture/overview.md) and the relevant
  ADRs in [`docs/adr/`](docs/adr/). The architecture is deliberate; do not re-derive it.
- Check [`docs/roadmap.md`](docs/roadmap.md) for what is in scope. Do not build a later
  item early, and do not build something the roadmap rules out.
- Prefer the smallest change that addresses the root cause. Do not refactor adjacent code
  that the task did not ask about.

## While you change code

- Fix the root cause, not the symptom. If the request asks for a patch over a design
  problem, push back and explain the simpler alternative.
- Do not introduce `try`/`catch` blocks that only rethrow or swallow. Let the error reach
  the boundary that can act on it.
- Do not add optional parameters to public API. Use overloads or an options object.
- Keep methods small and cyclomatic complexity low. Split rather than comment a long method.
- Comment only non-obvious logic, protocol quirks and deliberate workarounds.
- Remove orphaned code that *your own change* made dead. Report pre-existing dead code
  instead of deleting it.

## Before you finish

Run and pass all of these:

```bash
dotnet format DecisionKit.slnx --verify-no-changes
dotnet build DecisionKit.slnx -c Release
dotnet test --solution DecisionKit.slnx -c Release
```

Then confirm:

- [ ] Tests cover the new behaviour, and bug fixes started from a failing test.
- [ ] Tests are deterministic: no real clock, no real network, no real delay.
- [ ] `CHANGELOG.md` has an entry under `## [Unreleased]` for any user-visible change.
- [ ] Public API additions carry XML documentation.
- [ ] Commit messages follow Conventional Commits and contain no trailers.

## Environment notes

- Solution file is `DecisionKit.slnx`. `dotnet test` runs in Microsoft.Testing.Platform mode
  (configured in `global.json`), so pass the solution with `--solution`.
- The repository pins its own `NuGet.config` with only `nuget.org`. Do not add a feed.
- Working-tree line endings are LF, enforced by `.gitattributes` and `.editorconfig`.
- Warnings are errors. A build that emits a warning is a build that fails.

## When to stop and ask

Stop and ask the user rather than guessing when:

- the change would alter the public API shape or break a consumer;
- the change requires a new dependency;
- the requirement conflicts with an ADR or with a hard rule above;
- the protocol behaviour of the provider is ambiguous and no fixture documents it.
