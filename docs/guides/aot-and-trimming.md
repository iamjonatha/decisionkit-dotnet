# AOT and trimming

All four packages are trim-safe and Native AOT compatible. This page says what that means, how
it is enforced, and what you have to do on your side.

## The claim

Every shipping project sets:

```xml
<IsTrimmable>true</IsTrimmable>
<IsAotCompatible>true</IsAotCompatible>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
```

Warnings are errors in this repository, so an unannotated reflection call fails the build of
the package that introduced it. Nothing needs to be remembered at review time.

## How it is enforced

`samples/Aot` is a Native AOT console application that exercises the four things a naive
implementation would have done with reflection:

1. binding configuration onto the provider settings;
2. composing the container and resolving `IDecisionProvider`;
3. serializing a JEV request and deserializing a JEV response;
4. mapping a wire answer onto `ChoiceAnswer<TOption>`, generic over one of your own types.

```bash
dotnet publish samples/Aot -c Release -r linux-x64
./artifacts/publish/DecisionKit.Samples.Aot/release_linux-x64/DecisionKit.Samples.Aot
```

It publishes with `TrimMode=full` and `TrimmerSingleWarn=false`, so every warning is reported
individually and every one of them is an error. CI runs this on every push, and runs the
resulting binary afterwards — a clean publish that crashes on start would otherwise pass.

On Windows the native link step needs the Visual Studio C++ toolchain, and `vswhere.exe` must
be reachable:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish samples/Aot -c Release -r win-x64
```

The resulting self-contained binary is roughly 4 MB.

## The three design decisions that make it work

**Source-generated JSON.** JEV payloads go through a `JsonSerializerContext`, exposed as
`JevJsonSerialization`. Nothing is discovered at run time, so nothing can be trimmed away.
The `JsonTypeInfo<T>` for each wire model is public if you need to plug the same metadata into
your own serializer:

```csharp
JsonTypeInfo<JevRequest> info = JevJsonSerialization.RequestTypeInfo;
```

**A hand-written settings binder.** `AddJevProvider(IConfiguration)` does not use the
reflection-based configuration binder. `JevProviderSettings` is read explicitly, key by key,
so the binding survives trimming and every accepted key is visible in the source. See
[ADR-0011](../adr/0011-configuration-binding-shape.md).

**A non-generic question seam.** Mapping a wire answer onto `ChoiceAnswer<Department>` would
normally require `MakeGenericType` — the single most trim-hostile call in .NET. Instead the
question builds its own answer through `IChoiceQuestion`:

```csharp
Answer answer = choiceQuestion.CreateAnswer(outcome);
```

The generic instantiation already exists, because your code created the question. The provider
never constructs a type it did not see at compile time.

## What you have to do

**Use option types whose text means something.** An option travels to JEV as a string: its own
culture-invariant text, produced by `JevOptionLabel`. An enum member or a string works
directly. Whatever you choose, the label is what the model is asked to judge, so an opaque
code is a bad option name for reasons that have nothing to do with trimming.

**Do not serialize DecisionKit types with a reflection-based serializer.** If you persist a
`DecisionResult`, write your own DTO and your own source-generated context. The domain types
are not annotated for serialization, and nothing about them is stable enough to be a storage
format.

**Watch the untyped seams.** `Metadata`, `Properties` and `UnknownProperties` are
`object?`-valued. Reading a `double` or a `string` out of them is fine. Casting to a type you
expect the provider to have produced is not: under trimming that type may not exist.

**Bring your own `JsonSerializerContext` for your own payloads.** Nothing in DecisionKit
serializes your application types.

## If you hit a trim warning

A trim or AOT warning that points into a DecisionKit assembly is a defect, not a
configuration problem. Open an issue with the warning code, the assembly and the publish
command. Suppressing it in your own project hides a real hole.

A warning that points into `Microsoft.Extensions.Configuration.Binder` usually means something
in your application is calling `IConfiguration.Get<T>()`. That is not DecisionKit; enable the
configuration binding source generator, or bind explicitly.

## Trimming without AOT

Everything above applies to `PublishTrimmed` on its own. AOT is the stricter gate, so it is the
one that is tested; if AOT is clean, trimming is clean.
