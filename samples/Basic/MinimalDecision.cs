using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Providers;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// The smallest useful program: one question, one provider, one typed answer.
/// </summary>
internal static class MinimalDecision
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Minimal decision ==");

        // Any IDecisionProvider works here. The deterministic provider answers from a hash of the
        // request, so this sample runs with no network, no credential and no surprises.
        IDecisionProvider provider = new DeterministicDecisionProvider();

        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Churn))
        {
            Input = DecisionInput.FromText(TicketQuestions.Ticket),
        };

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);

        // The answer type comes from the question. No cast, no dictionary lookup by string.
        double churn = result.Get(TicketQuestions.Churn).Value.Value;

        Console.WriteLine($"Churn probability: {churn:P1}");
        Console.WriteLine($"Answered by '{result.Metadata.ProviderName}' at {result.Metadata.Timestamp:O}.");
        Console.WriteLine();
    }
}
