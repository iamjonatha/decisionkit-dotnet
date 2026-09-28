using System;
using System.Globalization;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Testing.Recording;

/// <summary>
/// One call a test double observed, kept with the position and the moment it happened.
/// </summary>
/// <remarks>
/// Assertions about ordering are as common as assertions about content: a retry test needs to know
/// that the second attempt carried the same idempotency key as the first. Recording the ordinal and
/// the timestamp alongside the request makes both kinds of assertion possible without the test
/// having to reconstruct them.
/// </remarks>
public sealed class RecordedDecisionCall
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordedDecisionCall"/> class.
    /// </summary>
    /// <param name="ordinal">The zero-based position of the call in the log.</param>
    /// <param name="request">The request the caller passed.</param>
    /// <param name="timestamp">The moment the call was observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ordinal"/> is negative.</exception>
    public RecordedDecisionCall(int ordinal, DecisionRequest request, DateTimeOffset timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentNullException.ThrowIfNull(request);

        Ordinal = ordinal;
        Request = request;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the zero-based position of the call in the log.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Gets the request the caller passed, by reference rather than by copy, so that assertions see
    /// exactly the instance the system under test built.
    /// </summary>
    public DecisionRequest Request { get; }

    /// <summary>
    /// Gets the moment the call was observed, read from the recorder's time provider.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the questions the request asked.
    /// </summary>
    public QuestionSet Questions => Request.Questions;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"#{Ordinal} at {Timestamp:O} asking {Request.Questions.Count} question(s)");
}
