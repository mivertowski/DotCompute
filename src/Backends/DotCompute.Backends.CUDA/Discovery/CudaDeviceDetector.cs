// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Ports;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Discovery;

/// <summary>
/// Provides device discovery capabilities for CUDA-capable GPUs.
/// Implements the hexagonal architecture pattern for device discovery port.
/// </summary>
public sealed partial class CudaDeviceDetector : IDeviceDiscoveryPort
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6900,
        Level = LogLevel.Information,
        Message = "Detecting CUDA devices, found {DeviceCount} devices")]
    private static partial void LogDeviceCount(ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 6901,
        Level = LogLevel.Warning,
        Message = "Failed to get CUDA device count: {Error}")]
    private static partial void LogFailedToGetDeviceCount(ILogger logger, string error);

    [LoggerMessage(
        EventId = 6902,
        Level = LogLevel.Warning,
        Message = "CUDA device {DeviceId} not found (total devices: {DeviceCount})")]
    private static partial void LogDeviceNotFound(ILogger logger, int deviceId, int deviceCount);

    [LoggerMessage(
        EventId = 6903,
        Level = LogLevel.Error,
        Message = "Failed to detect CUDA device {DeviceId}")]
    private static partial void LogFailedToDetectDevice(ILogger logger, Exception exception, int deviceId);

    [LoggerMessage(
        EventId = 6904,
        Level = LogLevel.Error,
        Message = "Failed to initialize CUDA device {DeviceId}")]
    private static partial void LogFailedToInitializeDevice(ILogger logger, Exception exception, int deviceId);

    #endregion

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaDeviceDetector"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public CudaDeviceDetector(ILogger<CudaDeviceDetector>? logger = null)
    {
        _logger = logger ?? NullLogger<CudaDeviceDetector>.Instance;
    }

    /// <inheritdoc />
    public BackendType Backend => BackendType.CUDA;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var devices = new List<DeviceInfo>();

            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success)
            {
                LogFailedToGetDeviceCount(_logger, CudaRuntime.GetErrorString(result));
                return devices;
            }

            LogDeviceCount(_logger, deviceCount);

            for (var i = 0; i < deviceCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var cudaDevice = new CudaDevice(i, _logger);
                    devices.Add(MapToDeviceInfo(cudaDevice));
                }
                catch (Exception ex)
                {
                    LogFailedToInitializeDevice(_logger, ex, i);
                }
            }

            return devices;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DeviceCapabilities> GetCapabilitiesAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!int.TryParse(deviceId, out var id))
            {
                throw new ArgumentException($"Invalid device ID format: {deviceId}", nameof(deviceId));
            }

            var device = new CudaDevice(id, _logger);
            return MapToDeviceCapabilities(device);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Detects a specific CUDA device by ID.
    /// </summary>
    /// <param name="deviceId">The device ID to detect.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A CudaDevice instance if successful, null otherwise.</returns>
    public static CudaDevice? Detect(int deviceId, ILogger<CudaDevice>? logger = null)
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success || deviceId >= deviceCount)
            {
                logger?.LogWarning("CUDA device {DeviceId} not found (total devices: {DeviceCount})",
                    deviceId, deviceCount);
                return null;
            }

            return new CudaDevice(deviceId, logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to detect CUDA device {DeviceId}", deviceId);
            return null;
        }
    }

    /// <summary>
    /// Detects all available CUDA devices on the system.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>An enumerable of detected CudaDevice instances.</returns>
    public static IEnumerable<CudaDevice> DetectAll(ILogger? logger = null)
    {
        var devices = new List<CudaDevice>();

        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success)
            {
                logger?.LogWarning("Failed to get CUDA device count: {Error}",
                    CudaRuntime.GetErrorString(result));
                return devices;
            }

            logger?.LogInformation("Detecting {DeviceCount} CUDA devices", deviceCount);

            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var device = new CudaDevice(i, logger);
                    devices.Add(device);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to initialize CUDA device {DeviceId}", i);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to detect CUDA devices");
        }

        return devices;
    }

    /// <summary>
    /// Gets the number of available CUDA devices.
    /// </summary>
    /// <returns>The device count, or 0 if detection fails.</returns>
    public static int GetDeviceCount()
    {
        var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
        return result == CudaError.Success ? deviceCount : 0;
    }

    /// <summary>
    /// Checks if any CUDA devices are available.
    /// </summary>
    /// <returns>True if at least one CUDA device is available.</returns>
    public static bool IsAvailable() => GetDeviceCount() > 0;

    private static DeviceInfo MapToDeviceInfo(CudaDevice device)
    {
        return new DeviceInfo(
            Id: device.DeviceId.ToString(),
            Name: device.Name,
            Backend: BackendType.CUDA,
            TotalMemoryBytes: (long)device.GlobalMemorySize,
            ComputeUnits: device.StreamingMultiprocessorCount,
            IsAvailable: true,
            Metadata: new Dictionary<string, object>
            {
                ["ComputeCapabilityMajor"] = device.ComputeCapabilityMajor,
                ["ComputeCapabilityMinor"] = device.ComputeCapabilityMinor,
                ["Architecture"] = device.ArchitectureGeneration,
                ["PciBusId"] = device.PciBusId,
                ["PciDeviceId"] = device.PciDeviceId,
                ["IsRTX2000Ada"] = device.IsRTX2000Ada
            });
    }

    private static DeviceCapabilities MapToDeviceCapabilities(CudaDevice device)
    {
        var features = new HashSet<string>();

        // Add feature flags based on compute capability
        if (device.ComputeCapabilityMajor >= 7)
            features.Add("TensorCores");

        if (device.ComputeCapabilityMajor >= 8)
        {
            features.Add("BFloat16");
            features.Add("AsyncCopy");
            features.Add("L2CacheResidencyControl");
        }

        if (device.ComputeCapabilityMajor >= 8 && device.ComputeCapabilityMinor >= 9)
            features.Add("Int4TensorCores");

        if (device.ComputeCapabilityMajor >= 9)
            features.Add("FP8TensorCores");

        if (device.SupportsManagedMemory)
            features.Add("ManagedMemory");

        if (device.SupportsConcurrentKernels)
            features.Add("ConcurrentKernels");

        if (device.SupportsUnifiedAddressing)
            features.Add("UnifiedAddressing");

        return new DeviceCapabilities(
            DeviceId: device.DeviceId.ToString(),
            MaxThreadsPerBlock: device.MaxThreadsPerBlock,
            MaxThreadsPerUnit: device.MaxThreadsPerMultiprocessor,
            WarpSize: device.WarpSize,
            MaxSharedMemoryPerBlock: (long)device.SharedMemoryPerBlock,
            MaxConstantMemory: (long)device.ConstantMemorySize,
            SupportedFeatures: features);
    }
}
