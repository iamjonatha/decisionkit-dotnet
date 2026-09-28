using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Samples.Testing;

/// <summary>
/// The team a support ticket can be routed to.
/// </summary>
public enum Department
{
    Billing,
    Support,
    Security,
}

/// <summary>
/// The system under test: application logic that asks a decision and then decides what to do
/// with it.
/// </summary>
/// <remarks>
/// It depends on <see cref="IDecisionProvider"/>, so a test substitutes the provider and never
/// touches HTTP, a credential or a clock.
/// </remarks>
public sealed class TicketTriage(IDecisionProvider provider)
{
    public static ChoiceQuestion<Department> Routing { get; } = new(
        new QuestionId("routing"),
        "Which team should handle this ticket?",
        [Department.Billing, Department.Support, Department.Security]);

    public static ScoreQuestion Frustration { get; } = new(
        new QuestionId("frustration"),
        "How frustrated is the customer?",
        minimum: 0,
        maximum: 10);

    /// <summary>
    /// Triages one ticket, falling back to a human when the provider cannot be reached.
    /// </summary>
    /// <param name="text">The ticket text.</param>
    /// <param name="cancellationToken">Abandons the call when the caller stops caring.</param>
    /// <returns>The team to route to, and whether a human should look first.</returns>
    public async Task<(Department Team, bool Escalate)> TriageAsync(string text, CancellationToken cancellationToken)
    {
        DecisionRequest request = new(QuestionSet.Create(Routing, Frustration))
        {
            Input = DecisionInput.FromText(text),
        };

        try
        {
            DecisionResult result = await provider.DecideAsync(request, cancellationToken);

            Choice<Department> routing = result.Get(Routing).Value;
            Score frustration = result.Get(Frustration).Value;

            return (routing.Selection, frustration.Normalize() >= 0.8);
        }
        catch (DecisionTransientException)
        {
            // A provider that is temporarily unavailable must not lose the ticket.
            return (Department.Support, true);
        }
    }
}
