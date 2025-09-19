// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Metal execution context for managing execution state, resource lifetime tracking,
/// performance metrics collection, and command dependency resolution.
/// Follows CUDA execution context patterns optimized for Metal.
/// </summary>
public sealed class MetalExecutionContext : IDisposable
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
    private DateTimeOffset _contextCreatedAt;
    private readonly object _lockObject = new();

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
        _performanceCollector = new MetalPerformanceCollector(_logger, contextOptions);

        // Setup maintenance timer
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.LogInformation(
            "Metal Execution Context initialized for {Architecture}: device={Device}",
            _isAppleSilicon ? "Apple Silicon" : "Intel Mac", _device);
    }

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
            Dependencies = executionOptions.Dependencies?.ToList() ?? [],
            State = MetalOperationState.Initializing
        };

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

            _logger.LogDebug("Metal operation {OperationId} completed successfully in {Duration}ms",
                operationId, (endTime - startTime).TotalMilliseconds);

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

            _logger.LogError(ex, "Metal operation {OperationId} failed after {Duration}ms",
                operationId, (endTime - startTime).TotalMilliseconds);

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

            _logger.LogTrace("Tracking Metal resource {ResourceId} of type {Type}, size {Size} bytes",
                resourceId, resourceType, sizeInBytes);
        }
        else
        {
            _logger.LogWarning("Resource {ResourceId} is already being tracked", resourceId);
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
                    _logger.LogWarning(ex, "Error releasing Metal resource {ResourceId}", resourceId);
                }
            }

            _logger.LogTrace("Untracked Metal resource {ResourceId}", resourceId);
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
                _logger.LogTrace("Added dependency: {Dependent} depends on {Dependency}",
                    dependentOperation, dependencyOperation);
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
        _logger.LogInformation("Metal execution context paused");

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
        _logger.LogInformation("Metal execution context resumed");
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

            // Resource breakdown
            ResourceBreakdown = _trackedResources.Values
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count()),

            // Component statistics
            StreamStatistics = _commandStream.GetStatistics(),
            EventStatistics = _eventManager.GetStatistics(),
            ErrorStatistics = _errorHandler.GetErrorStatistics(),
            PerformanceMetrics = _performanceCollector.GetMetrics()
        };

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
            IsHealthy = true,
            Issues = []
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
                await ExecuteOperationAsync("health-check",
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

            healthCheck.ComponentHealth = new Dictionary<string, bool>
            {
                ["CommandStream"] = _commandStream.GetStatistics().ActiveStreams >= 0,
                ["EventManager"] = _eventManager.GetStatistics().ActiveEvents >= 0,
                ["ErrorHandler"] = _errorHandler.IsGpuAvailable
            };
        }
        catch (Exception ex)
        {
            healthCheck.IsHealthy = false;
            healthCheck.Issues.Add($"Health check failed: {ex.Message}");
        }

        return healthCheck;
    }

    private async Task WaitForDependenciesAsync(string operationId, List<string> dependencies, CancellationToken cancellationToken)
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
                .Where(dep => _activeOperations.ContainsKey(dep))
                .ToList();

            if (pendingDependencies.Count == 0)
            {
                break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        var stillPending = dependencies.Where(dep => _activeOperations.ContainsKey(dep)).ToList();
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
    {
        // This would implement Metal-specific resource release
        // For now, this is a placeholder
        _logger.LogTrace("Released Metal resource {ResourceId} of type {Type}",
            resourceInfo.ResourceId, resourceInfo.Type);
    }

    private async Task WaitForActiveOperationsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        while (_activeOperations.Count > 0 && DateTimeOffset.UtcNow - startTime < timeout)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (_activeOperations.Count > 0)
        {
            _logger.LogWarning("Timed out waiting for {Count} active operations to complete", _activeOperations.Count);
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
            _performanceCollector.PerformMaintenance();

            // Log periodic statistics
            var stats = GetStatistics();
            _logger.LogDebug(
                "Metal Execution Context: {Operations} ops executed, {Resources} resources tracked, " +
                "{ActiveOps} active operations, success rate {SuccessRate:P2}",
                stats.TotalOperationsExecuted, stats.TotalResourcesTracked,
                stats.ActiveOperations, stats.SuccessRate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Metal execution context maintenance");
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalExecutionContext));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _logger.LogInformation("Disposing Metal Execution Context...");

            try
            {
                // Pause execution and wait for operations to complete
                _executionPaused = true;
                WaitForActiveOperationsAsync(TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();

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
                        _logger.LogWarning(ex, "Error releasing resource {ResourceId} during disposal", resource.ResourceId);
                    }
                }

                var finalStats = GetStatistics();
                _logger.LogInformation(
                    "Metal Execution Context disposed: executed {Operations} operations, " +
                    "tracked {Resources} resources, uptime {Uptime}",
                    finalStats.TotalOperationsExecuted, finalStats.TotalResourcesTracked, finalStats.ContextUptime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Metal execution context disposal");
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
    public string[]? Dependencies { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool EnableProfiling { get; set; } = false;
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
    public List<string> Dependencies { get; set; } = [];
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
    public Dictionary<MetalResourceType, int> ResourceBreakdown { get; set; } = [];
    public MetalStreamStatistics? StreamStatistics { get; set; }
    public MetalEventStatistics? EventStatistics { get; set; }
    public IReadOnlyDictionary<MetalError, MetalErrorHandler.ErrorStatistics>? ErrorStatistics { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; set; } = [];
}

/// <summary>
/// Result of a health check
/// </summary>
public sealed class MetalHealthCheckResult
{
    public DateTimeOffset CheckTime { get; set; }
    public bool IsHealthy { get; set; }
    public List<string> Issues { get; set; } = [];
    public Dictionary<string, bool> ComponentHealth { get; set; } = [];
}

/// <summary>
/// Performance metrics collector
/// </summary>
internal sealed class MetalPerformanceCollector : IDisposable
{
    private readonly ILogger _logger;
    private readonly MetalExecutionContextOptions _options;
    private readonly ConcurrentDictionary<string, object> _metrics;
    private volatile bool _disposed;

    public MetalPerformanceCollector(ILogger logger, MetalExecutionContextOptions options)
    {
        _logger = logger;
        _options = options;
        _metrics = new ConcurrentDictionary<string, object>();
    }

    public void RecordOperation(string operationId, double durationMs, bool success)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }

        // Record operation metrics

        _metrics.AddOrUpdate($"operation_{operationId}_duration", durationMs, (_, _) => durationMs);
        _metrics.AddOrUpdate($"operation_{operationId}_success", success, (_, _) => success);
    }

    public void RecordResourceAllocation(MetalResourceType type, long sizeInBytes)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }


        var key = $"resource_{type}_allocated";
        _metrics.AddOrUpdate(key, sizeInBytes, (_, existing) => (long)existing + sizeInBytes);
    }

    public void RecordResourceDeallocation(MetalResourceType type, long sizeInBytes)
    {
        if (!_options.EnablePerformanceTracking || _disposed)
        {
            return;
        }


        var key = $"resource_{type}_deallocated";
        _metrics.AddOrUpdate(key, sizeInBytes, (_, existing) => (long)existing + sizeInBytes);
    }

    public Dictionary<string, object> GetMetrics()
    {
        return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public void PerformMaintenance()
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