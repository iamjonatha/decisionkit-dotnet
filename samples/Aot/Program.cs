using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Every step below is one that a naive implementation would have done with reflection: binding
// configuration, composing a container, serializing the wire payload, and mapping an answer back
// onto a question type that is generic over one of the caller's own types. If any of them needed
// metadata the trimmer removed, this program would fail here rather than in production.

// ChoiceQuestion<TOption> is generic over a type the library never sees while it is being built.
ChoiceQuestion<Department> routing = new(
    new QuestionId("routing"),
    "Which team should handle this ticket?",
    [Department.Billing, Department.Support]);

DecisionRequest request = new(QuestionSet.Create(routing))
{
    Input = DecisionInput.FromText("I have been charged twice for the same month."),
};

Console.WriteLine("== Configuration binding and composition ==");

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jev:ApiKey"] = "not-a-real-key",
        ["Jev:Endpoint"] = "https://gateway.internal/jev/",
        ["Jev:Model"] = "jev-latest",
        ["Jev:OperationTimeout"] = "00:00:30",
        ["Jev:Retry:MaxAttempts"] = "3",
        ["Jev:Retry:InitialDelay"] = "00:00:00.500",
    })
    .Build();

ServiceCollection services = new();

// The settings binder is hand written precisely so that this line does not depend on the
// reflection-based configuration binder, which is not trim safe.
services
    .AddDecisionKit()
    .AddJevProvider(configuration.GetSection("Jev"));

using ServiceProvider container = services.BuildServiceProvider();

IDecisionProvider provider = container.GetRequiredService<IDecisionProvider>();

Console.WriteLine($"Resolved provider: {provider.Name}");
Console.WriteLine($"Supports choice questions: {provider.Capabilities.Supports(typeof(ChoiceQuestion<>))}");
Console.WriteLine();

Console.WriteLine("== Wire serialization ==");

// JEV payloads go through a source-generated serializer context, so no serializer metadata is
// discovered at run time and none of it can be trimmed away.
JevRequest wire = new RequestMapper().ToWire(request);

Console.WriteLine(JevJsonSerialization.Serialize(wire));
Console.WriteLine();

Console.WriteLine("== Answer mapping ==");

string label = JevOptionLabel.For(Department.Billing) ?? Department.Billing.ToString();

string payload = """
    {"model":"jev-latest","answers":{"routing":{"type":"choice","choice":"__CHOICE__","confidence":0.82}}}
    """.Replace("__CHOICE__", label, StringComparison.Ordinal);

JevResponse response = JevJsonSerialization.DeserializeResponse(payload);

// Mapping the answer back means producing a ChoiceAnswer<Department> without ever calling
// MakeGenericType. That is what the non-generic IChoiceQuestion seam exists for.
DecisionResult result = new ResponseMapper().ToDomain(request, response);
Choice<Department> choice = result.Get(routing).Value;

Console.WriteLine($"Selected: {choice.Selection}");

if (JevAnswers.TryGetConfidence(result.Get(routing), out double confidence))
{
    Console.WriteLine($"Confidence: {confidence.ToString(CultureInfo.InvariantCulture)}");
}

Console.WriteLine();
Console.WriteLine("Native AOT smoke test passed.");

await Task.CompletedTask;

internal enum Department
{
    Billing,
    Support,
}
