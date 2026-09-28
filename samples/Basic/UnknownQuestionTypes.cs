using System;
using System.Collections.Generic;
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
/// Asking a question DecisionKit does not model, and reading the answer anyway.
/// </summary>
/// <remarks>
/// A provider ships a new question type before this library learns about it. Rather than block the
/// caller until a release catches up, an <see cref="UnknownQuestion"/> carries the provider's own
/// type name and an opaque definition, and the answer comes back as an <see cref="UnknownAnswer"/>
/// with the payload preserved. Unknown is never discarded.
/// </remarks>
internal static class UnknownQuestionTypes
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("== Unknown question types ==");

        UnknownQuestion sentiment = new(
            new QuestionId("sentiment"),
            "What is the tone of this ticket?",
            providerType: "sentiment_v2")
        {
            RawDefinition = """{"buckets":["angry","neutral","pleased"]}""",
        };

        UnknownAnswer canned = new(sentiment.Id, sentiment.ProviderType)
        {
            RawPayload = """{"type":"sentiment_v2","bucket":"angry","intensity":0.91}""",
            UnknownProperties = new Dictionary<string, object?>
            {
                ["bucket"] = "angry",
                ["intensity"] = 0.91,
            },
        };

        FakeDecisionProvider provider = new FakeDecisionProvider().Returns(sentiment, canned);

        DecisionRequest request = new(QuestionSet.Create(sentiment))
        {
            Input = DecisionInput.FromText(TicketQuestions.Ticket),

            // Preserve is the default: keep what is not understood. Fail is for the caller that
            // would rather stop than process a partially understood result.
            Options = new DecisionOptions { UnknownTypeHandling = UnknownTypeHandling.Preserve },
        };

        DecisionResult result = await provider.DecideAsync(request, cancellationToken);
        UnknownAnswer answer = result.Get(sentiment);

        Console.WriteLine($"Provider type: {answer.ProviderType}");
        Console.WriteLine($"Raw payload:   {answer.RawPayload}");

        foreach (KeyValuePair<string, object?> property in answer.UnknownProperties)
        {
            Console.WriteLine($"  {property.Key} = {property.Value}");
        }

        Console.WriteLine();
    }
}
