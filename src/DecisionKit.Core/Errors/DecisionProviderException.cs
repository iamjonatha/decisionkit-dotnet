using System;

namespace DecisionKit.Errors;

/// <summary>
/// The provider processed the call and something went wrong on its side, or its response could not
/// be understood.
/// </summary>
/// <remarks>
/// This is the terminal category: the request was well-formed, the caller was allowed, and the
/// failure is neither transient nor fixable by the caller. It is also where an unclassified failure
/// lands, so that an unmapped provider error never disappears into a generic exception.
/// </remarks>
public sealed class DecisionProviderException : DecisionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionProviderException"/> class.
    /// </summary>
    public DecisionProviderException()
        : this("The provider failed to produce a decision result.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionProviderException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    public DecisionProviderException(string? message)
        : base(DecisionErrorCategory.ProviderError, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionProviderException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public DecisionProviderException(string? message, Exception? innerException)
        : base(DecisionErrorCategory.ProviderError, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionProviderException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionProviderException(DecisionError error)
        : base(error)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionProviderException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionProviderException(DecisionError error, Exception? innerException)
        : base(error, innerException)
    {
    }
}
