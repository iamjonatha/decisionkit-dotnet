namespace DecisionKit.Providers;

/// <summary>
/// What a provider does with a question or answer type it does not recognize.
/// </summary>
public enum UnknownTypeHandling
{
    /// <summary>
    /// Keep the unrecognized data, typed as <see cref="Questions.UnknownQuestion"/> or
    /// <see cref="Answers.UnknownAnswer"/>, with its provider type and raw payload intact.
    /// </summary>
    /// <remarks>
    /// This is the default, and it is what makes a provider able to ship a new type before
    /// DecisionKit models it without breaking callers that do not use it.
    /// </remarks>
    Preserve = 0,

    /// <summary>
    /// Fail the call when anything in the response cannot be recognized.
    /// </summary>
    /// <remarks>
    /// This is for callers who would rather stop than act on a partially understood result. It
    /// trades availability for certainty, and it is never the default.
    /// </remarks>
    Fail = 1,
}
