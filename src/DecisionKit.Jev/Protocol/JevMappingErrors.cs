using System.Collections.Generic;
using DecisionKit.Errors;
using DecisionKit.Identifiers;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Builds the failures the protocol mappers raise.
/// </summary>
/// <remarks>
/// Mapping failures are reported through the ordinary domain error model, so a caller never has to
/// catch a JEV-specific exception type to handle one. The category carries the distinction: a
/// request this package cannot express is a validation failure the caller can fix, while a response
/// it cannot interpret is an <see cref="DecisionErrorCategory.UnknownResponse"/>.
/// </remarks>
internal static class JevMappingErrors
{
    internal static DecisionException CannotSend(JevMappingOptions options, string code, string message) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.Validation, message)
            {
                Code = code,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.NotRetryable,
            });

    internal static DecisionException CannotSend(JevMappingOptions options, string code, string message, QuestionId questionId) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.Validation, message)
            {
                Code = code,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.NotRetryable,
                Properties = QuestionProperties(questionId),
            });

    internal static DecisionException CannotSend(
        JevMappingOptions options,
        string code,
        string message,
        QuestionId questionId,
        System.Exception innerException) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.Validation, message)
            {
                Code = code,
                ProviderName = options.ProviderName,
                Retry = DecisionRetryHint.NotRetryable,
                Properties = QuestionProperties(questionId),
            },
            innerException);

    internal static DecisionException CannotRead(JevMappingOptions options, string message) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.UnknownResponse, message)
            {
                Code = JevProtocol.UnmappableAnswerCode,
                ProviderName = options.ProviderName,
            });

    internal static DecisionException CannotRead(JevMappingOptions options, string message, QuestionId questionId) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.UnknownResponse, message)
            {
                Code = JevProtocol.UnmappableAnswerCode,
                ProviderName = options.ProviderName,
                Properties = QuestionProperties(questionId),
            });

    internal static DecisionException CannotRead(
        JevMappingOptions options,
        string message,
        QuestionId questionId,
        System.Exception innerException) =>
        DecisionException.FromError(
            new DecisionError(DecisionErrorCategory.UnknownResponse, message)
            {
                Code = JevProtocol.UnmappableAnswerCode,
                ProviderName = options.ProviderName,
                Properties = QuestionProperties(questionId),
            },
            innerException);

    private static Dictionary<string, object?> QuestionProperties(QuestionId questionId) =>
        new(System.StringComparer.Ordinal) { ["question_id"] = questionId.ToString() };
}
