// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Execution.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Metal execution context for managing execution state, resource lifetime tracking,
/// performance metrics collection, and command dependency resolution.
/// Follows CUDA execution context patterns optimized for Metal.
/// </summary>
public sealed partial class MetalExecutionContext : IDisposable, IAsyncDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalExecutionContext> _logger;
    private readonly MetalCommandStream _commandStream;
    private readonly MetalEventManager _eventManager;
    private readonly MetalErrorHandler _errorHandler;
    private readonly ConcurrentDictionary<string, MetalResourceInfo> _trackedResources;
    private readonly ConcurrentDictionary<string, MetalOperationContext> _activeOperations;
    private readonly ConcurrentDictionary<string, List<string>> _dependencyGraph;
    private readonly MetalPerformanceCollector _performanceCollector;
    private readonly Timer _maintenanceTimer;
    private readonly bool _isAppleSilicon;

    // Execution state
    private volatile bool _disposed;
    private volatile bool _executionPaused;
    private long _totalOperationsExecuted;
    private long _totalResourcesTracked;
    private readonly DateTimeOffset _contextCreatedAt;
    private readonly Lock _lockObject = new();

    // Performance tracking
    private readonly ConcurrentQueue<MetalExecutionMetrics> _recentMetrics;
    private const int MAX_RECENT_METRICS = 100;

    public MetalExecutionContext(
        IntPtr device,
        ILogger<MetalExecutionContext> logger,
        MetalExecutionContextOptions? options = null)
    {
        _device = device != IntPtr.Zero ? device : throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextCreatedAt = DateTimeOffset.UtcNow;
        _isAppleSilicon = DetectAppleSilicon();

        var contextOptions = options ?? new MetalExecutionContextOptions();

        // Initialize core components
        var streamLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalCommandStream>.Instance;
        _commandStream = new MetalCommandStream(_device, streamLogger);

        var eventLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalEventManager>.Instance;
        _eventManager = new MetalEventManager(_device, eventLogger);

        var errorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalErrorHandler>.Instance;
        _errorHandler = new MetalErrorHandler(errorLogger, contextOptions.ErrorRecoveryOptions);

        // Initialize tracking collections
        _trackedResources = new ConcurrentDictionary<string, MetalResourceInfo>();
        _activeOperations = new ConcurrentDictionary<string, MetalOperationContext>();
        _dependencyGraph = new ConcurrentDictionary<string, List<string>>();
        _recentMetrics = new ConcurrentQueue<MetalExecutionMetrics>();

        // Initialize performance collector
        _performanceCollector = new MetalPerformanceCollector(contextOptions);

        // Setup maintenance timer
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        LogExecutionContextInitialized(_logger, _isAppleSilicon ? "Apple Silicon" : "Intel Mac", _device);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6800,
        Level = LogLevel.Information,
        Message = "Metal Execution Context initialized for {Architecture}: device={Device}")]
    private static partial void LogExecutionContextInitialized(ILogger logger, string architecture, IntPtr device);

    [LoggerMessage(
        EventId = 6801,
        Level = LogLevel.Debug,
        Message = "Metal operation {OperationId} completed successfully in {Duration}ms")]
    private static partial void LogOperationCompletedSuccessfully(ILogger logger, string operationId, double duration);

    [LoggerMessage(
        EventId = 6802,
        Level = LogLevel.Error,
        Message = "Metal operation {OperationId} failed after {Duration}ms")]
    private static partial void LogOperationFailedAfter(ILogger logger, Exception ex, string operationId, double duration);

    [LoggerMessage(
        EventId = 6803,
        Level = LogLevel.Trace,
        Message = "Tracking Metal resource {ResourceId} of type {Type}, size {Size} bytes")]
    private static partial void LogTrackingResource(ILogger logger, string resourceId, MetalResourceType type, long size);

    [LoggerMessage(
        EventId = 6804,
        Level = LogLevel.Warning,
        Message = "Resource {ResourceId} is already being tracked")]
    private static partial void LogResourceAlreadyTracked(ILogger logger, string resourceId);

    [LoggerMessage(
        EventId = 6805,
        Level = LogLevel.Warning,
        Message = "Error releasing Metal resource {ResourceId}")]
    private static partial void LogErrorReleasingResource(ILogger logger, Exception ex, string resourceId);

    [LoggerMessage(
        EventId = 6806,
        Level = LogLevel.Trace,
        Message = "Untracked Metal resource {ResourceId}")]
    private static partial void LogUntrackedResource(ILogger logger, string resourceId);

    [LoggerMessage(
        EventId = 6807,
        Level = LogLevel.Trace,
        Message = "Added dependency: {Dependent} depends on {Dependency}")]
    private static partial void LogDependencyAdded(ILogger logger, string dependent, string dependency);

    [LoggerMessage(
        EventId = 6808,
        Level = LogLevel.Information,
        Message = "Metal execution context paused")]
    private static partial void LogExecutionPaused(ILogger logger);

    [LoggerMessage(
        EventId = 6809,
        Level = LogLevel.Information,
        Message = "Metal execution context resumed")]
    private static partial void LogExecutionResumed(ILogger logger);

    [LoggerMessage(
        EventId = 6810,
        Level = LogLevel.Trace,
        Message = "Released Metal resource {ResourceId} of type {Type}")]
    private static partial void LogResourceReleased(ILogger logger, string resourceId, MetalResourceType type);

    [LoggerMessage(
        EventId = 6811,
        Level = LogLevel.Warning,
        Message = "Timed out waiting for {Count} active operations to complete")]
    private static partial void LogWaitTimeout(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6812,
        Level = LogLevel.Debug,
        Message = "Metal Execution Context: {Operations} ops executed, {Resources} resources tracked, {ActiveOps} active operations, success rate {SuccessRate:P2}")]
    private static partial void LogMaintenanceStats(ILogger logger, long operations, long resources, int activeOps, double successRate);

    [LoggerMessage(
        EventId = 6813,
        Level = LogLevel.Warning,
        Message = "Error during Metal execution context maintenance")]
    private static partial void LogMaintenanceError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6814,
        Level = LogLevel.Information,
        Message = "Disposing Metal Execution Context...")]
    private static partial void LogDisposingContext(ILogger logger);

    [LoggerMessage(
        EventId = 6815,
        Level = LogLevel.Warning,
        Message = "Error releasing resource {ResourceId} during disposal")]
    private static partial void LogDisposalResourceError(ILogger logger, Exception ex, string resourceId);

    [LoggerMessage(
        EventId = 6816,
        Level = LogLevel.Information,
        Message = "Metal Execution Context disposed: executed {Operations} operations, tracked {Resources} resources, uptime {Uptime}")]
    private static partial void LogContextDisposed(ILogger logger, long operations, long resources, TimeSpan uptime);

    [LoggerMessage(
        EventId = 6817,
        Level = LogLevel.Error,
        Message = "Error during Metal execution context disposal")]
    private static partial void LogDisposalError(ILogger logger, Exception ex);

    #endregion

    /// <summary>
    /// Gets whether the context is running on Apple Silicon
    /// </summary>
    public bool IsAppleSilicon => _isAppleSilicon;

    /// <summary>
    /// Gets the Metal device handle
    /// </summary>
    public IntPtr Device => _device;

    /// <summary>
    /// Gets the command stream manager
    /// </summary>
    public MetalCommandStream CommandStream => _commandStream;

    /// <summary>
    /// Gets the event manager
    /// </summary>
    public MetalEventManager EventManager => _eventManager;

    /// <summary>
    /// Gets the error handler
    /// </summary>
    public MetalErrorHandler ErrorHandler => _errorHandler;

    /// <summary>
    /// Gets whether execution is currently paused
    /// </summary>
    public bool IsExecutionPaused => _executionPaused;

    /// <summary>
    /// Gets the total number of operations executed
    /// </summary>
    public long TotalOperationsExecuted => _totalOperationsExecuted;

    /// <summary>
    /// Gets the total number of resources tracked
    /// </summary>
    public long TotalResourcesTracked => _totalResourcesTracked;

    /// <summary>
    /// Executes an operation with full context management
    /// </summary>
    public async Task<T> ExecuteOperationAsync<T>(
        string operationId,
        Func<MetalOperationExecutionInfo, Task<T>> operation,
        MetalExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_executionPaused)
        {
            throw new InvalidOperationException("Execution context is paused");
        }

        var executionOptions = options ?? new MetalExecutionOptions();
        var startTime = DateTimeOffset.UtcNow;
        var operationContext = new MetalOperationContext
        {
            OperationId = operationId,
            StartTime = startTime,
            Priority = executionOptions.Priority,
            State = MetalOperationState.Initializing
        };

        // Add dependencies to collection
        if (executionOptions.Dependencies != null)
        {
            foreach (var dependency in executionOptions.Dependencies)
            {
                operationContext.Dependencies.Add(dependency);
            }
        }

        _activeOperations[operationId] = operationContext;

        try
        {
            // Wait for dependencies
            await WaitForDependenciesAsync(operationId, operationContext.Dependencies, cancellationToken).ConfigureAwait(false);

            // Create execution info
            var streamHandle = await _commandStream.CreateStreamAsync(
                MetalStreamFlags.Concurrent,

                ConvertPriority(executionOptions.Priority),

                cancellationToken).ConfigureAwait(false);

            var executionInfo = new MetalOperationExecutionInfo
            {
                OperationId = operationId,
                Device = _device,
                StreamHandle = streamHandle,
                EventManager = _eventManager,
                ErrorHandler = _errorHandler,
                StartTime = startTime
            };

            operationContext.State = MetalOperationState.Executing;
            operationContext.StreamId = streamHandle.StreamId;

            // Execute with error handling
            var result = await _errorHandler.ExecuteWithRetryAsync(
                async () => await operation(executionInfo).ConfigureAwait(false),
                operationId,
                cancellationToken).ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;
            operationContext.EndTime = endTime;
            operationContext.State = MetalOperationState.Completed;

            // Record metrics
            RecordOperationMetrics(operationId, startTime, endTime, true);

            // Clean up
            streamHandle.Dispose();
            _ = Interlocked.Increment(ref _totalOperationsExecuted);

            LogOperationCompletedSuccessfully(_logger, operationId, (endTime - startTime).TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            var endTime = DateTimeOffset.UtcNow;
            operationContext.EndTime = endTime;
            operationContext.State = MetalOperationState.Failed;
            operationContext.Error = ex;

            // Record failed metrics
            RecordOperationMetrics(operationId, startTime, endTime, false);

            LogOperationFailedAfter(_logger, ex, operationId, (endTime - startTime).TotalMilliseconds);

            throw;
        }
        finally
        {
            _ = _activeOperations.TryRemove(operationId, out _);
            RemoveDependency(operationId);
        }
    }

    /// <summary>
    /// Tracks a resource for lifetime management
    /// </summary>
    public void TrackResource(string resourceId, IntPtr resourceHandle, MetalResourceType resourceType, long sizeInBytes = 0)
    {
        ThrowIfDisposed();

        var resourceInfo = new MetalResourceInfo
        {
            ResourceId = resourceId,
            Handle = resourceHandle,
            Type = resourceType,
            SizeInBytes = sizeInBytes,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsed = DateTimeOffset.UtcNow
        };

        if (_trackedResources.TryAdd(resourceId, resourceInfo))
        {
            _ = Interlocked.Increment(ref _totalResourcesTracked);
            _performanceCollector.RecordResourceAllocation(resourceType, sizeInBytes);

            LogTrackingResource(_logger, resourceId, resourceType, sizeInBytes);
        }
        else
        {
            LogResourceAlreadyTracked(_logger, resourceId);
        }
    }

    /// <summary>
    /// Stops tracking a resource and optionally releases it
    /// </summary>
    public void UntrackResource(string resourceId, bool releaseResource = false)
    {
        ThrowIfDisposed();

        if (_trackedResources.TryRemove(resourceId, out var resourceInfo))
        {
            _performanceCollector.RecordResourceDeallocation(resourceInfo.Type, resourceInfo.SizeInBytes);

            if (releaseResource && resourceInfo.Handle != IntPtr.Zero)
            {
                try
                {
                    ReleaseResource(resourceInfo);
                }
                catch (Exception ex)
                {
                    LogErrorReleasingResource(_logger, ex, resourceId);
                }
            }

            LogUntrackedResource(_logger, resourceId);
        }
    }

    /// <summary>
    /// Adds a dependency between operations
    /// </summary>
    public void AddDependency(string dependentOperation, string dependencyOperation)
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            var dependencies = _dependencyGraph.GetOrAdd(dependentOperation, _ => []);
            if (!dependencies.Contains(dependencyOperation))
            {
                dependencies.Add(dependencyOperation);
                LogDependencyAdded(_logger, dependentOperation, dependencyOperation);
            }
        }
    }

    /// <summary>
    /// Pauses execution context (stops accepting new operations)
    /// </summary>
    public async Task PauseExecutionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _executionPaused = true;
        LogExecutionPaused(_logger);

        // Wait for active operations to complete
        await WaitForActiveOperationsAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes execution context
    /// </summary>
    public void ResumeExecution()
    {
        ThrowIfDisposed();

        _executionPaused = false;
        LogExecutionResumed(_logger);
    }

    /// <summary>
    /// Gets comprehensive execution statistics
    /// </summary>
    public MetalExecutionStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var recentMetricsArray = _recentMetrics.ToArray();
        var activeOperationCount = _activeOperations.Count;
        var trackedResourceCount = _trackedResources.Count;

        var stats = new MetalExecutionStatistics
        {
            TotalOperationsExecuted = _totalOperationsExecuted,
            TotalResourcesTracked = _totalResourcesTracked,
            ActiveOperations = activeOperationCount,
            TrackedResources = trackedResourceCount,
            IsExecutionPaused = _executionPaused,
            ContextUptime = DateTimeOffset.UtcNow - _contextCreatedAt,
            IsAppleSilicon = _isAppleSilicon,

            // Performance metrics
            AverageOperationDuration = recentMetricsArray.Length > 0
                ? TimeSpan.FromMilliseconds(recentMetricsArray.Average(m => m.DurationMs))
                : TimeSpan.Zero,


            SuccessRate = recentMetricsArray.Length > 0
                ? (double)recentMetricsArray.Count(m => m.Success) / recentMetricsArray.Length
                : 1.0,

            // Component statistics
            StreamStatistics = _commandStream.GetStatistics(),
            EventStatistics = _eventManager.GetStatistics(),
            ErrorStatistics = _errorHandler.GetErrorStatistics()
        };

        // Add resource breakdown
        var resourceBreakdown = _trackedResources.Values
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var kvp in resourceBreakdown)
        {
            stats.ResourceBreakdown[kvp.Key] = kvp.Value;
        }

        // Add performance metrics
        var performanceMetrics = _performanceCollector.GetMetrics();
        foreach (var kvp in performanceMetrics)
        {
            stats.PerformanceMetrics[kvp.Key] = kvp.Value;
        }

        return stats;
    }

    /// <summary>
    /// Performs a comprehensive health check
    /// </summary>
    public async Task<MetalHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var healthCheck = new MetalHealthCheckResult
        {
            CheckTime = DateTimeOffset.UtcNow,
            IsHealthy = true
        };

        try
        {
            // Check GPU availability
            if (!_errorHandler.IsGpuAvailable)
            {
                healthCheck.IsHealthy = false;
                healthCheck.Issues.Add("Metal GPU is not available");
            }

            // Check for stuck operations
            var stuckOperations = _activeOperations.Values
                .Where(op => DateTimeOffset.UtcNow - op.StartTime > TimeSpan.FromMinutes(5))
                .ToList();

            if (stuckOperations.Count > 0)
            {
                healthCheck.IsHealthy = false;
                healthCheck.Issues.Add($"Found {stuckOperations.Count} potentially stuck operations");
            }

            // Check resource leaks
            var oldResources = _trackedResources.Values
                .Where(r => DateTimeOffset.UtcNow - r.LastUsed > TimeSpan.FromHours(1))
                .ToList();

            if (oldResources.Count > 10)
            {
                healthCheck.IsHealthy = false;
                healthCheck.Issues.Add($"Potential resource leak: {oldResources.Count} old resources");
            }

            // Test basic operation
            try
            {
                _ = await ExecuteOperationAsync("health-check",
                    async info =>
                    {
                        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                        return true;
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                healthCheck.IsHealthy = false;
                healthCheck.Issues.Add($"Basic operation test failed: {ex.Message}");
            }

            healthCheck.ComponentHealth["CommandStream"] = _commandStream.GetStatistics().ActiveStreams >= 0;
            healthCheck.ComponentHealth["EventManager"] = _eventManager.GetStatistics().ActiveEvents >= 0;
            healthCheck.ComponentHealth["ErrorHandler"] = _errorHandler.IsGpuAvailable;
        }
        catch (Exception ex)
        {
            healthCheck.IsHealthy = false;
            healthCheck.Issues.Add($"Health check failed: {ex.Message}");
        }

        return healthCheck;
    }

    private async Task WaitForDependenciesAsync(string operationId, IList<string> dependencies, CancellationToken cancellationToken)
    {
        if (dependencies.Count == 0)
        {
            return;
        }

        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            var pendingDependencies = dependencies
                .Where(_activeOperations.ContainsKey)
                .ToList();

            if (pendingDependencies.Count == 0)
            {
                break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        var stillPending = dependencies.Where(_activeOperations.ContainsKey).ToList();
        if (stillPending.Count > 0)
        {
            throw new TimeoutException($"Operation {operationId} timed out waiting for dependencies: {string.Join(", ", stillPending)}");
        }
    }

    private void RemoveDependency(string operationId)
    {
        lock (_lockObject)
        {
            _ = _dependencyGraph.TryRemove(operationId, out _);

            // Remove this operation from other dependencies
            foreach (var kvp in _dependencyGraph.ToList())
            {
                if (kvp.Value.Remove(operationId) && kvp.Value.Count == 0)
                {
                    _ = _dependencyGraph.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private static MetalStreamPriority ConvertPriority(MetalOperationPriority priority)
    {
        return priority switch
        {
            MetalOperationPriority.Low => MetalStreamPriority.Low,
            MetalOperationPriority.High => MetalStreamPriority.High,
            _ => MetalStreamPriority.Normal
        };
    }

    private void RecordOperationMetrics(string operationId, DateTimeOffset startTime, DateTimeOffset endTime, bool success)
    {
        var metrics = new MetalExecutionMetrics
        {
            OperationId = operationId,
            StartTime = startTime,
            EndTime = endTime,
            DurationMs = (endTime - startTime).TotalMilliseconds,
            Success = success
        };

        _recentMetrics.Enqueue(metrics);

        // Keep only recent metrics
        while (_recentMetrics.Count > MAX_RECENT_METRICS)
        {
            _ = _recentMetrics.TryDequeue(out _);
        }

        _performanceCollector.RecordOperation(operationId, metrics.DurationMs, success);
    }

    private void ReleaseResource(MetalResourceInfo resourceInfo)
        // This would implement Metal-specific resource release
        // For now, this is a placeholder
        => LogResourceReleased(_logger, resourceInfo.ResourceId, resourceInfo.Type);

    private async Task WaitForActiveOperationsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        while (!_activeOperations.IsEmpty && DateTimeOffset.UtcNow - startTime < timeout)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (!_activeOperations.IsEmpty)
        {
            LogWaitTimeout(_logger, _activeOperations.Count);
        }
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Clean up old metrics
            while (_recentMetrics.Count > MAX_RECENT_METRICS)
            {
                _ = _recentMetrics.TryDequeue(out _);
            }

            // Update resource usage
            foreach (var resource in _trackedResources.Values)
            {
                // Update last used time based on actual usage
                // This is a placeholder - in practice, we'd track actual usage
            }

            // Perform component maintenance
            _commandStream.OptimizeStreamUsage();
            _eventManager.PerformMaintenance();
            MetalPerformanceCollector.PerformMaintenance();

            // Log periodic statistics
            var stats = GetStatistics();
            LogMaintenanceStats(_logger, stats.TotalOperationsExecuted, stats.TotalResourcesTracked,
                stats.ActiveOperations, stats.SuccessRate);
        }
        catch (Exception ex)
        {
            LogMaintenanceError(_logger, ex);
        }
    }

    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }


        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture ==

                   System.Runtime.InteropServices.Architecture.Arm64;
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            LogDisposingContext(_logger);

            try
            {
                // Pause execution and wait for operations to complete
                _executionPaused = true;
                await WaitForActiveOperationsAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);

                // Dispose components
                _maintenanceTimer?.Dispose();
                _commandStream?.Dispose();
                _eventManager?.Dispose();
                _errorHandler?.Dispose();
                _performanceCollector?.Dispose();

                // Clean up tracked resources
                foreach (var resource in _trackedResources.Values)
                {
                    try
                    {
                        ReleaseResource(resource);
                    }
                    catch (Exception ex)
                    {
                        LogDisposalResourceError(_logger, ex, resource.ResourceId);
                    }
                }

                var finalStats = GetStatistics();
                LogContextDisposed(_logger, finalStats.TotalOperationsExecuted,
                    finalStats.TotalResourcesTracked, finalStats.ContextUptime);
            }
            catch (Exception ex)
            {
                LogDisposalError(_logger, ex);
            }
        }
    }
}

// Supporting types and classes...

/// <summary>
/// Options for configuring Metal execution context
/// </summary>
public sealed class MetalExecutionContextOptions
{
    public MetalErrorRecoveryOptions? ErrorRecoveryOptions { get; set; }
    public bool EnablePerformanceTracking { get; set; } = true;
    public int MaxRecentMetrics { get; set; } = 100;
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Options for Metal operation execution
/// </summary>
public sealed class MetalExecutionOptions
{
    public MetalOperationPriority Priority { get; set; } = MetalOperationPriority.Normal;
    public IReadOnlyList<string>? Dependencies { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool EnableProfiling { get; set; }

    public MetalProfilingLevel ProfilingLevel { get; set; } = MetalProfilingLevel.None;
    public MetalExecutionConfiguration ExecutionConfiguration { get; set; } = new();
}

/// <summary>
/// Priority levels for Metal operations
/// </summary>
public enum MetalOperationPriority
{
    Low,
    Normal,
    High
}

/// <summary>
/// State of a Metal operation
/// </summary>
public enum MetalOperationState
{
    Initializing,
    WaitingForDependencies,
    Executing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Types of Metal resources
/// </summary>
public enum MetalResourceType
{
    Buffer,
    Texture,
    ComputePipelineState,
    RenderPipelineState,
    CommandQueue,
    Library,
    Function
}

/// <summary>
/// Information about an operation execution
/// </summary>
public sealed class MetalOperationExecutionInfo
{
    public string OperationId { get; set; } = string.Empty;
    public IntPtr Device { get; set; }
    public MetalStreamHandle StreamHandle { get; set; } = null!;
    public MetalEventManager EventManager { get; set; } = null!;
    public MetalErrorHandler ErrorHandler { get; set; } = null!;
    public DateTimeOffset StartTime { get; set; }
}

/// <summary>
/// Context for tracking an active operation
/// </summary>
internal sealed class MetalOperationContext
{
    public string OperationId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public MetalOperationPriority Priority { get; set; }
    public IList<string> Dependencies { get; } = [];
    public MetalOperationState State { get; set; }
    public StreamId? StreamId { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Information about a tracked resource
/// </summary>
internal sealed class MetalResourceInfo
{
    public string ResourceId { get; set; } = string.Empty;
    public IntPtr Handle { get; set; }
    public MetalResourceType Type { get; set; }
    public long SizeInBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsed { get; set; }
}

/// <summary>
/// Execution metrics for performance tracking
/// </summary>
internal sealed class MetalExecutionMetrics
{
    public string OperationId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Comprehensive execution statistics
/// </summary>
public sealed class MetalExecutionStatistics
{
    public long TotalOperationsExecuted { get; set; }
    public long TotalResourcesTracked { get; set; }
    public int ActiveOperations { get; set; }
    public int TrackedResources { get; set; }
    public bool IsExecutionPaused { get; set; }
    public TimeSpan ContextUptime { get; set; }
    public bool IsAppleSilicon { get; set; }
    public TimeSpan AverageOperationDuration { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<MetalResourceType, int> ResourceBreakdown { get; } = [];
    public MetalStreamStatistics? StreamStatistics { get; set; }
    public MetalEventStatistics? EventStatistics { get; set; }
    public IReadOnlyDictionary<MetalError, MetalErrorStatistics>? ErrorStatistics { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; } = [];
}

/// <summary>
/// Result of a health check
/// </summary>
public sealed class MetalHealthCheckResult
{
    public DateTimeOffset CheckTime { get; set; }
    public bool IsHealthy { get; set; }
    public IList<string> Issues { get; } = [];
    public Dictionary<string, bool> ComponentHealth { get; } = [];
}

/// <summary>
/// Performance metrics collector
/// </summary>
internal sealed class MetalPerformanceCollector(MetalExecutionContextOptions options) : IDisposable
{
    private readonly MetalExecutionContextOptions _options = options;
    private readonly ConcurrentDictionary<string, object> _metrics = new();
    private volatile bool _disposed;

    public void RecordOperation(string operationId, double durationMs, bool success)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }

        // Record operation metrics

        _ = _metrics.AddOrUpdate($"operation_{operationId}_duration", durationMs, (_, _) => durationMs);
        _ = _metrics.AddOrUpdate($"operation_{operationId}_success", success, (_, _) => success);
    }

    public void RecordResourceAllocation(MetalResourceType type, long sizeInBytes)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }


        var key = $"resource_{type}_allocated";
        _ = _metrics.AddOrUpdate(key, sizeInBytes, (_, existing) => (long)existing + sizeInBytes);
    }

    public void RecordResourceDeallocation(MetalResourceType type, long sizeInBytes)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }


        var key = $"resource_{type}_deallocated";
        _ = _metrics.AddOrUpdate(key, sizeInBytes, (_, existing) => (long)existing + sizeInBytes);
    }

    public Dictionary<string, object> GetMetrics() => _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public static void PerformMaintenance()
    {
        // Cleanup old metrics, aggregate data, etc.
        // Implementation details would go here
    }

    public void Dispose()
    {
        _disposed = true;
        _metrics.Clear();
    }
}
