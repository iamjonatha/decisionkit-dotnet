using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Assertions;
using DecisionKit.Testing.Providers;
using DecisionKit.Testing.Recording;
using DecisionKit.Testing.Tests.Fixtures;

namespace DecisionKit.Testing.Tests.Scenarios;

/// <summary>
/// Exercises the Phase 3 exit criterion end to end: arrange a fake provider, run application code,
/// assert the request it built, return a configured answer, assert the decision it reached — with
/// no HTTP anywhere.
/// </summary>
public sealed class ArrangeActAssertTests
{
    [Fact]
    public async Task ApplicationCodeIsTestedWithoutAProviderAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(TicketRouter.RoutingQuestion, Department.Billing)
            .Returns(TicketRouter.UrgencyQuestion, 8.0);

        TicketRouting routing = await new TicketRouter(provider)
            .RouteAsync("I was charged twice this month.", TestContext.Current.CancellationToken);

        RecordedDecisionCall call = DecisionAssert.CalledOnce(provider.Calls);
        DecisionAssert.AskedExactly(call.Request, "routing", "urgency");

        Assert.Equal("I was charged twice this month.", call.Request.Input.Text);
        Assert.NotNull(call.Request.ClientRequestId);

        Assert.Equal(Department.Billing, routing.Department);
        Assert.True(routing.IsUrgent);
    }

    [Fact]
    public async Task TheSameApplicationCodeRunsAgainstADeterministicProviderAsync()
    {
        RecordingDecisionProvider provider = new(new DeterministicDecisionProvider());

        TicketRouting first = await new TicketRouter(provider)
            .RouteAsync("The application crashes on startup.", TestContext.Current.CancellationToken);

        TicketRouting second = await new TicketRouter(provider)
            .RouteAsync("The application crashes on startup.", TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        DecisionAssert.CalledTimes(provider.Calls, 2);
    }

    [Fact]
    public async Task ADifferentTicketReachesADifferentDecisionAsync()
    {
        DeterministicDecisionProvider provider = new();

        DecisionResult billing = await provider.DecideAsync(Request("I was charged twice."), TestContext.Current.CancellationToken);
        DecisionResult crash = await provider.DecideAsync(Request("The application crashes."), TestContext.Current.CancellationToken);

        Assert.NotEqual(
            billing.Get(TicketRouter.UrgencyQuestion).Value.Value,
            crash.Get(TicketRouter.UrgencyQuestion).Value.Value);

        Assert.NotEqual(billing.Metadata.ProviderRequestId, crash.Metadata.ProviderRequestId);
    }

    [Fact]
    public async Task TheFakeDeclaresTheQuestionTypesItWasTaughtAsync()
    {
        FakeDecisionProvider provider = new FakeDecisionProvider()
            .Returns(TicketRouter.RoutingQuestion, Department.Sales)
            .Returns(TicketRouter.UrgencyQuestion, 2.0);

        DecisionRequest request = new(QuestionSet.Create(TicketRouter.RoutingQuestion, TicketRouter.UrgencyQuestion));

        Assert.True(provider.Capabilities.SupportsAll(request.Questions));

        await provider.DecideAsync(request, TestContext.Current.CancellationToken);
    }

    private static DecisionRequest Request(string ticket) =>
        new(QuestionSet.Create(TicketRouter.RoutingQuestion, TicketRouter.UrgencyQuestion))
        {
            Input = DecisionInput.FromText(ticket),
        };
}
