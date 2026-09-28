namespace DecisionKit.Jev.Protocol;

/// <summary>
/// The question type names the JEV protocol defines.
/// </summary>
/// <remarks>
/// The service defines exactly these three. A question of any other type is carried as an
/// <see cref="DecisionKit.Questions.UnknownQuestion"/>, whose provider type goes on the wire as it
/// is.
/// </remarks>
public static class JevQuestionTypes
{
    /// <summary>
    /// A yes or no question, answered with the probability that the answer is yes.
    /// </summary>
    /// <remarks>
    /// <c>Noul</c> is JEV's own name for this primitive. The domain calls it a
    /// <see cref="DecisionKit.Questions.ProbabilityQuestion"/>, and the JEV name stays inside this
    /// package.
    /// </remarks>
    public const string Noul = "noul";

    /// <summary>
    /// A question answered by picking one option from a set the caller defines.
    /// </summary>
    public const string Choice = "choice";

    /// <summary>
    /// A question answered by rating the state against an ordered rubric the caller defines.
    /// </summary>
    public const string Score = "score";
}
