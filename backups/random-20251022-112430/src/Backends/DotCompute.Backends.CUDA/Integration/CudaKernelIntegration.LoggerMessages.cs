// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// LoggerMessage delegates for CudaKernelIntegration.
/// Event IDs: 5400-5449
/// </summary>
public sealed partial class CudaKernelIntegration
{
    [LoggerMessage(
        EventId = 5400,
        Level = LogLevel.Information,
        Message = "CUDA Kernel Integration initialized for device {DeviceId}")]
    private partial void LogIntegrationInitialized(int deviceId);

    [LoggerMessage(
        EventId = 5401,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' loaded from cache")]
    private partial void LogKernelCacheHit(string kernelName);

    [LoggerMessage(
        EventId = 5402,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' compiled and cached")]
    private partial void LogKernelCompiledAndCached(string kernelName);

    [LoggerMessage(
        EventId = 5403,
        Level = LogLevel.Error,
        Message = "Failed to compile optimized kernel '{KernelName}'")]
    private partial void LogCompileOptimizedFailed(Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 5404,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' executed: Success={Success}, Duration={Duration}")]
    private partial void LogKernelExecuted(string kernelName, bool success, TimeSpan duration);

    [LoggerMessage(
        EventId = 5405,
        Level = LogLevel.Error,
        Message = "Kernel '{KernelName}' execution failed")]
    private partial void LogKernelExecutionFailed(Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 5406,
        Level = LogLevel.Debug,
        Message = "Kernel optimization completed")]
    private partial void LogOptimizationCompleted();

    [LoggerMessage(
        EventId = 5407,
        Level = LogLevel.Warning,
        Message = "Kernel optimization failed")]
    private partial void LogOptimizationFailed(Exception ex);

    [LoggerMessage(
        EventId = 5408,
        Level = LogLevel.Debug,
        Message = "Cleaned up {Count} old execution statistics")]
    private partial void LogCleanedUpOldStats(int count);

    [LoggerMessage(
        EventId = 5409,
        Level = LogLevel.Debug,
        Message = "Kernel maintenance completed")]
    private partial void LogMaintenanceCompleted();

    [LoggerMessage(
        EventId = 5410,
        Level = LogLevel.Error,
        Message = "Error during kernel maintenance")]
    private partial void LogMaintenanceError(Exception ex);

    [LoggerMessage(
        EventId = 5411,
        Level = LogLevel.Debug,
        Message = "Optimization strategies updated for workload profile")]
    private partial void LogStrategiesUpdated();

    [LoggerMessage(
        EventId = 5412,
        Level = LogLevel.Debug,
        Message = "CUDA Kernel Integration disposed")]
    private partial void LogIntegrationDisposed();

    [LoggerMessage(
        EventId = 6250,
        Level = LogLevel.Warning,
        Message = "Failed to get optimal execution config for kernel '{KernelName}'")]
    private partial void LogFailedToGetOptimalConfig(Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 6251,
        Level = LogLevel.Warning,
        Message = "Error calculating kernel health")]
    private partial void LogErrorCalculatingKernelHealth(Exception ex);

    [LoggerMessage(
        EventId = 6252,
        Level = LogLevel.Warning,
        Message = "Failed to record execution statistics for kernel '{KernelName}'")]
    private partial void LogFailedToRecordExecutionStats(Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 6253,
        Level = LogLevel.Warning,
        Message = "Kernel '{KernelName}' has poor performance: {SuccessRate:P2} success rate")]
    private partial void LogKernelPoorPerformance(string kernelName, double successRate);

    [LoggerMessage(
        EventId = 6254,
        Level = LogLevel.Warning,
        Message = "Error during periodic optimization")]
    private partial void LogErrorDuringPeriodicOptimization(Exception ex);
}
