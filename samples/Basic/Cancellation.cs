using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Testing.Providers;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// Cancelling a decision, and the two ways it happens.
/// </summary>
/// <remarks>
/// Cancellation is reported as <see cref="OperationCanceledException"/>, never as a
/// <c>DecisionException</c>, because a caller that asked to stop is not looking at a failure it has
/// to classify. The token reaches the transport, so an in-flight HTTP request is abandoned rather
/// than awaited to completion and then discarded.
/// </remarks>
internal static class Cancellation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== Cancellation ==");

        await AlreadyCancelledAsync();
        await CancelledWhileRunningAsync();

        Console.WriteLine();
    }

    private static async Task AlreadyCancelledAsync()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        FakeDecisionProvider provider = new();
        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Churn));

        try
        {
            _ = await provider.DecideAsync(request, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // No call was made: a provider checks the token before it does any work.
            Console.WriteLine($"  Cancelled before the call. Calls made: {provider.CallCount}");
        }
    }

    private static async Task CancelledWhileRunningAsync()
    {
        // A deadline is a cancellation with a timer attached. This is how a caller imposes its own
        // budget on top of whatever budget the provider enforces.
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));

        FakeDecisionProvider provider = new() { Latency = TimeSpan.FromSeconds(5) };
        DecisionRequest request = new(QuestionSet.Create(TicketQuestions.Churn));

        try
        {
            _ = await provider.DecideAsync(request, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Cancelled while waiting for the provider.");
        }
    }
}
