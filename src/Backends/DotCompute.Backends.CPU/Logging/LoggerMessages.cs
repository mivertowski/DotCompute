// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.CPU.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // CPU Initialization Messages
    [LoggerMessage(
        EventId = 40000,
        Level = LogLevel.Information,
        Message = "CPU backend initialized: {ProcessorCount} cores, {Architecture} architecture")]
    public static partial void CpuInitialized(this ILogger logger, int processorCount, string architecture);

    [LoggerMessage(
        EventId = 40001,
        Level = LogLevel.Information,
        Message = "SIMD support detected: {SimdLevel} with {VectorSize}-bit vectors")]
    public static partial void SimdSupported(this ILogger logger, string simdLevel, int vectorSize);

    // CPU Execution Messages
    [LoggerMessage(
        EventId = 41000,
        Level = LogLevel.Debug,
        Message = "Executing CPU kernel {KernelName} with {ThreadCount} threads")]
    public static partial void CpuKernelExecuting(this ILogger logger, string kernelName, int threadCount);

    [LoggerMessage(
        EventId = 41001,
        Level = LogLevel.Debug,
        Message = "CPU kernel {KernelName} completed in {ElapsedMs}ms")]
    public static partial void CpuKernelCompleted(this ILogger logger, string kernelName, double elapsedMs);

    [LoggerMessage(
        EventId = 41002,
        Level = LogLevel.Information,
        Message = "Vectorized operation {Operation} achieved {Speedup:F2}x speedup")]
    public static partial void VectorizedSpeedup(this ILogger logger, string operation, double speedup);

    // Thread Pool Messages
    [LoggerMessage(
        EventId = 42000,
        Level = LogLevel.Debug,
        Message = "Thread pool configured: Min={MinThreads}, Max={MaxThreads}")]
    public static partial void ThreadPoolConfigured(this ILogger logger, int minThreads, int maxThreads);

    [LoggerMessage(
        EventId = 42001,
        Level = LogLevel.Warning,
        Message = "Thread pool exhaustion detected: {PendingTasks} tasks pending")]
    public static partial void ThreadPoolExhaustion(this ILogger logger, int pendingTasks);

    // SIMD Optimization Messages
    [LoggerMessage(
        EventId = 43000,
        Level = LogLevel.Debug,
        Message = "Using {SimdType} intrinsics for {Operation}")]
    public static partial void SimdIntrinsicsUsed(this ILogger logger, string simdType, string operation);

    [LoggerMessage(
        EventId = 43001,
        Level = LogLevel.Information,
        Message = "AVX512 detected and enabled for enhanced performance")]
    public static partial void Avx512Enabled(this ILogger logger);

    [LoggerMessage(
        EventId = 43002,
        Level = LogLevel.Warning,
        Message = "Falling back to scalar implementation for {Operation}: SIMD not supported")]
    public static partial void SimdFallback(this ILogger logger, string operation);

    // Memory Messages
    [LoggerMessage(
        EventId = 44000,
        Level = LogLevel.Debug,
        Message = "CPU memory allocated: {SizeBytes} bytes, alignment: {Alignment}")]
    public static partial void CpuMemoryAllocated(this ILogger logger, long sizeBytes, int alignment);

    [LoggerMessage(
        EventId = 44001,
        Level = LogLevel.Debug,
        Message = "Cache-friendly memory layout applied: {LayoutType}")]
    public static partial void CacheFriendlyLayout(this ILogger logger, string layoutType);

    // Performance Messages
    [LoggerMessage(
        EventId = 45000,
        Level = LogLevel.Information,
        Message = "CPU utilization: {Utilization:F1}%, Cache misses: {CacheMisses}")]
    public static partial void CpuPerformanceMetrics(this ILogger logger, double utilization, long cacheMisses);

    // Error Messages
    [LoggerMessage(
        EventId = 46000,
        Level = LogLevel.Error,
        Message = "CPU backend error in {Component}: {ErrorMessage}")]
    public static partial void CpuError(this ILogger logger, string component, string errorMessage, Exception? exception = null);

    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 47000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessageWithException(this ILogger logger, Exception ex, string message);

    [LoggerMessage(EventId = 47005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 47006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);

    // ===== CpuKernelCache Messages (45000-45099) =====
    [LoggerMessage(EventId = 45000, Level = LogLevel.Debug, Message = "CpuKernelCache initialized with max size: {MaxSize}, expiry: {Expiry}")]
    public static partial void CacheInitialized(this ILogger logger, int maxSize, TimeSpan expiry);

    [LoggerMessage(EventId = 45001, Level = LogLevel.Debug, Message = "Cache hit for kernel: {CacheKey}")]
    public static partial void CacheHit(this ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 45002, Level = LogLevel.Debug, Message = "Cache miss (expired) for kernel: {CacheKey}")]
    public static partial void CacheMissExpired(this ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 45003, Level = LogLevel.Debug, Message = "Cache miss for kernel: {CacheKey}")]
    public static partial void CacheMiss(this ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 45004, Level = LogLevel.Debug, Message = "Stored kernel in cache: {CacheKey}, expires: {Expiry}")]
    public static partial void KernelStored(this ILogger logger, string cacheKey, DateTimeOffset expiry);

    [LoggerMessage(EventId = 45005, Level = LogLevel.Debug, Message = "Removed kernel from cache: {CacheKey}")]
    public static partial void KernelRemoved(this ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 45006, Level = LogLevel.Trace, Message = "Optimization profile cache hit: {ProfileKey}")]
    public static partial void ProfileCacheHit(this ILogger logger, string profileKey);

    [LoggerMessage(EventId = 45007, Level = LogLevel.Trace, Message = "Optimization profile cache miss (expired): {ProfileKey}")]
    public static partial void ProfileCacheMissExpired(this ILogger logger, string profileKey);

    [LoggerMessage(EventId = 45008, Level = LogLevel.Trace, Message = "Stored optimization profile: {ProfileKey}")]
    public static partial void ProfileStored(this ILogger logger, string profileKey);

    [LoggerMessage(EventId = 45009, Level = LogLevel.Trace, Message = "Performance metrics cache hit: {MetricsKey}")]
    public static partial void MetricsCacheHit(this ILogger logger, string metricsKey);

    [LoggerMessage(EventId = 45010, Level = LogLevel.Trace, Message = "Performance metrics cache miss (expired): {MetricsKey}")]
    public static partial void MetricsCacheMissExpired(this ILogger logger, string metricsKey);

    [LoggerMessage(EventId = 45011, Level = LogLevel.Trace, Message = "Stored performance metrics: {MetricsKey}")]
    public static partial void MetricsStored(this ILogger logger, string metricsKey);

    [LoggerMessage(EventId = 45012, Level = LogLevel.Trace, Message = "Updated performance metrics for kernel: {CacheKey}")]
    public static partial void MetricsUpdated(this ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 45013, Level = LogLevel.Information, Message = "Cleared all cache entries")]
    public static partial void CacheCleared(this ILogger logger);

    [LoggerMessage(EventId = 45014, Level = LogLevel.Debug, Message = "Preload requested for kernel: {KernelName}")]
    public static partial void PreloadRequested(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 45015, Level = LogLevel.Debug, Message = "Evicted {Count} cache entries due to size limit")]
    public static partial void EntriesEvicted(this ILogger logger, int count);

    [LoggerMessage(EventId = 45016, Level = LogLevel.Error, Message = "Error during cache cleanup")]
    public static partial void CacheCleanupError(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 45017, Level = LogLevel.Debug, Message = "Cleaned up expired cache entries: {Kernels} kernels, {Profiles} profiles, {Metrics} metrics")]
    public static partial void ExpiredEntriesCleaned(this ILogger logger, int kernels, int profiles, int metrics);

    [LoggerMessage(EventId = 45018, Level = LogLevel.Error, Message = "Error disposing cached kernel")]
    public static partial void KernelDisposeError(this ILogger logger, Exception ex);

    // ===== CpuKernelOptimizer Messages (43000-43099) =====
    [LoggerMessage(EventId = 43010, Level = LogLevel.Debug, Message = "CpuKernelOptimizer initialized")]
    public static partial void OptimizerInitialized(this ILogger logger);

    [LoggerMessage(EventId = 43011, Level = LogLevel.Debug, Message = "Creating optimized execution plan for kernel {KernelName} with {Optimization} optimization")]
    public static partial void CreatingExecutionPlan(this ILogger logger, string kernelName, string optimization);

    [LoggerMessage(EventId = 43012, Level = LogLevel.Debug, Message = "Using cached optimization profile for kernel {KernelName}")]
    public static partial void UsingCachedProfile(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43013, Level = LogLevel.Debug, Message = "Optimization plan created for kernel {KernelName} in {TimeMs:F2}ms")]
    public static partial void OptimizationPlanCreated(this ILogger logger, string kernelName, double timeMs);

    [LoggerMessage(EventId = 43014, Level = LogLevel.Error, Message = "Failed to create optimization plan for kernel {KernelName}")]
    public static partial void OptimizationPlanFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 43015, Level = LogLevel.Debug, Message = "Analyzing performance for kernel {KernelName}")]
    public static partial void AnalyzingPerformance(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43016, Level = LogLevel.Debug, Message = "Performance analysis completed for kernel {KernelName} with {Count} recommendations")]
    public static partial void PerformanceAnalysisCompleted(this ILogger logger, string kernelName, int count);

    [LoggerMessage(EventId = 43017, Level = LogLevel.Error, Message = "Performance analysis failed for kernel {KernelName}")]
    public static partial void PerformanceAnalysisFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 43018, Level = LogLevel.Information, Message = "Benchmarking execution strategies for kernel {KernelName}")]
    public static partial void BenchmarkingStrategies(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43019, Level = LogLevel.Information, Message = "Benchmark completed for kernel {KernelName}: Optimal={Optimal}")]
    public static partial void BenchmarkCompleted(this ILogger logger, string kernelName, string optimal);

    [LoggerMessage(EventId = 43020, Level = LogLevel.Error, Message = "Benchmarking failed for kernel {KernelName}")]
    public static partial void BenchmarkFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 43021, Level = LogLevel.Debug, Message = "Applying dynamic optimizations for kernel {KernelName}")]
    public static partial void ApplyingDynamicOptimizations(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43022, Level = LogLevel.Debug, Message = "Thread pool configuration optimized for kernel {KernelName}")]
    public static partial void ThreadPoolOptimized(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43023, Level = LogLevel.Debug, Message = "Vectorization settings optimized for kernel {KernelName}")]
    public static partial void VectorizationOptimized(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 43024, Level = LogLevel.Error, Message = "Dynamic optimization failed for kernel {KernelName}")]
    public static partial void DynamicOptimizationFailed(this ILogger logger, Exception ex, string kernelName);

    // ===== CpuKernelExecutor Messages (42000-42099) =====
    [LoggerMessage(EventId = 42010, Level = LogLevel.Debug, Message = "Executing kernel '{KernelName}' with {TotalWorkItems} items using {Strategy}")]
    public static partial void ExecutingKernel(this ILogger logger, string kernelName, long totalWorkItems, string strategy);

    [LoggerMessage(EventId = 42011, Level = LogLevel.Debug, Message = "Kernel '{KernelName}' executed in {ElapsedMs}ms")]
    public static partial void KernelExecuted(this ILogger logger, string kernelName, long elapsedMs);

    [LoggerMessage(EventId = 42012, Level = LogLevel.Error, Message = "Failed to execute kernel '{KernelName}'")]
    public static partial void KernelExecutionFailed(this ILogger logger, Exception ex, string kernelName);

    // ===== CpuKernelValidator Messages (44000-44099) =====
    [LoggerMessage(EventId = 44010, Level = LogLevel.Debug, Message = "CpuKernelValidator initialized with CPU capabilities: {Capabilities}")]
    public static partial void ValidatorInitialized(this ILogger logger, string capabilities);

    [LoggerMessage(EventId = 44011, Level = LogLevel.Debug, Message = "Validating kernel {KernelName} for CPU execution")]
    public static partial void ValidatingKernel(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 44012, Level = LogLevel.Debug, Message = "Kernel validation completed for {KernelName}: Valid={IsValid}, Issues={IssueCount}")]
    public static partial void ValidationCompleted(this ILogger logger, string kernelName, bool isValid, int issueCount);

    [LoggerMessage(EventId = 44013, Level = LogLevel.Error, Message = "Kernel validation failed for {KernelName}")]
    public static partial void ValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 44014, Level = LogLevel.Debug, Message = "Validating execution plan for kernel {KernelName}")]
    public static partial void ValidatingExecutionPlan(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 44015, Level = LogLevel.Error, Message = "Execution plan validation failed for {KernelName}")]
    public static partial void ExecutionPlanValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 44016, Level = LogLevel.Debug, Message = "Validating kernel arguments for {KernelName}")]
    public static partial void ValidatingArguments(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 44017, Level = LogLevel.Error, Message = "Argument validation failed for {KernelName}")]
    public static partial void ArgumentValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(EventId = 44018, Level = LogLevel.Error, Message = "Runtime validation failed for {KernelName}")]
    public static partial void RuntimeValidationFailed(this ILogger logger, Exception ex, string kernelName);
}
