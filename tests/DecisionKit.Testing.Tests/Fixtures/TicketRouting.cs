namespace DecisionKit.Testing.Tests.Fixtures;

/// <summary>
/// What the ticket router decided, expressed in the application's own vocabulary rather than in
/// DecisionKit's.
/// </summary>
/// <param name="Department">The department that should handle the ticket.</param>
/// <param name="IsUrgent">Whether the ticket needs to be picked up immediately.</param>
public sealed record TicketRouting(Department Department, bool IsUrgent);
