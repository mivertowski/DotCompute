// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

public partial class CudaBackend
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "DotCompute.Backends.CUDA is not available or did not load correctly. CUDA features will be unavailable")]
    private static partial void LogCudaBackendNotAvailable(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Initializing CUDA backend...")]
    private static partial void LogInitializingCudaBackend(ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Failed to detect CUDA devices")]
    private static partial void LogFailedToDetectCudaDevices(ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Found {DeviceCount} CUDA device(s)")]
    private static partial void LogFoundCudaDevices(ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "CUDA backend initialized successfully")]
    private static partial void LogCudaBackendInitialized(ILogger logger);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "Unable to set CUDA device {DeviceId}")]
    private static partial void LogUnableToSetCudaDevice(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Set active CUDA device to {DeviceId}")]
    private static partial void LogSetActiveCudaDevice(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Error,
        Message = "Failed to set CUDA device {DeviceId}")]
    private static partial void LogFailedToSetCudaDevice(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Warning,
        Message = "Failed to allocate CUDA buffer")]
    private static partial void LogFailedToAllocateCudaBuffer(ILogger logger);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Failed to create CUDA stream")]
    private static partial void LogFailedToCreateCudaStream(ILogger logger);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Warning,
        Message = "Failed to register memory for CUDA")]
    private static partial void LogFailedToRegisterMemory(ILogger logger);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Warning,
        Message = "Failed to copy data to CUDA device")]
    private static partial void LogFailedToCopyToDevice(ILogger logger);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Warning,
        Message = "Failed to copy data from CUDA device")]
    private static partial void LogFailedToCopyFromDevice(ILogger logger);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Warning,
        Message = "Failed to execute kernel on CUDA")]
    private static partial void LogFailedToExecuteKernel(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2015,
        Level = LogLevel.Error,
        Message = "Not implemented: ExecuteKernelAsync")]
    private static partial void LogExecuteKernelNotImplemented(ILogger logger);

    [LoggerMessage(
        EventId = 2016,
        Level = LogLevel.Information,
        Message = "Disposing CUDA context...")]
    private static partial void LogDisposingCudaContext(ILogger logger);

    [LoggerMessage(
        EventId = 2017,
        Level = LogLevel.Error,
        Message = "Failed to dispose CUDA context")]
    private static partial void LogFailedToDisposeCudaContext(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2018,
        Level = LogLevel.Information,
        Message = "Name: {Name}")]
    private static partial void LogDeviceName(ILogger logger, string name);

    [LoggerMessage(
        EventId = 2019,
        Level = LogLevel.Information,
        Message = "Compute Capability: {Major}.{Minor}")]
    private static partial void LogComputeCapability(ILogger logger, int major, int minor);

    [LoggerMessage(
        EventId = 2020,
        Level = LogLevel.Information,
        Message = "Total Memory: {TotalMemory} GB")]
    private static partial void LogTotalMemory(ILogger logger, double totalMemory);

    [LoggerMessage(
        EventId = 2021,
        Level = LogLevel.Information,
        Message = "Multiprocessors: {MultiprocessorCount}")]
    private static partial void LogMultiprocessorCount(ILogger logger, int multiprocessorCount);

    [LoggerMessage(
        EventId = 2022,
        Level = LogLevel.Information,
        Message = "Clock Rate: {ClockRate} MHz")]
    private static partial void LogClockRate(ILogger logger, double clockRate);

    [LoggerMessage(
        EventId = 2023,
        Level = LogLevel.Information,
        Message = "Memory Clock Rate: {MemoryClockRate} MHz")]
    private static partial void LogMemoryClockRate(ILogger logger, double memoryClockRate);

    [LoggerMessage(
        EventId = 2024,
        Level = LogLevel.Information,
        Message = "Memory Bus Width: {MemoryBusWidth} bits")]
    private static partial void LogMemoryBusWidth(ILogger logger, int memoryBusWidth);

    [LoggerMessage(
        EventId = 2025,
        Level = LogLevel.Information,
        Message = "Unified Addressing: {UnifiedAddressing}")]
    private static partial void LogUnifiedAddressing(ILogger logger, bool unifiedAddressing);

    [LoggerMessage(
        EventId = 2026,
        Level = LogLevel.Information,
        Message = "Concurrent Kernels: {ConcurrentKernels}")]
    private static partial void LogConcurrentKernels(ILogger logger, bool concurrentKernels);

    [LoggerMessage(
        EventId = 2027,
        Level = LogLevel.Information,
        Message = "ECC Enabled: {EccEnabled}")]
    private static partial void LogEccEnabled(ILogger logger, bool eccEnabled);
}