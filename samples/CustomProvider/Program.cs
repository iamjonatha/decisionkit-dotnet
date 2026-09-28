using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Samples.CustomProvider;

Dictionary<string, double> weights = new(StringComparer.OrdinalIgnoreCase)
{
    ["charged twice"] = 0.5,
    ["nobody has replied"] = 0.3,
    ["cancel"] = 0.4,
};

IDecisionProvider provider = new KeywordDecisionProvider(weights, TimeProvider.System);

ChoiceQuestion<string> routing = new(
    new QuestionId("routing"),
    "Which team should handle this ticket?",
    ["billing", "support", "security"]);

ScoreQuestion urgency = new(
    new QuestionId("urgency"),
    "How urgent is this ticket?",
    minimum: 0,
    maximum: 5);

// Ask the provider what it can do before asking it to do it. This is a capability check, not a
// try/catch: an unsupported question is a programming error, not a runtime condition.
QuestionSet questions = QuestionSet.Create(routing, urgency);

Console.WriteLine($"Provider '{provider.Name}' supports every question: {provider.Capabilities.SupportsAll(questions)}");
Console.WriteLine($"Idempotency keys honoured: {provider.Capabilities.SupportsIdempotencyKeys}");
Console.WriteLine();

DecisionRequest request = new(questions)
{
    Input = DecisionInput.FromText(
        "I have been charged twice for the same month and nobody has replied to my last two emails."),
};

DecisionResult result = await provider.DecideAsync(request, CancellationToken.None);

Console.WriteLine($"Team:    {result.Get(routing).Value.Selection}");
Console.WriteLine($"Urgency: {result.Get(urgency).Value.Value} / 5");
Console.WriteLine($"Model:   {result.Metadata.ModelVersion}");

if (result.Usage.TryGetMetric(DecisionUsageKeys.EvaluatedQuestions, out double evaluated))
{
    Console.WriteLine($"Questions evaluated: {evaluated}");
}

Console.WriteLine();

// The provider refuses a question type it never declared, instead of inventing an answer.
UnknownQuestion sentiment = new(new QuestionId("sentiment"), "What is the tone?", "sentiment_v2");

try
{
    _ = await provider.DecideAsync(new DecisionRequest(QuestionSet.Create(sentiment)) { Input = request.Input }, CancellationToken.None);
}
catch (DecisionValidationException ex)
{
    Console.WriteLine($"Refused as expected: {ex.Error.Message} (code {ex.Error.Code})");
}
