// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Backends.CPU.Tests.Helpers
{

/// <summary>
/// Fake logger implementation for testing purposes.
/// </summary>
public class FakeLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();

    public LogEntryCollector Collector { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry
        {
            Level = logLevel,
            EventId = eventId,
            Message = formatter(state, exception),
            Exception = exception,
            Timestamp = DateTimeOffset.UtcNow
        };

        _logs.Enqueue(entry);
        Collector.Add(entry);
    }

    public IEnumerable<LogEntry> GetLogs() => _logs.ToArray();

    public void Clear() => _logs.Clear();
}

/// <summary>
/// Log entry record for test verification.
/// </summary>
public record LogEntry
{
    public LogLevel Level { get; init; }
    public EventId EventId { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Log entry collector for test assertions.
/// </summary>
public class LogEntryCollector
{
    private readonly List<LogEntry> _entries = [];

    public void Add(LogEntry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        lock (_entries)
        {
            return _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_entries)
        {
            _entries.Clear();
        }
    }
}}
