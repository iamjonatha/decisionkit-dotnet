# Samples

Six runnable projects. Four of them need nothing but a .NET SDK; the other two need a JEV API
key or a native toolchain. Every sample is part of `DecisionKit.slnx`, so a broken sample is a
broken build.

| Sample | Needs a key | What it demonstrates |
| --- | --- | --- |
| [`Basic`](Basic) | no | Minimal console decision, typed choice, score, multiple questions, error handling, cancellation, unknown question types |
| [`DependencyInjection`](DependencyInjection) | yes | ASP.NET Core minimal API, configuration binding, `AddJevProvider`, error mapping to `ProblemDetails` |
| [`CustomProvider`](CustomProvider) | no | Implementing `IDecisionProvider` from scratch, without touching the JEV package |
| [`Testing`](Testing) | no | `FakeDecisionProvider` and `DecisionAssert` against application logic |
| [`Resilience`](Resilience) | no | How `JevRetryPolicy` computes backoff, honours `Retry-After` and classifies failures |
| [`Aot`](Aot) | no | Native AOT publish with zero trim warnings |

## Running them

```bash
dotnet run --project samples/Basic
dotnet run --project samples/CustomProvider
dotnet run --project samples/Testing
dotnet run --project samples/Resilience
```

`Testing` exits non-zero when an assertion fails, so it doubles as a smoke test.

`DependencyInjection` talks to the real service and therefore needs a credential. The key is
never committed; supply it from the environment:

```bash
export Jev__ApiKey="<your-api-key>"
dotnet run --project samples/DependencyInjection
```

Then:

```bash
curl -s localhost:5000/tickets/triage -H 'content-type: application/json' \
  -d '{"text":"I have been charged twice for the same month."}'
```

`Aot` runs like any other sample, but the point is the publish:

```bash
dotnet publish samples/Aot -c Release -r linux-x64
./artifacts/publish/DecisionKit.Samples.Aot/release_linux-x64/DecisionKit.Samples.Aot
```

Warnings are errors here too, so any reflection introduced in a shipping package fails this
publish before it reaches a user. On Windows use `-r win-x64`; the native link step needs the
Visual Studio C++ toolchain, and `vswhere.exe` must be on `PATH`:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish samples/Aot -c Release -r win-x64
```

## Conventions

Samples target `net10.0` only, have `ImplicitUsings` enabled and are never packed. They are
written to be read: each one prints what it is doing, and the interesting part is the source,
not the output.

`samples/.editorconfig` relaxes CA1859 for samples only. Holding a provider through
`IDecisionProvider` rather than its concrete type is the abstraction the library is about, so
the analyzer advice is wrong here and right everywhere else.
