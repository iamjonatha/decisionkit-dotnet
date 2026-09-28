using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Providers;
using DecisionKit.Values;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// A choice question answered with one of the caller's own types.
/// </summary>
/// <remarks>
/// <c>ChoiceQuestion&lt;TOption&gt;</c> is generic over the option type, so the selection comes back
/// as a <see cref="Department"/> rather than as a string the caller has to parse back into one.
/// </remarks>
internal static class ChoiceQuestions
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Typed choice question ==");

        // The fake provider states the answer, which is what a unit test wants.
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(
                TicketQuestions.Routing,
                Choice.Distributed(
                    [
                        new OptionProbability<Department>(Department.Billing, new Probability(0.72)),
                        new OptionProbability<Department>(Department.Support, new Probability(0.21)),
                        new OptionProbability<Department>(Department.Security, new Probability(0.07)),
                    ],
                    Department.Billing));

        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Routing))
        {
            Input = DecisionInput.FromText(TicketQuestions.Ticket),
        };

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);
        Choice<Department> choice = result.Get(TicketQuestions.Routing).Value;

        // Selection is the provider's pick. It is not always present: a provider may return a
        // distribution and leave the decision to the application.
        if (choice.TryGetSelection(out Department team))
        {
            Console.WriteLine($"Route to: {team}");
        }

        foreach (OptionProbability<Department> option in choice.Distribution)
        {
            Console.WriteLine($"  {option.Option,-8} {option.Probability.Value:P1}");
        }

        if (choice.TryGetProbability(Department.Security, out Probability security))
        {
            Console.WriteLine($"Security involvement: {security.Value:P1}");
        }

        Console.WriteLine();
    }
}
