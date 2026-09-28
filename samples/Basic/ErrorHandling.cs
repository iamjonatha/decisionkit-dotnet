using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Testing.Providers;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// Telling failures apart without parsing a message.
/// </summary>
/// <remarks>
/// Every failure is a <see cref="DecisionException"/> carrying a <see cref="DecisionError"/>. The
/// exception type answers "can I do anything about this?" and the error answers "what exactly
/// happened?". Catching <see cref="DecisionException"/> alone is always correct; catching a derived
/// type is how a caller reacts differently.
/// </remarks>
internal static class ErrorHandling
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Error handling ==");

        await ReportAsync(DecisionErrorCategory.Validation, "The score question declares no scale.", cancellationToken);
        await ReportAsync(DecisionErrorCategory.Authentication, "The API key was rejected.", cancellationToken);
        await ReportAsync(DecisionErrorCategory.RateLimit, "Too many requests.", cancellationToken);

        Console.WriteLine();
    }

    private static async Task ReportAsync(
        DecisionErrorCategory category,
        string message,
        CancellationToken cancellationToken)
    {
        DecisionError error = new(category, message)
        {
            ProviderName = "fake",
            Code = "sample_failure",
            Retry = category == DecisionErrorCategory.RateLimit
                ? DecisionRetryHint.After(TimeSpan.FromSeconds(2))
                : DecisionRetryHint.NotRetryable,
        };

        FakeDecisionProvider provider = new FakeDecisionProvider().Fails(error);
        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Churn));

        try
        {
            _ = await provider.DecideAsync(request, cancellationToken);
        }
        catch (DecisionValidationException ex)
        {
            // The request is wrong. Repeating it unchanged cannot help.
            Console.WriteLine($"  Validation: {ex.Error.Message} (code {ex.Error.Code})");
        }
        catch (DecisionAuthenticationException ex)
        {
            // The credential is wrong. This is an operator problem, not a transient one.
            Console.WriteLine($"  Authentication: {ex.Error.Message}");
        }
        catch (DecisionTransientException ex) when (ex.Error.Retry.Retryability == DecisionRetryability.Retryable)
        {
            Console.WriteLine($"  Transient: {ex.Error.Message}, retry after {ex.Error.Retry.RetryAfter}.");
        }
        catch (DecisionException ex)
        {
            // The catch-all that stays correct when a new category is added.
            Console.WriteLine($"  {ex.Error.Category}: {ex.Error.Message}");
        }
    }
}
