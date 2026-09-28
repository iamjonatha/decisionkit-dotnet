namespace DecisionKit.Errors;

/// <summary>
/// Classifies why a decision call failed.
/// </summary>
/// <remarks>
/// The category is the shared vocabulary every provider maps its failures onto, so that callers,
/// logs and dashboards classify failures the same way regardless of which provider produced them.
/// It is deliberately finer-grained than the exception hierarchy: the hierarchy exists so that a
/// caller can react, the category exists so that an operator can diagnose.
/// </remarks>
public enum DecisionErrorCategory
{
    /// <summary>
    /// The failure could not be classified. Treated as a provider failure.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The request was rejected because it is not valid for the provider.
    /// </summary>
    Validation = 1,

    /// <summary>
    /// The caller could not be authenticated. Credentials are missing, malformed or expired.
    /// </summary>
    Authentication = 2,

    /// <summary>
    /// The caller was authenticated but is not allowed to perform the operation.
    /// </summary>
    Authorization = 3,

    /// <summary>
    /// The caller exceeded a quota or a request rate limit.
    /// </summary>
    RateLimit = 4,

    /// <summary>
    /// The call did not complete within the time allowed for it.
    /// </summary>
    Timeout = 5,

    /// <summary>
    /// The caller cancelled the call.
    /// </summary>
    /// <remarks>
    /// This category never produces a <see cref="DecisionException"/>. Cancellation is reported as
    /// <see cref="System.OperationCanceledException"/>, because that is what every .NET caller and
    /// every framework already handles.
    /// </remarks>
    Canceled = 6,

    /// <summary>
    /// The provider could not be reached, or the connection failed while the call was in flight.
    /// </summary>
    Transport = 7,

    /// <summary>
    /// A payload could not be written or read.
    /// </summary>
    Serialization = 8,

    /// <summary>
    /// The provider processed the request and reported a failure of its own.
    /// </summary>
    ProviderError = 9,

    /// <summary>
    /// The provider returned a well-formed response that the current version cannot interpret.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="Serialization"/>: the payload was readable, its meaning was
    /// not. It is also distinct from an unrecognized answer type, which is preserved rather than
    /// failed. See <see cref="Answers.UnknownAnswer"/>.
    /// </remarks>
    UnknownResponse = 10,
}
