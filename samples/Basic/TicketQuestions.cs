using DecisionKit.Identifiers;
using DecisionKit.Questions;

namespace DecisionKit.Samples.Basic;

/// <summary>
/// The team a support ticket can be routed to.
/// </summary>
internal enum Department
{
    Billing,
    Support,
    Security,
}

/// <summary>
/// The questions every scenario in this sample asks about a support ticket.
/// </summary>
/// <remarks>
/// Questions are values: they are built once, shared, and reused for both asking and reading. The
/// same instance that goes into the request is the key that reads the answer back out, which is how
/// the answer keeps its type without a cast.
/// </remarks>
internal static class TicketQuestions
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

    public static ProbabilityQuestion Churn { get; } = new(
        new QuestionId("churn"),
        "How likely is this customer to cancel within 30 days?");

    public const string Ticket =
        "I have been charged twice for the same month and nobody has replied to my last two emails.";
}
