// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Factory;

/// <summary>
/// LoggerMessage delegates for CudaAcceleratorFactory.
/// Event IDs: 5350-5399
/// </summary>
public sealed partial class CudaAcceleratorFactory
{
    [LoggerMessage(
        EventId = 5350,
        Level = LogLevel.Information,
        Message = "Production CUDA Accelerator Factory initialized")]
    private partial void LogFactoryInitialized();

    [LoggerMessage(
        EventId = 5351,
        Level = LogLevel.Information,
        Message = "Creating production CUDA accelerator for device {DeviceId} with config: {@Config}")]
    private partial void LogCreatingAccelerator(int deviceId, ProductionConfiguration config);

    [LoggerMessage(
        EventId = 5352,
        Level = LogLevel.Information,
        Message = "Production accelerator created for device {DeviceId} with {FeatureCount} features enabled")]
    private partial void LogAcceleratorCreated(int deviceId, int featureCount);

    [LoggerMessage(
        EventId = 5353,
        Level = LogLevel.Warning,
        Message = "CUDA runtime API call failed with error: {ErrorCode}")]
    private partial void LogCudaApiError(string errorCode);

    [LoggerMessage(
        EventId = 5354,
        Level = LogLevel.Warning,
        Message = "No CUDA devices found")]
    private partial void LogNoDevicesFound();

    [LoggerMessage(
        EventId = 5355,
        Level = LogLevel.Information,
        Message = "CUDA driver version: {DriverVersion}")]
    private partial void LogDriverVersion(string driverVersion);

    [LoggerMessage(
        EventId = 5356,
        Level = LogLevel.Information,
        Message = "CUDA runtime version: {RuntimeVersion}")]
    private partial void LogRuntimeVersion(string runtimeVersion);

    [LoggerMessage(
        EventId = 5357,
        Level = LogLevel.Information,
        Message = "Found {DeviceCount} CUDA device(s)")]
    private partial void LogDeviceCountFound(int deviceCount);

    [LoggerMessage(
        EventId = 5358,
        Level = LogLevel.Error,
        Message = "CUDA runtime library not found")]
    private partial void LogRuntimeLibraryNotFound(Exception ex);

    [LoggerMessage(
        EventId = 5359,
        Level = LogLevel.Error,
        Message = "Error checking CUDA availability")]
    private partial void LogAvailabilityCheckError(Exception ex);

    [LoggerMessage(
        EventId = 5360,
        Level = LogLevel.Warning,
        Message = "CUDA not available, no accelerators created")]
    private partial void LogCudaNotAvailable();

    [LoggerMessage(
        EventId = 5361,
        Level = LogLevel.Information,
        Message = "Enumerating {DeviceCount} CUDA devices")]
    private partial void LogEnumeratingDevices(int deviceCount);

    [LoggerMessage(
        EventId = 5362,
        Level = LogLevel.Information,
        Message = "Created production accelerator for {DeviceName} (CC {ComputeCapability})")]
    private partial void LogAcceleratorCreatedForDevice(string deviceName, string computeCapability);

    [LoggerMessage(
        EventId = 5363,
        Level = LogLevel.Error,
        Message = "Failed to create accelerator for device {DeviceId}: {DeviceName}")]
    private partial void LogCreateAcceleratorFailed(Exception ex, int deviceId, string deviceName);

    [LoggerMessage(
        EventId = 5364,
        Level = LogLevel.Warning,
        Message = "CUDA not available, cannot create default accelerator")]
    private partial void LogCannotCreateDefault();

    [LoggerMessage(
        EventId = 5365,
        Level = LogLevel.Information,
        Message = "Selected device {DeviceId} as default")]
    private partial void LogDefaultDeviceSelected(int deviceId);

    [LoggerMessage(
        EventId = 5366,
        Level = LogLevel.Error,
        Message = "Failed to create default accelerator")]
    private partial void LogCreateDefaultFailed(Exception ex);

    [LoggerMessage(
        EventId = 5367,
        Level = LogLevel.Information,
        Message = "Disposing Production CUDA Factory")]
    private partial void LogDisposingFactory();

    [LoggerMessage(
        EventId = 5368,
        Level = LogLevel.Error,
        Message = "Error disposing accelerator for device {DeviceId}")]
    private partial void LogDisposeAcceleratorError(Exception ex, int deviceId);
}
