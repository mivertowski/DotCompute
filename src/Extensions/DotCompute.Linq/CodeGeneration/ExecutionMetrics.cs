// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Captures performance metrics for a kernel execution.
/// </summary>
public sealed class ExecutionMetrics
{
    /// <summary>
    /// Gets or sets the time spent compiling the kernel.
    /// </summary>
    public TimeSpan CompilationTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent executing the kernel on the backend.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent transferring data between host and device.
    /// Only applicable for GPU execution.
    /// </summary>
    public TimeSpan TransferTime { get; set; }

    /// <summary>
    /// Gets or sets the backend that executed this kernel.
    /// </summary>
    public ComputeBackend Backend { get; set; }

    /// <summary>
    /// Gets or sets the total number of elements processed.
    /// </summary>
    public int DataSize { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel was retrieved from cache.
    /// </summary>
    public bool CacheHit { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when execution started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when execution completed.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets the total wall-clock time for the operation.
    /// </summary>
    public TimeSpan TotalTime => CompilationTime + ExecutionTime + TransferTime;

    /// <summary>
    /// Gets the throughput in elements per second.
    /// </summary>
    public double Throughput
    {
        get
        {
            var totalSeconds = TotalTime.TotalSeconds;
            return totalSeconds > 0 ? DataSize / totalSeconds : 0;
        }
    }

    /// <summary>
    /// Gets the execution efficiency (execution time / total time).
    /// Higher values indicate less overhead from compilation and transfers.
    /// </summary>
    public double Efficiency
    {
        get
        {
            var totalMs = TotalTime.TotalMilliseconds;
            return totalMs > 0 ? ExecutionTime.TotalMilliseconds / totalMs : 0;
        }
    }

    /// <summary>
    /// Gets or sets the amount of memory allocated in bytes.
    /// </summary>
    public long MemoryAllocated { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during execution.
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Gets or sets whether the execution succeeded.
    /// </summary>
    public bool Success => Error == null;

    /// <summary>
    /// Gets or sets additional context information.
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// Creates a new ExecutionMetrics instance.
    /// </summary>
    public ExecutionMetrics()
    {
        StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the execution as completed and records the end time.
    /// </summary>
    public void Complete()
    {
        EndTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Records an error that occurred during execution.
    /// </summary>
    public void RecordError(Exception exception)
    {
        Error = exception;
        Complete();
    }

    /// <summary>
    /// Returns a formatted summary of the execution metrics.
    /// </summary>
    public override string ToString()
    {
        var efficiency = Efficiency * 100;
        var throughput = Throughput / 1_000_000; // Convert to millions of elements per second

        return $"ExecutionMetrics[Backend={Backend}, Size={DataSize}, " +
               $"Total={TotalTime.TotalMilliseconds:F2}ms, " +
               $"Exec={ExecutionTime.TotalMilliseconds:F2}ms, " +
               $"Transfer={TransferTime.TotalMilliseconds:F2}ms, " +
               $"Compile={CompilationTime.TotalMilliseconds:F2}ms, " +
               $"Efficiency={efficiency:F1}%, Throughput={throughput:F2}M/s, " +
               $"Cached={CacheHit}, Success={Success}]";
    }

    /// <summary>
    /// Creates a stopwatch-style timer for measuring execution phases.
    /// </summary>
    public static ExecutionMetricsTimer StartTimer(ComputeBackend backend, int dataSize)
    {
        return new ExecutionMetricsTimer(backend, dataSize);
    }
}

/// <summary>
/// Helper class for measuring execution metrics with stopwatch-style timing.
/// </summary>
public sealed class ExecutionMetricsTimer : IDisposable
{
    private readonly ExecutionMetrics _metrics;
    private readonly Stopwatch _totalTimer;
    private Stopwatch? _phaseTimer;
    private ExecutionPhase _currentPhase;

    /// <summary>
    /// Gets the current metrics being collected.
    /// </summary>
    public ExecutionMetrics Metrics => _metrics;

    private enum ExecutionPhase
    {
        None,
        Compilation,
        Transfer,
        Execution
    }

    internal ExecutionMetricsTimer(ComputeBackend backend, int dataSize)
    {
        _metrics = new ExecutionMetrics
        {
            Backend = backend,
            DataSize = dataSize,
            StartTime = DateTime.UtcNow
        };
        _totalTimer = Stopwatch.StartNew();
        _currentPhase = ExecutionPhase.None;
    }

    /// <summary>
    /// Starts timing the compilation phase.
    /// </summary>
    public void StartCompilation()
    {
        EndCurrentPhase();
        _currentPhase = ExecutionPhase.Compilation;
        _phaseTimer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Starts timing the transfer phase.
    /// </summary>
    public void StartTransfer()
    {
        EndCurrentPhase();
        _currentPhase = ExecutionPhase.Transfer;
        _phaseTimer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Starts timing the execution phase.
    /// </summary>
    public void StartExecution()
    {
        EndCurrentPhase();
        _currentPhase = ExecutionPhase.Execution;
        _phaseTimer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Marks the kernel as retrieved from cache (zero compilation time).
    /// </summary>
    public void MarkCacheHit()
    {
        _metrics.CacheHit = true;
        _metrics.CompilationTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Records an error and completes the metrics.
    /// </summary>
    public void RecordError(Exception exception)
    {
        EndCurrentPhase();
        _metrics.RecordError(exception);
    }

    private void EndCurrentPhase()
    {
        if (_phaseTimer == null) return;

        _phaseTimer.Stop();
        var elapsed = _phaseTimer.Elapsed;

        switch (_currentPhase)
        {
            case ExecutionPhase.Compilation:
                _metrics.CompilationTime += elapsed;
                break;
            case ExecutionPhase.Transfer:
                _metrics.TransferTime += elapsed;
                break;
            case ExecutionPhase.Execution:
                _metrics.ExecutionTime += elapsed;
                break;
        }

        _phaseTimer = null;
        _currentPhase = ExecutionPhase.None;
    }

    /// <summary>
    /// Completes the metrics collection and stops all timers.
    /// </summary>
    public void Dispose()
    {
        EndCurrentPhase();
        _totalTimer.Stop();
        _metrics.Complete();
    }
}
