using System;

namespace DecisionKit.Errors;

/// <summary>
/// The request was rejected before it could be evaluated, because it is not valid for the provider.
/// </summary>
/// <remarks>
/// The caller reacts by changing the request. Repeating the same call cannot succeed. This is
/// distinct from <see cref="ArgumentException"/>, which reports a request that is malformed
/// according to DecisionKit itself and never reaches a provider.
/// </remarks>
public sealed class DecisionValidationException : DecisionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionValidationException"/> class.
    /// </summary>
    public DecisionValidationException()
        : this("The decision request was rejected as invalid by the provider.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionValidationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    public DecisionValidationException(string? message)
        : base(DecisionErrorCategory.Validation, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionValidationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public DecisionValidationException(string? message, Exception? innerException)
        : base(DecisionErrorCategory.Validation, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionValidationException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionValidationException(DecisionError error)
        : base(error)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionValidationException"/> class from a
    /// structured error.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public DecisionValidationException(DecisionError error, Exception? innerException)
        : base(error, innerException)
    {
    }
}
