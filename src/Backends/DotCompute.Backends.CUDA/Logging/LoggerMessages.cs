// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.CUDA.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // CUDA Initialization Messages
    [LoggerMessage(
        EventId = 20000,
        Level = LogLevel.Information,
        Message = "CUDA Runtime initialized. Version: {Version}, Driver: {DriverVersion}")]
    public static partial void CudaInitialized(this ILogger logger, string version, string driverVersion);

    [LoggerMessage(
        EventId = 20001,
        Level = LogLevel.Information,
        Message = "Found {DeviceCount} CUDA devices")]
    public static partial void CudaDevicesFound(this ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 20002,
        Level = LogLevel.Information,
        Message = "CUDA Device {DeviceId}: {DeviceName}, Compute Capability: {ComputeCapability}, Memory: {MemoryGB:F1}GB")]
    public static partial void CudaDeviceInfo(this ILogger logger, int deviceId, string deviceName, string computeCapability, double memoryGB);

    // CUDA Compilation Messages
    [LoggerMessage(
        EventId = 21000,
        Level = LogLevel.Debug,
        Message = "Compiling CUDA kernel {KernelName} to {TargetFormat} for compute capability {ComputeCapability}")]
    public static partial void CudaCompilationStarted(this ILogger logger, string kernelName, string targetFormat, string computeCapability);

    [LoggerMessage(
        EventId = 21001,
        Level = LogLevel.Information,
        Message = "CUDA kernel {KernelName} compiled successfully in {ElapsedMs}ms")]
    public static partial void CudaCompilationSuccess(this ILogger logger, string kernelName, double elapsedMs);

    [LoggerMessage(
        EventId = 21002,
        Level = LogLevel.Error,
        Message = "CUDA compilation failed for kernel {KernelName}: {ErrorMessage}")]
    public static partial void CudaCompilationFailed(this ILogger logger, string kernelName, string errorMessage);

    [LoggerMessage(
        EventId = 21003,
        Level = LogLevel.Warning,
        Message = "NVRTC compilation warning for {KernelName}: {Warning}")]
    public static partial void NvrtcWarning(this ILogger logger, string kernelName, string warning);

    // CUDA Execution Messages
    [LoggerMessage(
        EventId = 22000,
        Level = LogLevel.Debug,
        Message = "Launching CUDA kernel {KernelName} with grid({GridX},{GridY},{GridZ}) and block({BlockX},{BlockY},{BlockZ})")]
    public static partial void CudaKernelLaunch(this ILogger logger, string kernelName, int gridX, int gridY, int gridZ, int blockX, int blockY, int blockZ);

    [LoggerMessage(
        EventId = 22001,
        Level = LogLevel.Debug,
        Message = "CUDA kernel {KernelName} execution completed in {ElapsedMs}ms")]
    public static partial void CudaKernelCompleted(this ILogger logger, string kernelName, double elapsedMs);

    [LoggerMessage(
        EventId = 22002,
        Level = LogLevel.Error,
        Message = "CUDA kernel {KernelName} execution failed: {CudaError}")]
    public static partial void CudaKernelFailed(this ILogger logger, string kernelName, string cudaError);

    // CUDA Memory Messages
    [LoggerMessage(
        EventId = 23000,
        Level = LogLevel.Debug,
        Message = "Allocated {SizeBytes} bytes of CUDA memory on device {DeviceId}")]
    public static partial void CudaMemoryAllocated(this ILogger logger, long sizeBytes, int deviceId);

    [LoggerMessage(
        EventId = 23001,
        Level = LogLevel.Debug,
        Message = "Freed {SizeBytes} bytes of CUDA memory on device {DeviceId}")]
    public static partial void CudaMemoryFreed(this ILogger logger, long sizeBytes, int deviceId);

    [LoggerMessage(
        EventId = 23002,
        Level = LogLevel.Debug,
        Message = "CUDA memory transfer: {Direction} {SizeBytes} bytes in {ElapsedMs}ms ({BandwidthGBps:F2} GB/s)")]
    public static partial void CudaMemoryTransfer(this ILogger logger, string direction, long sizeBytes, double elapsedMs, double bandwidthGBps);

    [LoggerMessage(
        EventId = 23003,
        Level = LogLevel.Warning,
        Message = "CUDA memory allocation failed: {ErrorMessage}. Available: {AvailableBytes} bytes")]
    public static partial void CudaMemoryAllocationFailed(this ILogger logger, string errorMessage, long availableBytes);

    [LoggerMessage(
        EventId = 23004,
        Level = LogLevel.Information,
        Message = "Enabled peer-to-peer access between devices {Device1} and {Device2}")]
    public static partial void CudaP2PEnabled(this ILogger logger, int device1, int device2);

    // CUDA Graph Messages
    [LoggerMessage(
        EventId = 24000,
        Level = LogLevel.Information,
        Message = "Created CUDA graph {GraphId} with {NodeCount} nodes")]
    public static partial void CudaGraphCreated(this ILogger logger, string graphId, int nodeCount);

    [LoggerMessage(
        EventId = 24001,
        Level = LogLevel.Debug,
        Message = "Instantiated CUDA graph {GraphId} as executable")]
    public static partial void CudaGraphInstantiated(this ILogger logger, string graphId);

    [LoggerMessage(
        EventId = 24002,
        Level = LogLevel.Debug,
        Message = "Launched CUDA graph {GraphId} on stream {StreamId}")]
    public static partial void CudaGraphLaunched(this ILogger logger, string graphId, long streamId);

    // Tensor Core Messages
    [LoggerMessage(
        EventId = 25000,
        Level = LogLevel.Information,
        Message = "Tensor cores enabled for operation {Operation} with {Precision} precision")]
    public static partial void TensorCoresEnabled(this ILogger logger, string operation, string precision);

    [LoggerMessage(
        EventId = 25001,
        Level = LogLevel.Debug,
        Message = "Tensor core GEMM operation: {M}x{N}x{K} completed in {ElapsedMs}ms ({TFlops:F2} TFLOPS)")]
    public static partial void TensorCoreGemm(this ILogger logger, int m, int n, int k, double elapsedMs, double tFlops);

    // CUDA Profiling Messages
    [LoggerMessage(
        EventId = 26000,
        Level = LogLevel.Debug,
        Message = "CUPTI profiling started for kernel {KernelName}")]
    public static partial void CuptiProfilingStarted(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 26001,
        Level = LogLevel.Information,
        Message = "Kernel {KernelName} occupancy: {Occupancy:F1}%, Register usage: {Registers}, Shared memory: {SharedMemBytes} bytes")]
    public static partial void CudaOccupancyMetrics(this ILogger logger, string kernelName, double occupancy, int registers, int sharedMemBytes);

    [LoggerMessage(
        EventId = 26002,
        Level = LogLevel.Warning,
        Message = "Low occupancy detected for kernel {KernelName}: {Occupancy:F1}% (target: {TargetOccupancy:F1}%)")]
    public static partial void LowOccupancyWarning(this ILogger logger, string kernelName, double occupancy, double targetOccupancy);

    // NVML Monitoring Messages
    [LoggerMessage(
        EventId = 27000,
        Level = LogLevel.Information,
        Message = "NVML initialized. Monitoring {DeviceCount} GPUs")]
    public static partial void NvmlInitialized(this ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 27001,
        Level = LogLevel.Debug,
        Message = "GPU {DeviceId} metrics: Temp: {Temperature}°C, Power: {PowerWatts}W, Util: {Utilization}%")]
    public static partial void NvmlMetrics(this ILogger logger, int deviceId, int temperature, double powerWatts, int utilization);

    [LoggerMessage(
        EventId = 27002,
        Level = LogLevel.Warning,
        Message = "GPU {DeviceId} temperature warning: {Temperature}°C (threshold: {Threshold}°C)")]
    public static partial void NvmlTemperatureWarning(this ILogger logger, int deviceId, int temperature, int threshold);

    // Error Recovery Messages
    [LoggerMessage(
        EventId = 28000,
        Level = LogLevel.Warning,
        Message = "CUDA error detected: {ErrorCode} - {ErrorMessage}. Attempting recovery...")]
    public static partial void CudaErrorRecovery(this ILogger logger, int errorCode, string errorMessage);

    [LoggerMessage(
        EventId = 28001,
        Level = LogLevel.Information,
        Message = "CUDA context reset successful for device {DeviceId}")]
    public static partial void CudaContextReset(this ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 28002,
        Level = LogLevel.Error,
        Message = "CUDA fatal error: {ErrorMessage}. Device {DeviceId} is in unrecoverable state")]
    public static partial void CudaFatalError(this ILogger logger, string errorMessage, int deviceId);

    // Cache Messages
    [LoggerMessage(
        EventId = 29000,
        Level = LogLevel.Debug,
        Message = "CUDA kernel cache hit for {KernelHash}")]
    public static partial void CudaCacheHit(this ILogger logger, string kernelHash);

    [LoggerMessage(
        EventId = 29001,
        Level = LogLevel.Debug,
        Message = "CUDA kernel cache miss for {KernelHash}")]
    public static partial void CudaCacheMiss(this ILogger logger, string kernelHash);

    // General Messages
    [LoggerMessage(
        EventId = 30000,
        Level = LogLevel.Error,
        Message = "Unexpected CUDA error in {Component}: {ErrorMessage}")]
    public static partial void UnexpectedCudaError(this ILogger logger, string component, string errorMessage, Exception? exception = null);

    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 31000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 31001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 31002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 31003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 31004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, Exception ex, string message);

    [LoggerMessage(EventId = 31005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 31006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);
}
