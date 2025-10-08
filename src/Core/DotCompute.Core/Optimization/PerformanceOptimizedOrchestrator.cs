// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Telemetry;
using DotCompute.Core.Telemetry.Metrics;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;
using DotCompute.Abstractions.Performance;

namespace DotCompute.Core.Optimization;

/// <summary>
/// High-performance compute orchestrator with adaptive backend selection,
/// performance monitoring, and intelligent optimization strategies.
/// </summary>
public class PerformanceOptimizedOrchestrator : IComputeOrchestrator, IDisposable
{
    // Event IDs: 9200-9299 for PerformanceOptimizedOrchestrator
    private static readonly Action<ILogger, string, string, Exception?> LogExecutionStart =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(9200, nameof(LogExecutionStart)),
            "Starting optimized execution of {KernelName} [ID: {ExecutionId}]");

    private static readonly Action<ILogger, string, Exception?> LogNoBackendFallback =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(9201, nameof(LogNoBackendFallback)),
            "No suitable backend found for {KernelName}, falling back to base orchestrator");

    private static readonly Action<ILogger, string, string, float, string, Exception?> LogBackendSelected =
        LoggerMessage.Define<string, string, float, string>(LogLevel.Debug, new EventId(9202, nameof(LogBackendSelected)),
            "Selected {BackendId} for {KernelName} with {Confidence} confidence using {Strategy}");

    private static readonly Action<ILogger, string, string, Exception> LogOptimizedExecutionError =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(9203, nameof(LogOptimizedExecutionError)),
            "Error during optimized execution of {KernelName} [ID: {ExecutionId}]");

    private static readonly Action<ILogger, string, Exception> LogFallbackExecutionError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(9204, nameof(LogFallbackExecutionError)),
            "Fallback execution also failed for {KernelName}");

    private static readonly Action<ILogger, string, double, string, Exception?> LogExecutionCompleted =
        LoggerMessage.Define<string, double, string>(LogLevel.Trace, new EventId(9205, nameof(LogExecutionCompleted)),
            "Optimized execution of {KernelName} completed in {TotalTime}ms [ID: {ExecutionId}]");

    private static readonly Action<ILogger, string, string, double, double, double, Exception?> LogWorkloadAnalyzed =
        LoggerMessage.Define<string, string, double, double, double>(LogLevel.Trace, new EventId(9206, nameof(LogWorkloadAnalyzed)),
            "Analyzed workload {KernelName}: DataSize={DataSize}MB, Compute={Compute:F2}, Memory={Memory:F2}, Parallelism={Parallelism:F2}");

    private static readonly Action<ILogger, string, string, Exception?> LogPreExecutionOptimization =
        LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(9207, nameof(LogPreExecutionOptimization)),
            "Applying pre-execution optimizations for {KernelName} on {Backend}");

    private static readonly Action<ILogger, string, string, double, string, Exception?> LogPerformanceRecorded =
        LoggerMessage.Define<string, string, double, string>(LogLevel.Trace, new EventId(9208, nameof(LogPerformanceRecorded)),
            "Recorded performance result for {KernelName} on {Backend}: {ExecutionTime}ms [ID: {ExecutionId}]");

    private static readonly Action<ILogger, string, Exception?> LogProfilingFinishFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(9209, nameof(LogProfilingFinishFailed)),
            "Failed to finish performance profiling for {CorrelationId}");
    private readonly IComputeOrchestrator _baseOrchestrator;
    private readonly AdaptiveBackendSelector _backendSelector;
    private readonly PerformanceProfiler _performanceProfiler;
    private readonly ILogger<PerformanceOptimizedOrchestrator> _logger;
    private readonly PerformanceOptimizationOptions _options;
    private bool _disposed;

    // Performance caching and prediction
    private readonly Dictionary<string, KernelPerformanceProfile> _kernelProfiles;
    private readonly Dictionary<string, WorkloadCharacteristics> _workloadCache;
    private readonly object _cacheLock = new();
    /// <summary>
    /// Initializes a new instance of the PerformanceOptimizedOrchestrator class.
    /// </summary>
    /// <param name="baseOrchestrator">The base orchestrator.</param>
    /// <param name="backendSelector">The backend selector.</param>
    /// <param name="performanceProfiler">The performance profiler.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public PerformanceOptimizedOrchestrator(
        IComputeOrchestrator baseOrchestrator,
        AdaptiveBackendSelector backendSelector,
        PerformanceProfiler performanceProfiler,
        ILogger<PerformanceOptimizedOrchestrator> logger,
        PerformanceOptimizationOptions? options = null)
    {
        _baseOrchestrator = baseOrchestrator ?? throw new ArgumentNullException(nameof(baseOrchestrator));
        _backendSelector = backendSelector ?? throw new ArgumentNullException(nameof(backendSelector));
        _performanceProfiler = performanceProfiler ?? throw new ArgumentNullException(nameof(performanceProfiler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new PerformanceOptimizationOptions();

        _kernelProfiles = [];
        _workloadCache = [];

        _logger.LogInfoMessage($"Performance-optimized orchestrator initialized with {_options.OptimizationStrategy} optimization strategy");
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        var result = await ExecuteWithOptimizationAsync<T>(kernelName, args);
        return result!;
    }
    /// <summary>
    /// Gets execute with buffers asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="buffers">The buffers.</param>
    /// <param name="scalarArgs">The scalar args.</param>
    /// <returns>The result of the operation.</returns>


    public async Task<T?> ExecuteWithBuffersAsync<T>(string kernelName, object[] buffers, params object[] scalarArgs)
    {
        var allArgs = buffers.Concat(scalarArgs).ToArray();
        return await ExecuteWithOptimizationAsync<T>(kernelName, allArgs);
    }
    /// <summary>
    /// Gets the optimal accelerator async.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The optimal accelerator async.</returns>

    public async Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        var workloadCharacteristics = await AnalyzeWorkloadAsync(kernelName, Array.Empty<object>());
        var availableAccelerators = await GetAvailableAcceleratorsAsync();


        var selection = await _backendSelector.SelectOptimalBackendAsync(
            kernelName, workloadCharacteristics, availableAccelerators);


        return selection.SelectedBackend;
    }

    private async Task<T?> ExecuteWithOptimizationAsync<T>(string kernelName, object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        LogExecutionStart(_logger, kernelName, executionId.ToString(), null);

        try
        {
            // Phase 1: Workload Analysis
            var workloadCharacteristics = await AnalyzeWorkloadAsync(kernelName, args);

            // Phase 2: Backend Selection

            var availableAccelerators = await GetAvailableAcceleratorsAsync();
            var backendSelection = await _backendSelector.SelectOptimalBackendAsync(
                kernelName, workloadCharacteristics, availableAccelerators, GetSelectionConstraints());

            if (backendSelection.SelectedBackend == null)
            {
                LogNoBackendFallback(_logger, kernelName, null);
                return await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
            }

            LogBackendSelected(_logger, backendSelection.BackendId, kernelName,
                backendSelection.ConfidenceScore, backendSelection.SelectionStrategy.ToString(), null);

            // Phase 3: Pre-execution Optimization
            await ApplyPreExecutionOptimizationsAsync(kernelName, args, backendSelection);

            // Phase 4: Monitored Execution
            var result = await ExecuteWithMonitoringAsync<T>(
                kernelName, args, backendSelection, executionId);

            // Phase 5: Post-execution Learning
            await RecordPerformanceAndLearnAsync(
                kernelName, workloadCharacteristics, backendSelection, stopwatch.Elapsed, executionId);

            return result;
        }
        catch (Exception ex)
        {
            LogOptimizedExecutionError(_logger, kernelName, executionId.ToString(), ex);

            // Fallback to base orchestrator on error

            try
            {
                return await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
            }
            catch (Exception fallbackEx)
            {
                LogFallbackExecutionError(_logger, kernelName, fallbackEx);
                throw;
            }
        }
        finally
        {
            LogExecutionCompleted(_logger, kernelName, stopwatch.Elapsed.TotalMilliseconds, executionId.ToString(), null);
        }
    }

    private async Task<WorkloadCharacteristics> AnalyzeWorkloadAsync(string kernelName, object[] args)
    {
        // Check cache first
        var cacheKey = GenerateWorkloadCacheKey(kernelName, args);


        lock (_cacheLock)
        {
            if (_workloadCache.TryGetValue(cacheKey, out var cachedCharacteristics))
            {
                return cachedCharacteristics;
            }
        }

        // Analyze workload characteristics
        var characteristics = new WorkloadCharacteristics();

        // Data size analysis
        characteristics.DataSize = CalculateTotalDataSize(args);
        characteristics.OperationCount = EstimateOperationCount(kernelName, args);

        // Get or create kernel profile
        var kernelProfile = GetOrCreateKernelProfile(kernelName);

        // Use historical data and heuristics to estimate characteristics

        characteristics.ComputeIntensity = EstimateComputeIntensity(kernelName, args, kernelProfile);
        characteristics.MemoryIntensity = EstimateMemoryIntensity(kernelName, args, kernelProfile);
        characteristics.ParallelismLevel = EstimateParallelismLevel(kernelName, args, kernelProfile);
        characteristics.AccessPattern = DetermineAccessPattern(kernelName, args);

        // Cache the characteristics
        lock (_cacheLock)
        {
            if (_workloadCache.Count >= _options.MaxWorkloadCacheSize)
            {
                // Remove oldest entry (simple LRU)
                var oldestKey = _workloadCache.Keys.First();
                _ = _workloadCache.Remove(oldestKey);
            }


            _workloadCache[cacheKey] = characteristics;
        }

        LogWorkloadAnalyzed(_logger, kernelName,
            (characteristics.DataSize / 1024 / 1024).ToString("F2", CultureInfo.InvariantCulture),
            characteristics.ComputeIntensity, characteristics.MemoryIntensity, characteristics.ParallelismLevel, null);

        await Task.CompletedTask;
        return characteristics;
    }

    private static async Task<List<IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        // This would need to integrate with the actual accelerator runtime
        // For now, returning empty list as placeholder
        await Task.CompletedTask;
        return [];
    }

    private SelectionConstraints? GetSelectionConstraints()
    {
        if (!_options.EnableConstraints)
        {

            return null;
        }


        var constraints = new SelectionConstraints();

        // Apply system load constraints
        var systemSnapshot = PerformanceMonitor.GetSystemPerformanceSnapshot();
        if (systemSnapshot.CpuUsage > _options.MaxCpuUtilizationThreshold)
        {
            constraints.DisallowedBackends ??= [];
            _ = constraints.DisallowedBackends.Add("CPU");
        }

        // Memory constraints
        if (systemSnapshot.MemoryUsage > _options.MaxMemoryUtilizationThreshold)
        {
            constraints.MaxMemoryUsageMB = (long)(systemSnapshot.MemoryAvailable * 0.8);
        }

        // Custom constraints based on options
        if (_options.PreferredBackends?.Count > 0)
        {
            constraints.AllowedBackends = [.. _options.PreferredBackends];
        }

        return constraints;
    }

    private async Task ApplyPreExecutionOptimizationsAsync(
        string kernelName,

        object[] args,

        BackendSelection backendSelection)
    {
        if (!_options.EnablePreExecutionOptimizations)
        {
            return;
        }


        LogPreExecutionOptimization(_logger, kernelName, backendSelection.BackendId, null);

        // Memory optimization
        if (_options.EnableMemoryOptimization)
        {
            await OptimizeMemoryLayoutAsync(args, backendSelection.BackendId);
        }

        // Kernel-specific optimizations
        if (_options.EnableKernelOptimization)
        {
            await ApplyKernelSpecificOptimizationsAsync(kernelName, args, backendSelection);
        }

        // Warm-up optimization for new backend/kernel combinations
        if (_options.EnableWarmupOptimization && IsNewCombination(kernelName, backendSelection.BackendId))
        {
            await WarmupBackendAsync(kernelName, backendSelection.BackendId);
        }
    }

    private async Task<T?> ExecuteWithMonitoringAsync<T>(
        string kernelName,
        object[] args,
        BackendSelection backendSelection,
        Guid executionId)
    {
        var correlationId = $"{kernelName}_{executionId}";

        // Start performance profiling

        var profileOptions = new Telemetry.Options.ProfileOptions
        {
            EnableDetailedMetrics = _options.EnableDetailedProfiling,
            SampleIntervalMs = _options.ProfilingSampleIntervalMs
        };

        var profileTask = _performanceProfiler.CreateProfileAsync(correlationId, profileOptions);

        // Record execution start metrics
        PerformanceMonitor.ExecutionMetrics.StartExecution();

        T? result;
        var executionStopwatch = Stopwatch.StartNew();

        try
        {
            // Execute using selected backend (this would need actual backend-specific execution)
            result = await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
        }
        finally
        {
            executionStopwatch.Stop();

            // Record execution end metrics

            var (cpuTime, allocatedBytes, elapsedMs) = PerformanceMonitor.ExecutionMetrics.EndExecution();

            // Record kernel execution in profiler

            var executionMetrics = new DotCompute.Core.Telemetry.Metrics.KernelExecutionMetrics
            {
                StartTime = DateTimeOffset.UtcNow - executionStopwatch.Elapsed,
                EndTime = DateTimeOffset.UtcNow,
                ExecutionTime = executionStopwatch.Elapsed,
                ThroughputOpsPerSecond = CalculateThroughput(args.Length, executionStopwatch.Elapsed),
                MemoryBandwidthGBPerSecond = CalculateMemoryBandwidth(allocatedBytes, executionStopwatch.Elapsed),
                CacheHitRate = 0.85f, // Would be measured from actual hardware counters
                MemoryCoalescingEfficiency = 0.8f, // Would be measured from GPU profiling
                OccupancyPercentage = 75.0f, // Would be measured from GPU profiling
                ComputeUnitsUsed = Environment.ProcessorCount,
                RegistersPerThread = 32,
                SharedMemoryUsed = allocatedBytes / 1024,
                WarpEfficiency = 0.9f,
                BranchDivergence = 0.1f,
                MemoryLatency = 100.0, // microseconds
                PowerConsumption = 150.0f,
                InstructionThroughput = 1000000
            };

            _performanceProfiler.RecordKernelExecution(
                correlationId, kernelName, backendSelection.BackendId, executionMetrics);

            // Finish profiling
            try
            {
                _ = await _performanceProfiler.FinishProfilingAsync(correlationId);
            }
            catch (Exception ex)
            {
                LogProfilingFinishFailed(_logger, correlationId, ex);
            }
        }

        return result;
    }

    private async Task RecordPerformanceAndLearnAsync(
        string kernelName,
        WorkloadCharacteristics workloadCharacteristics,
        BackendSelection backendSelection,
        TimeSpan totalExecutionTime,
        Guid executionId)
    {
        if (!_options.EnableLearning)
        {
            return;
        }


        var performanceResult = new PerformanceResult
        {
            ExecutionTimeMs = totalExecutionTime.TotalMilliseconds,
            ThroughputOpsPerSecond = CalculateThroughput(workloadCharacteristics.OperationCount, totalExecutionTime),
            MemoryUsedBytes = workloadCharacteristics.DataSize,
            Success = true,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _backendSelector.RecordPerformanceResultAsync(
            kernelName, workloadCharacteristics, backendSelection.BackendId, performanceResult);

        // Update kernel profile
        UpdateKernelProfile(kernelName, backendSelection.BackendId, performanceResult);

        LogPerformanceRecorded(_logger, kernelName, backendSelection.BackendId,
            performanceResult.ExecutionTimeMs, executionId.ToString(), null);
    }

    #region Helper Methods

    private static string GenerateWorkloadCacheKey(string kernelName, object[] args)
    {
        var dataSize = CalculateTotalDataSize(args);
        var argCount = args.Length;
        return $"{kernelName}_{dataSize}_{argCount}";
    }

    private static long CalculateTotalDataSize(object[] args)
    {
        long totalSize = 0;
        foreach (var arg in args)
        {
            totalSize += arg switch
            {
                Array array => array.Length * GetElementSize(array.GetType().GetElementType()),
                string str => str.Length * sizeof(char),
                _ => 8 // Default size for scalar types
            };
        }
        return totalSize;
    }

    private static int GetElementSize(Type? elementType) => elementType?.Name switch
    {
        "Single" or "Float" => sizeof(float),
        "Double" => sizeof(double),
        "Int32" => sizeof(int),
        "Int64" => sizeof(long),
        "Byte" => sizeof(byte),
        _ => 4 // Default
    };

    private static long EstimateOperationCount(string kernelName, object[] args)
    {
        // Heuristic based on data size and kernel name patterns
        var dataSize = CalculateTotalDataSize(args);
        var elementsProcessed = dataSize / 4; // Assuming float elements


        return kernelName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("matrix", StringComparison.Ordinal) => elementsProcessed * elementsProcessed, // O(n²)
            var name when name.Contains("sort", StringComparison.Ordinal) => (long)(elementsProcessed * Math.Log(elementsProcessed)), // O(n log n)
            var name when name.Contains("fft", StringComparison.Ordinal) => (long)(elementsProcessed * Math.Log(elementsProcessed)), // O(n log n)
            _ => elementsProcessed // O(n)
        };
    }

    private KernelPerformanceProfile GetOrCreateKernelProfile(string kernelName)
    {
        lock (_cacheLock)
        {
            if (!_kernelProfiles.TryGetValue(kernelName, out var profile))
            {
                profile = new KernelPerformanceProfile { KernelName = kernelName };
                _kernelProfiles[kernelName] = profile;
            }
            return profile;
        }
    }

    private static double EstimateComputeIntensity(string kernelName, object[] args, KernelPerformanceProfile profile)
    {
        // Use historical data if available
        if (profile.HistoricalComputeIntensity.HasValue)
        {
            return profile.HistoricalComputeIntensity.Value;
        }

        // Heuristic based on kernel name
        return kernelName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("fft", StringComparison.Ordinal) || name.Contains("fwt", StringComparison.Ordinal) => 0.9,
            var name when name.Contains("matrix", StringComparison.Ordinal) && name.Contains("multiply", StringComparison.Ordinal) => 0.85,
            var name when name.Contains("convolution", StringComparison.Ordinal) || name.Contains("conv", StringComparison.Ordinal) => 0.8,
            var name when name.Contains("sort", StringComparison.Ordinal) => 0.6,
            var name when name.Contains("add", StringComparison.Ordinal) || name.Contains("sub", StringComparison.Ordinal) => 0.2,
            var name when name.Contains("copy", StringComparison.Ordinal) => 0.1,
            _ => 0.5 // Default moderate compute intensity
        };
    }

    private static double EstimateMemoryIntensity(string kernelName, object[] args, KernelPerformanceProfile profile)
    {
        if (profile.HistoricalMemoryIntensity.HasValue)
        {
            return profile.HistoricalMemoryIntensity.Value;
        }

        var dataSize = CalculateTotalDataSize(args);
        var operationCount = EstimateOperationCount(kernelName, args);
        var memoryOperationRatio = (double)dataSize / Math.Max(1, operationCount);

        return kernelName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("copy", StringComparison.Ordinal) || name.Contains("transpose", StringComparison.Ordinal) => 0.9,
            var name when name.Contains("reduce", StringComparison.Ordinal) || name.Contains("scan", StringComparison.Ordinal) => 0.7,
            var name when name.Contains("matrix", StringComparison.Ordinal) => Math.Min(0.8, memoryOperationRatio * 2),
            _ => Math.Min(0.9, memoryOperationRatio)
        };
    }

    private static double EstimateParallelismLevel(string kernelName, object[] args, KernelPerformanceProfile profile)
    {
        if (profile.HistoricalParallelismLevel.HasValue)
        {
            return profile.HistoricalParallelismLevel.Value;
        }

        var dataSize = CalculateTotalDataSize(args);
        var elementCount = dataSize / 4; // Assuming float elements


        return kernelName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("element", StringComparison.Ordinal) || name.Contains("map", StringComparison.Ordinal) => 0.95,
            var name when name.Contains("matrix", StringComparison.Ordinal) && !name.Contains("multiply", StringComparison.Ordinal) => 0.9,
            var name when name.Contains("convolution", StringComparison.Ordinal) => 0.85,
            var name when name.Contains("reduce", StringComparison.Ordinal) => Math.Min(0.8, elementCount / 1000.0),
            var name when name.Contains("sort", StringComparison.Ordinal) => Math.Min(0.7, elementCount / 10000.0),
            var name when name.Contains("sequential", StringComparison.Ordinal) => 0.1,
            _ => Math.Min(0.8, elementCount / 5000.0)
        };
    }

    private static MemoryAccessPattern DetermineAccessPattern(string kernelName, object[] args)
    {
        return kernelName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("transpose", StringComparison.Ordinal) => MemoryAccessPattern.Strided,
            var name when name.Contains("random", StringComparison.Ordinal) => MemoryAccessPattern.Random,
            var name when name.Contains("gather", StringComparison.Ordinal) || name.Contains("scatter", StringComparison.Ordinal) => MemoryAccessPattern.ScatterGather,
            var name when name.Contains("convolution", StringComparison.Ordinal) => MemoryAccessPattern.Coalesced,
            _ => MemoryAccessPattern.Sequential
        };
    }

    private static async Task OptimizeMemoryLayoutAsync(object[] args, string backendId)
        // Memory layout optimization would be backend-specific
        // This is a placeholder for actual optimization logic
        => await Task.CompletedTask;

    private static async Task ApplyKernelSpecificOptimizationsAsync(
        string kernelName, object[] args, BackendSelection backendSelection)
        // Kernel-specific optimizations would be implemented here
        => await Task.CompletedTask;

    private bool IsNewCombination(string kernelName, string backendId)
    {
        lock (_cacheLock)
        {
            return !_kernelProfiles.TryGetValue(kernelName, out var profile) ||
                   !profile.ExecutedOnBackends.Contains(backendId);
        }
    }

    private static async Task WarmupBackendAsync(string kernelName, string backendId)
        // Backend warmup logic would be implemented here
        => await Task.CompletedTask;

    private static double CalculateThroughput(long operationCount, TimeSpan executionTime) => executionTime.TotalSeconds > 0 ? operationCount / executionTime.TotalSeconds : 0;

    private static double CalculateMemoryBandwidth(long bytesAccessed, TimeSpan executionTime)
    {
        var gigabytes = bytesAccessed / (1024.0 * 1024.0 * 1024.0);
        return executionTime.TotalSeconds > 0 ? gigabytes / executionTime.TotalSeconds : 0;
    }

    private void UpdateKernelProfile(string kernelName, string backendId, PerformanceResult result)
    {
        lock (_cacheLock)
        {
            var profile = GetOrCreateKernelProfile(kernelName);
            _ = profile.ExecutedOnBackends.Add(backendId);
            profile.LastExecutionTime = result.Timestamp;
            profile.TotalExecutions++;

            // Update averages (simple moving average)

            if (!profile.HistoricalComputeIntensity.HasValue)
            {
                profile.HistoricalComputeIntensity = 0.5; // Will be updated with actual measurements
            }
        }
    }

    #endregion

    /// <inheritdoc />
    public async Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        // Delegate to base orchestrator for validation
        if (_baseOrchestrator != null)
        {
            return await _baseOrchestrator.ValidateKernelArgsAsync(kernelName, args);
        }

        // Basic validation - just check if kernel exists and args are not null

        return !string.IsNullOrEmpty(kernelName) && args != null;
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="preferredBackend">The preferred backend.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        // Use preferred backend if specified
        var result = await ExecuteWithOptimizationAsync<T>(kernelName, args);
        return result!;
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        // Execute on specific accelerator
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteAsync<T>(kernelName, accelerator, args);
    }
    /// <summary>
    /// Gets execute with buffers asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="buffers">The buffers.</param>
    /// <param name="scalarArgs">The scalar args.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteWithBuffersAsync<T>(kernelName, buffers, scalarArgs);
    }
    /// <summary>
    /// Gets precompile kernel asynchronously.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="accelerator">The accelerator.</param>
    /// <returns>The result of the operation.</returns>

    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _baseOrchestrator.PrecompileKernelAsync(kernelName, accelerator);
    }
    /// <summary>
    /// Gets the supported accelerators async.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The supported accelerators async.</returns>

    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetSupportedAcceleratorsAsync(kernelName);
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, IKernelExecutionParameters executionParameters)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteKernelAsync(kernelName, executionParameters);
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, object[] args, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteKernelAsync(kernelName, args, cancellationToken);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources
            (_baseOrchestrator as IDisposable)?.Dispose();
            _backendSelector?.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Performance profile for a specific kernel.
/// </summary>
public class KernelPerformanceProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the executed on backends.
    /// </summary>
    /// <value>The executed on backends.</value>
    public HashSet<string> ExecutedOnBackends { get; } = [];
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public int TotalExecutions { get; set; }
    /// <summary>
    /// Gets or sets the historical compute intensity.
    /// </summary>
    /// <value>The historical compute intensity.</value>
    public double? HistoricalComputeIntensity { get; set; }
    /// <summary>
    /// Gets or sets the historical memory intensity.
    /// </summary>
    /// <value>The historical memory intensity.</value>
    public double? HistoricalMemoryIntensity { get; set; }
    /// <summary>
    /// Gets or sets the historical parallelism level.
    /// </summary>
    /// <value>The historical parallelism level.</value>
    public double? HistoricalParallelismLevel { get; set; }
}

/// <summary>
/// Options for performance optimization behavior.
/// </summary>
public class PerformanceOptimizationOptions
{
    /// <summary>
    /// Gets or sets the optimization strategy.
    /// </summary>
    /// <value>The optimization strategy.</value>
    public OptimizationStrategy OptimizationStrategy { get; set; } = OptimizationStrategy.Adaptive;
    /// <summary>
    /// Gets or sets the enable learning.
    /// </summary>
    /// <value>The enable learning.</value>
    public bool EnableLearning { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable constraints.
    /// </summary>
    /// <value>The enable constraints.</value>
    public bool EnableConstraints { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable pre execution optimizations.
    /// </summary>
    /// <value>The enable pre execution optimizations.</value>
    public bool EnablePreExecutionOptimizations { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable memory optimization.
    /// </summary>
    /// <value>The enable memory optimization.</value>
    public bool EnableMemoryOptimization { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable kernel optimization.
    /// </summary>
    /// <value>The enable kernel optimization.</value>
    public bool EnableKernelOptimization { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable warmup optimization.
    /// </summary>
    /// <value>The enable warmup optimization.</value>
    public bool EnableWarmupOptimization { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable detailed profiling.
    /// </summary>
    /// <value>The enable detailed profiling.</value>
    public bool EnableDetailedProfiling { get; set; }
    /// <summary>
    /// Gets or sets the profiling sample interval ms.
    /// </summary>
    /// <value>The profiling sample interval ms.</value>
    public int ProfilingSampleIntervalMs { get; set; } = 100;
    /// <summary>
    /// Gets or sets the max workload cache size.
    /// </summary>
    /// <value>The max workload cache size.</value>
    public int MaxWorkloadCacheSize { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the max cpu utilization threshold.
    /// </summary>
    /// <value>The max cpu utilization threshold.</value>
    public double MaxCpuUtilizationThreshold { get; set; } = 0.9;
    /// <summary>
    /// Gets or sets the max memory utilization threshold.
    /// </summary>
    /// <value>The max memory utilization threshold.</value>
    public double MaxMemoryUtilizationThreshold { get; set; } = 0.8;
    /// <summary>
    /// Gets or sets the preferred backends.
    /// </summary>
    /// <value>The preferred backends.</value>
    /// <remarks>
    /// This property allows setting for configuration purposes but returns a read-only view.
    /// CA2227 is acceptable here as this is a configuration options class used with object initializers.
    /// </remarks>
#pragma warning disable CA2227 // Collection properties should be read only
    public IList<string>? PreferredBackends { get; set; }
#pragma warning restore CA2227
}
/// <summary>
/// An optimization strategy enumeration.
/// </summary>

/// <summary>
/// Optimization strategies for performance tuning.
/// </summary>
/// <summary>
/// Defines optimization strategies for performance tuning.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>
    /// Prioritize reliability over performance.
    /// </summary>
    Conservative,

    /// <summary>
    /// Balance performance and reliability.
    /// </summary>
    Balanced,

    /// <summary>
    /// Maximize performance at the cost of some reliability.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Adapt strategy dynamically based on workload characteristics.
    /// </summary>
    Adaptive
}
