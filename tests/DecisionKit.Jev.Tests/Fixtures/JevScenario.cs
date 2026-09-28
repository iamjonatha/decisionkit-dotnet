using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Questions;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// The decision every JEV test asks about, so that a payload recorded for one test can be read by
/// another.
/// </summary>
/// <remarks>
/// The scenario follows the support-ticket triage published as the reference example for the
/// System One endpoint: one <c>noul</c> question, one <c>choice</c> question and one <c>score</c>
/// question asked against a single state. A fourth question, of a type DecisionKit does not model,
/// is added so that pass-through has a subject.
/// </remarks>
public static class JevScenario
{
    public static QuestionId Urgent { get; } = new("is_urgent");

    public static QuestionId Team { get; } = new("department");

    public static QuestionId Frustration { get; } = new("frustration");

    public static QuestionId Tone { get; } = new("tone");

    public static ProbabilityQuestion UrgentQuestion { get; } =
        new(Urgent, "Whether the ticket needs attention today");

    public static ChoiceQuestion<Department> TeamQuestion { get; } = new(
        Team,
        "Which team should handle this ticket",
        [Department.Sales, Department.Billing, Department.Support]);

    public static JevScoreQuestion FrustrationQuestion { get; } = new(
        Frustration,
        "How frustrated the customer appears",
        [
            "Calm, just stating facts",
            "Frustrated but civil",
            "Very angry, strong language",
        ]);

    public static UnknownQuestion ToneQuestion { get; } = new(Tone, "What is the tone of this message?", "sentiment")
    {
        RawDefinition = """{"labels":["positive","negative"]}""",
    };

    public static DecisionRequest CreateRequest() => new(CreateQuestions())
    {
        Input = DecisionInput.Create("I was charged twice for the same order.", new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["customer_tier"] = 2,
        }),
        ClientRequestId = new RequestId("11111111-1111-1111-1111-111111111111"),
        Metadata = new Dictionary<string, object?> { ["tenant"] = "acme" },
    };

    public static QuestionSet CreateQuestions() =>
        new([UrgentQuestion, TeamQuestion, FrustrationQuestion, ToneQuestion]);
}
