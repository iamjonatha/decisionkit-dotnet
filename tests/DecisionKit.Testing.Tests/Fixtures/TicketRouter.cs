using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;

namespace DecisionKit.Testing.Tests.Fixtures;

/// <summary>
/// Stands in for the application code a consumer would write: it owns its questions, builds a
/// request, and turns the answers into a decision of its own.
/// </summary>
/// <remarks>
/// Nothing here knows which provider answers, which is the whole point of the test package: the
/// same class runs against a fake, against a deterministic provider and against the real one.
/// </remarks>
public sealed class TicketRouter
{
    private readonly IDecisionProvider _provider;

    public TicketRouter(IDecisionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
    }

    public static ChoiceQuestion<Department> RoutingQuestion { get; } = new(
        new QuestionId("routing"),
        "Which department should handle this ticket?",
        [Department.Billing, Department.Technical, Department.Sales]);

    public static ScoreQuestion UrgencyQuestion { get; } = new(
        new QuestionId("urgency"),
        "How urgent is this ticket?",
        0,
        10);

    public async Task<TicketRouting> RouteAsync(string ticket, CancellationToken cancellationToken)
    {
        DecisionRequest request = new(QuestionSet.Create(RoutingQuestion, UrgencyQuestion))
        {
            Input = DecisionInput.FromText(ticket),
            ClientRequestId = RequestId.New(),
        };

        DecisionResult result = await _provider.DecideAsync(request, cancellationToken).ConfigureAwait(false);

        ChoiceAnswer<Department> routing = result.Get(RoutingQuestion);
        ScoreAnswer urgency = result.Get(UrgencyQuestion);

        return new TicketRouting(routing.Value.Selection, urgency.Value.Value >= 7);
    }
}
