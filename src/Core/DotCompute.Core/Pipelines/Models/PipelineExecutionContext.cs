// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Interfaces.Pipelines.Profiling;

namespace DotCompute.Core.Pipelines.Models;

/// <summary>
/// Execution context for pipeline operations in the Core pipeline system.
/// </summary>
public sealed class PipelineExecutionContext : DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext, IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private string? _pipelineId;
    private DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private TimeSpan _timeout = TimeSpan.FromMinutes(30);
    private CancellationToken _cancellationToken = CancellationToken.None;
    private IAccelerator? _accelerator;
    private IPipelineProfiler? _profiler;

    /// <summary>
    /// Gets the unique identifier for this execution context.
    /// </summary>
    public override string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the correlation ID for tracking across services.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the pipeline identifier.
    /// </summary>
    public override string? PipelineId
    {
        get => _pipelineId;
        set => _pipelineId = value;
    }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    public override DateTimeOffset StartTime => _startTime;

    /// <summary>
    /// Gets the execution timeout.
    /// </summary>
    public override TimeSpan Timeout => _timeout;

    /// <summary>
    /// Gets the cancellation token for the execution.
    /// </summary>
    public override CancellationToken CancellationToken => _cancellationToken;


    /// <summary>
    /// Gets the accelerator to use for execution.
    /// </summary>
    public override IAccelerator? Accelerator => _accelerator;

    /// <summary>
    /// Gets or sets the profiler for pipeline performance analysis.
    /// </summary>
    public override IPipelineProfiler? Profiler
    {
        get => _profiler;
        set => _profiler = value;
    }

    /// <summary>
    /// Sets the pipeline identifier.
    /// </summary>
    public void SetPipelineId(string? pipelineId) => _pipelineId = pipelineId;

    /// <summary>
    /// Sets the execution start time.
    /// </summary>
    public void SetStartTime(DateTimeOffset startTime) => _startTime = startTime;

    /// <summary>
    /// Sets the execution timeout.
    /// </summary>
    public void SetTimeout(TimeSpan timeout) => _timeout = timeout;

    /// <summary>
    /// Sets the cancellation token for the execution.
    /// </summary>
    public void SetCancellationToken(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

    /// <summary>
    /// Sets the accelerator to use for execution.
    /// </summary>
    public void SetAccelerator(IAccelerator? accelerator) => _accelerator = accelerator;

    /// <summary>
    /// Sets the profiler for pipeline performance analysis.
    /// </summary>
    public void SetProfiler(IPipelineProfiler? profiler) => _profiler = profiler;

    /// <summary>
    /// Gets or sets execution options.
    /// </summary>
    public override object? Options { get; set; }

    /// <summary>
    /// Gets or sets the execution state dictionary.
    /// </summary>
    public override Dictionary<string, object> State { get; } = [];

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    public override string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the shared data dictionary for passing data between stages.
    /// </summary>
    public override IDictionary<string, object> SharedData { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the memory manager for pipeline operations.
    /// </summary>
    private IPipelineMemoryManager? _memoryManager;

    /// <summary>
    /// Gets the memory manager for pipeline operations.
    /// </summary>
    public override IPipelineMemoryManager? MemoryManager => _memoryManager;

    /// <summary>
    /// Sets the memory manager for pipeline operations.
    /// </summary>
    public void SetMemoryManager(IPipelineMemoryManager? memoryManager) => _memoryManager = memoryManager;

    /// <summary>
    /// Gets the input data for pipeline execution.
    /// </summary>
    public override IDictionary<string, object> Inputs { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the compute device to use for execution.
    /// </summary>
    private object? _device;

    /// <summary>
    /// Gets the compute device to use for execution.
    /// </summary>
    public override object? Device => _device;

    /// <summary>
    /// Sets the compute device to use for execution.
    /// </summary>
    public void SetDevice(object? device) => _device = device;

    /// <summary>
    /// Gets or sets the execution metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Gets or sets the telemetry data.
    /// </summary>
    public Dictionary<string, object> Telemetry { get; init; } = [];

    /// <summary>
    /// Gets or sets the performance counters.
    /// </summary>
    public Dictionary<string, long> PerformanceCounters { get; init; } = [];

    /// <summary>
    /// Gets or sets the memory allocations tracking.
    /// </summary>
    public Dictionary<string, long> MemoryAllocations { get; init; } = [];

    /// <summary>
    /// Gets or sets the execution history for debugging.
    /// </summary>
    public IList<string> ExecutionHistory { get; init; } = [];

    /// <summary>
    /// Gets or sets whether debugging is enabled.
    /// </summary>
    public bool DebugEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory usage allowed.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = long.MaxValue;

    /// <summary>
    /// Gets or sets whether to enable performance profiling.
    /// </summary>
    public bool ProfilingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the execution priority.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets a value indicating whether the execution has been cancelled.
    /// </summary>
    public override bool IsCancelled => CancellationToken.IsCancellationRequested;

    /// <summary>
    /// Gets a value indicating whether the execution has timed out.
    /// </summary>
    public override bool IsTimedOut => DateTimeOffset.UtcNow - StartTime > Timeout;

    /// <summary>
    /// Adds an entry to the execution history.
    /// </summary>
    /// <param name="entry">The history entry</param>
    public void AddHistoryEntry(string entry) => ExecutionHistory.Add($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {entry}");

    /// <summary>
    /// Increments a performance counter.
    /// </summary>
    /// <param name="counterName">The counter name</param>
    /// <param name="increment">The increment value</param>
    public void IncrementCounter(string counterName, long increment = 1) => PerformanceCounters[counterName] = PerformanceCounters.GetValueOrDefault(counterName, 0) + increment;

    /// <summary>
    /// Records memory allocation.
    /// </summary>
    /// <param name="category">The allocation category</param>
    /// <param name="bytes">The number of bytes allocated</param>
    public void RecordMemoryAllocation(string category, long bytes) => MemoryAllocations[category] = MemoryAllocations.GetValueOrDefault(category, 0) + bytes;

    /// <summary>
    /// Creates a child context for sub-pipeline execution.
    /// </summary>
    /// <param name="childId">The child context ID</param>
    /// <returns>A new child context</returns>
    public PipelineExecutionContext CreateChildContext(string childId)
    {
        var childContext = new PipelineExecutionContext();
        childContext.CorrelationId = CorrelationId;
        childContext.SetPipelineId($"{PipelineId}.{childId}");
        childContext.SetStartTime(DateTimeOffset.UtcNow);
        childContext.SetTimeout(Timeout);
        childContext.SetCancellationToken(CancellationToken);
        childContext.SetDevice(Device);
        childContext.SetAccelerator(Accelerator);
        childContext.SetProfiler(Profiler);
        childContext.Options = Options;
        childContext.SetMemoryManager(MemoryManager);

        // Copy input data
        foreach (var kvp in Inputs)
        {
            childContext.Inputs[kvp.Key] = kvp.Value;
        }
        childContext.DebugEnabled = DebugEnabled;
        childContext.MaxMemoryUsage = MaxMemoryUsage;
        childContext.ProfilingEnabled = ProfilingEnabled;
        childContext.Priority = Priority;

        // Copy shared data and metadata
        foreach (var kvp in SharedData)
        {
            childContext.SharedData[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in Metadata)
        {
            childContext.Metadata[kvp.Key] = kvp.Value;
        }

        return childContext;
    }

    /// <summary>
    /// Disposes the execution context and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clean up any disposable resources in SharedData

        foreach (var value in SharedData.Values.OfType<IDisposable>())
        {
            try
            {
                value.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        SharedData.Clear();
        Metadata.Clear();
        Telemetry.Clear();
        PerformanceCounters.Clear();
        MemoryAllocations.Clear();
        ExecutionHistory.Clear();
        Inputs.Clear();

        // Memory manager disposal is handled in DisposeAsync
        _disposed = true;
    }

    /// <summary>
    /// Asynchronously disposes the execution context and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Clean up any disposable resources in SharedData

        foreach (var value in SharedData.Values.OfType<IDisposable>())
        {
            try
            {
                value.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        // Clean up any async disposable resources in SharedData
        foreach (var value in SharedData.Values.OfType<IAsyncDisposable>())
        {
            try
            {
                await value.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        SharedData.Clear();
        Metadata.Clear();
        Telemetry.Clear();
        PerformanceCounters.Clear();
        MemoryAllocations.Clear();
        ExecutionHistory.Clear();
        Inputs.Clear();

        // Dispose memory manager if we own it
        if (MemoryManager != null)
        {
            try
            {
                await MemoryManager.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _disposed = true;
    }
}

/// <summary>
/// Options for pipeline execution.
/// </summary>
public sealed class PipelineExecutionOptions
{
    /// <summary>
    /// Gets or sets whether to enable parallel execution.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum degree of parallelism.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to enable memory optimization.
    /// </summary>
    public bool EnableMemoryOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable automatic retry on failure.
    /// </summary>
    public bool EnableAutoRetry { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry count.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to enable caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache TTL.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets whether to continue execution on error.
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets additional configuration options.
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; init; } = [];

    /// <summary>
    /// Gets the default execution options.
    /// </summary>
    public static PipelineExecutionOptions Default { get; } = new();
}