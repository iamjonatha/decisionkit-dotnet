using System;

namespace DecisionKit.Errors;

/// <summary>
/// The base type of every failure a decision provider reports.
/// </summary>
/// <remarks>
/// <para>
/// The hierarchy is deliberately shallow. There is one derived type per reaction a caller can
/// have — fix the request, fix the credentials, try again later, escalate to the provider — and not
/// one per failure category. The finer classification lives in <see cref="DecisionErrorCategory"/>,
/// which callers read, log and alert on without having to catch ten exception types to tell two
/// situations apart.
/// </para>
/// <para>
/// Argument exceptions are not part of this hierarchy. Passing a null question set is a programming
/// error, not a decision failure, and it keeps throwing <see cref="ArgumentException"/> and its
/// relatives. Cancellation is not part of it either: a cancelled call throws
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public abstract class DecisionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionException"/> class from a structured
    /// error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    protected DecisionException(DecisionError error)
        : base(MessageOf(error))
    {
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionException"/> class from a structured
    /// error and the exception that caused it.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    protected DecisionException(DecisionError error, Exception? innerException)
        : base(MessageOf(error), innerException)
    {
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionException"/> class from a message, and
    /// builds the structured error from it.
    /// </summary>
    /// <param name="category">The category the failure belongs to.</param>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure, when there is one.</param>
    protected DecisionException(DecisionErrorCategory category, string? message, Exception? innerException)
        : base(message, innerException)
    {
        Error = new DecisionError(category, Message);
    }

    /// <summary>
    /// Gets the structured description of the failure, including everything the provider reported
    /// about it.
    /// </summary>
    public DecisionError Error { get; }

    /// <summary>
    /// Gets the category the failure belongs to.
    /// </summary>
    public DecisionErrorCategory Category => Error.Category;

    /// <summary>
    /// Gets what the provider said about repeating the call.
    /// </summary>
    public DecisionRetryHint Retry => Error.Retry;

    /// <summary>
    /// Creates the exception that corresponds to the category of an error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <returns>The exception a caller is expected to catch for that category.</returns>
    /// <remarks>
    /// This is the single place that maps a category onto the hierarchy, so that a provider never
    /// has to hard-code the mapping and two providers cannot disagree about it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is categorized as <see cref="DecisionErrorCategory.Canceled"/>,
    /// which is reported as <see cref="OperationCanceledException"/> instead.
    /// </exception>
    public static DecisionException FromError(DecisionError error) => FromError(error, null);

    /// <summary>
    /// Creates the exception that corresponds to the category of an error, preserving the exception
    /// that caused it.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <returns>The exception a caller is expected to catch for that category.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is categorized as <see cref="DecisionErrorCategory.Canceled"/>,
    /// which is reported as <see cref="OperationCanceledException"/> instead.
    /// </exception>
    public static DecisionException FromError(DecisionError error, Exception? innerException)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Category switch
        {
            DecisionErrorCategory.Validation =>
                new DecisionValidationException(error, innerException),
            DecisionErrorCategory.Authentication or DecisionErrorCategory.Authorization =>
                new DecisionAuthenticationException(error, innerException),
            DecisionErrorCategory.RateLimit or DecisionErrorCategory.Timeout or DecisionErrorCategory.Transport =>
                new DecisionTransientException(error, innerException),
            DecisionErrorCategory.Canceled => throw new ArgumentException(
                "A cancelled call is reported as OperationCanceledException, not as a DecisionException.",
                nameof(error)),
            _ => new DecisionProviderException(error, innerException),
        };
    }

    private static string MessageOf(DecisionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Message;
    }
}
