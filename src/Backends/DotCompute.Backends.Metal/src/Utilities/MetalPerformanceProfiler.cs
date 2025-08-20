// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Provides performance profiling capabilities for Metal operations.
/// </summary>
public sealed class MetalPerformanceProfiler : IDisposable
{
    private readonly ILogger<MetalPerformanceProfiler> _logger;
    private readonly Dictionary<string, PerformanceMetrics> _metrics = [];
    private readonly object _lock = new();
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the MetalPerformanceProfiler.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public MetalPerformanceProfiler(ILogger<MetalPerformanceProfiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts profiling an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being profiled.</param>
    /// <returns>A disposable profiling session.</returns>
    public IDisposable Profile(string operationName)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));
        }

        return new ProfilingSession(this, operationName);
    }

    /// <summary>
    /// Records the completion of an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="elapsed">Time taken for the operation.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public void RecordOperation(string operationName, TimeSpan elapsed, bool success = true)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        lock (_lock)
        {
            if (!_metrics.TryGetValue(operationName, out var metrics))
            {
                metrics = new PerformanceMetrics(operationName);
                _metrics[operationName] = metrics;
            }

            metrics.RecordExecution(elapsed, success);

            // Log slow operations
            if (elapsed.TotalMilliseconds > 100) // Log operations > 100ms
            {
                _logger.LogWarning("Slow Metal operation detected: {Operation} took {Duration:F2}ms", 
                    operationName, elapsed.TotalMilliseconds);
            }
            else if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Metal operation completed: {Operation} took {Duration:F2}ms", 
                    operationName, elapsed.TotalMilliseconds);
            }
        }
    }

    /// <summary>
    /// Gets performance metrics for a specific operation.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <returns>Performance metrics or null if not found.</returns>
    public PerformanceMetrics? GetMetrics(string operationName)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        lock (_lock)
        {
            _metrics.TryGetValue(operationName, out var metrics);
            return metrics;
        }
    }

    /// <summary>
    /// Gets all performance metrics.
    /// </summary>
    /// <returns>Dictionary of operation names to metrics.</returns>
    public Dictionary<string, PerformanceMetrics> GetAllMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        lock (_lock)
        {
            return new Dictionary<string, PerformanceMetrics>(_metrics);
        }
    }

    /// <summary>
    /// Resets all performance metrics.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        lock (_lock)
        {
            _metrics.Clear();
            _logger.LogInformation("Reset all Metal performance metrics");
        }
    }

    /// <summary>
    /// Generates a performance report.
    /// </summary>
    /// <returns>A formatted performance report.</returns>
    public string GenerateReport()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        lock (_lock)
        {
            if (_metrics.Count == 0)
            {
                return "No performance data available.";
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine("Metal Performance Report");
            report.AppendLine(new string('=', 50));

            foreach (var kvp in _metrics.OrderByDescending(x => x.Value.TotalTime))
            {
                var metrics = kvp.Value;
                report.AppendLine();
                report.AppendLine($"Operation: {metrics.OperationName}");
                report.AppendLine($"  Executions: {metrics.ExecutionCount:N0}");
                report.AppendLine($"  Success Rate: {metrics.SuccessRate:P2}");
                report.AppendLine($"  Total Time: {metrics.TotalTime.TotalMilliseconds:F2} ms");
                report.AppendLine($"  Average Time: {metrics.AverageTime.TotalMilliseconds:F2} ms");
                report.AppendLine($"  Min Time: {metrics.MinTime.TotalMilliseconds:F2} ms");
                report.AppendLine($"  Max Time: {metrics.MaxTime.TotalMilliseconds:F2} ms");

                if (metrics.ExecutionCount > 1)
                {
                    var variance = metrics.TimeVariance;
                    var stdDev = Math.Sqrt(variance);
                    report.AppendLine($"  Std Dev: {stdDev:F2} ms");
                }
            }

            return report.ToString();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_logger.IsEnabled(LogLevel.Information) && _metrics.Count > 0)
            {
                _logger.LogInformation("Metal Performance Summary:\n{Report}", GenerateReport());
            }
            
            _metrics.Clear();
        }

        _logger.LogDebug("Disposed Metal performance profiler");
    }

    /// <summary>
    /// Represents a profiling session for a single operation.
    /// </summary>
    private sealed class ProfilingSession : IDisposable
    {
        private readonly MetalPerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public ProfilingSession(MetalPerformanceProfiler profiler, string operationName)
        {
            _profiler = profiler;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            _profiler.RecordOperation(_operationName, _stopwatch.Elapsed, success: true);
        }
    }
}

/// <summary>
/// Represents performance metrics for a specific operation.
/// </summary>
public sealed class PerformanceMetrics
{
    private readonly List<double> _executionTimes = [];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the PerformanceMetrics class.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    public PerformanceMetrics(string operationName)
    {
        OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
    }

    /// <summary>
    /// Gets the name of the operation.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the total number of executions.
    /// </summary>
    public int ExecutionCount { get; private set; }

    /// <summary>
    /// Gets the number of successful executions.
    /// </summary>
    public int SuccessfulExecutions { get; private set; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessfulExecutions / ExecutionCount : 0.0;

    /// <summary>
    /// Gets the total time spent on this operation.
    /// </summary>
    public TimeSpan TotalTime { get; private set; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageTime => ExecutionCount > 0 
        ? TimeSpan.FromMilliseconds(TotalTime.TotalMilliseconds / ExecutionCount) 
        : TimeSpan.Zero;

    /// <summary>
    /// Gets the minimum execution time.
    /// </summary>
    public TimeSpan MinTime { get; private set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Gets the maximum execution time.
    /// </summary>
    public TimeSpan MaxTime { get; private set; }

    /// <summary>
    /// Gets the variance of execution times in milliseconds squared.
    /// </summary>
    public double TimeVariance
    {
        get
        {
            lock (_lock)
            {
                if (_executionTimes.Count < 2)
                {
                    return 0.0;
                }

                var mean = _executionTimes.Average();
                return _executionTimes.Select(x => Math.Pow(x - mean, 2)).Average();
            }
        }
    }

    /// <summary>
    /// Records an execution of this operation.
    /// </summary>
    /// <param name="elapsed">Time taken for the execution.</param>
    /// <param name="success">Whether the execution was successful.</param>
    public void RecordExecution(TimeSpan elapsed, bool success)
    {
        lock (_lock)
        {
            ExecutionCount++;
            if (success)
            {
                SuccessfulExecutions++;
            }

            TotalTime = TotalTime.Add(elapsed);

            if (elapsed < MinTime)
            {
                MinTime = elapsed;
            }

            if (elapsed > MaxTime)
            {
                MaxTime = elapsed;
            }

            // Store execution times for variance calculation (limit to last 1000 entries)
            _executionTimes.Add(elapsed.TotalMilliseconds);
            if (_executionTimes.Count > 1000)
            {
                _executionTimes.RemoveAt(0);
            }
        }
    }
}