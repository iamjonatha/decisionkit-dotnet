namespace DecisionKit.Results;

/// <summary>
/// The metric names DecisionKit expects providers to use in <see cref="DecisionUsage"/> when the
/// underlying concept applies.
/// </summary>
/// <remarks>
/// A provider is free to report additional, provider-specific metrics. It must not reuse one of
/// these names for a different meaning. Monetary amounts are expressed in the provider's billing
/// currency; the currency itself belongs in
/// <see cref="DecisionMetadata.Properties"/>, because it is not a number.
/// </remarks>
public static class DecisionUsageKeys
{
    /// <summary>
    /// The number of tokens consumed by the request sent to the provider.
    /// </summary>
    public const string InputTokens = "input_tokens";

    /// <summary>
    /// The number of tokens consumed by the response the provider returned.
    /// </summary>
    public const string OutputTokens = "output_tokens";

    /// <summary>
    /// The total number of tokens consumed by the call.
    /// </summary>
    public const string TotalTokens = "total_tokens";

    /// <summary>
    /// The monetary cost of the call, in the provider's billing currency.
    /// </summary>
    public const string Cost = "cost";

    /// <summary>
    /// The number of prepaid credits the call consumed.
    /// </summary>
    public const string Credits = "credits";

    /// <summary>
    /// The number of provider-defined request units the call consumed.
    /// </summary>
    public const string RequestUnits = "request_units";

    /// <summary>
    /// The number of questions the provider evaluated.
    /// </summary>
    public const string EvaluatedQuestions = "evaluated_questions";
}
