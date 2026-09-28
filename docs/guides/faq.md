# FAQ

## Is this a TypeSafe SDK?

No. It is an independent, community-maintained client, not affiliated with or endorsed by
TypeSafe. More importantly, it is not shaped like an SDK: the decision domain lives in
`DecisionKit.Core`, which knows nothing about JEV, HTTP or JSON. JEV is the first provider, not
the architecture.

## Why not just call the JEV API directly?

You can, and for one call in one place you probably should. DecisionKit earns its place when
decisions are part of your domain: questions become values you can name, test and reuse, the
answer type comes from the question rather than from a cast, and replacing the provider does
not rewrite your application model.

## Can I use it without JEV?

Yes. `DecisionKit.Core` plus `DecisionKit.Testing` is a complete, runnable system with no
network. Write your own provider against `IDecisionProvider` — see
[writing a provider](../providers/custom-provider.md).

## Which package do I reference?

`DecisionKit.Extensions` in a host application: it pulls in `Core` and `Jev`.
`DecisionKit.Core` in a domain library that must not know which provider you use.
`DecisionKit.Testing` in test projects.

## Why does `ScoreQuestion` fail against JEV?

Because JEV does not score against a numeric range. It scores against described levels, and the
description is what the model rates against, so it is mandatory. Use `JevScoreQuestion`, which
carries the rubric. The answer is still an ordinary `ScoreAnswer`. See
[the JEV provider guide](../providers/jev.md#scores-are-a-rubric-not-a-range).

## Why is retrying off by default?

JEV has no idempotency mechanism. A repeated call is a second evaluation: it is billed again,
and it may answer differently. A library that quietly doubled your bill after a timeout would
be making a decision that belongs to you. Turn it on with `MaxAttempts` —
[ADR-0010](../adr/0010-retry-is-opt-in.md).

## Why does setting `Idempotency` throw?

Because the alternative is lying. An idempotency key means something only when the service
deduplicates on it, and JEV does not. `JevRetryPolicy.Idempotency` accepts only `Disabled`, and
rejects anything else where you set it rather than ignoring it at run time.

## Why are `RetryableStatusCodes` and `NonRetryableStatusCodes` empty?

They are a per-deployment override, not the classification baseline. The error mapper has
already judged every failure and recorded a `DecisionRetryHint`. You fill these lists in only
when something in front of JEV — a gateway, a proxy — reports a status that means something
different in your deployment.

## How do I test code that makes decisions?

Replace the provider. `FakeDecisionProvider` scripts answers and injects failures;
`DeterministicDecisionProvider` derives stable answers from the request, which is what you want
in a sample or a snapshot test. Neither touches the network. See the
[testing guide](testing.md).

## How do I simulate latency or a timeout without waiting?

Give the provider a `FakeTimeProvider`. Every delay in the library goes through `TimeProvider`,
so virtual time advances instantly — [ADR-0007](../adr/0007-time-abstraction.md).

## What happens when the provider returns something you do not model?

It is preserved. `UnknownQuestion` and `UnknownAnswer` carry the provider's own type name and
the raw payload, and `DecisionOptions.UnknownTypeHandling` chooses between `Preserve` (the
default) and `Fail`. Unknown is never discarded.

## Does it work with Native AOT?

Yes, all four packages, and it is verified on every CI run by publishing `samples/Aot` with
full trimming and treating every trim warning as an error. See
[AOT and trimming](aot-and-trimming.md).

## Does it log my API key or my data?

No. `JevApiKey.ToString()` is redacted, so an interpolated log message cannot leak it. Request
and response bodies are not logged. Raw payload capture is opt-in via `PreserveRawPayloads`,
and what it captures is your input, so treat it as such. See
[handling secrets](secrets.md) and [`SECURITY.md`](../../SECURITY.md).

## Can I use more than one provider at a time?

Yes. `AddJevProvider(name, …)` registers named providers, each with its own configuration
section, credential and `HttpClient`. See the
[dependency injection guide](dependency-injection.md).

## Can I wrap the provider with caching, metrics or logging?

Yes, with `Decorate`:

```csharp
.AddJevProvider(builder.Configuration.GetSection("Jev"))
.Decorate((container, inner) => new MeteredDecisionProvider(inner, container.GetRequiredService<IMeterFactory>()));
```

A decorator is just an `IDecisionProvider` that holds another one. Shipping first-class
caching and metrics decorators is planned, not present.

## Is there telemetry?

Not yet. An optional `DecisionKit.Telemetry` package with `ActivitySource`, metrics and latency
histograms is the first item after the first release. The library logs through the
`ILogger` you give it and nothing else.

## Does it batch?

No. `DecisionProviderCapabilities.SupportsBatching` exists so a provider can declare it, but
batching will only be implemented for a provider that supports it natively. A loop is not a
batch.

## Which .NET versions?

`net8.0` and `net10.0`. The public surface is identical on both — there is no conditional
compilation anywhere in `src`. See [ADR-0002](../adr/0002-target-frameworks.md).

## Why are there so few dependencies?

By policy: shipping projects take only Microsoft-maintained framework and extension packages.
Test and benchmark tooling is the single documented exception, because it never reaches a
package. Adding anything else needs an ADR — [ADR-0003](../adr/0003-dependency-policy.md).

## Is the API stable?

No. It is unstable until `1.0.0`. Every public symbol is tracked in `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`, so an addition or a removal is visible in the diff of the pull
request that made it, and package validation fails a build that breaks a released contract.

## How do I report a security issue?

Privately. See [`SECURITY.md`](../../SECURITY.md). Do not open a public issue.
