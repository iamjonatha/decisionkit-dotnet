# Getting started

From nothing to a typed decision, in the order you will actually do it.

## Install

```bash
dotnet add package DecisionKit.Core
```

That is enough to model decisions and to test them. Add what you need on top:

| Package | Add it when |
| --- | --- |
| `DecisionKit.Core` | always — the domain model and `IDecisionProvider` |
| `DecisionKit.Jev` | you want to call the TypeSafe JEV service |
| `DecisionKit.Extensions` | you run inside a host and want DI, configuration and `IHttpClientFactory` |
| `DecisionKit.Testing` | in test projects, to replace the provider |

`DecisionKit.Extensions` pulls in `Core` and `Jev`, so a typical web application references
`DecisionKit.Extensions` and nothing else.

> Until `0.1.0` is published the packages are built from source. Clone the repository and add
> a project reference, or consume the artifacts produced by CI.

## Model the decision

A question is a value. Build it once, keep it, and reuse the same instance to ask and to read.

```csharp
using DecisionKit.Identifiers;
using DecisionKit.Questions;

public enum Department { Billing, Support, Security }

public static class TicketQuestions
{
    public static ChoiceQuestion<Department> Routing { get; } = new(
        new QuestionId("routing"),
        "Which team should handle this ticket?",
        [Department.Billing, Department.Support, Department.Security]);

    public static ProbabilityQuestion Churn { get; } = new(
        new QuestionId("churn"),
        "How likely is this customer to cancel within 30 days?");
}
```

Three question types cover most decisions:

| Question | Answer | Reads as |
| --- | --- | --- |
| `ChoiceQuestion<TOption>` | `ChoiceAnswer<TOption>` | `Choice<TOption>`: a selection and, when the provider gives one, a distribution |
| `ScoreQuestion` | `ScoreAnswer` | `Score`: a value on the scale you declared |
| `ProbabilityQuestion` | `ProbabilityAnswer` | `Probability`: a value between 0 and 1 |

A fourth, `UnknownQuestion`, exists for provider features this library does not model yet.

## Ask

```csharp
using DecisionKit.Providers;
using DecisionKit.Results;

DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Routing, TicketQuestions.Churn))
{
    Input = DecisionInput.FromText(ticketText),
    ClientRequestId = RequestId.New(),
};

DecisionResult result = await provider.DecideAsync(request, cancellationToken);
```

`QuestionSet.Create` rejects duplicate identifiers at construction, so a malformed request
never reaches the network.

## Read

```csharp
Department team = result.Get(TicketQuestions.Routing).Value.Selection;
double churn = result.Get(TicketQuestions.Churn).Value.Value;
```

No cast and no string lookup: `Get` is typed by the question you pass it. See
[type safety](type-safety.md) for how that works and what it buys you.

## Run it without a provider

You do not need a credential to start. `DecisionKit.Testing` ships two providers that answer
locally:

```csharp
using DecisionKit.Testing.Providers;

IDecisionProvider provider = new DeterministicDecisionProvider();
```

`DeterministicDecisionProvider` derives an answer from a hash of the request, so it is stable
across runs and machines. `FakeDecisionProvider` lets you script exactly what comes back, and
is the one to use in tests. See the [testing guide](testing.md).

## Point it at JEV

```csharp
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;

JevDecisionProvider provider = new(httpClient, new StaticJevCredentialProvider(apiKey));
```

The provider never owns the `HttpClient`, and it asks the credential provider on every call,
so a rotated key takes effect without rebuilding anything.

JEV is stricter than the domain in one place: it scores against a rubric of described levels
rather than a numeric range, so a plain `ScoreQuestion` is rejected. Use `JevScoreQuestion`,
which carries the rubric. The [JEV provider guide](../providers/jev.md) covers this and
everything else the provider does or refuses to do.

## Register it in a host

```csharp
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"));
```

```json
{
  "Jev": {
    "OperationTimeout": "00:01:00",
    "Retry": { "MaxAttempts": 3 }
  }
}
```

The API key does not belong in `appsettings.json`. Use user secrets in development and an
environment variable or a vault in production — see [handling secrets](secrets.md).

Configuration is validated while the host starts, so a typo fails at startup rather than on
the first request. Every setting is listed in the
[dependency injection guide](dependency-injection.md).

## Handle failure

```csharp
try
{
    return await provider.DecideAsync(request, cancellationToken);
}
catch (DecisionTransientException ex)
{
    logger.LogWarning("Provider {Provider} failed transiently: {Reason}.", ex.Error.ProviderName, ex.Error.Message);
    throw;
}
catch (DecisionException ex)
{
    logger.LogError("Decision failed: {Category}.", ex.Category);
    throw;
}
```

Catching `DecisionException` is always correct. Catching a derived type is how a caller reacts
differently to a wrong request, a wrong credential and a bad afternoon on the network. See
[error handling](error-handling.md).

## Next

- [Type safety](type-safety.md) — why the answer knows its own type
- [Error handling](error-handling.md) — the failure model, end to end
- [Testing](testing.md) — fake providers, virtual time, assertions
- [Dependency injection](dependency-injection.md) — every setting, and what it does
- [JEV provider](../providers/jev.md) — the provider in production
- [Samples](../../samples) — six runnable projects, four of which need no credential
