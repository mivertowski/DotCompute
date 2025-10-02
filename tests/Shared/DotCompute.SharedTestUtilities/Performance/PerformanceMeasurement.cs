// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.SharedTestUtilities.Performance;

/// <summary>
/// Comprehensive performance measurement utility for benchmarking operations.
/// </summary>
public sealed class PerformanceMeasurement : IDisposable
{
    private readonly ILogger<PerformanceMeasurement> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly MemoryTracker? _memoryTracker;
    private readonly List<Checkpoint> _checkpoints = [];
    private bool _disposed;
    private bool _isRunning;

    // High-performance LoggerMessage delegates
    private static readonly Action<ILogger, string, Exception?> s_logPerformanceMeasurementInitialized =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "PerformanceMeasurementInitialized"),
            "Performance measurement initialized for '{Operation}'");

    private static readonly Action<ILogger, string, Exception?> s_logAlreadyRunning =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "AlreadyRunning"),
            "Performance measurement for '{Operation}' is already running");

    private static readonly Action<ILogger, string, Exception?> s_logStartedMeasuring =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, "StartedMeasuring"),
            "Started measuring '{Operation}'");

    private static readonly Action<ILogger, string, Exception?> s_logNotRunning =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4, "NotRunning"),
            "Performance measurement for '{Operation}' is not running");

    private static readonly Action<ILogger, string, double, Exception?> s_logCompletedMeasuring =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(5, "CompletedMeasuring"),
            "Completed measuring '{Operation}': {Duration:F3}ms");

    private static readonly Action<ILogger, string, Exception?> s_logCannotAddCheckpoint =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6, "CannotAddCheckpoint"),
            "Cannot add checkpoint '{Name}' - measurement is not running");

    private static readonly Action<ILogger, string, double, Exception?> s_logAddedCheckpoint =
        LoggerMessage.Define<string, double>(
            LogLevel.Debug,
            new EventId(7, "AddedCheckpoint"),
            "Added checkpoint '{Name}' at {Elapsed:F3}ms");

    private static readonly Action<ILogger, string, Exception?> s_logPerformanceResults =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(8, "PerformanceResults"),
            "Performance Results for '{Operation}':");

    private static readonly Action<ILogger, double, Exception?> s_logDuration =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(9, "Duration"),
            "  Duration: {Duration:F3}ms");

    private static readonly Action<ILogger, Exception?> s_logCheckpoints =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(10, "Checkpoints"),
            "  Checkpoints:");

    private static readonly Action<ILogger, string, double, double, Exception?> s_logCheckpointDetail =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(11, "CheckpointDetail"),
            "    {Name}: {Elapsed:F3}ms (+{Delta:F3}ms)");

    private static readonly Action<ILogger, Exception?> s_logMemoryUsage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(12, "MemoryUsage"),
            "  Memory Usage:");

    private static readonly Action<ILogger, long, Exception?> s_logMemoryDelta =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(13, "MemoryDelta"),
            "    Delta: {Delta:+N0} bytes");

    private static readonly Action<ILogger, long, Exception?> s_logMemoryPeak =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(14, "MemoryPeak"),
            "    Peak: {Peak:N0} bytes");

    public string OperationName => _operationName;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public TimeSpan Duration => _stopwatch.Elapsed;
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;
    public bool IsRunning => _isRunning;

    public PerformanceMeasurement(string operationName, ITestOutputHelper output)
        : this(operationName, false, null)
    {
        // Ignore output for now - could create a logger that writes to it if needed
    }

    public PerformanceMeasurement(string operationName, bool trackMemory = true, ILogger<PerformanceMeasurement>? logger = null)
    {
        _operationName = operationName;
        if (logger == null)
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = _loggerFactory.CreateLogger<PerformanceMeasurement>();
        }
        else
        {
            _logger = logger;
            _loggerFactory = null;
        }
        _stopwatch = new Stopwatch();

        if (trackMemory)
        {
            _memoryTracker = new MemoryTracker();
        }

        s_logPerformanceMeasurementInitialized(_logger, operationName, null);
    }

    /// <summary>
    /// Starts the performance measurement.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            s_logAlreadyRunning(_logger, _operationName, null);
            return;
        }

        _memoryTracker?.Checkpoint("Start");
        _stopwatch.Start();
        _isRunning = true;

        s_logStartedMeasuring(_logger, _operationName, null);
    }

    /// <summary>
    /// Stops the performance measurement.
    /// </summary>
    public PerformanceResult Stop()
    {
        if (!_isRunning)
        {
            s_logNotRunning(_logger, _operationName, null);
            return CreateResult();
        }

        _stopwatch.Stop();
        _memoryTracker?.Checkpoint("End");
        _isRunning = false;

        var result = CreateResult();
        s_logCompletedMeasuring(_logger, _operationName, result.Duration.TotalMilliseconds, null);

        return result;
    }

    /// <summary>
    /// Adds a checkpoint during measurement.
    /// </summary>
    public void AddCheckpoint(string name)
    {
        if (!_isRunning)
        {
            s_logCannotAddCheckpoint(_logger, name, null);
            return;
        }

        var checkpoint = new Checkpoint
        {
            Name = name,
            Timestamp = DateTime.UtcNow,
            ElapsedTime = _stopwatch.Elapsed
        };

        _checkpoints.Add(checkpoint);
        _memoryTracker?.Checkpoint(name);

        s_logAddedCheckpoint(_logger, name, checkpoint.ElapsedTime.TotalMilliseconds, null);
    }

    /// <summary>
    /// Measures the execution time of an operation.
    /// </summary>
    public static PerformanceResult Measure(string operationName, Action operation, bool trackMemory = true)
    {
        using var measurement = new PerformanceMeasurement(operationName, trackMemory);
        measurement.Start();
        operation();
        return measurement.Stop();
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    public static async Task<PerformanceResult> MeasureAsync(string operationName, Func<Task> operation, bool trackMemory = true)
    {
        using var measurement = new PerformanceMeasurement(operationName, trackMemory);
        measurement.Start();
        await operation();
        return measurement.Stop();
    }

    /// <summary>
    /// Measures the execution time of an operation with a return value.
    /// </summary>
    public static (T result, PerformanceResult performance) Measure<T>(string operationName, Func<T> operation, bool trackMemory = true)
    {
        using var measurement = new PerformanceMeasurement(operationName, trackMemory);
        measurement.Start();
        var result = operation();
        var performance = measurement.Stop();
        return (result, performance);
    }

    /// <summary>
    /// Measures the execution time of an async operation with a return value.
    /// </summary>
    public static async Task<(T result, PerformanceResult performance)> MeasureAsync<T>(string operationName, Func<Task<T>> operation, bool trackMemory = true)
    {
        using var measurement = new PerformanceMeasurement(operationName, trackMemory);
        measurement.Start();
        var result = await operation();
        var performance = measurement.Stop();
        return (result, performance);
    }

    /// <summary>
    /// Logs the current performance results.
    /// </summary>
    public void LogResults()
    {
        var result = CreateResult();
        LogResults(result);
    }

    /// <summary>
    /// Logs performance results.
    /// </summary>
    public void LogResults(PerformanceResult result)
    {
        s_logPerformanceResults(_logger, result.OperationName, null);
        s_logDuration(_logger, result.Duration.TotalMilliseconds, null);

        if (result.Checkpoints.Length > 0)
        {
            s_logCheckpoints(_logger, null);
            var previousTime = TimeSpan.Zero;

            foreach (var checkpoint in result.Checkpoints)
            {
                var delta = checkpoint.ElapsedTime - previousTime;
                s_logCheckpointDetail(_logger, checkpoint.Name, checkpoint.ElapsedTime.TotalMilliseconds, delta.TotalMilliseconds, null);
                previousTime = checkpoint.ElapsedTime;
            }
        }

        if (result.MemoryReport != null)
        {
            s_logMemoryUsage(_logger, null);
            s_logMemoryDelta(_logger, result.MemoryReport.TotalDelta, null);
            s_logMemoryPeak(_logger, result.MemoryReport.PeakMemory, null);
        }
    }

    private PerformanceResult CreateResult()
    {
        return new PerformanceResult
        {
            OperationName = _operationName,
            Duration = _stopwatch.Elapsed,
            Checkpoints = [.. _checkpoints],
            MemoryReport = _memoryTracker?.GetReport(),
            Timestamp = DateTime.UtcNow
        };
    }

    public void LogResults(long dataSize = 0)
    {
        var result = Stop();
        if (dataSize > 0)
        {
            var throughput = dataSize / result.Duration.TotalSeconds;
            var gbps = throughput / (1024.0 * 1024.0 * 1024.0);
            s_logDuration(_logger, result.Duration.TotalMilliseconds, null);
            _logger.LogInformation($"  Throughput: {gbps:F2} GB/s");
        }
        else
        {
            s_logDuration(_logger, result.Duration.TotalMilliseconds, null);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isRunning)
            {
                _ = Stop();
            }

            _memoryTracker?.Dispose();
            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a checkpoint during performance measurement.
/// </summary>
public sealed class Checkpoint
{
    public required string Name { get; init; }
    public required DateTime Timestamp { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
}

/// <summary>
/// Results of a performance measurement.
/// </summary>
public sealed class PerformanceResult
{
    public required string OperationName { get; init; }
    public required TimeSpan Duration { get; init; }
    public required Checkpoint[] Checkpoints { get; init; }
    public required DateTime Timestamp { get; init; }
    public MemoryReport? MemoryReport { get; init; }

    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double GetThroughput(long operationCount) => operationCount / Duration.TotalSeconds;

    /// <summary>
    /// Gets the time between two checkpoints.
    /// </summary>
    public TimeSpan GetCheckpointDuration(string startCheckpoint, string endCheckpoint)
    {
        var start = Checkpoints.FirstOrDefault(c => c.Name == startCheckpoint);
        var end = Checkpoints.FirstOrDefault(c => c.Name == endCheckpoint);

        if (start != null && end != null)
        {
            return end.ElapsedTime - start.ElapsedTime;
        }

        return TimeSpan.Zero;
    }
}