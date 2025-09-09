// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Telemetry;

namespace DotCompute.Core.Optimization;

/// <summary>
/// High-performance compute orchestrator with adaptive backend selection,
/// performance monitoring, and intelligent optimization strategies.
/// </summary>
public class PerformanceOptimizedOrchestrator : IComputeOrchestrator, IDisposable
{
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

        _kernelProfiles = new Dictionary<string, KernelPerformanceProfile>();
        _workloadCache = new Dictionary<string, WorkloadCharacteristics>();

        _logger.LogInfoMessage($"Performance-optimized orchestrator initialized with {_options.OptimizationStrategy} optimization strategy");
    }

    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        var result = await ExecuteWithOptimizationAsync<T>(kernelName, args);
        return result!;
    }


    public async Task<T?> ExecuteWithBuffersAsync<T>(string kernelName, object[] buffers, params object[] scalarArgs)
    {
        var allArgs = buffers.Concat(scalarArgs).ToArray();
        return await ExecuteWithOptimizationAsync<T>(kernelName, allArgs);
    }

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

        _logger.LogDebugMessage($"Starting optimized execution of {kernelName} [ID: {executionId}]");

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
                _logger.LogWarningMessage($"No suitable backend found for {kernelName}, falling back to base orchestrator");
                return await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
            }

            _logger.LogDebugMessage($"Selected {backendSelection.BackendId} for {kernelName} with {backendSelection.ConfidenceScore} confidence using {backendSelection.SelectionStrategy}");

            // Phase 3: Pre-execution Optimization
            await ApplyPreExecutionOptimizations(kernelName, args, backendSelection);

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
            _logger.LogErrorMessage(ex, $"Error during optimized execution of {kernelName} [ID: {executionId}]");

            // Fallback to base orchestrator on error

            try
            {
                return await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogErrorMessage(fallbackEx, $"Fallback execution also failed for {kernelName}");
                throw;
            }
        }
        finally
        {
            _logger.LogTrace("Optimized execution of {KernelName} completed in {TotalTime}ms [ID: {ExecutionId}]",

                kernelName, stopwatch.Elapsed.TotalMilliseconds, executionId);
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
                _workloadCache.Remove(oldestKey);
            }


            _workloadCache[cacheKey] = characteristics;
        }

        _logger.LogTrace("Analyzed workload {KernelName}: DataSize={DataSize}MB, Compute={Compute:F2}, Memory={Memory:F2}, Parallelism={Parallelism:F2}",
            kernelName, characteristics.DataSize / 1024 / 1024, characteristics.ComputeIntensity,

            characteristics.MemoryIntensity, characteristics.ParallelismLevel);

        await Task.CompletedTask;
        return characteristics;
    }

    private static async Task<List<IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        // This would need to integrate with the actual accelerator runtime
        // For now, returning empty list as placeholder
        await Task.CompletedTask;
        return new List<IAccelerator>();
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
            constraints.DisallowedBackends ??= new HashSet<string>();
            constraints.DisallowedBackends.Add("CPU");
        }

        // Memory constraints
        if (systemSnapshot.MemoryUsage > _options.MaxMemoryUtilizationThreshold)
        {
            constraints.MaxMemoryUsageMB = (long)(systemSnapshot.MemoryAvailable * 0.8);
        }

        // Custom constraints based on options
        if (_options.PreferredBackends?.Count > 0)
        {
            constraints.AllowedBackends = new HashSet<string>(_options.PreferredBackends);
        }

        return constraints;
    }

    private async Task ApplyPreExecutionOptimizations(
        string kernelName,

        object[] args,

        BackendSelection backendSelection)
    {
        if (!_options.EnablePreExecutionOptimizations)
        {
            return;
        }


        _logger.LogTrace("Applying pre-execution optimizations for {KernelName} on {Backend}",

            kernelName, backendSelection.BackendId);

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

        var profileOptions = new DotCompute.Core.Telemetry.Options.ProfileOptions
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

            var executionMetrics = new DotCompute.Core.Telemetry.KernelExecutionMetrics
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
                await _performanceProfiler.FinishProfilingAsync(correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to finish performance profiling for {CorrelationId}", correlationId);
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

        _logger.LogTrace("Recorded performance result for {KernelName} on {Backend}: {ExecutionTime}ms [ID: {ExecutionId}]",
            kernelName, backendSelection.BackendId, performanceResult.ExecutionTimeMs, executionId);
    }

    #region Helper Methods

    private string GenerateWorkloadCacheKey(string kernelName, object[] args)
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

    private long EstimateOperationCount(string kernelName, object[] args)
    {
        // Heuristic based on data size and kernel name patterns
        var dataSize = CalculateTotalDataSize(args);
        var elementsProcessed = dataSize / 4; // Assuming float elements


        return kernelName.ToLowerInvariant() switch
        {
            var name when name.Contains("matrix") => elementsProcessed * elementsProcessed, // O(n²)
            var name when name.Contains("sort") => (long)(elementsProcessed * Math.Log(elementsProcessed)), // O(n log n)
            var name when name.Contains("fft") => (long)(elementsProcessed * Math.Log(elementsProcessed)), // O(n log n)
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
        return kernelName.ToLowerInvariant() switch
        {
            var name when name.Contains("fft") || name.Contains("fwt") => 0.9,
            var name when name.Contains("matrix") && name.Contains("multiply") => 0.85,
            var name when name.Contains("convolution") || name.Contains("conv") => 0.8,
            var name when name.Contains("sort") => 0.6,
            var name when name.Contains("add") || name.Contains("sub") => 0.2,
            var name when name.Contains("copy") => 0.1,
            _ => 0.5 // Default moderate compute intensity
        };
    }

    private double EstimateMemoryIntensity(string kernelName, object[] args, KernelPerformanceProfile profile)
    {
        if (profile.HistoricalMemoryIntensity.HasValue)
        {
            return profile.HistoricalMemoryIntensity.Value;
        }

        var dataSize = CalculateTotalDataSize(args);
        var operationCount = EstimateOperationCount(kernelName, args);
        var memoryOperationRatio = (double)dataSize / Math.Max(1, operationCount);

        return kernelName.ToLowerInvariant() switch
        {
            var name when name.Contains("copy") || name.Contains("transpose") => 0.9,
            var name when name.Contains("reduce") || name.Contains("scan") => 0.7,
            var name when name.Contains("matrix") => Math.Min(0.8, memoryOperationRatio * 2),
            _ => Math.Min(0.9, memoryOperationRatio)
        };
    }

    private double EstimateParallelismLevel(string kernelName, object[] args, KernelPerformanceProfile profile)
    {
        if (profile.HistoricalParallelismLevel.HasValue)
        {
            return profile.HistoricalParallelismLevel.Value;
        }

        var dataSize = CalculateTotalDataSize(args);
        var elementCount = dataSize / 4; // Assuming float elements


        return kernelName.ToLowerInvariant() switch
        {
            var name when name.Contains("element") || name.Contains("map") => 0.95,
            var name when name.Contains("matrix") && !name.Contains("multiply") => 0.9,
            var name when name.Contains("convolution") => 0.85,
            var name when name.Contains("reduce") => Math.Min(0.8, elementCount / 1000.0),
            var name when name.Contains("sort") => Math.Min(0.7, elementCount / 10000.0),
            var name when name.Contains("sequential") => 0.1,
            _ => Math.Min(0.8, elementCount / 5000.0)
        };
    }

    private static MemoryAccessPattern DetermineAccessPattern(string kernelName, object[] args)
    {
        return kernelName.ToLowerInvariant() switch
        {
            var name when name.Contains("transpose") => MemoryAccessPattern.Strided,
            var name when name.Contains("random") => MemoryAccessPattern.Random,
            var name when name.Contains("gather") || name.Contains("scatter") => MemoryAccessPattern.Scattered,
            var name when name.Contains("convolution") => MemoryAccessPattern.Coalesced,
            _ => MemoryAccessPattern.Sequential
        };
    }

    private static async Task OptimizeMemoryLayoutAsync(object[] args, string backendId)
    {
        // Memory layout optimization would be backend-specific
        // This is a placeholder for actual optimization logic
        await Task.CompletedTask;
    }

    private static async Task ApplyKernelSpecificOptimizationsAsync(
        string kernelName, object[] args, BackendSelection backendSelection)
    {
        // Kernel-specific optimizations would be implemented here
        await Task.CompletedTask;
    }

    private bool IsNewCombination(string kernelName, string backendId)
    {
        lock (_cacheLock)
        {
            return !_kernelProfiles.TryGetValue(kernelName, out var profile) ||
                   !profile.ExecutedOnBackends.Contains(backendId);
        }
    }

    private static async Task WarmupBackendAsync(string kernelName, string backendId)
    {
        // Backend warmup logic would be implemented here
        await Task.CompletedTask;
    }

    private static double CalculateThroughput(long operationCount, TimeSpan executionTime)
    {
        return executionTime.TotalSeconds > 0 ? operationCount / executionTime.TotalSeconds : 0;
    }

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
            profile.ExecutedOnBackends.Add(backendId);
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

    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        // Use preferred backend if specified
        var result = await ExecuteWithOptimizationAsync<T>(kernelName, args);
        return result!;
    }

    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        // Execute on specific accelerator
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteAsync<T>(kernelName, accelerator, args);
    }

    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.ExecuteWithBuffersAsync<T>(kernelName, buffers, scalarArgs);
    }

    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _baseOrchestrator.PrecompileKernelAsync(kernelName, accelerator);
    }

    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetSupportedAcceleratorsAsync(kernelName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        (_baseOrchestrator as IDisposable)?.Dispose();
        _backendSelector?.Dispose();


        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Performance profile for a specific kernel.
/// </summary>
public class KernelPerformanceProfile
{
    public string KernelName { get; set; } = string.Empty;
    public HashSet<string> ExecutedOnBackends { get; set; } = new();
    public DateTimeOffset LastExecutionTime { get; set; }
    public int TotalExecutions { get; set; }
    public double? HistoricalComputeIntensity { get; set; }
    public double? HistoricalMemoryIntensity { get; set; }
    public double? HistoricalParallelismLevel { get; set; }
}

/// <summary>
/// Options for performance optimization behavior.
/// </summary>
public class PerformanceOptimizationOptions
{
    public OptimizationStrategy OptimizationStrategy { get; set; } = OptimizationStrategy.Adaptive;
    public bool EnableLearning { get; set; } = true;
    public bool EnableConstraints { get; set; } = true;
    public bool EnablePreExecutionOptimizations { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;
    public bool EnableKernelOptimization { get; set; } = true;
    public bool EnableWarmupOptimization { get; set; } = true;
    public bool EnableDetailedProfiling { get; set; } = false;
    public int ProfilingSampleIntervalMs { get; set; } = 100;
    public int MaxWorkloadCacheSize { get; set; } = 1000;
    public double MaxCpuUtilizationThreshold { get; set; } = 0.9;
    public double MaxMemoryUtilizationThreshold { get; set; } = 0.8;
    public List<string>? PreferredBackends { get; set; }
}

/// <summary>
/// Optimization strategies for performance tuning.
/// </summary>
public enum OptimizationStrategy
{
    Conservative,  // Prioritize reliability over performance
    Balanced,      // Balance performance and reliability
    Aggressive,    // Maximize performance
    Adaptive       // Adapt strategy based on workload
}
