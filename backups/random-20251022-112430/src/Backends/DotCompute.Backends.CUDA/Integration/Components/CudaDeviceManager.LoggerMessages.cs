// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// LoggerMessage delegates for CudaDeviceManager.
/// Event IDs: 5300-5349
/// </summary>
public sealed partial class CudaDeviceManager
{
    [LoggerMessage(
        EventId = 5300,
        Level = LogLevel.Warning,
        Message = "Failed to query CUDA device count")]
    private partial void LogDeviceCountQueryFailed(Exception ex);

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Warning,
        Message = "Failed to query memory for device {DeviceId}")]
    private partial void LogMemoryQueryFailed(Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5302,
        Level = LogLevel.Warning,
        Message = "Failed to query CUDA driver version")]
    private partial void LogDriverVersionQueryFailed(Exception ex);

    [LoggerMessage(
        EventId = 5303,
        Level = LogLevel.Debug,
        Message = "Initializing CUDA device cache for {DeviceCount} devices")]
    private partial void LogInitializingDeviceCache(int deviceCount);

    [LoggerMessage(
        EventId = 5304,
        Level = LogLevel.Debug,
        Message = "Cached device info for device {DeviceId}: {DeviceName}")]
    private partial void LogDeviceInfoCached(int deviceId, string deviceName);

    [LoggerMessage(
        EventId = 5305,
        Level = LogLevel.Warning,
        Message = "Failed to cache device info for device {DeviceId}")]
    private partial void LogCacheDeviceInfoFailed(Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5306,
        Level = LogLevel.Error,
        Message = "Failed to initialize CUDA device cache")]
    private partial void LogInitializeCacheFailed(Exception ex);

    [LoggerMessage(
        EventId = 5307,
        Level = LogLevel.Error,
        Message = "Failed to query device properties for device {DeviceId}")]
    private partial void LogQueryDevicePropertiesFailed(Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5308,
        Level = LogLevel.Warning,
        Message = "Error disposing accelerator wrapper")]
    private partial void LogDisposeAcceleratorWrapperError(Exception ex);

    [LoggerMessage(
        EventId = 5309,
        Level = LogLevel.Debug,
        Message = "CUDA device manager disposed")]
    private partial void LogDeviceManagerDisposed();
}
