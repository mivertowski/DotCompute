// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA
{

    public partial class CudaBackend
    {
        [LoggerMessage(
            EventId = 2001,
            Level = LogLevel.Warning,
            Message = "CUDA is not available on this platform")]
        private static partial void LogCudaNotAvailable(ILogger logger);

        [LoggerMessage(
            EventId = 2002,
            Level = LogLevel.Information,
            Message = "Discovering CUDA devices...")]
        private static partial void LogDiscoveringCudaDevices(ILogger logger);

        [LoggerMessage(
            EventId = 2003,
            Level = LogLevel.Error,
            Message = "Failed to get CUDA device count: {Error}")]
        private static partial void LogFailedToGetDeviceCount(ILogger logger, string error);

        [LoggerMessage(
            EventId = 2004,
            Level = LogLevel.Information,
            Message = "No CUDA devices found")]
        private static partial void LogNoCudaDevicesFound(ILogger logger);

        [LoggerMessage(
            EventId = 2005,
            Level = LogLevel.Information,
            Message = "Found {DeviceCount} CUDA device(s)")]
        private static partial void LogFoundCudaDevices(ILogger logger, int deviceCount);

        [LoggerMessage(
            EventId = 2006,
            Level = LogLevel.Information,
            Message = "CUDA device discovery completed - {AcceleratorCount} accelerators available")]
        private static partial void LogDeviceDiscoveryCompleted(ILogger logger, int acceleratorCount);

        [LoggerMessage(
            EventId = 2007,
            Level = LogLevel.Warning,
            Message = "Failed to initialize CUDA device {DeviceId}")]
        private static partial void LogFailedToInitializeDevice(ILogger logger, Exception ex, int deviceId);

        [LoggerMessage(
            EventId = 2008,
            Level = LogLevel.Error,
            Message = "Failed to discover CUDA accelerators")]
        private static partial void LogFailedToDiscoverAccelerators(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 2009,
            Level = LogLevel.Warning,
            Message = "Could not determine CUDA runtime version for device {DeviceId}")]
        private static partial void LogCouldNotDetermineRuntimeVersion(ILogger logger, int deviceId);

        [LoggerMessage(
            EventId = 2010,
            Level = LogLevel.Warning,
            Message = "Could not determine CUDA driver version for device {DeviceId}")]
        private static partial void LogCouldNotDetermineDriverVersion(ILogger logger, int deviceId);

        [LoggerMessage(
            EventId = 2011,
            Level = LogLevel.Warning,
            Message = "CUDA device {DeviceId} requires CUDA 11.0 or higher. Runtime: {Runtime}, Driver: {Driver}")]
        private static partial void LogCudaVersionTooLow(ILogger logger, int deviceId, string runtime, string driver);

        [LoggerMessage(
            EventId = 2012,
            Level = LogLevel.Warning,
            Message = "Cannot access CUDA device {DeviceId}: {Error}")]
        private static partial void LogCannotAccessDevice(ILogger logger, int deviceId, string error);

        [LoggerMessage(
            EventId = 2013,
            Level = LogLevel.Warning,
            Message = "CUDA device {DeviceId} failed synchronization test: {Error}")]
        private static partial void LogDeviceFailedSynchronization(ILogger logger, int deviceId, string error);

        [LoggerMessage(
            EventId = 2014,
            Level = LogLevel.Warning,
            Message = "Error validating CUDA device {DeviceId} accessibility")]
        private static partial void LogErrorValidatingDevice(ILogger logger, Exception ex, int deviceId);

        [LoggerMessage(
            EventId = 2015,
            Level = LogLevel.Error,
            Message = "Failed to get properties for CUDA device {DeviceId}: {Error}")]
        private static partial void LogFailedToGetDeviceProperties(ILogger logger, int deviceId, string error);

        [LoggerMessage(
            EventId = 2016,
            Level = LogLevel.Information,
            Message = "Skipping CUDA device {DeviceId} ({Name}) - compute capability {Major}.{Minor} is below minimum 5.0")]
        private static partial void LogSkippingDeviceLowComputeCapability(ILogger logger, int deviceId, string name, int major, int minor);

        [LoggerMessage(
            EventId = 2017,
            Level = LogLevel.Error,
            Message = "Failed to create accelerator for CUDA device {DeviceId}")]
        private static partial void LogFailedToCreateAccelerator(ILogger logger, Exception ex, int deviceId);

        [LoggerMessage(
            EventId = 2018,
            Level = LogLevel.Information,
            Message = "CUDA Device: {Name} (ID: {Id})")]
        private static partial void LogCudaDevice(ILogger logger, string name, string id);

        [LoggerMessage(
            EventId = 2019,
            Level = LogLevel.Information,
            Message = "  Compute Capability: {ComputeCapability}")]
        private static partial void LogComputeCapability(ILogger logger, Version? computeCapability);

        [LoggerMessage(
            EventId = 2020,
            Level = LogLevel.Information,
            Message = "  Total Memory: {TotalMemory:N0} bytes ({MemoryGB:F1} GB)")]
        private static partial void LogTotalMemory(ILogger logger, long totalMemory, double memoryGB);

        [LoggerMessage(
            EventId = 2021,
            Level = LogLevel.Information,
            Message = "  Multiprocessors: {ComputeUnits}")]
        private static partial void LogMultiprocessors(ILogger logger, int computeUnits);

        [LoggerMessage(
            EventId = 2022,
            Level = LogLevel.Information,
            Message = "  Clock Rate: {ClockRate} MHz")]
        private static partial void LogClockRate(ILogger logger, long clockRate);

        [LoggerMessage(
            EventId = 2023,
            Level = LogLevel.Information,
            Message = "  Shared Memory per Block: {SharedMem:N0} bytes")]
        private static partial void LogSharedMemoryPerBlock(ILogger logger, object sharedMem);

        [LoggerMessage(
            EventId = 2024,
            Level = LogLevel.Information,
            Message = "  Max Threads per Block: {MaxThreads}")]
        private static partial void LogMaxThreadsPerBlock(ILogger logger, object maxThreads);

        [LoggerMessage(
            EventId = 2025,
            Level = LogLevel.Information,
            Message = "  Warp Size: {WarpSize}")]
        private static partial void LogWarpSize(ILogger logger, object warpSize);

        [LoggerMessage(
            EventId = 2026,
            Level = LogLevel.Information,
            Message = "  ECC Memory: Enabled")]
        private static partial void LogEccMemoryEnabled(ILogger logger);

        [LoggerMessage(
            EventId = 2027,
            Level = LogLevel.Information,
            Message = "  Unified Virtual Addressing: Supported")]
        private static partial void LogUnifiedAddressingSupported(ILogger logger);
    }
}
