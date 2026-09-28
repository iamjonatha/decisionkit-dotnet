using System;

namespace DecisionKit.Errors;

/// <summary>
/// The call failed for a reason that may not repeat: a rate limit, a timeout, or a transport
/// failure.
/// </summary>
/// <remarks>
/// This is the exception a resilience policy watches for. Being transient makes a retry plausible,
/// not correct: read <see cref="DecisionException.Retry"/> for what the provider actually said, and
/// remember that repeating a non-idempotent call is an explicit decision.
/// </remarks>
public sealed class DecisionTransientException : DecisionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionTransientException"/> class.
    /// </summary>
    public DecisionTransientException()
        : this("The decision call failed for a transient reason.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionTransientException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    public DecisionTransientException(string? message)
        : base(DecisionErrorCategory.Transport, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionTransientException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public DecisionTransientException(string? message, Exception? innerException)
        : base(DecisionErrorCategory.Transport, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionTransientException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionTransientException(DecisionError error)
        : base(error)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionTransientException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionTransientException(DecisionError error, Exception? innerException)
        : base(error, innerException)
    {
    }
}
