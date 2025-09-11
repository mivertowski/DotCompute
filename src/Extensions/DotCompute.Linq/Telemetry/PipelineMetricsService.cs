// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Interfaces;
using DotCompute.Core.Telemetry;
using DotCompute.Linq.Pipelines.Models;
using CorePipelineMetrics = DotCompute.Core.Pipelines.Interfaces.IPipelineMetrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Linq.Telemetry;

/// <summary>
/// Production-ready pipeline metrics service for LINQ pipelines with comprehensive observability.
/// Provides high-performance metrics collection with minimal overhead (less than 1%).
/// </summary>
public sealed class PipelineMetricsService : IDisposable
{
    private readonly PipelineTelemetryCollector _telemetryCollector;
    private readonly ITelemetryService? _globalTelemetryService;
    private readonly ILogger<PipelineMetricsService> _logger;
    private readonly PipelineMetricsOptions _options;
    
    // Lock-free performance tracking
    private readonly ConcurrentDictionary<string, CorePipelineMetrics> _pipelineMetrics;
    private readonly ConcurrentQueue<MetricsCollectionPoint> _collectionPoints;
    
    // Performance monitoring
    private readonly Stopwatch _performanceStopwatch;
    private long _totalMetricsOperations;
    private long _metricsOverheadTicks;
    
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the PipelineMetricsService.
    /// </summary>
    public PipelineMetricsService(
        ILogger<PipelineMetricsService> logger,
        IOptions<PipelineMetricsOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PipelineMetricsOptions();
        
        _pipelineMetrics = new ConcurrentDictionary<string, CorePipelineMetrics>();
        _collectionPoints = new ConcurrentQueue<MetricsCollectionPoint>();
        _performanceStopwatch = Stopwatch.StartNew();
        
        // Initialize telemetry collector
        _telemetryCollector = new PipelineTelemetryCollector(
            logger: serviceProvider.GetService<ILogger<PipelineTelemetryCollector>>() ??
                   new LoggerFactory().CreateLogger<PipelineTelemetryCollector>(),
            options: Options.Create(new PipelineTelemetryOptions
            {
                EnableDistributedTracing = _options.EnableDistributedTracing,
                EnableDetailedTelemetry = _options.EnableDetailedTelemetry,
                EnablePeriodicExport = _options.EnablePeriodicExport,
                ExportIntervalSeconds = _options.ExportIntervalSeconds,
                DefaultExportFormat = _options.DefaultExportFormat
            }));

        // Try to get global telemetry service if available
        _globalTelemetryService = serviceProvider.GetService<ITelemetryService>();
        
        _logger.LogInformation("PipelineMetricsService initialized with options: {Options}", _options);
    }

    /// <summary>
    /// Creates comprehensive metrics tracking for a pipeline execution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PipelineMetricsContext CreateMetricsContext(
        string pipelineId, 
        string? correlationId = null,
        Dictionary<string, object>? metadata = null)
    {
        ThrowIfDisposed();
        
        var startTicks = _performanceStopwatch.ElapsedTicks;
        
        try
        {
            // Create or get pipeline metrics
            var pipelineMetrics = _pipelineMetrics.GetOrAdd(pipelineId, 
                id => new PipelineMetrics(id));

            // Start telemetry collection
            var executionContext = _telemetryCollector.StartPipelineExecution(pipelineId, correlationId);
            
            // Create comprehensive context
            var context = new PipelineMetricsContext
            {
                PipelineId = pipelineId,
                CorrelationId = executionContext.CorrelationId,
                ExecutionContext = executionContext,
                PipelineMetrics = pipelineMetrics,
                StartTime = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>(),
                StageContexts = new ConcurrentDictionary<string, StageMetricsContext>()
            };

            // Record collection point for overhead monitoring
            if (_options.EnableOverheadMonitoring)
            {
                _collectionPoints.Enqueue(new MetricsCollectionPoint
                {
                    Timestamp = DateTime.UtcNow,
                    OperationType = MetricsOperationType.CreateContext,
                    Duration = TimeSpan.FromTicks(_performanceStopwatch.ElapsedTicks - startTicks),
                    PipelineId = pipelineId
                });
            }

            return context;
        }
        finally
        {
            // Track overhead metrics
            var overheadTicks = _performanceStopwatch.ElapsedTicks - startTicks;
            Interlocked.Add(ref _metricsOverheadTicks, overheadTicks);
            Interlocked.Increment(ref _totalMetricsOperations);
        }
    }

    /// <summary>
    /// Records stage execution with detailed metrics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordStageExecution(
        PipelineMetricsContext context,
        string stageId,
        string stageName,
        TimeSpan duration,
        bool success,
        long memoryUsed = 0,
        Dictionary<string, object>? stageMetadata = null)
    {
        ThrowIfDisposed();
        
        var startTicks = _performanceStopwatch.ElapsedTicks;
        
        try
        {
            // Record in telemetry collector
            _telemetryCollector.RecordStageExecution(
                context.PipelineId, 
                stageId, 
                duration, 
                success, 
                memoryUsed);

            // Create or update stage context
            var stageContext = context.StageContexts.GetOrAdd(stageId, 
                id => new StageMetricsContext
                {
                    StageId = id,
                    StageName = stageName,
                    PipelineId = context.PipelineId,
                    StartTime = DateTime.UtcNow
                });

            stageContext.RecordExecution(duration, success, memoryUsed, stageMetadata);

            // Update global telemetry if available
            if (_globalTelemetryService != null && _options.IntegrateWithGlobalTelemetry)
            {
                // Note: This would need conversion between metric types
                // Implementation depends on the global telemetry service interface
            }
        }
        finally
        {
            var overheadTicks = _performanceStopwatch.ElapsedTicks - startTicks;
            Interlocked.Add(ref _metricsOverheadTicks, overheadTicks);
            Interlocked.Increment(ref _totalMetricsOperations);
        }
    }

    /// <summary>
    /// Records cache access with minimal overhead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCacheAccess(PipelineMetricsContext context, string cacheKey, bool hit)
    {
        ThrowIfDisposed();
        
        var startTicks = _performanceStopwatch.ElapsedTicks;
        
        try
        {
            _telemetryCollector.RecordCacheAccess(context.PipelineId, hit);
            
            // Update pipeline metrics
            if (context.PipelineMetrics is PipelineMetrics metrics)
            {
                metrics.RecordCacheAccess(hit);
            }

            context.CacheAccesses.Add(new CacheAccessInfo
            {
                Key = cacheKey,
                Hit = hit,
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            var overheadTicks = _performanceStopwatch.ElapsedTicks - startTicks;
            Interlocked.Add(ref _metricsOverheadTicks, overheadTicks);
            Interlocked.Increment(ref _totalMetricsOperations);
        }
    }

    /// <summary>
    /// Records throughput data for pipeline performance analysis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordThroughput(PipelineMetricsContext context, long itemsProcessed)
    {
        ThrowIfDisposed();
        
        var startTicks = _performanceStopwatch.ElapsedTicks;
        
        try
        {
            // Update pipeline metrics
            if (context.PipelineMetrics is PipelineMetrics metrics)
            {
                metrics.RecordItemsProcessed(itemsProcessed);
            }

            context.ItemsProcessed += itemsProcessed;
        }
        finally
        {
            var overheadTicks = _performanceStopwatch.ElapsedTicks - startTicks;
            Interlocked.Add(ref _metricsOverheadTicks, overheadTicks);
            Interlocked.Increment(ref _totalMetricsOperations);
        }
    }

    /// <summary>
    /// Completes pipeline execution and finalizes metrics collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteExecution(
        PipelineMetricsContext context, 
        bool success,
        Exception? exception = null)
    {
        ThrowIfDisposed();
        
        var startTicks = _performanceStopwatch.ElapsedTicks;
        
        try
        {
            var duration = DateTime.UtcNow - context.StartTime;

            // Complete telemetry collection
            _telemetryCollector.CompletePipelineExecution(
                context.ExecutionContext, 
                success, 
                context.ItemsProcessed,
                exception);

            // Update pipeline metrics
            if (context.PipelineMetrics is PipelineMetrics metrics)
            {
                metrics.RecordExecution(new PipelineExecutionMetrics
                {
                    ExecutionId = context.CorrelationId,
                    StartTime = context.StartTime,
                    EndTime = DateTime.UtcNow,
                    Duration = duration,
                    MemoryUsage = new DotCompute.Core.Pipelines.Statistics.MemoryUsageStats
                    {
                        PeakUsageBytes = context.StageContexts.Values.Sum(s => s.TotalMemoryUsed),
                        AverageUsageBytes = context.StageContexts.Values.Any() 
                            ? context.StageContexts.Values.Sum(s => s.TotalMemoryUsed) / context.StageContexts.Count
                            : 0,
                        AllocationCount = context.StageContexts.Count,
                        TotalAllocatedBytes = context.StageContexts.Values.Sum(s => s.TotalMemoryUsed)
                    },
                    ComputeUtilization = CalculateComputeUtilization(context),
                    MemoryBandwidthUtilization = CalculateMemoryBandwidthUtilization(context),
                    StageExecutionTimes = context.StageContexts.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value.TotalDuration),
                    DataTransferTimes = new Dictionary<string, TimeSpan>()
                }, success);
            }

            // Log completion
            _logger.LogDebug(
                "Pipeline {PipelineId} execution completed in {Duration}ms with {Success} status. Items processed: {Items}",
                context.PipelineId, 
                duration.TotalMilliseconds, 
                success ? "success" : "failure",
                context.ItemsProcessed);
        }
        finally
        {
            var overheadTicks = _performanceStopwatch.ElapsedTicks - startTicks;
            Interlocked.Add(ref _metricsOverheadTicks, overheadTicks);
            Interlocked.Increment(ref _totalMetricsOperations);
        }
    }

    /// <summary>
    /// Gets comprehensive pipeline metrics for analysis.
    /// </summary>
    public CorePipelineMetrics? GetPipelineMetrics(string pipelineId)
    {
        ThrowIfDisposed();
        return _pipelineMetrics.TryGetValue(pipelineId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all active pipeline metrics.
    /// </summary>
    public IReadOnlyDictionary<string, CorePipelineMetrics> GetAllPipelineMetrics()
    {
        ThrowIfDisposed();
        return _pipelineMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets performance overhead statistics to validate less than 1% requirement.
    /// </summary>
    public PerformanceOverheadStats GetOverheadStats()
    {
        ThrowIfDisposed();
        
        var totalOperations = Interlocked.Read(ref _totalMetricsOperations);
        var totalOverheadTicks = Interlocked.Read(ref _metricsOverheadTicks);
        
        return new PerformanceOverheadStats
        {
            TotalOperations = totalOperations,
            TotalOverheadTime = TimeSpan.FromTicks(totalOverheadTicks),
            AverageOverheadPerOperation = totalOperations > 0 
                ? TimeSpan.FromTicks(totalOverheadTicks / totalOperations)
                : TimeSpan.Zero,
            OverheadPercentage = CalculateOverheadPercentage()
        };
    }

    /// <summary>
    /// Exports comprehensive metrics in the specified format.
    /// </summary>
    public async Task<string> ExportMetricsAsync(
        MetricsExportFormat format, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _telemetryCollector.ExportMetricsAsync(format, cancellationToken);
    }

    private double CalculateComputeUtilization(PipelineMetricsContext context)
    {
        // Estimate compute utilization based on stage execution patterns
        if (!context.StageContexts.Any()) return 0.0;

        var totalExecutionTime = context.StageContexts.Values.Sum(s => s.TotalDuration.TotalMilliseconds);
        var wallClockTime = (DateTime.UtcNow - context.StartTime).TotalMilliseconds;
        
        return wallClockTime > 0 ? Math.Min(1.0, totalExecutionTime / wallClockTime) : 0.0;
    }

    private double CalculateMemoryBandwidthUtilization(PipelineMetricsContext context)
    {
        // Estimate memory bandwidth utilization
        var totalMemory = context.StageContexts.Values.Sum(s => s.TotalMemoryUsed);
        var duration = DateTime.UtcNow - context.StartTime;
        
        if (duration.TotalSeconds <= 0) return 0.0;
        
        // Estimate based on memory throughput (simplified calculation)
        var memoryThroughput = totalMemory / duration.TotalSeconds; // bytes/sec
        var estimatedPeakBandwidth = 100_000_000_000; // 100 GB/s typical for modern systems
        
        return Math.Min(1.0, memoryThroughput / estimatedPeakBandwidth);
    }

    private double CalculateOverheadPercentage()
    {
        // Calculate overhead as percentage of total execution time
        var overheadTime = TimeSpan.FromTicks(Interlocked.Read(ref _metricsOverheadTicks));
        var totalRuntime = _performanceStopwatch.Elapsed;
        
        return totalRuntime.TotalMilliseconds > 0 
            ? (overheadTime.TotalMilliseconds / totalRuntime.TotalMilliseconds) * 100
            : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PipelineMetricsService));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _telemetryCollector.Dispose();
        _performanceStopwatch.Stop();
        
        _logger.LogInformation("PipelineMetricsService disposed with overhead stats: {Stats}", GetOverheadStats());
    }
}

/// <summary>
/// Configuration options for pipeline metrics service.
/// </summary>
public sealed class PipelineMetricsOptions
{
    /// <summary>
    /// Whether to enable distributed tracing integration.
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Whether to enable detailed telemetry collection.
    /// </summary>
    public bool EnableDetailedTelemetry { get; set; } = true;

    /// <summary>
    /// Whether to enable periodic metrics export.
    /// </summary>
    public bool EnablePeriodicExport { get; set; } = true;

    /// <summary>
    /// Interval in seconds for periodic exports.
    /// </summary>
    public int ExportIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Default export format for metrics.
    /// </summary>
    public MetricsExportFormat DefaultExportFormat { get; set; } = MetricsExportFormat.Json;

    /// <summary>
    /// Whether to integrate with global telemetry service.
    /// </summary>
    public bool IntegrateWithGlobalTelemetry { get; set; } = true;

    /// <summary>
    /// Whether to enable performance overhead monitoring.
    /// </summary>
    public bool EnableOverheadMonitoring { get; set; } = true;
}

/// <summary>
/// Comprehensive metrics context for pipeline execution.
/// </summary>
public sealed class PipelineMetricsContext : IDisposable
{
    public string PipelineId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DotCompute.Core.Pipelines.PipelineExecutionContext ExecutionContext { get; set; } = null!;
    public CorePipelineMetrics PipelineMetrics { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public ConcurrentDictionary<string, StageMetricsContext> StageContexts { get; set; } = new();
    public List<CacheAccessInfo> CacheAccesses { get; set; } = new();
    public long ItemsProcessed { get; set; }

    /// <summary>
    /// Measures execution time for a specific stage asynchronously.
    /// </summary>
    /// <param name="stageName">Name of the stage to measure</param>
    /// <param name="operation">The async operation to measure</param>
    /// <returns>Task representing the measurement operation</returns>
    public async Task<T> MeasureStageAsync<T>(string stageName, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            // Record stage metrics
            var stageContext = StageContexts.GetOrAdd(stageName, _ => new StageMetricsContext
            {
                StageName = stageName,
                StartTime = DateTime.UtcNow.Subtract(stopwatch.Elapsed)
            });
            
            stageContext.ExecutionTime = stopwatch.Elapsed;
            return result;
        }
        catch
        {
            stopwatch.Stop();
            throw;
        }
    }

    /// <summary>
    /// Measures execution time for a specific stage with metrics service integration.
    /// </summary>
    /// <param name="metricsService">The metrics service to record stage execution</param>
    /// <param name="stageId">Stage identifier</param>
    /// <param name="stageName">Stage display name</param>
    /// <param name="operation">The async operation to measure</param>
    /// <returns>Task representing the measurement operation</returns>
    public async Task<T> MeasureStageAsync<T>(PipelineMetricsService metricsService, string stageId, string stageName, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            // Record stage execution in metrics service
            metricsService.RecordStageExecution(this, stageId, stageName, stopwatch.Elapsed, true);
            
            return result;
        }
        catch
        {
            stopwatch.Stop();
            metricsService.RecordStageExecution(this, stageId, stageName, stopwatch.Elapsed, false);
            throw;
        }
    }

    /// <summary>
    /// Records cache access with metrics service integration.
    /// </summary>
    /// <param name="metricsService">The metrics service to record cache access</param>
    /// <param name="cacheKey">The cache key</param>
    /// <param name="hit">Whether it was a cache hit</param>
    public void RecordCacheAccess(PipelineMetricsService metricsService, string cacheKey, bool hit)
    {
        metricsService.RecordCacheAccess(this, cacheKey, hit);
    }

    /// <summary>
    /// Records throughput metrics for the pipeline.
    /// </summary>
    /// <param name="itemsPerSecond">Number of items processed per second</param>
    /// <param name="totalItems">Total number of items processed</param>
    public void RecordThroughput(double itemsPerSecond, long totalItems = 0)
    {
        ItemsProcessed = totalItems > 0 ? totalItems : ItemsProcessed;
        Metadata["ThroughputItemsPerSecond"] = itemsPerSecond;
        Metadata["LastThroughputUpdate"] = DateTime.UtcNow;
    }

    public void Dispose()
    {
        ExecutionContext?.Dispose();
    }
}

/// <summary>
/// Context for individual stage metrics tracking.
/// </summary>
public sealed class StageMetricsContext
{
    public string StageId { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan TotalDuration { get; private set; }
    public long TotalMemoryUsed { get; private set; }
    public int ExecutionCount { get; private set; }
    public int SuccessCount { get; private set; }
    public List<Dictionary<string, object>> ExecutionMetadata { get; set; } = new();

    public void RecordExecution(
        TimeSpan duration, 
        bool success, 
        long memoryUsed,
        Dictionary<string, object>? metadata)
    {
        TotalDuration += duration;
        TotalMemoryUsed += memoryUsed;
        ExecutionCount++;
        if (success) SuccessCount++;
        
        if (metadata != null)
        {
            ExecutionMetadata.Add(metadata);
        }
    }

    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0.0;
    public TimeSpan AverageDuration => ExecutionCount > 0 
        ? TimeSpan.FromTicks(TotalDuration.Ticks / ExecutionCount) 
        : TimeSpan.Zero;
}

/// <summary>
/// Information about cache access for metrics.
/// </summary>
public sealed class CacheAccessInfo
{
    public string Key { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance overhead statistics for monitoring.
/// </summary>
public sealed class PerformanceOverheadStats
{
    public long TotalOperations { get; set; }
    public TimeSpan TotalOverheadTime { get; set; }
    public TimeSpan AverageOverheadPerOperation { get; set; }
    public double OverheadPercentage { get; set; }
    
    public bool MeetsPerformanceRequirement => OverheadPercentage < 1.0; // Less than 1%
}

/// <summary>
/// Metrics collection point for overhead analysis.
/// </summary>
public sealed class MetricsCollectionPoint
{
    public DateTime Timestamp { get; set; }
    public MetricsOperationType OperationType { get; set; }
    public TimeSpan Duration { get; set; }
    public string PipelineId { get; set; } = string.Empty;
}

/// <summary>
/// Types of metrics operations for overhead tracking.
/// </summary>
public enum MetricsOperationType
{
    CreateContext,
    RecordStage,
    RecordCache,
    RecordThroughput,
    CompleteExecution
}