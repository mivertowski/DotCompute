// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.Metal.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // Original delegates
    [LoggerMessage(EventId = 90000, Level = LogLevel.Information, Message = "Metal backend initialized on device {DeviceName}")]
    public static partial void MetalInitialized(this ILogger logger, string deviceName);

    [LoggerMessage(EventId = 90001, Level = LogLevel.Warning, Message = "Metal backend not implemented: {Feature}")]
    public static partial void MetalNotImplemented(this ILogger logger, string feature);

    [LoggerMessage(EventId = 90002, Level = LogLevel.Error, Message = "Metal backend error: {ErrorMessage}")]
    public static partial void MetalError(this ILogger logger, string errorMessage, Exception? exception = null);

    // Trace Level - Kernel Execution (90010-90019)
    [LoggerMessage(EventId = 90010, Level = LogLevel.Trace, Message = "Executing Metal kernel: {KernelName}")]
    public static partial void KernelExecuting(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90011, Level = LogLevel.Trace, Message = "Dispatching kernel with grid size: ({GridX}, {GridY}, {GridZ}), threadgroup size: ({ThreadX}, {ThreadY}, {ThreadZ})")]
    public static partial void KernelDispatch(this ILogger logger, nuint gridX, nuint gridY, nuint gridZ, nuint threadX, nuint threadY, nuint threadZ);

    [LoggerMessage(EventId = 90012, Level = LogLevel.Trace, Message = "Metal kernel execution completed: {KernelName}")]
    public static partial void KernelExecutionCompleted(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90013, Level = LogLevel.Trace, Message = "Calculated dispatch dimensions - Grid: ({GridX}, {GridY}, {GridZ}), Threadgroup: ({ThreadX}, {ThreadY}, {ThreadZ})")]
    public static partial void KernelCalculatedDispatch(this ILogger logger, nuint gridX, nuint gridY, nuint gridZ, nuint threadX, nuint threadY, nuint threadZ);

    [LoggerMessage(EventId = 90014, Level = LogLevel.Trace, Message = "Using cached Metal library for kernel: {KernelName}")]
    public static partial void CompilerUsingCache(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90015, Level = LogLevel.Trace, Message = "Compiling Metal kernel: {KernelName}")]
    public static partial void CompilerCompilingKernel(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90016, Level = LogLevel.Trace, Message = "Successfully compiled Metal kernel: {KernelName}")]
    public static partial void CompilerKernelCompiled(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90017, Level = LogLevel.Trace, Message = "Enabled fast math optimizations for kernel: {KernelName}")]
    public static partial void FastMathOptimization(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90018, Level = LogLevel.Trace, Message = "Maximum optimization level requested for kernel: {KernelName}")]
    public static partial void MaxOptimizationRequested(this ILogger logger, string kernelName);

    [LoggerMessage(EventId = 90019, Level = LogLevel.Trace, Message = "Executing compute shader with {BufferCount} buffers")]
    public static partial void ComputeShaderExecuting(this ILogger logger, int bufferCount);

    // Trace Level - Command Buffer Pool (90020-90029)
    [LoggerMessage(EventId = 90020, Level = LogLevel.Trace, Message = "Reused command buffer from pool: {Buffer:X}")]
    public static partial void CommandBufferReused(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90021, Level = LogLevel.Trace, Message = "Created new command buffer: {Buffer:X}")]
    public static partial void CommandBufferCreated(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90022, Level = LogLevel.Trace, Message = "Returned command buffer to pool: {Buffer:X}")]
    public static partial void CommandBufferReturned(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90023, Level = LogLevel.Trace, Message = "Disposed command buffer: {Buffer:X}")]
    public static partial void CommandBufferDisposed(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90024, Level = LogLevel.Trace, Message = "Compute shader execution completed")]
    public static partial void ComputeShaderExecutionCompleted(this ILogger logger);

    [LoggerMessage(EventId = 90025, Level = LogLevel.Trace, Message = "Metal operation completed: {Operation} took {Duration:F2}ms")]
    public static partial void OperationCompleted(this ILogger logger, string operation, double duration);

    // Debug Level (90030-90039)
    [LoggerMessage(EventId = 90030, Level = LogLevel.Debug, Message = "Created Metal command buffer pool with max size: {MaxSize}")]
    public static partial void CommandBufferPoolCreated(this ILogger logger, int maxSize);

    [LoggerMessage(EventId = 90031, Level = LogLevel.Debug, Message = "Cleaned up {Count} stale command buffers from pool")]
    public static partial void CommandBufferPoolCleanup(this ILogger logger, int count);

    [LoggerMessage(EventId = 90032, Level = LogLevel.Debug, Message = "Disposing Metal command buffer pool")]
    public static partial void CommandBufferPoolDisposing(this ILogger logger);

    [LoggerMessage(EventId = 90033, Level = LogLevel.Debug, Message = "Disposed Metal command buffer pool")]
    public static partial void CommandBufferPoolDisposed(this ILogger logger);

    [LoggerMessage(EventId = 90034, Level = LogLevel.Debug, Message = "Disposed Metal performance profiler")]
    public static partial void PerformanceProfilerDisposed(this ILogger logger);

    // Information Level (90040-90049)
    [LoggerMessage(EventId = 90040, Level = LogLevel.Information, Message = "Reset all Metal performance metrics")]
    public static partial void PerformanceMetricsReset(this ILogger logger);

    [LoggerMessage(EventId = 90041, Level = LogLevel.Information, Message = "Metal Performance Summary:\n{Report}")]
    public static partial void PerformanceSummary(this ILogger logger, string report);

    // Warning Level (90050-90059)
    [LoggerMessage(EventId = 90050, Level = LogLevel.Warning, Message = "Slow Metal operation detected: {Operation} took {Duration:F2}ms")]
    public static partial void SlowOperationDetected(this ILogger logger, string operation, double duration);

    [LoggerMessage(EventId = 90051, Level = LogLevel.Warning, Message = "Failed to track active command buffer: {Buffer:X}")]
    public static partial void CommandBufferTrackingFailed(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90052, Level = LogLevel.Warning, Message = "Attempted to return untracked command buffer: {Buffer:X}")]
    public static partial void CommandBufferUntracked(this ILogger logger, long buffer);

    [LoggerMessage(EventId = 90053, Level = LogLevel.Warning, Message = "Found stale active command buffer (age: {Age}), disposing: {Buffer:X}")]
    public static partial void StaleActiveCommandBuffer(this ILogger logger, TimeSpan age, long buffer);

    [LoggerMessage(EventId = 90054, Level = LogLevel.Warning, Message = "Metal is not available on this platform")]
    public static partial void MetalNotAvailable(this ILogger logger);

    [LoggerMessage(EventId = 90055, Level = LogLevel.Warning, Message = "Failed to create Metal device at index {DeviceIndex}")]
    public static partial void FailedToCreateDevice(this ILogger logger, int deviceIndex);

    // Error Level (90060-90069)
    [LoggerMessage(EventId = 90060, Level = LogLevel.Error, Message = "Metal kernel execution failed: {KernelName}")]
    public static partial void KernelExecutionFailed(this ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(EventId = 90061, Level = LogLevel.Error, Message = "Failed to compile Metal function: {FunctionName}")]
    public static partial void MetalFunctionCompilationFailed(this ILogger logger, Exception exception, string functionName);

    [LoggerMessage(EventId = 90062, Level = LogLevel.Error, Message = "Failed to execute compute shader")]
    public static partial void ComputeShaderExecutionFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 90063, Level = LogLevel.Error, Message = "Failed to create command queue")]
    public static partial void CommandQueueCreationFailed(this ILogger logger, Exception exception);
}