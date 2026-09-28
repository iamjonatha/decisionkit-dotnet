using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Samples.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// One line registers the provider as a singleton, gives it an IHttpClientFactory client named
// DecisionKit.Jev.jev, the host's logger and the host's TimeProvider, and validates the
// configuration while the host starts rather than on the first request.
builder.Services
    .AddDecisionKit()
    .AddJevProvider(builder.Configuration.GetSection("Jev"));

builder.Services.AddSingleton<TicketTriage>();

WebApplication app = builder.Build();

app.MapPost("/tickets/triage", async (
    TriageRequest body,
    TicketTriage triage,
    CancellationToken cancellationToken) =>
{
    try
    {
        TicketTriageResult result = await triage.TriageAsync(body.Text, cancellationToken);

        return Results.Ok(result);
    }
    catch (DecisionValidationException ex)
    {
        // The request was wrong. Repeating it unchanged cannot help, so do not make the caller try.
        return Problem(ex, StatusCodes.Status400BadRequest);
    }
    catch (DecisionAuthenticationException ex)
    {
        // The credential is this service's problem, never the caller's.
        return Problem(ex, StatusCodes.Status500InternalServerError);
    }
    catch (DecisionTransientException ex)
    {
        // Retrying is already configured; reaching here means the budget ran out.
        return Problem(ex, StatusCodes.Status503ServiceUnavailable);
    }
});

await app.RunAsync();

static IResult Problem(DecisionException exception, int statusCode) =>
    Results.Problem(
        title: exception.Error.Category.ToString(),
        detail: exception.Error.Message,
        statusCode: statusCode,
        extensions: new Dictionary<string, object?>
        {
            ["providerRequestId"] = exception.Error.ProviderRequestId,
            ["retryAfter"] = exception.Error.Retry.RetryAfter,
        });
