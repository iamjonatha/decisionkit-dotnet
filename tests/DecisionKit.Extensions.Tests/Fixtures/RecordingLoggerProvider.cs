using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DecisionKit.Extensions.Tests.Fixtures;

/// <summary>
/// A logger provider that keeps every message it is given, so a test can assert that the provider
/// resolved the application's log rather than a null one.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new Recorder(_entries);

    public void Dispose()
    {
    }

    private sealed class Recorder : ILogger
    {
        private readonly ConcurrentQueue<string> _entries;

        public Recorder(ConcurrentQueue<string> entries)
        {
            _entries = entries;
        }

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
            ArgumentNullException.ThrowIfNull(formatter);

            _entries.Enqueue(formatter(state, exception));
        }
    }
}
