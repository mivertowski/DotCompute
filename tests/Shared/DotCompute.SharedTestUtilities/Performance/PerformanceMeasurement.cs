// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.SharedTestUtilities.Performance;

/// <summary>
/// Comprehensive performance measurement utility for benchmarking operations.
/// </summary>
public sealed class PerformanceMeasurement : IDisposable
{
    private readonly ILogger<PerformanceMeasurement> _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly MemoryTracker? _memoryTracker;
    private readonly List<Checkpoint> _checkpoints = new();
    private bool _disposed;
    private bool _isRunning;

    public string OperationName => _operationName;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public bool IsRunning => _isRunning;

    public PerformanceMeasurement(string operationName, bool trackMemory = true, ILogger<PerformanceMeasurement>? logger = null)
    {
        _operationName = operationName;
        _logger = logger ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PerformanceMeasurement>();
        _stopwatch = new Stopwatch();

        if (trackMemory)
        {
            _memoryTracker = new MemoryTracker();
        }

        _logger.LogDebug("Performance measurement initialized for '{Operation}'", operationName);
    }

    /// <summary>
    /// Starts the performance measurement.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogWarning("Performance measurement for '{Operation}' is already running", _operationName);
            return;
        }

        _memoryTracker?.Checkpoint("Start");
        _stopwatch.Start();
        _isRunning = true;

        _logger.LogDebug("Started measuring '{Operation}'", _operationName);
    }

    /// <summary>
    /// Stops the performance measurement.
    /// </summary>
    public PerformanceResult Stop()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Performance measurement for '{Operation}' is not running", _operationName);
            return CreateResult();
        }

        _stopwatch.Stop();
        _memoryTracker?.Checkpoint("End");
        _isRunning = false;

        var result = CreateResult();
        _logger.LogInformation("Completed measuring '{Operation}': {Duration:F3}ms",
            _operationName, result.Duration.TotalMilliseconds);

        return result;
    }

    /// <summary>
    /// Adds a checkpoint during measurement.
    /// </summary>
    public void AddCheckpoint(string name)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Cannot add checkpoint '{Name}' - measurement is not running", name);
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

        _logger.LogDebug("Added checkpoint '{Name}' at {Elapsed:F3}ms", name, checkpoint.ElapsedTime.TotalMilliseconds);
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
        _logger.LogInformation("Performance Results for '{Operation}':", result.OperationName);
        _logger.LogInformation("  Duration: {Duration:F3}ms", result.Duration.TotalMilliseconds);

        if (result.Checkpoints.Length > 0)
        {
            _logger.LogInformation("  Checkpoints:");
            TimeSpan previousTime = TimeSpan.Zero;

            foreach (var checkpoint in result.Checkpoints)
            {
                var delta = checkpoint.ElapsedTime - previousTime;
                _logger.LogInformation("    {Name}: {Elapsed:F3}ms (+{Delta:F3}ms)",
                    checkpoint.Name, checkpoint.ElapsedTime.TotalMilliseconds, delta.TotalMilliseconds);
                previousTime = checkpoint.ElapsedTime;
            }
        }

        if (result.MemoryReport != null)
        {
            _logger.LogInformation("  Memory Usage:");
            _logger.LogInformation("    Delta: {Delta:+N0} bytes", result.MemoryReport.TotalDelta);
            _logger.LogInformation("    Peak: {Peak:N0} bytes", result.MemoryReport.PeakMemory);
        }
    }

    private PerformanceResult CreateResult()
    {
        return new PerformanceResult
        {
            OperationName = _operationName,
            Duration = _stopwatch.Elapsed,
            Checkpoints = _checkpoints.ToArray(),
            MemoryReport = _memoryTracker?.GetReport(),
            Timestamp = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isRunning)
            {
                Stop();
            }

            _memoryTracker?.Dispose();
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
    public double GetThroughput(long operationCount)
    {
        return operationCount / Duration.TotalSeconds;
    }

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