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
/// A score question, and the scale that travels with its answer.
/// </summary>
/// <remarks>
/// A bare <c>7.0</c> means nothing on its own. <see cref="Score"/> carries the bounds the question
/// declared, so the value can be compared, normalized and rendered without the caller having to
/// remember which scale it belongs to.
/// </remarks>
internal static class ScoreQuestions
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Score question ==");

        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(TicketQuestions.Frustration, 8.5);

        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Frustration))
        {
            Input = DecisionInput.FromText(TicketQuestions.Ticket),
        };

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);
        Score frustration = result.Get(TicketQuestions.Frustration).Value;

        Console.WriteLine($"Frustration: {frustration.Value} on {frustration.Minimum}..{frustration.Maximum}");

        // Normalize projects any scale onto 0..1, which is what a threshold should be written against.
        Console.WriteLine($"Normalized: {frustration.Normalize():F2}");

        if (frustration.Normalize() >= 0.8)
        {
            Console.WriteLine("Escalating to a human agent.");
        }

        Console.WriteLine();
    }
}
