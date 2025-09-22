// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.SharedTestUtilities.Performance;

/// <summary>
/// Tracks memory usage and allocation patterns during test execution.
/// </summary>
public sealed class MemoryTracker : IDisposable
{
    private readonly ILogger<MemoryTracker> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly long _initialMemory;
    private readonly Dictionary<string, long> _checkpoints = new();
    private readonly List<MemorySnapshot> _snapshots = new();
    private bool _disposed;

    // High-performance LoggerMessage delegates
    private static readonly Action<ILogger, long, Exception?> s_logMemoryTrackingStarted =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(1, "MemoryTrackingStarted"),
            "Memory tracking started. Initial memory: {Memory:N0} bytes");

    private static readonly Action<ILogger, string, long, long, Exception?> s_logMemoryCheckpoint =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Debug,
            new EventId(2, "MemoryCheckpoint"),
            "Memory checkpoint '{Name}': {Memory:N0} bytes ({Delta:+N0} from start)");

    private static readonly Action<ILogger, Exception?> s_logMemoryUsageSummary =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, "MemoryUsageSummary"),
            "Memory Usage Summary:");

    private static readonly Action<ILogger, long, Exception?> s_logInitialMemory =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(4, "InitialMemory"),
            "  Initial: {Initial:N0} bytes");

    private static readonly Action<ILogger, long, Exception?> s_logCurrentMemory =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(5, "CurrentMemory"),
            "  Current: {Current:N0} bytes");

    private static readonly Action<ILogger, long, Exception?> s_logPeakMemory =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(6, "PeakMemory"),
            "  Peak: {Peak:N0} bytes");

    private static readonly Action<ILogger, long, Exception?> s_logTotalDelta =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(7, "TotalDelta"),
            "  Total Delta: {Delta:+N0} bytes");

    private static readonly Action<ILogger, Exception?> s_logCheckpointsHeader =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(8, "CheckpointsHeader"),
            "  Checkpoints:");

    private static readonly Action<ILogger, string, long, long, Exception?> s_logCheckpointDetail =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Information,
            new EventId(9, "CheckpointDetail"),
            "    {Name}: {Memory:N0} bytes ({Delta:+N0})");

    private static readonly Action<ILogger, int, int, int, Exception?> s_logGcCollections =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(10, "GcCollections"),
            "  GC Collections: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}");

    private static readonly Action<ILogger, long, long, Exception?> s_logMemoryExceededLimit =
        LoggerMessage.Define<long, long>(
            LogLevel.Warning,
            new EventId(11, "MemoryExceededLimit"),
            "Memory usage exceeded limit. Delta: {Delta:N0} bytes, Limit: {Limit:N0} bytes");

    public MemoryTracker(ILogger<MemoryTracker>? logger = null)
    {
        if (logger == null)
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = _loggerFactory.CreateLogger<MemoryTracker>();
        }
        else
        {
            _logger = logger;
            _loggerFactory = null;
        }
        _initialMemory = GC.GetTotalMemory(false);

        s_logMemoryTrackingStarted(_logger, _initialMemory, null);
    }

    /// <summary>
    /// Creates a checkpoint with the current memory usage.
    /// </summary>
    public void Checkpoint(string name)
    {
        if (_disposed)
            return;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var currentMemory = GC.GetTotalMemory(false);
        _checkpoints[name] = currentMemory;

        var snapshot = new MemorySnapshot
        {
            Name = name,
            Timestamp = DateTime.UtcNow,
            TotalMemory = currentMemory,
            WorkingSet = Process.GetCurrentProcess().WorkingSet64,
            Generation0Collections = GC.CollectionCount(0),
            Generation1Collections = GC.CollectionCount(1),
            Generation2Collections = GC.CollectionCount(2)
        };

        _snapshots.Add(snapshot);

        s_logMemoryCheckpoint(_logger, name, currentMemory, currentMemory - _initialMemory, null);
    }

    /// <summary>
    /// Gets the memory usage at a specific checkpoint.
    /// </summary>
    public long GetCheckpointMemory(string name)
    {
        return _checkpoints.TryGetValue(name, out var memory) ? memory : -1;
    }

    /// <summary>
    /// Gets the memory difference between two checkpoints.
    /// </summary>
    public long GetMemoryDifference(string startCheckpoint, string endCheckpoint)
    {
        var start = GetCheckpointMemory(startCheckpoint);
        var end = GetCheckpointMemory(endCheckpoint);

        return start >= 0 && end >= 0 ? end - start : 0;
    }

    /// <summary>
    /// Gets current memory statistics.
    /// </summary>
    public MemoryStatistics GetCurrentStatistics()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var process = Process.GetCurrentProcess();
        var currentMemory = GC.GetTotalMemory(false);

        return new MemoryStatistics
        {
            TotalManagedMemory = currentMemory,
            WorkingSet = process.WorkingSet64,
            PrivateMemorySize = process.PrivateMemorySize64,
            VirtualMemorySize = process.VirtualMemorySize64,
            Generation0Collections = GC.CollectionCount(0),
            Generation1Collections = GC.CollectionCount(1),
            Generation2Collections = GC.CollectionCount(2),
            DeltaFromStart = currentMemory - _initialMemory
        };
    }

    /// <summary>
    /// Gets memory usage report with all checkpoints.
    /// </summary>
    public MemoryReport GetReport()
    {
        var currentStats = GetCurrentStatistics();
        var peakMemory = _snapshots.Count > 0 ? _snapshots.Max(s => s.TotalMemory) : currentStats.TotalManagedMemory;

        return new MemoryReport
        {
            InitialMemory = _initialMemory,
            CurrentMemory = currentStats.TotalManagedMemory,
            PeakMemory = peakMemory,
            TotalDelta = currentStats.DeltaFromStart,
            Checkpoints = new Dictionary<string, long>(_checkpoints),
            Snapshots = _snapshots.ToArray(),
            CurrentStatistics = currentStats
        };
    }

    /// <summary>
    /// Logs a summary of memory usage.
    /// </summary>
    public void LogSummary()
    {
        var report = GetReport();

        s_logMemoryUsageSummary(_logger, null);
        s_logInitialMemory(_logger, report.InitialMemory, null);
        s_logCurrentMemory(_logger, report.CurrentMemory, null);
        s_logPeakMemory(_logger, report.PeakMemory, null);
        s_logTotalDelta(_logger, report.TotalDelta, null);

        if (_checkpoints.Count > 0)
        {
            s_logCheckpointsHeader(_logger, null);
            foreach (var checkpoint in _checkpoints.OrderBy(c => c.Value))
            {
                s_logCheckpointDetail(_logger, checkpoint.Key, checkpoint.Value, checkpoint.Value - _initialMemory, null);
            }
        }

        var stats = report.CurrentStatistics;
        s_logGcCollections(_logger, stats.Generation0Collections, stats.Generation1Collections, stats.Generation2Collections, null);
    }

    /// <summary>
    /// Validates that memory usage is within acceptable limits.
    /// </summary>
    public bool ValidateMemoryUsage(long maxMemoryIncrease = 100 * 1024 * 1024) // 100 MB default
    {
        var report = GetReport();
        var acceptable = report.TotalDelta <= maxMemoryIncrease;

        if (!acceptable)
        {
            s_logMemoryExceededLimit(_logger, report.TotalDelta, maxMemoryIncrease, null);
        }

        return acceptable;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            LogSummary();
            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a memory usage snapshot at a specific point in time.
/// </summary>
public sealed class MemorySnapshot
{
    public required string Name { get; init; }
    public required DateTime Timestamp { get; init; }
    public required long TotalMemory { get; init; }
    public required long WorkingSet { get; init; }
    public required int Generation0Collections { get; init; }
    public required int Generation1Collections { get; init; }
    public required int Generation2Collections { get; init; }
}

/// <summary>
/// Comprehensive memory statistics.
/// </summary>
public sealed class MemoryStatistics
{
    public required long TotalManagedMemory { get; init; }
    public required long WorkingSet { get; init; }
    public required long PrivateMemorySize { get; init; }
    public required long VirtualMemorySize { get; init; }
    public required int Generation0Collections { get; init; }
    public required int Generation1Collections { get; init; }
    public required int Generation2Collections { get; init; }
    public required long DeltaFromStart { get; init; }
}

/// <summary>
/// Complete memory usage report.
/// </summary>
public sealed class MemoryReport
{
    public required long InitialMemory { get; init; }
    public required long CurrentMemory { get; init; }
    public required long PeakMemory { get; init; }
    public required long TotalDelta { get; init; }
    public required IReadOnlyDictionary<string, long> Checkpoints { get; init; }
    public required MemorySnapshot[] Snapshots { get; init; }
    public required MemoryStatistics CurrentStatistics { get; init; }
}