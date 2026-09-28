# 0011. Bind configuration onto a separate mutable settings type

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** @iamjonatha

## Context

`DecisionKit.Extensions` puts the JEV provider in a `IServiceCollection`, so an application can write
`builder.Services.AddDecisionKit().AddJevProvider(builder.Configuration.GetSection("Jev"))`
and get a working, authenticated, logged, clock-aware provider.

That request runs into the shape the provider already has. `JevProviderOptions`,
`JevMappingOptions`, `JevEndpoint` and `JevRetryPolicy` are immutable records with `init`
accessors that validate on assignment: an endpoint that is not an absolute address, a
timeout that cannot elapse, a backoff factor below one and an idempotency policy this
provider cannot honour all throw at the point they are set. That is the behaviour
[ADR-0010](0010-retry-is-opt-in.md) relies on — a deployment learns about an impossible retry
policy while configuring it.

A configuration binder cannot write into that shape. `IConfiguration` binding needs settable
properties and a parameterless constructor, and `IOptions<T>` needs to hand the same instance
to several `Configure` callbacks in turn. Worse, binding onto a mutable clone with
non-nullable properties loses the distinction the defaults depend on: a `MaxAttempts` of `0`
would be indistinguishable from a `MaxAttempts` nobody wrote, and every unmentioned setting
would arrive as a deliberate zero.

Two further constraints apply. The shipping projects are `IsTrimmable` and `IsAotCompatible`
with warnings as errors, and `ConfigurationBinder.Bind` is annotated
`RequiresUnreferencedCode`. The source-generated binder does not help: it is driven by the
call site, and here the call site is inside this library rather than in the application that
can see the settings type.

## Decision

Configuration binds onto `JevProviderSettings` and `JevRetrySettings` — mutable, public,
and nullable in every member — and `JevSettingsMapper` translates the result into the
validated immutable options once, when the provider is built.

The nullability is the design, not laziness. `null` means *not configured*, and every unset
member falls back to `JevProviderOptions.Default`, `JevMappingOptions.Default` or
`JevRetryPolicy.None`. Defaults live in exactly one place and the settings type repeats none
of them.

Binding is written by hand in `JevSettingsBinder`. It reads named keys, converts each with an
invariant culture, and rejects a value it cannot read with a message naming the configuration
path, the value found and what was expected. No reflection, no trimming warning, and a
failure that points at a line in `appsettings.json`.

Validation runs twice, on purpose. `JevSettingsValidator` is registered through
`ValidateOnStart()`, so a misconfigured deployment fails while the host is starting rather
than on its first decision; the same mapper runs again when the provider is built, because
`Configure` callbacks added after registration can still change the settings.

Every part of a registration is named. Options, HTTP client (`DecisionKit.Jev.<name>`),
credential provider and provider itself are keyed by the registration name, which defaults to
`JevProtocol.ProviderName`. Two JEV registrations pointing at different endpoints coexist
without either knowing about the other.

The keyed `IDecisionProvider` is the single instance, and an unkeyed registration forwards to
it, so `GetRequiredService`, `GetRequiredKeyedService` and `IEnumerable<IDecisionProvider>`
all observe the same object.

## Consequences

### Positive

- The immutable options keep validating on assignment. Nothing in the provider was weakened
  to make it configurable.
- A configuration mistake is reported by path and value, at startup, without the API key
  appearing in the message — `JevProviderSettings.ToString()` renders it as `<redacted>` and
  no failure message quotes it.
- The extensions package is trim- and AOT-clean, with no `RequiresUnreferencedCode` call in
  it.
- The settings type is a documented, stable description of what may appear under a `Jev`
  section, independent of the internal shape of the options.
- `WithCredentials(...)` and `Decorate(...)` may be called before or after the settings are
  supplied: they mutate a registration object that the factories capture, so nothing is read
  until the provider is resolved.

### Negative

- Two types describe the same settings, and adding an option means touching both plus the
  mapper and the binder. The mapper is the single seam between them, which keeps the drift
  visible, but the duplication is real.
- The binder must be extended by hand for every new key. This is the cost of not using
  reflection, and it buys the failure messages.
- A `Retry` section that is present but says nothing about `MaxAttempts` leaves retrying off,
  because the remaining keys describe *how* to retry rather than *whether* to. This matches
  `JevRetryPolicy` itself, where `MaxAttempts` is the switch, but it does mean a section full
  of delays can look configured while doing nothing.

### Neutral

- Only the API key is re-read after a configuration reload; the credential provider asks
  `IOptionsMonitor` on every call. The endpoint, timeouts and retry policy are read once when
  the provider is built, because rebuilding a provider under a live caller would change the
  meaning of an in-flight operation. Rotating a key is the case that matters operationally.

## Alternatives considered

**Make `JevProviderOptions` mutable and bindable.** Rejected. It would move every validation
from assignment to a validator, and the constructor-time guarantees the rest of the library
relies on would become conventions.

**Bind with `ConfigurationBinder.Bind` and suppress the trimming warning.** Rejected. The
suppression would be a lie in an `IsTrimmable` assembly, and the failure messages would
degrade to whatever the binder says about a `TimeSpan` it could not parse.

**Ship a source-generated binder.** Rejected for now. The generator runs in the project that
calls it, so it would have to be invoked from the application, which is exactly the coupling
this package exists to remove.

**Register the provider unkeyed only.** Rejected. A second JEV registration is a real
scenario — one endpoint for interactive traffic and one for batch — and keyed services are
how the platform expresses it since .NET 8.

**Use `Microsoft.Extensions.Http.Resilience` for the HTTP client pipeline.** Rejected, and
already foreclosed by [ADR-0003](0003-dependency-policy.md) and
[ADR-0010](0010-retry-is-opt-in.md). The client is configured with an infinite timeout so
that DecisionKit's three budgets are the only ones, and an application remains free to add
its own handlers to the returned `IHttpClientBuilder`.
