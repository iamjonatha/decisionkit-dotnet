using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Providers;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// Several questions asked in one call, and read back one at a time.
/// </summary>
/// <remarks>
/// One request carries a whole question set, so a provider that evaluates in batch does one round
/// trip instead of three. Whether it actually does is reported by
/// <c>Capabilities.SupportsBatching</c>, not assumed.
/// </remarks>
internal static class MultipleQuestions
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Several questions, one call ==");

        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(TicketQuestions.Routing, Department.Billing)
            .Returns(TicketQuestions.Frustration, 8.5)
            .Returns(TicketQuestions.Churn, 0.34);

        DecisionRequest request = new(QuestionSet.Create(
            TicketQuestions.Routing,
            TicketQuestions.Frustration,
            TicketQuestions.Churn))
        {
            Input = DecisionInput.FromText(TicketQuestions.Ticket),
            ClientRequestId = RequestId.New(),
        };

        Console.WriteLine($"Batching supported: {provider.Capabilities.SupportsBatching}");

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);

        Console.WriteLine($"Team:        {result.Get(TicketQuestions.Routing).Value.Selection}");
        Console.WriteLine($"Frustration: {result.Get(TicketQuestions.Frustration).Value.Value}");
        Console.WriteLine($"Churn:       {result.Get(TicketQuestions.Churn).Value.Value:P1}");

        // A provider is allowed to answer fewer questions than were asked. TryGet says so without
        // throwing, which is the difference between a missing answer and a wrong one.
        if (!result.TryGet(TicketQuestions.Churn, out _))
        {
            Console.WriteLine("The provider did not answer the churn question.");
        }

        // The untyped view is there for logging and diagnostics, where the question is not known
        // statically.
        foreach (Answer answer in result.Answers)
        {
            Console.WriteLine($"  {answer.QuestionId.Value,-12} {answer.GetType().Name}");
        }

        Console.WriteLine();
    }
}
