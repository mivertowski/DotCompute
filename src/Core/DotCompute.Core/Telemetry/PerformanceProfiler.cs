using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Core.Logging;
using DotCompute.Core.Telemetry.Analysis;
using DotCompute.Core.Telemetry.Enums;
using DotCompute.Core.Telemetry.Metrics;
using DotCompute.Core.Telemetry.Options;
using DotCompute.Core.Telemetry.Profiles;
using DotCompute.Core.Telemetry.Samples;
using DotCompute.Core.Telemetry.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProfileOptions = DotCompute.Core.Telemetry.Options.ProfileOptions;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Advanced performance profiler for detailed kernel analysis, bottleneck identification, and optimization recommendations.
/// Provides deep insights into kernel execution patterns, memory access efficiency, and device utilization.
/// </summary>
public sealed class PerformanceProfiler : IPerformanceProfiler
{
    private readonly ILogger<PerformanceProfiler> _logger;

    // Event IDs: 9000-9099 for PerformanceProfiler
    private static readonly Action<ILogger, string, Exception?> _logProfileStart =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(9000, nameof(_logProfileStart)),
            "Started performance profiling for correlation ID {CorrelationId}");

    private static readonly Action<ILogger, string, Exception?> _logOrphanedRecord =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(9001, nameof(_logOrphanedRecord)),
            "Recording kernel execution for unknown profile {CorrelationId}");

    private static readonly Action<ILogger, string, string, double, double, double, Exception?> _logKernelExecution =
        LoggerMessage.Define<string, string, double, double, double>(LogLevel.Trace, new EventId(9002, nameof(_logKernelExecution)),
            "Recorded kernel execution profile for {KernelName} on {DeviceId}: {ExecutionTime}ms, {Throughput} ops/sec, {Occupancy}% occupancy");

    private static readonly Action<ILogger, string, double, Exception?> _logProfileFinish =
        LoggerMessage.Define<string, double>(LogLevel.Information, new EventId(9003, nameof(_logProfileFinish)),
            "Finishing performance profile for {CorrelationId} after {Duration}ms");

    private static readonly Action<ILogger, string, Exception> _logHwCounterReadFailed =
        LoggerMessage.Define<string>(LogLevel.Trace, new EventId(9004, nameof(_logHwCounterReadFailed)),
            "Failed to read hardware counter {CounterName}");

    private static readonly Action<ILogger, Exception?> _logHwCountersNotAvailable =
        LoggerMessage.Define(LogLevel.Trace, new EventId(9005, nameof(_logHwCountersNotAvailable)),
            "Hardware performance counters not available on this platform");

    private static readonly Action<ILogger, Exception> _logProcessorUsageFailed =
        LoggerMessage.Define(LogLevel.Trace, new EventId(9006, nameof(_logProcessorUsageFailed)),
            "Failed to get processor usage from hardware counter");

    private static readonly Action<ILogger, Exception> _logHwCounterInitFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(9007, nameof(_logHwCounterInitFailed)),
            "Failed to initialize hardware performance counters");

    private static readonly Action<ILogger, Exception> _logWindowsCounterInitFailed =
        LoggerMessage.Define(LogLevel.Trace, new EventId(9008, nameof(_logWindowsCounterInitFailed)),
            "Could not initialize some Windows performance counters");

    private readonly PerformanceProfilerOptions _options;
    private readonly ConcurrentDictionary<string, ActiveProfile> _activeProfiles;
    private readonly ConcurrentQueue<ProfileSample> _profileSamples;
    private readonly Timer _samplingTimer = null!;
    private readonly SemaphoreSlim _profilingSemaphore;
    private volatile bool _disposed;

    // Hardware performance counters (platform-specific)

#if WINDOWS
    private readonly Dictionary<string, PerformanceCounter> _hwCounters;
#else
    private readonly Dictionary<string, object> _hwCounters;
    /// <summary>
    /// Initializes a new instance of the PerformanceProfiler class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
#endif



    public PerformanceProfiler(ILogger<PerformanceProfiler> logger, IOptions<PerformanceProfilerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options?.Value ?? new PerformanceProfilerOptions();


        _activeProfiles = new ConcurrentDictionary<string, ActiveProfile>();
        _profileSamples = new ConcurrentQueue<ProfileSample>();
        _profilingSemaphore = new SemaphoreSlim(_options.MaxConcurrentProfiles, _options.MaxConcurrentProfiles);
#if WINDOWS
        _hwCounters = [];
#else
        _hwCounters = [];
#endif

        // Initialize hardware performance counters if available

        InitializeHardwareCounters();

        // Start sampling timer for continuous profiling

        if (_options.EnableContinuousProfiling)
        {
            _samplingTimer = new Timer(CollectProfileSamples, null,
                TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.SamplingIntervalMs));
        }
    }

    /// <summary>
    /// Creates a comprehensive performance profile for a specific operation or kernel.
    /// </summary>
    /// <param name="correlationId">Correlation ID for the operation being profiled</param>
    /// <param name="profileOptions">Profiling configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed performance profile</returns>
    public async Task<PerformanceProfile> CreateProfileAsync(string correlationId,
        ProfileOptions? profileOptions = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await _profilingSemaphore.WaitAsync(cancellationToken);


        try
        {
            var options = profileOptions ?? new ProfileOptions();
            var startTime = DateTimeOffset.UtcNow;


            var activeProfile = new ActiveProfile
            {
                CorrelationId = correlationId,
                StartTime = startTime,
                Options = options
            };
            // Note: All collections are auto-initialized by their property default values


            _ = _activeProfiles.TryAdd(correlationId, activeProfile);


            _logProfileStart(_logger, correlationId, null);

            // Collect baseline metrics

            await CollectBaselineMetricsAsync(activeProfile, cancellationToken);

            // Wait for profiling duration or until manually stopped

            if (options.AutoStopAfter.HasValue)
            {
                await Task.Delay(options.AutoStopAfter.Value, cancellationToken);
                return await FinishProfilingAsync(correlationId, cancellationToken);
            }


            return new PerformanceProfile
            {
                CorrelationId = correlationId,
                StartTime = startTime,
                Status = ProfileStatus.Active,
                Message = "Profiling started successfully"
            };
        }
        finally
        {
            _ = _profilingSemaphore.Release();
        }
    }

    /// <summary>
    /// Records kernel execution details for profiling analysis.
    /// </summary>
    /// <param name="correlationId">Correlation ID of the active profile</param>
    /// <param name="kernelName">Name of the executed kernel</param>
    /// <param name="deviceId">ID of the device that executed the kernel</param>
    /// <param name="executionMetrics">Detailed execution metrics</param>
    public void RecordKernelExecution(string correlationId, string kernelName, string deviceId,
        KernelExecutionMetrics executionMetrics)
    {
        ThrowIfDisposed();


        if (!_activeProfiles.TryGetValue(correlationId, out var profile))
        {
            if (_options.AllowOrphanedRecords)
            {
                _logOrphanedRecord(_logger, correlationId, null);
            }
            else
            {
                return;
            }
        }


        var executionProfile = new KernelExecutionProfile
        {
            KernelName = kernelName,
            DeviceId = deviceId,
            StartTime = executionMetrics.StartTime,
            EndTime = executionMetrics.EndTime,
            ExecutionTime = executionMetrics.ExecutionTime,

            // Performance characteristics

            ThroughputOpsPerSecond = executionMetrics.ThroughputOpsPerSecond,
            OccupancyPercentage = executionMetrics.OccupancyPercentage,
            InstructionThroughput = executionMetrics.InstructionThroughput,

            // Memory metrics

            MemoryBandwidthGBPerSecond = executionMetrics.MemoryBandwidthGBPerSecond,
            CacheHitRate = executionMetrics.CacheHitRate,
            MemoryCoalescingEfficiency = executionMetrics.MemoryCoalescingEfficiency,

            // Resource utilization

            ComputeUnitsUsed = executionMetrics.ComputeUnitsUsed,
            RegistersPerThread = executionMetrics.RegistersPerThread,
            SharedMemoryUsed = executionMetrics.SharedMemoryUsed,

            // Advanced metrics

            WarpEfficiency = executionMetrics.WarpEfficiency,
            BranchDivergence = executionMetrics.BranchDivergence,
            MemoryLatency = executionMetrics.MemoryLatency,
            PowerConsumption = executionMetrics.PowerConsumption
        };


        profile?.KernelExecutions.Add(executionProfile);

        // Update device-specific metrics
        if (profile is not null)
        {
            _ = profile.DeviceMetrics.AddOrUpdate(
                deviceId,
                new DeviceProfileMetrics
                {
                    UtilizationPercentage = executionMetrics.OccupancyPercentage,
                    PowerConsumptionWatts = executionMetrics.PowerConsumption
                },
                (_, existing) =>
                {
                    // Update with weighted average
                    existing.UtilizationPercentage = (existing.UtilizationPercentage + executionMetrics.OccupancyPercentage) / 2;
                    existing.PowerConsumptionWatts = (existing.PowerConsumptionWatts + executionMetrics.PowerConsumption) / 2;
                    return existing;
                });
        }

        _logKernelExecution(_logger, kernelName, deviceId, executionMetrics.ExecutionTime.TotalMilliseconds,
            executionMetrics.ThroughputOpsPerSecond, executionMetrics.OccupancyPercentage, null);
    }

    /// <summary>
    /// Records memory operation details for access pattern analysis.
    /// </summary>
    /// <param name="correlationId">Correlation ID of the active profile</param>
    /// <param name="operationType">Type of memory operation</param>
    /// <param name="deviceId">Device performing the operation</param>
    /// <param name="memoryMetrics">Detailed memory operation metrics</param>
    public void RecordMemoryOperation(string correlationId, string operationType, string deviceId,
        MemoryOperationMetrics memoryMetrics)
    {
        ThrowIfDisposed();


        if (!_activeProfiles.TryGetValue(correlationId, out var profile))
        {
            return;
        }


        var operationProfile = new MemoryOperationProfile
        {
            OperationType = operationType,
            DeviceId = deviceId,
            StartTime = memoryMetrics.StartTime,
            Duration = memoryMetrics.Duration,
            BytesTransferred = memoryMetrics.BytesTransferred,
            BandwidthGBPerSecond = memoryMetrics.BandwidthGBPerSecond,
            AccessPattern = memoryMetrics.AccessPattern,
            CoalescingEfficiency = memoryMetrics.CoalescingEfficiency,
            CacheHitRate = memoryMetrics.CacheHitRate,
            MemorySegment = memoryMetrics.MemorySegment,
            TransferDirection = memoryMetrics.TransferDirection,
            QueueDepth = memoryMetrics.QueueDepth
        };


        profile.MemoryOperations.Add(operationProfile);
    }

    /// <summary>
    /// Finishes profiling for a correlation ID and generates comprehensive analysis.
    /// </summary>
    /// <param name="correlationId">Correlation ID of the profile to finish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete performance profile with analysis</returns>
    public async Task<PerformanceProfile> FinishProfilingAsync(string correlationId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        if (!_activeProfiles.TryRemove(correlationId, out var activeProfile))
        {
            return new PerformanceProfile
            {
                CorrelationId = correlationId,
                Status = ProfileStatus.NotFound,
                Message = $"No active profile found for correlation ID {correlationId}"
            };
        }


        var endTime = DateTimeOffset.UtcNow;
        var totalDuration = endTime - activeProfile.StartTime;


        _logProfileFinish(_logger, correlationId, totalDuration.TotalMilliseconds, null);

        // Perform comprehensive analysis

        var analysis = await AnalyzeProfileAsync(activeProfile, cancellationToken);


        var profile = new PerformanceProfile
        {
            CorrelationId = correlationId,
            StartTime = activeProfile.StartTime,
            EndTime = endTime,
            TotalDuration = totalDuration,
            Status = ProfileStatus.Completed,

            // Summary metrics

            TotalKernelExecutions = activeProfile.KernelExecutions.Count,
            TotalMemoryOperations = activeProfile.MemoryOperations.Count,
            DevicesInvolved = activeProfile.DeviceMetrics.Count,

            // Performance analysis

            Analysis = analysis,

            // Detailed data

            KernelExecutions = [.. activeProfile.KernelExecutions],
            MemoryOperations = [.. activeProfile.MemoryOperations],
            DeviceMetrics = activeProfile.DeviceMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };


        return profile;
    }

    /// <summary>
    /// Analyzes kernel performance characteristics and identifies optimization opportunities.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze</param>
    /// <param name="timeWindow">Time window for analysis</param>
    /// <returns>Detailed kernel analysis results</returns>
    public KernelAnalysisResult AnalyzeKernelPerformance(string kernelName, TimeSpan? timeWindow = null)
    {
        ThrowIfDisposed();


        var window = timeWindow ?? TimeSpan.FromMinutes(10);
        var cutoff = DateTimeOffset.UtcNow - window;


        var kernelExecutions = _activeProfiles.Values
            .SelectMany(p => p.KernelExecutions)
            .Where(k => k.KernelName == kernelName && k.StartTime > cutoff)
            .ToList();


        if (kernelExecutions.Count == 0)
        {
            return new KernelAnalysisResult
            {
                KernelName = kernelName,
                Status = AnalysisStatus.NoData,
                Message = $"No execution data found for kernel {kernelName} in the last {window}"
            };
        }


        var analysis = new KernelAnalysisResult
        {
            KernelName = kernelName,
            Status = AnalysisStatus.Success,
            TimeWindow = window,
            ExecutionCount = kernelExecutions.Count,

            // Timing analysis

            AverageExecutionTime = kernelExecutions.Average(k => k.ExecutionTime.TotalMilliseconds),
            MinExecutionTime = kernelExecutions.Min(k => k.ExecutionTime.TotalMilliseconds),
            MaxExecutionTime = kernelExecutions.Max(k => k.ExecutionTime.TotalMilliseconds),
            ExecutionTimeStdDev = CalculateStandardDeviation(kernelExecutions.Select(k => k.ExecutionTime.TotalMilliseconds)),

            // Performance metrics

            AverageThroughput = kernelExecutions.Average(k => k.ThroughputOpsPerSecond),
            AverageOccupancy = kernelExecutions.Average(k => k.OccupancyPercentage),
            AverageCacheHitRate = kernelExecutions.Average(k => k.CacheHitRate),
            AverageMemoryBandwidth = kernelExecutions.Average(k => k.MemoryBandwidthGBPerSecond),

            // Resource utilization

            AverageWarpEfficiency = kernelExecutions.Average(k => k.WarpEfficiency),
            AverageBranchDivergence = kernelExecutions.Average(k => k.BranchDivergence),
            AverageMemoryCoalescing = kernelExecutions.Average(k => k.MemoryCoalescingEfficiency),

            // Device distribution

            DeviceDistribution = kernelExecutions
                .GroupBy(k => k.DeviceId)
                .ToDictionary(g => g.Key, g => g.Count()),

            // Performance trends

            PerformanceTrend = AnalyzePerformanceTrend(kernelExecutions)
        };

        // Generate optimization recommendations
        var recommendations = GenerateKernelOptimizationRecommendations(analysis);
        analysis.OptimizationRecommendations.Clear();
        foreach (var recommendation in recommendations)
        {
            analysis.OptimizationRecommendations.Add(recommendation);
        }

        return analysis;
    }

    /// <summary>
    /// Analyzes memory access patterns across all profiled operations.
    /// </summary>
    /// <param name="timeWindow">Time window for analysis</param>
    /// <returns>Memory access pattern analysis results</returns>
    public MemoryAccessAnalysisResult AnalyzeMemoryAccessPatterns(TimeSpan? timeWindow = null)
    {
        ThrowIfDisposed();


        var window = timeWindow ?? TimeSpan.FromMinutes(10);
        var cutoff = DateTimeOffset.UtcNow - window;


        var memoryOperations = _activeProfiles.Values
            .SelectMany(p => p.MemoryOperations)
            .Where(m => m.StartTime > cutoff)
            .ToList();


        if (memoryOperations.Count == 0)
        {
            return new MemoryAccessAnalysisResult
            {
                Status = AnalysisStatus.NoData,
                Message = "No memory operations found in the specified time window"
            };
        }


        var analysis = new MemoryAccessAnalysisResult
        {
            Status = AnalysisStatus.Success,
            TimeWindow = window,
            TotalOperations = memoryOperations.Count,

            // Bandwidth analysis

            AverageBandwidth = memoryOperations.Average(m => m.BandwidthGBPerSecond),
            PeakBandwidth = memoryOperations.Max(m => m.BandwidthGBPerSecond),
            TotalBytesTransferred = memoryOperations.Sum(m => m.BytesTransferred),

            // Access pattern analysis

            AccessPatternDistribution = memoryOperations
                .GroupBy(m => m.AccessPattern)
                .ToDictionary(g => g.Key, g => g.Count()),

            // Efficiency metrics

            AverageCoalescingEfficiency = memoryOperations.Average(m => m.CoalescingEfficiency),
            AverageCacheHitRate = memoryOperations.Average(m => m.CacheHitRate),

            // Transfer direction analysis

            TransferDirectionDistribution = memoryOperations
                .GroupBy(m => m.TransferDirection)
                .ToDictionary(g => g.Key, g => g.Count()),

            // Device utilization

            DeviceBandwidthUtilization = memoryOperations
                .GroupBy(m => m.DeviceId)
                .ToDictionary(g => g.Key, g => g.Sum(op => op.BandwidthGBPerSecond)),

            // Memory segment analysis

            MemorySegmentUsage = memoryOperations
                .GroupBy(m => m.MemorySegment)
                .ToDictionary(g => g.Key, g => new MemorySegmentStats
                {
                    OperationCount = g.Count(),
                    TotalBytes = g.Sum(op => op.BytesTransferred),
                    AverageBandwidth = g.Average(op => op.BandwidthGBPerSecond)
                })
        };

        // Generate memory optimization recommendations
        var memoryRecommendations = GenerateMemoryOptimizationRecommendations(analysis);
        analysis.OptimizationRecommendations.Clear();
        foreach (var recommendation in memoryRecommendations)
        {
            analysis.OptimizationRecommendations.Add(recommendation);
        }

        return analysis;
    }

    /// <summary>
    /// Gets real-time system performance snapshot for monitoring.
    /// </summary>
    /// <returns>Current system performance metrics</returns>
    public SystemPerformanceSnapshot GetSystemPerformanceSnapshot()
    {
        ThrowIfDisposed();


        var snapshot = new SystemPerformanceSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ActiveProfiles = _activeProfiles.Count,

            // CPU metrics

            ProcessorUsage = GetProcessorUsage(),
            MemoryUsage = GC.GetTotalMemory(false),

            // Threading metrics

            ThreadCount = Process.GetCurrentProcess().Threads.Count,
            ThreadPoolWorkItems = GetThreadPoolMetrics(),

            // GC metrics

            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };

        // Add hardware counter data if available
#if WINDOWS
        foreach (var counter in _hwCounters)
        {
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                snapshot.HardwareCounters[counter.Key] = counter.Value.NextValue();
#pragma warning restore CA1416 // Validate platform compatibility
            }
            catch (Exception ex)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                _logHwCounterReadFailed(_logger, counter.Key, ex);
#pragma warning restore CA1416 // Validate platform compatibility
            }
        }
#else
        // Performance counters not available on non-Windows platforms

        _logHwCountersNotAvailable(_logger, null);
#endif


        return snapshot;
    }

    private async Task CollectBaselineMetricsAsync(ActiveProfile profile, CancellationToken cancellationToken)
    {
        var baseline = new SystemSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            CpuUsage = GetProcessorUsage(),
            MemoryUsage = GC.GetTotalMemory(false),
            ThreadCount = Process.GetCurrentProcess().Threads.Count
        };


        profile.SystemSnapshots.Enqueue(baseline);
        await Task.Delay(1, cancellationToken); // Yield control
    }

    private static async Task<ProfileAnalysis> AnalyzeProfileAsync(ActiveProfile profile,
        CancellationToken cancellationToken)
    {
        await Task.Yield();


        var kernelExecutions = profile.KernelExecutions.ToList();
        var memoryOperations = profile.MemoryOperations.ToList();


        var analysis = new ProfileAnalysis
        {
            AnalysisTimestamp = DateTimeOffset.UtcNow,

            // Overall performance metrics

            TotalExecutionTime = kernelExecutions.Sum(k => k.ExecutionTime.TotalMilliseconds),
            AverageKernelExecutionTime = kernelExecutions.Count != 0 ?

                kernelExecutions.Average(k => k.ExecutionTime.TotalMilliseconds) : 0,

            // Throughput analysis

            OverallThroughput = kernelExecutions.Count != 0 ?
                kernelExecutions.Sum(k => k.ThroughputOpsPerSecond) : 0,

            // Memory analysis

            TotalMemoryTransferred = memoryOperations.Sum(m => m.BytesTransferred),
            AverageMemoryBandwidth = memoryOperations.Count != 0 ?
                memoryOperations.Average(m => m.BandwidthGBPerSecond) : 0,

            // Efficiency metrics

            AverageOccupancy = kernelExecutions.Count != 0 ?
                kernelExecutions.Average(k => k.OccupancyPercentage) : 0,
            AverageCacheHitRate = kernelExecutions.Count != 0 ?
                kernelExecutions.Average(k => k.CacheHitRate) : 0,

            // Resource utilization

            DeviceUtilizationEfficiency = CalculateDeviceUtilizationEfficiency(profile),
            ParallelismEfficiency = CalculateParallelismEfficiency(kernelExecutions),

            // Bottlenecks and recommendations

            IdentifiedBottlenecks = IdentifyProfileBottlenecks(profile)
        };

        // Generate profile optimization recommendations
        var profileRecommendations = GenerateProfileOptimizationRecommendations(profile);
        analysis.OptimizationRecommendations.Clear();
        foreach (var recommendation in profileRecommendations)
        {
            analysis.OptimizationRecommendations.Add(recommendation);
        }

        return analysis;
    }

    private void CollectProfileSamples(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var systemSnapshot = GetSystemPerformanceSnapshot();
            var sample = new ProfileSample
            {
                Timestamp = DateTimeOffset.UtcNow,
                ActiveProfileCount = _activeProfiles.Count,
                SystemSnapshot = new SystemSnapshot
                {
                    Timestamp = systemSnapshot.Timestamp,
                    CpuUsage = systemSnapshot.CpuUsage,
                    MemoryUsage = systemSnapshot.MemoryUsage,
                    ThreadCount = systemSnapshot.ThreadCount
                }
            };


            _profileSamples.Enqueue(sample);

            // Trim old samples (keep last hour)

            if (_profileSamples.Count > 3600)
            {
                _ = _profileSamples.TryDequeue(out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to collect profile samples");
        }
    }

    private void InitializeHardwareCounters()
    {
        try
        {
            // Initialize platform-specific performance counters
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindowsCounters();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                InitializeLinuxCounters();
            }
        }
        catch (Exception ex)
        {
            _logHwCounterInitFailed(_logger, ex);
        }
    }

    private void InitializeWindowsCounters()
    {
        // Windows Performance Counters
        try
        {
#if WINDOWS
#pragma warning disable CA1416 // Validate platform compatibility
            _hwCounters["processor_time"] = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _hwCounters["memory_available"] = new PerformanceCounter("Memory", "Available MBytes");
#pragma warning restore CA1416 // Validate platform compatibility
#endif
        }
        catch (Exception ex)
        {
            _logWindowsCounterInitFailed(_logger, ex);
        }
    }

    private static void InitializeLinuxCounters()
    {
        // Linux would use different mechanisms (perf_event_open, /proc/stat, etc.)
        // For now, we'll use managed alternatives
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count <= 1)
        {
            return 0;
        }


        var average = valuesList.Average();
        var sumOfSquaresDiff = valuesList.Select(val => Math.Pow(val - average, 2)).Sum();
        return Math.Sqrt(sumOfSquaresDiff / valuesList.Count);
    }

    private static PerformanceTrend AnalyzePerformanceTrend(IReadOnlyList<KernelExecutionProfile> executions)
    {
        if (executions.Count < 2)
        {
            return PerformanceTrend.Stable;
        }


        var orderedExecutions = executions.OrderBy(e => e.StartTime).ToList();
        var firstHalf = orderedExecutions.Take(orderedExecutions.Count / 2);
        var secondHalf = orderedExecutions.Skip(orderedExecutions.Count / 2);


        var firstHalfAvg = firstHalf.Average(e => e.ExecutionTime.TotalMilliseconds);
        var secondHalfAvg = secondHalf.Average(e => e.ExecutionTime.TotalMilliseconds);


        var changePercentage = (secondHalfAvg - firstHalfAvg) / firstHalfAvg;


        return changePercentage switch
        {
            > 0.1 => PerformanceTrend.Degrading,
            < -0.1 => PerformanceTrend.Improving,
            _ => PerformanceTrend.Stable
        };
    }

    private static List<string> GenerateKernelOptimizationRecommendations(KernelAnalysisResult analysis)
    {
        var recommendations = new List<string>();


        if (analysis.AverageOccupancy < 50)
        {
            recommendations.Add("Low occupancy detected. Consider optimizing register usage or shared memory allocation.");
        }


        if (analysis.AverageCacheHitRate < 0.8)
        {
            recommendations.Add("Poor cache performance. Consider optimizing memory access patterns for better locality.");
        }


        if (analysis.AverageWarpEfficiency < 0.7)
        {
            recommendations.Add("Low warp efficiency. Reduce thread divergence and optimize control flow.");
        }


        if (analysis.ExecutionTimeStdDev > analysis.AverageExecutionTime * 0.3)
        {
            recommendations.Add("High execution time variance. Investigate load balancing and resource contention.");
        }


        return recommendations;
    }

    private static List<string> GenerateMemoryOptimizationRecommendations(MemoryAccessAnalysisResult analysis)
    {
        var recommendations = new List<string>();


        if (analysis.AverageCoalescingEfficiency < 0.8)
        {
            recommendations.Add("Poor memory coalescing. Restructure data access patterns for better alignment.");
        }


        if (analysis.AverageCacheHitRate < 0.9)
        {
            recommendations.Add("Suboptimal cache utilization. Consider data prefetching or blocking strategies.");
        }


        var bandwidth = analysis.AverageBandwidth;
        if (bandwidth < analysis.PeakBandwidth * 0.6)
        {
            recommendations.Add("Memory bandwidth underutilized. Consider increasing parallelism or data reuse.");
        }


        return recommendations;
    }

    private static double CalculateDeviceUtilizationEfficiency(ActiveProfile profile)
    {
        if (profile.DeviceMetrics.IsEmpty)
        {
            return 0;
        }


        return profile.DeviceMetrics.Values.Average(d => d.UtilizationPercentage) / 100.0;
    }

    private static double CalculateParallelismEfficiency(IReadOnlyList<KernelExecutionProfile> executions)
    {
        if (executions.Count <= 1)
        {
            return 1.0;
        }


        var totalTime = executions.Sum(e => e.ExecutionTime.TotalMilliseconds);
        var timeSpan = executions.Max(e => e.EndTime) - executions.Min(e => e.StartTime);


        return timeSpan.TotalMilliseconds > 0 ?

            Math.Min(1.0, totalTime / timeSpan.TotalMilliseconds) : 0;
    }

    private static List<string> IdentifyProfileBottlenecks(ActiveProfile profile)
    {
        var bottlenecks = new List<string>();

        // Check for long-running kernels (only if we have kernel executions)
        if (!profile.KernelExecutions.IsEmpty)
        {
            var avgKernelTime = profile.KernelExecutions.Average(k => k.ExecutionTime.TotalMilliseconds);
            var longRunningKernels = profile.KernelExecutions
                .Where(k => k.ExecutionTime.TotalMilliseconds > avgKernelTime * 2)
                .ToList();

            if (longRunningKernels.Count != 0)
            {
                bottlenecks.Add($"{longRunningKernels.Count} kernels are taking significantly longer than average");
            }
        }

        // Check for memory bandwidth issues (only if we have memory operations)
        if (!profile.MemoryOperations.IsEmpty)
        {
            var avgBandwidth = profile.MemoryOperations.Average(m => m.BandwidthGBPerSecond);
            var lowBandwidthOps = profile.MemoryOperations
                .Where(m => m.BandwidthGBPerSecond < avgBandwidth * 0.5)
                .ToList();

            if (lowBandwidthOps.Count > 0)
            {
                bottlenecks.Add($"{lowBandwidthOps.Count} memory operations are showing poor bandwidth utilization");
            }
        }

        return bottlenecks;
    }

    private static List<string> GenerateProfileOptimizationRecommendations(ActiveProfile profile)
    {
        var recommendations = new List<string>();


        if (!profile.KernelExecutions.IsEmpty)
        {
            var avgOccupancy = profile.KernelExecutions.Average(k => k.OccupancyPercentage);
            if (avgOccupancy < 60)
            {
                recommendations.Add("Overall low GPU occupancy. Consider kernel fusion or parameter tuning.");
            }
        }


        if (!profile.MemoryOperations.IsEmpty)
        {
            var avgCoalescing = profile.MemoryOperations.Average(m => m.CoalescingEfficiency);
            if (avgCoalescing < 0.7)
            {
                recommendations.Add("Poor memory access coalescing across operations. Review data layouts.");
            }
        }


        return recommendations;
    }

    private double GetProcessorUsage()
    {
        // Platform-specific CPU usage calculation
        try
        {
#if WINDOWS
#pragma warning disable CA1416 // Validate platform compatibility
            if (_hwCounters.TryGetValue("processor_time", out var counter))
            {
                return counter.NextValue();
            }
#pragma warning restore CA1416 // Validate platform compatibility
#endif
        }
        catch (Exception ex)
        {
            _logProcessorUsageFailed(_logger, ex);
        }

        // Fallback to process-based calculation

        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime.TotalMilliseconds;
    }

    private static int GetThreadPoolMetrics()
    {

        ThreadPool.GetAvailableThreads(out var workerThreads, out _);

        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out _);
        return maxWorkerThreads - workerThreads;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        // Dispose all active profiles

        foreach (var profile in _activeProfiles.Values)
        {
            // Cleanup resources if needed
        }

        // Dispose hardware counters

#if WINDOWS
#pragma warning disable CA1416 // Validate platform compatibility
        foreach (var counter in _hwCounters.Values)
        {
            counter?.Dispose();
        }
#pragma warning restore CA1416 // Validate platform compatibility
#else
        _hwCounters.Clear();
#endif


        _samplingTimer?.Dispose();
        _profilingSemaphore?.Dispose();
    }
}


// Supporting data structures continue...
