// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Factory;

/// <summary>
/// Factory for creating OpenCL accelerator instances.
/// Handles device selection, validation, and accelerator configuration.
/// </summary>
public sealed class OpenCLAcceleratorFactory
{
    private readonly ILogger<OpenCLAcceleratorFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenCLDeviceManager _deviceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAcceleratorFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    public OpenCLAcceleratorFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<OpenCLAcceleratorFactory>();
        _deviceManager = new OpenCLDeviceManager(_loggerFactory.CreateLogger<OpenCLDeviceManager>());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAcceleratorFactory"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLAcceleratorFactory(ILogger<OpenCLAcceleratorFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Create a simple logger factory from the provided logger using extension method
        _loggerFactory = CreateLoggerFactory(logger);
        _deviceManager = new OpenCLDeviceManager(_loggerFactory.CreateLogger<OpenCLDeviceManager>());
    }

    /// <summary>
    /// Creates the best available OpenCL accelerator.
    /// </summary>
    /// <returns>A configured OpenCL accelerator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no suitable OpenCL devices are found.</exception>
    public OpenCLAccelerator CreateBest()
    {
        _logger.LogDebug("Creating best available OpenCL accelerator");

        var bestDevice = _deviceManager.GetBestDevice();
        if (bestDevice == null)
        {
            throw new InvalidOperationException("No suitable OpenCL devices found. Ensure OpenCL drivers are installed.");
        }

        _logger.LogInformation("Creating OpenCL accelerator for device: {DeviceName} ({DeviceType})",
            bestDevice.Name, bestDevice.Type);

        return new OpenCLAccelerator(bestDevice, _loggerFactory.CreateLogger<OpenCLAccelerator>());
    }

    /// <summary>
    /// Creates an OpenCL accelerator for a specific device type.
    /// </summary>
    /// <param name="deviceType">The type of device to use.</param>
    /// <param name="deviceIndex">The device index within the type (default: 0).</param>
    /// <returns>A configured OpenCL accelerator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no device of the specified type is found.</exception>
    public OpenCLAccelerator CreateForDeviceType(DeviceType deviceType, int deviceIndex = 0)
    {
        _logger.LogDebug("Creating OpenCL accelerator for device type: {DeviceType}, index: {Index}",
            deviceType, deviceIndex);

        var devices = _deviceManager.GetDevices(deviceType).ToList();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException($"No OpenCL devices found for type: {deviceType}");
        }

        if (deviceIndex >= devices.Count)
        {
            throw new InvalidOperationException(
                $"Device index {deviceIndex} is out of range. Found {devices.Count} devices of type {deviceType}");
        }

        var selectedDevice = devices[deviceIndex];
        _logger.LogInformation("Creating OpenCL accelerator for device: {DeviceName} ({DeviceType})",
            selectedDevice.Name, selectedDevice.Type);

        return new OpenCLAccelerator(selectedDevice, _loggerFactory.CreateLogger<OpenCLAccelerator>());
    }

    /// <summary>
    /// Creates an OpenCL accelerator for a device from a specific vendor.
    /// </summary>
    /// <param name="vendorName">The vendor name (case-insensitive partial match).</param>
    /// <param name="deviceIndex">The device index within the vendor (default: 0).</param>
    /// <returns>A configured OpenCL accelerator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no device from the specified vendor is found.</exception>
    public OpenCLAccelerator CreateForVendor(string vendorName, int deviceIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(vendorName))
            throw new ArgumentException("Vendor name cannot be null or empty", nameof(vendorName));

        _logger.LogDebug("Creating OpenCL accelerator for vendor: {Vendor}, index: {Index}",
            vendorName, deviceIndex);

        var devices = _deviceManager.GetDevicesByVendor(vendorName).ToList();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException($"No OpenCL devices found for vendor: {vendorName}");
        }

        if (deviceIndex >= devices.Count)
        {
            throw new InvalidOperationException(
                $"Device index {deviceIndex} is out of range. Found {devices.Count} devices for vendor {vendorName}");
        }

        var selectedDevice = devices[deviceIndex];
        _logger.LogInformation("Creating OpenCL accelerator for device: {DeviceName} ({Vendor})",
            selectedDevice.Name, selectedDevice.Vendor);

        return new OpenCLAccelerator(selectedDevice, _loggerFactory.CreateLogger<OpenCLAccelerator>());
    }

    /// <summary>
    /// Creates an OpenCL accelerator for a specific device.
    /// </summary>
    /// <param name="deviceId">The OpenCL device ID.</param>
    /// <returns>A configured OpenCL accelerator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the device is not found.</exception>
    public OpenCLAccelerator CreateForDevice(DeviceId deviceId)
    {
        _logger.LogDebug("Creating OpenCL accelerator for device ID: {DeviceId:X}", deviceId.Handle);

        var device = _deviceManager.GetDevice(deviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"OpenCL device not found: {deviceId.Handle:X}");
        }

        _logger.LogInformation("Creating OpenCL accelerator for device: {DeviceName} ({DeviceType})",
            device.Name, device.Type);

        return new OpenCLAccelerator(device, _loggerFactory.CreateLogger<OpenCLAccelerator>());
    }

    /// <summary>
    /// Gets information about all available OpenCL devices.
    /// </summary>
    /// <returns>A list of all available OpenCL devices across all platforms.</returns>
    public IEnumerable<OpenCLDeviceInfo> GetAvailableDevices()
    {
        _logger.LogDebug("Retrieving all available OpenCL devices");
        return _deviceManager.AllDevices;
    }

    /// <summary>
    /// Gets information about all available OpenCL platforms.
    /// </summary>
    /// <returns>A list of all available OpenCL platforms.</returns>
    public IReadOnlyList<OpenCLPlatformInfo> GetAvailablePlatforms()
    {
        _logger.LogDebug("Retrieving all available OpenCL platforms");
        return _deviceManager.Platforms;
    }

    /// <summary>
    /// Checks if OpenCL is available on the current system.
    /// </summary>
    /// <returns>True if OpenCL is available, false otherwise.</returns>
    public bool IsOpenCLAvailable()
    {
        try
        {
            return _deviceManager.IsOpenCLAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking OpenCL availability");
            return false;
        }
    }

    /// <summary>
    /// Validates that a device meets minimum requirements for compute operations.
    /// </summary>
    /// <param name="device">The device to validate.</param>
    /// <returns>True if the device is suitable for compute operations.</returns>
    public static bool IsDeviceSuitable(OpenCLDeviceInfo device)
    {
        // Check basic requirements
        if (!device.Available || !device.CompilerAvailable)
            return false;

        // Minimum memory requirement (128 MB)
        if (device.GlobalMemorySize < 128 * 1024 * 1024)
            return false;

        // Minimum compute units
        if (device.MaxComputeUnits < 1)
            return false;

        // Minimum work group size
        if (device.MaxWorkGroupSize < 64)
            return false;

        return true;
    }

    /// <summary>
    /// Gets recommended device types in order of preference for compute workloads.
    /// </summary>
    /// <returns>Device types ordered by preference for compute operations.</returns>
    public static DeviceType[] GetPreferredDeviceTypes()
    {
        return
        [
            DeviceType.GPU,        // Discrete GPUs are typically best
            DeviceType.Accelerator, // Dedicated compute accelerators
            DeviceType.CPU         // CPUs as fallback
        ];
    }

    /// <summary>
    /// Creates a logger factory from a single logger instance.
    /// </summary>
    /// <param name="logger">The logger to wrap in a factory.</param>
    /// <returns>A logger factory that returns the provided logger.</returns>
    private static ILoggerFactory CreateLoggerFactory(ILogger logger)
    {
        return LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(logger)));
    }
}