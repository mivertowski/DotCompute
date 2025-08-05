// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal;

/// <summary>
/// Logger message definitions for MetalBackend
/// </summary>
public partial class MetalBackend
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "Metal device {DeviceIndex} requires macOS 11.0 or higher. Current: {OSVersion}")]
    private static partial void LogMacOSVersionWarning(ILogger logger, int deviceIndex, Version osVersion);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Error validating Metal device {DeviceIndex}")]
    private static partial void LogDeviceValidationError(ILogger logger, Exception ex, int deviceIndex);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Metal device {DeviceIndex} does not support compute operations")]
    private static partial void LogComputeSupportWarning(ILogger logger, int deviceIndex);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Metal device {DeviceIndex} does not meet minimum GPU family requirements. Families: {Families}")]
    private static partial void LogGPUFamilyWarning(ILogger logger, int deviceIndex, string families);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Metal device {DeviceIndex} has insufficient memory capacity: {MaxBuffer} bytes")]
    private static partial void LogInsufficientMemoryWarning(ILogger logger, int deviceIndex, ulong maxBuffer);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "Error validating compute support for Metal device {DeviceIndex}")]
    private static partial void LogComputeValidationError(ILogger logger, Exception ex, int deviceIndex);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Warning,
        Message = "Metal device {DeviceIndex} failed shader compilation test: {ErrorDetails}")]
    private static partial void LogShaderCompilationWarning(ILogger logger, int deviceIndex, string errorDetails);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Warning,
        Message = "Error testing shader compilation for Metal device {DeviceIndex}")]
    private static partial void LogShaderCompilationError(ILogger logger, Exception ex, int deviceIndex);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Discovering Metal accelerators...")]
    private static partial void LogDiscoveringAccelerators(ILogger logger);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "No Metal devices found on this system")]
    private static partial void LogNoDevicesFound(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Detected {DeviceCount} Metal device(s)")]
    private static partial void LogDeviceCount(ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Discovered {AcceleratorCount} valid Metal accelerator(s)")]
    private static partial void LogAcceleratorCount(ILogger logger, int acceleratorCount);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Metal Device: {Name} (ID: {Id})")]
    private static partial void LogDeviceInfo(ILogger logger, string name, string id);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "  Device Type: {DeviceType}")]
    private static partial void LogDeviceType(ILogger logger, string deviceType);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "  Compute Capability: {ComputeCapability}")]
    private static partial void LogComputeCapability(ILogger logger, string computeCapability);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "  Total Memory: {TotalMemory:N0} bytes ({MemoryGB:F1} GB)")]
    private static partial void LogTotalMemory(ILogger logger, long totalMemory, double memoryGB);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "  Compute Units: {ComputeUnits}")]
    private static partial void LogComputeUnits(ILogger logger, int computeUnits);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "  Max Threadgroup Size: {MaxThreadgroup}")]
    private static partial void LogMaxThreadgroupSize(ILogger logger, object maxThreadgroup);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Information,
        Message = "  Max Threads per Threadgroup: {MaxThreads}")]
    private static partial void LogMaxThreadsPerThreadgroup(ILogger logger, object maxThreads);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Information,
        Message = "  Unified Memory: Supported")]
    private static partial void LogUnifiedMemorySupported(ILogger logger);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Information,
        Message = "  GPU Families: {Families}")]
    private static partial void LogGPUFamilies(ILogger logger, object families);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Information,
        Message = "  Location: {Location}")]
    private static partial void LogLocation(ILogger logger, object location);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Warning,
        Message = "Failed to initialize Metal device {DeviceIndex}")]
    private static partial void LogDeviceInitializationWarning(ILogger logger, Exception ex, int deviceIndex);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "Failed to discover Metal accelerators")]
    private static partial void LogDiscoveryError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Creating Metal accelerator for device: {DeviceName}")]
    private static partial void LogCreatingAccelerator(ILogger logger, string deviceName);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Failed to create Metal accelerator for device {DeviceIndex}")]
    private static partial void LogAcceleratorCreationError(ILogger logger, Exception ex, int deviceIndex);
}