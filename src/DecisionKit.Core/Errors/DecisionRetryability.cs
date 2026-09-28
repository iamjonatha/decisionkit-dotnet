namespace DecisionKit.Errors;

/// <summary>
/// States whether repeating a failed call can reasonably succeed.
/// </summary>
public enum DecisionRetryability
{
    /// <summary>
    /// The provider gave no usable signal. The caller decides, and the decision is not guessed here.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The same call can be attempted again.
    /// </summary>
    Retryable = 1,

    /// <summary>
    /// Repeating the call will fail the same way. Something must change first.
    /// </summary>
    NotRetryable = 2,
}
