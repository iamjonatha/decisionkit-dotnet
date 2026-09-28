using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DecisionKit.Providers;

namespace DecisionKit.Testing.Recording;

/// <summary>
/// The ordered log of the calls a test double observed.
/// </summary>
/// <remarks>
/// <para>
/// The log is the only record of what the system under test actually asked. It is deliberately a
/// separate type rather than a property bag on a provider, so that the same capture can be attached
/// to a fake, to a recording decorator, or to a provider written by a consumer.
/// </para>
/// <para>
/// The log is safe to write from several threads, because application code under test may well call
/// a provider concurrently.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "A call log is a domain concept. Renaming it to DecisionCallCollection would describe the implementation instead of the purpose.")]
public sealed class DecisionCallLog : IReadOnlyList<RecordedDecisionCall>
{
    private readonly List<RecordedDecisionCall> _calls = [];
    private readonly object _gate = new();

    /// <summary>
    /// Gets the number of calls recorded so far.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether nothing has been recorded.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets the call at the given position.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The call at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the log.</exception>
    public RecordedDecisionCall this[int index]
    {
        get
        {
            lock (_gate)
            {
                return _calls[index];
            }
        }
    }

    /// <summary>
    /// Gets the most recent call.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing has been recorded.</exception>
    public RecordedDecisionCall Last
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count > 0
                    ? _calls[^1]
                    : throw new InvalidOperationException("No decision call has been recorded yet.");
            }
        }
    }

    /// <summary>
    /// Gets the recorded requests, in call order.
    /// </summary>
    public IReadOnlyList<DecisionRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                List<DecisionRequest> requests = new(_calls.Count);

                foreach (RecordedDecisionCall call in _calls)
                {
                    requests.Add(call.Request);
                }

                return requests;
            }
        }
    }

    /// <summary>
    /// Records one call.
    /// </summary>
    /// <param name="request">The request the caller passed.</param>
    /// <param name="timestamp">The moment the call was observed.</param>
    /// <returns>The recorded call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public RecordedDecisionCall Record(DecisionRequest request, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            RecordedDecisionCall call = new(_calls.Count, request, timestamp);
            _calls.Add(call);

            return call;
        }
    }

    /// <summary>
    /// Returns the single recorded call, for the common case of a test that expects exactly one.
    /// </summary>
    /// <returns>The only recorded call.</returns>
    /// <exception cref="InvalidOperationException">The log holds no call, or more than one.</exception>
    public RecordedDecisionCall Only()
    {
        lock (_gate)
        {
            return _calls.Count == 1
                ? _calls[0]
                : throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Expected exactly one decision call, but {_calls.Count} were recorded."));
        }
    }

    /// <summary>
    /// Forgets everything recorded so far.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _calls.Clear();
        }
    }

    /// <inheritdoc />
    public IEnumerator<RecordedDecisionCall> GetEnumerator()
    {
        RecordedDecisionCall[] snapshot;

        lock (_gate)
        {
            snapshot = _calls.ToArray();
        }

        return ((IEnumerable<RecordedDecisionCall>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
