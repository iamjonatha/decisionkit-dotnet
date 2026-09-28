# Dependency injection

`DecisionKit.Extensions` registers a provider in an `IServiceCollection`. It is the only
package in the library that knows about hosting, configuration and logging; the core and the
JEV provider are usable without it.

```bash
dotnet add package DecisionKit.Extensions
```

## The shortest working registration

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"));
```

```json
{
  "Jev": {
    "ApiKey": "<your-api-key>"
  }
}
```

```csharp
public sealed class TriageService(IDecisionProvider provider)
{
    private static readonly ChoiceQuestion<Department> Routing = new(
        new QuestionId("routing"),
        "Which team should handle this ticket?",
        [Department.Billing, Department.Support]);

    public async Task<Department> RouteAsync(string ticket, CancellationToken cancellationToken)
    {
        var request = new DecisionRequest(QuestionSet.Create(Routing))
        {
            Input = DecisionInput.FromText(ticket),
        };

        var result = await provider.DecideAsync(request, cancellationToken);

        return result.Get(Routing).Value.Selection;
    }
}
```

The provider is a singleton. Its `HttpClient` comes from `IHttpClientFactory`, its logger and
its `TimeProvider` come from the container, and its credential is read from the settings on
every call.

## Settings

Every key is optional except the credential. An unmentioned key keeps its default, so a
section only ever contains what a deployment actually changed.

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `ApiKey` | string | — | The JEV API key. Required unless `WithCredentials(...)` supplies one. |
| `Endpoint` | absolute URI | `https://api.typesafe.ai` | Base address of the JEV service. |
| `ProviderName` | string | the registration name | The name reported by `IDecisionProvider.Name`. |
| `Model` | string | `jev-latest` | The JEV model to evaluate with. |
| `PreserveRawPayloads` | bool | `false` | Attach the raw request and response to the result. |
| `AttemptTimeout` | duration | `00:00:30` | Bounds one HTTP exchange. |
| `OperationTimeout` | duration | `00:01:00` | Bounds every attempt and every wait together. |
| `RetryDelayTimeout` | duration | `00:00:30` | Bounds a single wait between attempts. |

A duration is written the way `TimeSpan` parses it — `00:00:30`, `00:00:00.250`, `0:05:00` —
or as the word `infinite`.

### Retry

Retrying is off until `Retry:MaxAttempts` says otherwise. See
[ADR-0010](../adr/0010-retry-is-opt-in.md) for why: JEV documents no idempotency mechanism, so
a repeated call is a second evaluation and a second charge.

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `MaxAttempts` | int | `1` | Total attempts, including the first. Set it above `1` to retry. |
| `MaxElapsedTime` | duration | infinite | Gives up once this much time has passed. |
| `InitialDelay` | duration | `00:00:00.500` | Wait before the second attempt. |
| `BackoffFactor` | number | `2` | Multiplies the delay after each attempt. |
| `MaxDelay` | duration | `00:00:10` | Caps the computed delay. |
| `Jitter` | number | `0.2` | Fraction of the delay spread randomly, `0` to `1`. |
| `RespectRetryAfter` | bool | `true` | Honour a `Retry-After` header. |
| `RetryUnknownFailures` | bool | `false` | Repeat a failure the provider did not classify. |
| `Idempotency` | enum | `Disabled` | `Disabled` is the only value JEV can honour. |
| `RetryableStatusCodes` | int array | — | Treat these status codes as transient. |
| `NonRetryableStatusCodes` | int array | — | Never repeat these status codes. |

```json
{
  "Jev": {
    "ApiKey": "<your-api-key>",
    "OperationTimeout": "00:01:00",
    "Retry": {
      "MaxAttempts": 3,
      "InitialDelay": "00:00:00.500",
      "NonRetryableStatusCodes": [ 409 ]
    }
  }
}
```

## Configuring in code

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(settings =>
    {
        settings.ApiKey = key;
        settings.Model = "jev-1";
        settings.Retry.MaxAttempts = 3;
    });
```

Configuration and code compose, and code that runs later wins:

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"))
    .Configure(settings => settings.PreserveRawPayloads = builder.Environment.IsDevelopment());
```

## Credentials

`WithCredentials(...)` replaces whatever `ApiKey` was bound, and satisfies the startup check
on its own. Use it when the key does not come from `IConfiguration`.

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider()
    .WithCredentials(container => new DelegateJevCredentialProvider(
        async token => new JevApiKey(await container.GetRequiredService<Vault>().ReadKeyAsync(token))));
```

The credential provider is asked on every call, so a rotated key takes effect immediately.
The same is true of `ApiKey` when it is bound from a reloading configuration source. Nothing
else is re-read after a reload: the endpoint, the timeouts and the retry policy are fixed when
the provider is built.

See [Handling secrets](secrets.md) for where to keep the key.

## Startup validation

A registration is validated while the host starts, not on its first decision. A missing
credential, an endpoint that is not an absolute address, a timeout that cannot elapse and an
impossible retry policy all stop startup with a message naming the provider, the setting and
the value found. The API key never appears in one.

```text
DecisionKit JEV provider 'jev': the 'Endpoint' setting is not usable. 'not-an-address' is not
an absolute URI. (Parameter 'baseAddress')

DecisionKit JEV provider 'batch': the configuration value 'Jev:Batch:AttemptTimeout' is
'soon', which is not a duration such as '00:00:30', or 'infinite'.
```

## Shaping the HTTP client

`HttpClient` exposes the `IHttpClientBuilder` behind the registration, so an application can
add its own handlers.

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"))
    .HttpClient
        .AddHttpMessageHandler<CorrelationIdHandler>();
```

Two details are already set and should be left alone unless you mean it. The client's own
timeout is infinite, so that DecisionKit's three budgets are the only ones that can fire; and
the primary handler pools connections for two minutes, which is how a singleton-held factory
client avoids stale DNS.

## Decorating the provider

`Decorate(...)` wraps the provider without the application having to re-register it. The last
decorator added is the outermost one.

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"))
    .Decorate((container, inner) => new CachingDecisionProvider(inner, container.GetRequiredService<IMemoryCache>()))
    .Decorate((container, inner) => new MeteredDecisionProvider(inner, container.GetRequiredService<IMeterFactory>()));
```

Callers resolve `IDecisionProvider` and get the outermost decorator. Nothing they wrote needs
to change.

## More than one provider

A registration can be named. Everything about it — its settings section, its HTTP client, its
credential and the provider itself — is keyed by that name.

```csharp
var decisionKit = builder.Services.AddDecisionKit();

decisionKit.AddJevProvider("interactive", builder.Configuration.GetSection("Jev:Interactive"));
decisionKit.AddJevProvider("batch", builder.Configuration.GetSection("Jev:Batch"));
```

```csharp
public sealed class Triage([FromKeyedServices("interactive")] IDecisionProvider provider);
```

The unnamed overloads register the name `jev`. A name may be used once; registering it twice
throws with a message saying so.

The first registration is also resolvable without a key, so `IDecisionProvider` keeps working
for an application that only has one provider. When there are several, resolve by key or take
`IEnumerable<IDecisionProvider>`.

## Testing

Register a fake instead of the provider and the rest of the graph is unchanged:

```csharp
services.AddSingleton<IDecisionProvider>(
    new FakeDecisionProvider().Returns(Routing, Department.Billing));
```

See [Testing](testing.md).
