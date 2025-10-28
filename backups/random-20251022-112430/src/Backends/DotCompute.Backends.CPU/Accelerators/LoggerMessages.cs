// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Centralized LoggerMessage delegates for CPU backend accelerators.
/// Event ID ranges:
/// - 40000-40999: CPU Backend general operations
/// - 41000-41999: Memory management
/// - 42000-42999: Kernel execution
/// - 43000-43999: Kernel optimization
/// - 44000-44999: Kernel validation
/// - 45000-45999: Kernel caching
/// </summary>
internal static partial class LoggerMessages
{
    // ========================================
    // CPU Memory Manager (41000-41999)
    // ========================================

    [LoggerMessage(
        EventId = 41000,
        Level = LogLevel.Debug,
        Message = "Selected NUMA node {Node} for {Size} bytes allocation with policy {Policy}")]
    public static partial void LogNumaNodeSelected(this ILogger logger, int node, long size, string policy);

    [LoggerMessage(
        EventId = 41001,
        Level = LogLevel.Warning,
        Message = "No suitable NUMA nodes found for allocation of {Size} bytes, using node 0")]
    public static partial void LogNoSuitableNumaNodes(this ILogger logger, long size);

    [LoggerMessage(
        EventId = 41002,
        Level = LogLevel.Debug,
        Message = "Failed to get current thread NUMA node, using node 0")]
    public static partial void LogFailedToGetNumaNode(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41003,
        Level = LogLevel.Debug,
        Message = "Failed to get per-node memory info, using fallback")]
    public static partial void LogFailedToGetPerNodeMemory(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41004,
        Level = LogLevel.Debug,
        Message = "Created NUMA-aware buffer: {Size} bytes on node {Node}")]
    public static partial void LogNumaBufferCreated(this ILogger logger, long size, int node);

    [LoggerMessage(
        EventId = 41005,
        Level = LogLevel.Warning,
        Message = "Failed to create NUMA-aware buffer, falling back to default allocation")]
    public static partial void LogNumaBufferCreationFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41006,
        Level = LogLevel.Debug,
        Message = "Pinning {Size} bytes for buffer {BufferId}")]
    public static partial void LogPinningBuffer(this ILogger logger, long size, int bufferId);

    [LoggerMessage(
        EventId = 41007,
        Level = LogLevel.Warning,
        Message = "Failed to pin buffer memory")]
    public static partial void LogPinBufferFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41008,
        Level = LogLevel.Debug,
        Message = "Binding buffer to NUMA node {Node}")]
    public static partial void LogBindingBufferToNode(this ILogger logger, int node);

    [LoggerMessage(
        EventId = 41009,
        Level = LogLevel.Warning,
        Message = "Failed to bind buffer to NUMA node {Node}")]
    public static partial void LogBindBufferFailed(this ILogger logger, Exception ex, int node);

    [LoggerMessage(
        EventId = 41010,
        Level = LogLevel.Debug,
        Message = "Created shared memory view: offset={Offset}, length={Length}")]
    public static partial void LogSharedMemoryViewCreated(this ILogger logger, long offset, long length);

    [LoggerMessage(
        EventId = 41011,
        Level = LogLevel.Error,
        Message = "Failed to create shared memory view")]
    public static partial void LogSharedMemoryViewFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41012,
        Level = LogLevel.Debug,
        Message = "Copied {Count} elements between CPU buffers")]
    public static partial void LogCopiedBetweenBuffers(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 41013,
        Level = LogLevel.Error,
        Message = "Failed to copy between CPU buffers")]
    public static partial void LogCopyBetweenBuffersFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41014,
        Level = LogLevel.Debug,
        Message = "Copied {Count} elements with offsets: src={SrcOffset}, dst={DstOffset}")]
    public static partial void LogCopiedWithOffsets(this ILogger logger, int count, int srcOffset, int dstOffset);

    [LoggerMessage(
        EventId = 41015,
        Level = LogLevel.Error,
        Message = "Failed to copy between CPU buffers with offsets")]
    public static partial void LogCopyWithOffsetsFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41016,
        Level = LogLevel.Debug,
        Message = "Copied {Count} elements from CPU buffer to host memory")]
    public static partial void LogCopiedToHost(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 41017,
        Level = LogLevel.Error,
        Message = "Failed to copy from CPU buffer to host memory")]
    public static partial void LogCopyToHostFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41018,
        Level = LogLevel.Debug,
        Message = "Copied {Count} elements from host memory to CPU buffer")]
    public static partial void LogCopiedFromHost(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 41019,
        Level = LogLevel.Error,
        Message = "Failed to copy from host memory to CPU buffer")]
    public static partial void LogCopyFromHostFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41020,
        Level = LogLevel.Error,
        Message = "Failed to create typed buffer view")]
    public static partial void LogTypedBufferViewFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 41021,
        Level = LogLevel.Debug,
        Message = "Allocating {Size} bytes on NUMA node {Node}")]
    public static partial void LogAllocatingOnNode(this ILogger logger, long size, int node);

    // ========================================
    // CPU Kernel Cache (45000-45999)
    // ========================================

    [LoggerMessage(
        EventId = 45000,
        Level = LogLevel.Debug,
        Message = "CpuKernelCache initialized with max size: {maxSize}, expiry: {expiry}")]
    public static partial void LogCacheInitialized(this ILogger logger, int maxSize, TimeSpan expiry);

    [LoggerMessage(
        EventId = 45001,
        Level = LogLevel.Debug,
        Message = "Cache hit for kernel: {cacheKey}")]
    public static partial void LogCacheHit(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 45002,
        Level = LogLevel.Debug,
        Message = "Cache miss (expired) for kernel: {cacheKey}")]
    public static partial void LogCacheMissExpired(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 45003,
        Level = LogLevel.Debug,
        Message = "Cache miss for kernel: {cacheKey}")]
    public static partial void LogCacheMiss(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 45004,
        Level = LogLevel.Debug,
        Message = "Stored kernel in cache: {cacheKey}, expires: {expiry}")]
    public static partial void LogKernelStored(this ILogger logger, string cacheKey, DateTimeOffset expiry);

    [LoggerMessage(
        EventId = 45005,
        Level = LogLevel.Debug,
        Message = "Removed kernel from cache: {cacheKey}")]
    public static partial void LogKernelRemoved(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 45006,
        Level = LogLevel.Trace,
        Message = "Optimization profile cache hit: {profileKey}")]
    public static partial void LogProfileCacheHit(this ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 45007,
        Level = LogLevel.Trace,
        Message = "Optimization profile cache miss (expired): {profileKey}")]
    public static partial void LogProfileCacheMissExpired(this ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 45008,
        Level = LogLevel.Trace,
        Message = "Stored optimization profile: {profileKey}")]
    public static partial void LogProfileStored(this ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 45009,
        Level = LogLevel.Trace,
        Message = "Performance metrics cache hit: {metricsKey}")]
    public static partial void LogMetricsCacheHit(this ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 45010,
        Level = LogLevel.Trace,
        Message = "Performance metrics cache miss (expired): {metricsKey}")]
    public static partial void LogMetricsCacheMissExpired(this ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 45011,
        Level = LogLevel.Trace,
        Message = "Stored performance metrics: {metricsKey}")]
    public static partial void LogMetricsStored(this ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 45012,
        Level = LogLevel.Trace,
        Message = "Updated performance metrics for kernel: {cacheKey}")]
    public static partial void LogMetricsUpdated(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 45013,
        Level = LogLevel.Information,
        Message = "Cleared all cache entries")]
    public static partial void LogCacheCleared(this ILogger logger);

    [LoggerMessage(
        EventId = 45014,
        Level = LogLevel.Debug,
        Message = "Preload requested for kernel: {kernelName}")]
    public static partial void LogPreloadRequested(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 45015,
        Level = LogLevel.Debug,
        Message = "Evicted {count} cache entries due to size limit")]
    public static partial void LogEntriesEvicted(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 45016,
        Level = LogLevel.Error,
        Message = "Error during cache cleanup")]
    public static partial void LogCleanupError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 45017,
        Level = LogLevel.Debug,
        Message = "Cleaned up expired cache entries: {kernels} kernels, {profiles} profiles, {metrics} metrics")]
    public static partial void LogCleanupCompleted(this ILogger logger, int kernels, int profiles, int metrics);

    [LoggerMessage(
        EventId = 45018,
        Level = LogLevel.Error,
        Message = "Error disposing cached kernel")]
    public static partial void LogDisposeKernelError(this ILogger logger, Exception ex);

    // ========================================
    // CPU Kernel Optimizer (43000-43999)
    // ========================================

    [LoggerMessage(
        EventId = 43000,
        Level = LogLevel.Debug,
        Message = "CpuKernelOptimizer initialized")]
    public static partial void LogOptimizerInitialized(this ILogger logger);

    [LoggerMessage(
        EventId = 43001,
        Level = LogLevel.Debug,
        Message = "Creating optimized execution plan for kernel {kernelName} with {optimization} optimization")]
    public static partial void LogCreatingExecutionPlan(this ILogger logger, string kernelName, string optimization);

    [LoggerMessage(
        EventId = 43002,
        Level = LogLevel.Debug,
        Message = "Using cached optimization profile for kernel {kernelName}")]
    public static partial void LogUsingCachedProfile(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43003,
        Level = LogLevel.Debug,
        Message = "Optimization plan created for kernel {kernelName} in {time:F2}ms")]
    public static partial void LogOptimizationPlanCreated(this ILogger logger, string kernelName, double time);

    [LoggerMessage(
        EventId = 43004,
        Level = LogLevel.Error,
        Message = "Failed to create optimization plan for kernel {kernelName}")]
    public static partial void LogOptimizationPlanFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 43005,
        Level = LogLevel.Debug,
        Message = "Analyzing performance for kernel {kernelName}")]
    public static partial void LogAnalyzingPerformance(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43006,
        Level = LogLevel.Debug,
        Message = "Performance analysis completed for kernel {kernelName} with {count} recommendations")]
    public static partial void LogPerformanceAnalysisCompleted(this ILogger logger, string kernelName, int count);

    [LoggerMessage(
        EventId = 43007,
        Level = LogLevel.Error,
        Message = "Performance analysis failed for kernel {kernelName}")]
    public static partial void LogPerformanceAnalysisFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 43008,
        Level = LogLevel.Information,
        Message = "Benchmarking execution strategies for kernel {kernelName}")]
    public static partial void LogBenchmarkingStrategies(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43009,
        Level = LogLevel.Information,
        Message = "Benchmark completed for kernel {kernelName}: Optimal={optimal}")]
    public static partial void LogBenchmarkCompleted(this ILogger logger, string kernelName, string optimal);

    [LoggerMessage(
        EventId = 43010,
        Level = LogLevel.Error,
        Message = "Benchmarking failed for kernel {kernelName}")]
    public static partial void LogBenchmarkFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 43011,
        Level = LogLevel.Debug,
        Message = "Applying dynamic optimizations for kernel {kernelName}")]
    public static partial void LogApplyingDynamicOptimizations(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43012,
        Level = LogLevel.Debug,
        Message = "Thread pool configuration optimized for kernel {kernelName}")]
    public static partial void LogThreadPoolOptimized(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43013,
        Level = LogLevel.Debug,
        Message = "Vectorization settings optimized for kernel {kernelName}")]
    public static partial void LogVectorizationOptimized(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 43014,
        Level = LogLevel.Error,
        Message = "Dynamic optimization failed for kernel {kernelName}")]
    public static partial void LogDynamicOptimizationFailed(this ILogger logger, Exception ex, string kernelName);

    // ========================================
    // CPU Kernel Executor (42000-42999)
    // ========================================

    [LoggerMessage(
        EventId = 42000,
        Level = LogLevel.Debug,
        Message = "CpuKernelExecutor initialized for kernel {kernelName} with vectorization: {useVectorization}")]
    public static partial void LogExecutorInitialized(this ILogger logger, string kernelName, bool useVectorization);

    [LoggerMessage(
        EventId = 42001,
        Level = LogLevel.Debug,
        Message = "Compiled delegate set for kernel {kernelName}")]
    public static partial void LogCompiledDelegateSet(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 42002,
        Level = LogLevel.Debug,
        Message = "Executing kernel {kernelName} with work dimensions [{x}, {y}, {z}]")]
    public static partial void LogExecutingKernel(this ILogger logger, string kernelName, long x, long y, long z);

    [LoggerMessage(
        EventId = 42003,
        Level = LogLevel.Error,
        Message = "Kernel execution failed for {kernelName}")]
    public static partial void LogExecutionFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 42004,
        Level = LogLevel.Debug,
        Message = "Executing vectorized kernel {kernelName}")]
    public static partial void LogExecutingVectorized(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 42005,
        Level = LogLevel.Debug,
        Message = "Executing compiled delegate for kernel {kernelName}")]
    public static partial void LogExecutingCompiledDelegate(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 42006,
        Level = LogLevel.Debug,
        Message = "Executing scalar kernel {kernelName}")]
    public static partial void LogExecutingScalar(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 42007,
        Level = LogLevel.Trace,
        Message = "Vectorized execution for {count} work items with vector width {width}")]
    public static partial void LogVectorizedExecution(this ILogger logger, int count, int width);

    [LoggerMessage(
        EventId = 42008,
        Level = LogLevel.Error,
        Message = "Vectorized work item execution failed")]
    public static partial void LogVectorizedExecutionFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 42009,
        Level = LogLevel.Error,
        Message = "Compiled delegate work item execution failed")]
    public static partial void LogCompiledDelegateExecutionFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 42010,
        Level = LogLevel.Error,
        Message = "Scalar work item execution failed")]
    public static partial void LogScalarExecutionFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 42011,
        Level = LogLevel.Trace,
        Message = "Executing basic kernel for work item [{workItem}]")]
    public static partial void LogExecutingBasicKernel(this ILogger logger, string workItem);

    [LoggerMessage(
        EventId = 42012,
        Level = LogLevel.Trace,
        Message = "Kernel {kernelName} executed in {executionTime:F2}ms (execution #{count})")]
    public static partial void LogKernelExecuted(this ILogger logger, string kernelName, double executionTime, long count);

    [LoggerMessage(
        EventId = 42013,
        Level = LogLevel.Information,
        Message = "Kernel {kernelName} performance: {count} executions, {avgTime:F2}ms average")]
    public static partial void LogKernelPerformance(this ILogger logger, string kernelName, long count, double avgTime);

    // ========================================
    // CPU Kernel Validator (44000-44999)
    // ========================================

    [LoggerMessage(
        EventId = 44000,
        Level = LogLevel.Debug,
        Message = "CpuKernelValidator initialized with CPU capabilities: {capabilities}")]
    public static partial void LogValidatorInitialized(this ILogger logger, string capabilities);

    [LoggerMessage(
        EventId = 44001,
        Level = LogLevel.Debug,
        Message = "Validating kernel {kernelName} for CPU execution")]
    public static partial void LogValidatingKernel(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 44002,
        Level = LogLevel.Debug,
        Message = "Kernel validation completed for {kernelName}: Valid={isValid}, Issues={issueCount}")]
    public static partial void LogValidationCompleted(this ILogger logger, string kernelName, bool isValid, int issueCount);

    [LoggerMessage(
        EventId = 44003,
        Level = LogLevel.Error,
        Message = "Kernel validation failed for {kernelName}")]
    public static partial void LogValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 44004,
        Level = LogLevel.Debug,
        Message = "Validating execution plan for kernel {kernelName}")]
    public static partial void LogValidatingExecutionPlan(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 44005,
        Level = LogLevel.Error,
        Message = "Execution plan validation failed for {kernelName}")]
    public static partial void LogExecutionPlanValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 44006,
        Level = LogLevel.Debug,
        Message = "Validating kernel arguments for {kernelName}")]
    public static partial void LogValidatingArguments(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 44007,
        Level = LogLevel.Error,
        Message = "Argument validation failed for {kernelName}")]
    public static partial void LogArgumentValidationFailed(this ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 44008,
        Level = LogLevel.Error,
        Message = "Runtime validation failed for {kernelName}")]
    public static partial void LogRuntimeValidationFailed(this ILogger logger, Exception ex, string kernelName);
}
