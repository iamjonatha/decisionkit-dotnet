# DecisionKit

**A strongly typed, provider-independent decision engine for .NET, with TypeSafe JEV support.**

[![CI](https://github.com/iamjonatha/decisionkit-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/iamjonatha/decisionkit-dotnet/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

DecisionKit lets a .NET application model a decision the way the domain sees it — typed
questions, typed answers, explicit probabilities and scores — and execute that decision
against a pluggable provider. TypeSafe JEV is the first production provider; it is not
the architecture.

> This is **not** another TypeSafe SDK. The decision domain lives in `DecisionKit.Core`
> and knows nothing about JEV, HTTP or JSON. Swapping the provider does not rewrite your
> application model.

---

## Status

**Pre-release — under active development.** The public API is unstable until `1.0.0`.
See the [roadmap](docs/roadmap.md) for what is implemented and what comes next.

---

## Why

Provider SDKs typically fuse the protocol, the transport, the retry policy and the domain
model into one surface. The result works, but the application model ends up shaped by the
vendor's wire format, and replacing the vendor means rewriting the domain.

DecisionKit inverts that:

```text
Application
    |
    v
DecisionKit.Core            <- domain: questions, answers, results, provider abstraction
    |
    +---- DecisionKit.Jev          <- TypeSafe JEV protocol, HTTP, auth, retry
    +---- DecisionKit.Extensions   <- DI, configuration, HttpClientFactory, logging
    +---- DecisionKit.Testing      <- deterministic fakes, request capture, error injection
```

The dependency arrow only ever points at `Core`.

---

## Packages

| Package | Purpose | Dependencies |
| --- | --- | --- |
| `DecisionKit.Core` | Decision domain model and `IDecisionProvider` abstraction | none |
| `DecisionKit.Jev` | TypeSafe JEV provider: protocol, transport, auth, resilience | `Core` |
| `DecisionKit.Extensions` | `IServiceCollection`, `IOptions`, `IHttpClientFactory`, logging | `Core`, `Jev` |
| `DecisionKit.Testing` | Fake and deterministic providers for tests | `Core` |

Target frameworks: `net8.0` and `net10.0`.

---

## Design principles

1. **Provider independence.** `Core` contains no provider URL, header, credential or DTO.
2. **Strong typing.** A question determines the type of its answer at compile time.
3. **Forward compatibility.** Known shapes map to typed models; unknown shapes are preserved
   as raw data. Unknown is never discarded.
4. **Explicit failure.** Validation, authentication, authorization, transport, timeout,
   cancellation, rate limiting, serialization and provider errors are distinguishable.
5. **Resilience is semantics, not plumbing.** Retry, timeout and idempotency are modelled
   as part of the operation's meaning.
6. **Testability by construction.** Deterministic providers and virtual time; no HTTP in tests.
7. **Minimal public surface.** A type is public only when a consumer must use it.

The full rationale lives in [`docs/architecture/`](docs/architecture/).

---

## Quick start

> `DecisionKit.Core` is real and the snippet below compiles against it. `DecisionKit.Jev`
> now calls the TypeSafe JEV service, so `provider` can be a `JevDecisionProvider` — see
> [Using the JEV provider](#using-the-jev-provider) below.

```csharp
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;

var routing = new ChoiceQuestion<Department>(
    new QuestionId("routing"),
    "Which team should handle this ticket?",
    [Department.Billing, Department.Support]);

var frustration = new ScoreQuestion(
    new QuestionId("frustration"),
    "How frustrated is the customer?",
    minimum: 0,
    maximum: 10);

var request = new DecisionRequest(QuestionSet.Create(routing, frustration))
{
    Input = DecisionInput.FromText(ticketText),
    ClientRequestId = RequestId.New(),
};

DecisionResult result = await provider.DecideAsync(request, cancellationToken);

// The answer type comes from the question, not from a cast.
Department team = result.Get(routing).Value.Selection;
double score = result.Get(frustration).Value.Value;
```

Failures are distinguishable without parsing a message:

```csharp
try
{
    return await provider.DecideAsync(request, cancellationToken);
}
catch (DecisionTransientException ex) when (ex.Retry.Retryability == DecisionRetryability.Retryable)
{
    logger.LogWarning("Provider {Provider} asked to retry after {Delay}.", ex.Error.ProviderName, ex.Retry.RetryAfter);
    throw;
}
```

### Using the JEV provider

`JevDecisionProvider` takes an `HttpClient` it never owns and a credential provider it asks
on every call, so a rotated key takes effect without rebuilding anything.

```csharp
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;

var provider = new JevDecisionProvider(
    httpClient,
    new StaticJevCredentialProvider(apiKey));
```

Timeouts, the base address, retrying and the mapping rules are configured together:

```csharp
var provider = new JevDecisionProvider(
    httpClient,
    new StaticJevCredentialProvider(apiKey),
    new JevProviderOptions
    {
        Endpoint = new JevEndpoint(new Uri("https://gateway.internal/jev/")),
        AttemptTimeout = TimeSpan.FromSeconds(10),
        OperationTimeout = TimeSpan.FromSeconds(30),
        Retry = JevRetryPolicy.Standard,
    },
    logger);
```

Retrying is off by default. JEV has no idempotency mechanism, so a repeated call is a second
evaluation that is billed again and may answer differently: turning it on is the
application's decision, not the library's.

The [JEV protocol reference](docs/providers/jev-protocol.md) records the wire contract, the
header precedence, the retry rules and every failure the transport can report.

### With dependency injection

`DecisionKit.Extensions` registers the provider as a singleton, gives it an
`IHttpClientFactory` client, the application's logger and the application's `TimeProvider`,
and validates the configuration while the host starts.

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"));
```

```json
{
  "Jev": {
    "ApiKey": "<your-api-key>",
    "OperationTimeout": "00:01:00",
    "Retry": { "MaxAttempts": 3 }
  }
}
```

Registrations can be named, decorated and pointed at a credential that never touches
configuration:

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider("batch", builder.Configuration.GetSection("Jev:Batch"))
    .WithCredentials(container => container.GetRequiredService<VaultCredentials>())
    .Decorate((container, inner) => new MeteredDecisionProvider(inner, container.GetRequiredService<IMeterFactory>()));
```

See the [dependency injection guide](docs/guides/dependency-injection.md) for every setting
and what it does.

### In tests

`DecisionKit.Testing` replaces the provider, so application code is tested with no HTTP:

```csharp
using DecisionKit.Testing.Assertions;
using DecisionKit.Testing.Providers;

var provider = new FakeDecisionProvider()
    .Returns(routing, Department.Billing)
    .Returns(frustration, 7.0);

var result = await sut.HandleAsync(ticket, CancellationToken.None);

DecisionAssert.AskedExactly(DecisionAssert.CalledOnce(provider.Calls).Request, "routing", "frustration");
Assert.Equal(Department.Billing, result.Team);
```

Failures are injected with `Fails` and `FailsTimes`, latency is simulated on a
`TimeProvider`, and `DeterministicDecisionProvider` derives stable answers for snapshots and
samples. See the [testing guide](docs/guides/testing.md).

---

## Documentation

| Document | Contents |
| --- | --- |
| [Getting started](docs/guides/getting-started.md) | From installing a package to reading a typed answer |
| [Roadmap](docs/roadmap.md) | What exists today, and what is being considered next |
| [Architecture overview](docs/architecture/overview.md) | Layers, boundaries, data flow |
| [Type safety](docs/guides/type-safety.md) | How a question decides the type of its answer |
| [Error handling](docs/guides/error-handling.md) | Categories, exception types, retry hints, cancellation |
| [Testing](docs/guides/testing.md) | Testing application code with fake and deterministic providers |
| [AOT and trimming](docs/guides/aot-and-trimming.md) | What is guaranteed, and how it is enforced |
| [JEV provider](docs/providers/jev.md) | Credentials, rubrics, timeouts, retrying, failure codes |
| [JEV wire protocol](docs/providers/jev-protocol.md) | The JEV payload and how it maps to the domain |
| [Writing a provider](docs/providers/custom-provider.md) | Implementing `IDecisionProvider` yourself |
| [FAQ](docs/guides/faq.md) | The questions that come up first |
| [Samples](samples/) | Six runnable projects, four of which need no credential |
| [Decision records](docs/adr/) | Why the architecture is what it is |
| [Contributing](CONTRIBUTING.md) | Development rules, commit conventions, review gates |
| [Security policy](SECURITY.md) | Vulnerability reporting and secret handling |
| [Changelog](CHANGELOG.md) | Released changes, Semantic Versioning |

---

## Security and privacy

DecisionKit sends your application data to a third-party provider when you configure one.

- API keys are never logged, never serialized into diagnostics and never included in exceptions.
- Request and response payloads are not logged by default.
- Raw protocol capture is opt-in and can be disabled entirely.
- HTTPS is required for production endpoints.

Report vulnerabilities privately — see [`SECURITY.md`](SECURITY.md).

---

## Relationship to TypeSafe

TypeSafe and JEV are products of their respective owners. This project is an independent,
community-maintained client. It is not affiliated with, endorsed by, or supported by TypeSafe.

---

## License

[MIT](LICENSE) © Jonatha Panni
