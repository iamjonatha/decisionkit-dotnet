# Error handling

Every failure that reaches your code answers two questions without you parsing a message:
*can I do anything about this?* and *what exactly happened?*

The first is the exception type. The second is the `DecisionError` it carries.

The design and what was rejected are recorded in [ADR-0006](../adr/0006-error-model.md).

## The shape

```csharp
try
{
    DecisionResult result = await provider.DecideAsync(request, cancellationToken);
}
catch (DecisionValidationException ex)
{
    // The request is wrong. Repeating it unchanged cannot help.
}
catch (DecisionAuthenticationException ex)
{
    // The credential is wrong or insufficient. An operator has to act.
}
catch (DecisionTransientException ex)
{
    // Rate limit, timeout or transport. Trying again may work.
}
catch (DecisionException ex)
{
    // Everything else, including categories added in a later version.
}
```

Catching `DecisionException` alone is always correct and stays correct when a category is
added. Catch a derived type only where you react differently.

## Categories

`DecisionError.Category` is the precise answer. `DecisionException.FromError` maps it to the
exception type:

| Category | Exception | Meaning |
| --- | --- | --- |
| `Validation` | `DecisionValidationException` | The request is malformed or unsupported |
| `Authentication` | `DecisionAuthenticationException` | The credential is missing or rejected |
| `Authorization` | `DecisionAuthenticationException` | The credential is valid but not allowed |
| `RateLimit` | `DecisionTransientException` | Quota exhausted; usually carries a delay |
| `Timeout` | `DecisionTransientException` | The attempt or the operation ran out of time |
| `Transport` | `DecisionTransientException` | The call did not complete at the network level |
| `Serialization` | `DecisionProviderException` | The payload could not be read or written |
| `ProviderError` | `DecisionProviderException` | The provider reported its own failure |
| `UnknownResponse` | `DecisionProviderException` | The response was understood as JSON but not as an answer |
| `Unknown` | `DecisionProviderException` | No better classification was available |
| `Canceled` | *none* | See below |

## Cancellation is not an error

A cancelled call throws `OperationCanceledException`, the way the rest of .NET reports it.
`DecisionException.FromError` refuses to build an exception from a `Canceled` error for exactly
that reason.

```csharp
try
{
    return await provider.DecideAsync(request, cancellationToken);
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // The caller asked to stop. This is not a failure.
    throw;
}
```

A timeout inside the provider is different: it is a `Timeout` category and a
`DecisionTransientException`, because nobody asked to stop.

## What the error carries

```csharp
public sealed class DecisionError
{
    public DecisionErrorCategory Category { get; }
    public string Message { get; }
    public string? Code { get; init; }
    public string? ProviderName { get; init; }
    public string? ProviderRequestId { get; init; }
    public DecisionRetryHint Retry { get; init; }
    public Uri? DocumentationUrl { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; }
}
```

`Code` is the provider's own stable identifier — match on it, not on `Message`. `Message` is
for humans and may change.

`ProviderRequestId` is what you quote in a support ticket. Log it on every failure.

`Properties` carries whatever else the provider reported. The JEV provider puts the HTTP
status code there:

```csharp
if (ex.Error.Properties.TryGetValue(JevProtocol.StatusCodeProperty, out object? status))
{
    logger.LogWarning("JEV returned {Status}.", status);
}
```

## The retry hint

`DecisionRetryHint` is the provider's own opinion about whether trying again can help:

```csharp
catch (DecisionTransientException ex) when (ex.Retry.Retryability == DecisionRetryability.Retryable)
{
    TimeSpan? delay = ex.Retry.RetryAfter;
}
```

| Retryability | Meaning |
| --- | --- |
| `Retryable` | The provider says the same request may succeed later |
| `NotRetryable` | The provider says it will not |
| `Unknown` | The provider did not say |

`RetryAfter` is set when the provider gave a concrete delay, typically from a `Retry-After`
header. `DecisionRetryHint.After(delay)` produces a retryable hint carrying that delay.

The hint is advice, not behaviour. Nothing retries unless you configure it to — see
[ADR-0010](../adr/0010-retry-is-opt-in.md) and the
[JEV provider guide](../providers/jev.md#retrying).

## Mapping to an HTTP response

In a web application, the categories map cleanly onto status codes:

```csharp
catch (DecisionException ex)
{
    int status = ex.Category switch
    {
        DecisionErrorCategory.Validation => StatusCodes.Status400BadRequest,
        DecisionErrorCategory.Authentication => StatusCodes.Status502BadGateway,
        DecisionErrorCategory.Authorization => StatusCodes.Status502BadGateway,
        DecisionErrorCategory.RateLimit => StatusCodes.Status429TooManyRequests,
        DecisionErrorCategory.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway,
    };

    return Results.Problem(title: ex.Error.Message, statusCode: status);
}
```

`Authentication` becomes `502`, not `401`: your credential to the provider is broken, not your
caller's credential to you. `samples/DependencyInjection` implements this.

Never put `ex.Error.Properties` or the raw payload into a response body. It is provider data,
and it may contain the input you sent.

## Testing failure paths

`FakeDecisionProvider` injects errors directly, so every branch above is reachable in a unit
test with no network:

```csharp
DecisionError error = new(DecisionErrorCategory.RateLimit, "Too many requests.")
{
    ProviderName = "jev",
    Code = "rate_limited",
    Retry = DecisionRetryHint.After(TimeSpan.FromSeconds(2)),
};

FakeDecisionProvider provider = new FakeDecisionProvider().Fails(error);
```

`FailsTimes(n, error)` fails the first `n` calls and then succeeds, which is how you test a
retry loop without waiting for one. See the [testing guide](testing.md).

`samples/Basic` runs the whole catch ladder end to end.
