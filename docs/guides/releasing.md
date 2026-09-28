# Releasing

Releases are cut from `main` by a maintainer. The process is deliberately manual up to the
tag, and fully automated after it.

## Versioning

The project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

| Change | Bump |
| --- | --- |
| Breaking public API or behaviour change | `MAJOR` |
| Backward-compatible capability | `MINOR` |
| Backward-compatible bug fix | `PATCH` |

While the major version is `0`, the public API may change in any minor release. That
allowance ends at `1.0.0`.

All packages share a single version, declared once in `VersionPrefix` in
`Directory.Build.props`.

## Steps

1. **Confirm CI is green on `main`** for the commit you intend to release.

2. **Review the public API diff.** `git diff` on `src/*/PublicAPI.Unshipped.txt` since the
   previous release is the exact list of what this version adds or removes. Anything
   surprising there is a design conversation, not a release step.

3. **Promote the public API.** Move every line from each `PublicAPI.Unshipped.txt` into the
   corresponding `PublicAPI.Shipped.txt`, keeping `#nullable enable` as the first line of the
   shipped file, and leave the unshipped file containing only `#nullable enable`. Shipped
   entries are sorted; the analyzer reports `RS0024`/`RS0025` if they are not.

4. **Finalize the changelog.** Move the entries under `## [Unreleased]` into a new
   `## [x.y.z] - YYYY-MM-DD` section, and add the comparison link at the bottom of the file.

5. **Bump the version.** Set `VersionPrefix` in `Directory.Build.props` to `x.y.z`.

6. **Verify locally.**

   ```bash
   dotnet format DecisionKit.slnx --verify-no-changes
   dotnet build DecisionKit.slnx -c Release
   dotnet test --solution DecisionKit.slnx -c Release
   dotnet pack DecisionKit.slnx -c Release -o ./nupkg
   ```

   Inspect the produced `.nupkg` files: correct version, README included, symbols package
   present, no unexpected dependency.

7. **Commit and merge** through a pull request:

   ```text
   chore(build): release x.y.z
   ```

8. **Tag the merge commit** and push the tag:

   ```bash
   git tag -a vx.y.z -m "vx.y.z"
   git push origin vx.y.z
   ```

9. **Let the release workflow run.** It builds, tests, packs, pushes to NuGet using the
   `NUGET_API_KEY` secret from the `nuget` environment, and creates the GitHub release.

10. **Verify** that the packages are listed on NuGet and that the GitHub release notes are
    correct.

## After the release

Set `DecisionKitBaselineVersion` in `Directory.Build.props` to the version you just published.
That switches on baseline package validation, so from the next build onwards a change that
breaks the released contract fails locally and in CI instead of reaching a consumer.

The property is empty until the first package exists on nuget.org, because validation cannot
compare against something that was never published.

## Pre-release versions

For a preview, tag `vx.y.z-preview.N`. The workflow publishes it as a pre-release package;
NuGet does not surface it as the latest stable version.

## After a breaking change

A `MAJOR` release must ship with:

- a changelog section that names each break and its migration path;
- a migration guide in `docs/guides/`;
- an ADR if the break follows from an architectural decision.
