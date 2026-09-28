# Handling secrets

DecisionKit needs a provider credential to call TypeSafe JEV. This guide describes where to
put it and what the library guarantees about it.

## Never do this

```csharp
// Do not embed a credential in source. It ends up in git history and in your build output.
options.ApiKey = "sk_live_0123456789";
```

A key committed to a repository is compromised, even in a private repository, even after a
force push. Rotate it rather than trying to erase it.

## Local development

Use the .NET user secrets store. It lives outside the repository.

```bash
dotnet user-secrets init
dotnet user-secrets set "Jev:ApiKey" "<your-api-key>"
```

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"));
```

The key is read from the bound settings on every call, so a reloaded configuration source
rotates it without a restart. If the key does not come from `IConfiguration` at all, supply a
credential provider instead and leave `Jev:ApiKey` unset:

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"))
    .WithCredentials(container => new DelegateJevCredentialProvider(
        async token => new JevApiKey(await vault.ReadCurrentKeyAsync(token))));
```

Without the hosting package, read the key yourself and pass it to the provider:

```csharp
var provider = new JevDecisionProvider(
    httpClient,
    new StaticJevCredentialProvider(builder.Configuration["Jev:ApiKey"]!));
```

An `appsettings.Development.json` file is acceptable only if it is git-ignored and contains
no real key. Prefer user secrets.

## Continuous integration

Store the key as a repository or environment secret and pass it as an environment variable:

```yaml
env:
  Jev__ApiKey: ${{ secrets.JEV_API_KEY }}
```

Never echo the value, never write it to a log, and never pass it on a command line where it
would appear in process listings or build output.

## Production

Use the secret store your platform provides, and read it through `IConfiguration`:

- Azure Key Vault
- AWS Secrets Manager
- HashiCorp Vault
- Kubernetes secrets mounted as environment variables or files

Rotate keys on a schedule. `IJevCredentialProvider` is asked for the key on **every** call
rather than once at construction, so a rotating key takes effect on the next call without
restarting the application or rebuilding the provider:

```csharp
var provider = new JevDecisionProvider(
    httpClient,
    new DelegateJevCredentialProvider(
        async token => new JevApiKey(await vault.ReadCurrentKeyAsync(token))));
```

`DelegateJevCredentialProvider` deliberately does not cache. An application that wants
caching owns that decision, because only it knows how long its key stays valid.

## What the library guarantees

- The API key and the `Authorization` header are never written to a log, an exception
  message, a `ToString()` result, or a diagnostic dump. `JevApiKey` enforces this by
  construction: its value is internal and its `ToString()` returns `<redacted>`.
- Request and response payloads are not logged by default.
- Raw protocol capture is off unless `PreserveRawPayloads` turns it on, and what it captures
  is attached to the result rather than logged.
- A configuration failure names the setting, its path and the value found, and never quotes
  the key. `JevProviderSettings.ToString()` renders it as `<redacted>`.
- Nothing is persisted by the library itself.

A regression in any of these is treated as a vulnerability. See [`../../SECURITY.md`](../../SECURITY.md).

## What the library cannot guarantee

- What your application chooses to send to the provider.
- What your own logging does with a request object you constructed.
- The security of the provider service itself.
