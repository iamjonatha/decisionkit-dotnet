using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// A logger that keeps everything it was told, so that a test can assert on what was written and,
/// more importantly, on what was not.
/// </summary>
/// <remarks>
/// Each entry holds both the formatted message and the structured state, because a credential could
/// leak through either one.
/// </remarks>
public sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Enqueue(string.Create(
            CultureInfo.InvariantCulture,
            $"{logLevel} {eventId.Id} {formatter(state, exception)} | state={state} | exception={exception}"));
    }
}
