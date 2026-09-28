## Summary

<!-- What does this change and why. One paragraph. -->

## Related issues

<!-- Closes #123 / Refs #123 -->

## Type of change

- [ ] `feat` — new capability
- [ ] `fix` — bug fix
- [ ] `perf` — performance
- [ ] `refactor` — no behavioural change
- [ ] `docs` — documentation only
- [ ] `test` — tests only
- [ ] `build` / `ci` / `chore`

## Checklist

- [ ] Everything is written in English, including commit messages
- [ ] No `Co-authored-by` or tool attribution trailers in any commit
- [ ] Commits follow Conventional Commits
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build -c Release` is warning-free on `net8.0` and `net10.0`
- [ ] `dotnet test` passes
- [ ] `DecisionKit.Core` gained no provider-specific code or dependency
- [ ] Cancellation is honoured end to end
- [ ] Unknown provider data is preserved, not discarded
- [ ] No secret reaches a log, an exception message or a diagnostic dump
- [ ] Public API additions are intentional and carry XML documentation
- [ ] `CHANGELOG.md` updated under `## [Unreleased]`

## Breaking changes

<!-- Describe the break and the migration path, or write "None". -->
