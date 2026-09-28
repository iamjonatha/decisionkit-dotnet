using System;

namespace DecisionKit.Errors;

/// <summary>
/// The call was refused because of who the caller is: credentials are missing, rejected or
/// insufficient.
/// </summary>
/// <remarks>
/// Authentication and authorization share an exception because the caller reacts the same way — stop
/// calling and fix the identity configuration — and differ by
/// <see cref="DecisionException.Category"/> when the difference matters for diagnostics.
/// </remarks>
public sealed class DecisionAuthenticationException : DecisionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAuthenticationException"/> class.
    /// </summary>
    public DecisionAuthenticationException()
        : this("The provider refused the call because the caller could not be authenticated.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAuthenticationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    public DecisionAuthenticationException(string? message)
        : base(DecisionErrorCategory.Authentication, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAuthenticationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public DecisionAuthenticationException(string? message, Exception? innerException)
        : base(DecisionErrorCategory.Authentication, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAuthenticationException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionAuthenticationException(DecisionError error)
        : base(error)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAuthenticationException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionAuthenticationException(DecisionError error, Exception? innerException)
        : base(error, innerException)
    {
    }
}
