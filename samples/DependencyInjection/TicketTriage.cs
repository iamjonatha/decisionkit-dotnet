using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Samples.DependencyInjection;

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
/// The body of a triage request.
/// </summary>
/// <param name="Text">The ticket text to triage.</param>
public sealed record TriageRequest(string Text);

/// <summary>
/// What the application decided about one ticket.
/// </summary>
/// <param name="Team">The team the ticket is routed to.</param>
/// <param name="Frustration">How frustrated the customer sounds, on the question's own scale.</param>
/// <param name="Escalate">Whether a human should look at the ticket first.</param>
public sealed record TicketTriageResult(Department Team, double Frustration, bool Escalate);

/// <summary>
/// The application service that asks the decision. It depends on
/// <see cref="IDecisionProvider"/> and on nothing else.
/// </summary>
/// <remarks>
/// This class has no idea that JEV exists. Replacing the provider registration in
/// <c>Program.cs</c> with a second provider, or with a fake in a test, changes nothing here. That
/// is the whole point of the abstraction.
/// </remarks>
public sealed class TicketTriage(IDecisionProvider provider)
{
    private static readonly ChoiceQuestion<Department> s_routing = new(
        new QuestionId("routing"),
        "Which team should handle this ticket?",
        [Department.Billing, Department.Support, Department.Security]);

    private static readonly ScoreQuestion s_frustration = new(
        new QuestionId("frustration"),
        "How frustrated is the customer?",
        minimum: 0,
        maximum: 10);

    /// <summary>
    /// Triages one ticket.
    /// </summary>
    /// <param name="text">The ticket text.</param>
    /// <param name="cancellationToken">Abandons the call when the caller stops caring.</param>
    /// <returns>The triage decision.</returns>
    public async Task<TicketTriageResult> TriageAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        DecisionRequest request = new(QuestionSet.Create(s_routing, s_frustration))
        {
            Input = DecisionInput.FromText(text),

            // Correlates this application's logs with the provider's own request identifier.
            ClientRequestId = RequestId.New(),
        };

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);

        Choice<Department> routing = result.Get(s_routing).Value;
        Score frustration = result.Get(s_frustration).Value;

        Department team = routing.TryGetSelection(out Department selection)
            ? selection
            : Department.Support;

        return new TicketTriageResult(team, frustration.Value, frustration.Normalize() >= 0.8);
    }
}
