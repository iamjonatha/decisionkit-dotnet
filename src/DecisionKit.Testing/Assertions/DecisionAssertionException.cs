using System;

namespace DecisionKit.Testing.Assertions;

/// <summary>
/// Thrown when an assertion made through <see cref="DecisionAssert"/> does not hold.
/// </summary>
/// <remarks>
/// A dedicated exception keeps <c>DecisionKit.Testing</c> independent of any test framework. Every
/// runner reports a thrown exception as a failure, so the helpers work unchanged under xUnit, NUnit
/// and MSTest, and no consumer is forced to adopt the framework this repository happens to use.
/// </remarks>
public sealed class DecisionAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAssertionException"/> class.
    /// </summary>
    public DecisionAssertionException()
        : base("A decision assertion failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAssertionException"/> class.
    /// </summary>
    /// <param name="message">What was expected and what was found.</param>
    public DecisionAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionAssertionException"/> class.
    /// </summary>
    /// <param name="message">What was expected and what was found.</param>
    /// <param name="innerException">The exception that caused the assertion to fail.</param>
    public DecisionAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
