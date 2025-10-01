// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.DeviceManagement;

/// <summary>
/// Manages OpenCL device discovery, enumeration, and capability detection.
/// Provides centralized access to available OpenCL platforms and devices.
/// </summary>
internal sealed class OpenCLDeviceManager
{
    private readonly ILogger<OpenCLDeviceManager> _logger;
    private readonly Lazy<IReadOnlyList<OpenCLPlatformInfo>> _platforms;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLDeviceManager"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLDeviceManager(ILogger<OpenCLDeviceManager> logger)
    {
        _logger = logger;
        _platforms = new Lazy<IReadOnlyList<OpenCLPlatformInfo>>(DiscoverPlatforms);
    }

    /// <summary>
    /// Gets all available OpenCL platforms and their devices.
    /// </summary>
    public IReadOnlyList<OpenCLPlatformInfo> Platforms => _platforms.Value;

    /// <summary>
    /// Gets all available OpenCL devices across all platforms.
    /// </summary>
    public IEnumerable<OpenCLDeviceInfo> AllDevices => 
        Platforms.SelectMany(p => p.Devices);

    /// <summary>
    /// Gets whether OpenCL is available on the current system.
    /// </summary>
    public bool IsOpenCLAvailable => Platforms.Count > 0;

    /// <summary>
    /// Gets the best available compute device for general-purpose computing.
    /// Prioritizes GPU devices with high compute capability and memory.
    /// </summary>
    public OpenCLDeviceInfo? GetBestDevice()
    {
        _logger.LogDebug("Finding best OpenCL device");

        // Prefer discrete GPUs first, then integrated GPUs, then accelerators, finally CPUs
        var bestDevice = AllDevices
            .Where(d => d.Available && d.CompilerAvailable)
            .OrderBy(d => GetDeviceTypePriority(d.Type))
            .ThenByDescending(d => d.EstimatedGFlops)
            .ThenByDescending(d => d.GlobalMemorySize)
            .FirstOrDefault();

        if (bestDevice != null)
        {
            _logger.LogInformation($"Selected best device: {bestDevice.Name} ({bestDevice.Type})");
        }
        else
        {
            _logger.LogWarning("No suitable OpenCL devices found");
        }

        return bestDevice;
    }

    /// <summary>
    /// Gets devices of a specific type across all platforms.
    /// </summary>
    /// <param name="deviceType">The device type to filter by.</param>
    /// <param name="requireCompiler">Whether to require compiler availability.</param>
    /// <returns>Filtered list of devices.</returns>
    public IEnumerable<OpenCLDeviceInfo> GetDevices(
        DeviceType deviceType = DeviceType.All, 
        bool requireCompiler = true)
    {
        return Platforms.SelectMany(p => p.GetDevices(deviceType, requireCompiler));
    }

    /// <summary>
    /// Finds devices by vendor name (case-insensitive partial match).
    /// </summary>
    /// <param name="vendorName">Vendor name to search for.</param>
    /// <returns>Devices from matching vendors.</returns>
    public IEnumerable<OpenCLDeviceInfo> GetDevicesByVendor(string vendorName)
    {
        if (string.IsNullOrEmpty(vendorName))
            return [];

        return AllDevices.Where(d => 
            d.Vendor.Contains(vendorName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets device information by device ID.
    /// </summary>
    /// <param name="deviceId">The device ID to find.</param>
    /// <returns>Device information if found, null otherwise.</returns>
    public OpenCLDeviceInfo? GetDevice(DeviceId deviceId)
    {
        return AllDevices.FirstOrDefault(d => d.DeviceId.Handle == deviceId.Handle);
    }

    /// <summary>
    /// Discovers all OpenCL platforms and their devices.
    /// </summary>
    private IReadOnlyList<OpenCLPlatformInfo> DiscoverPlatforms()
    {
        _logger.LogDebug("Discovering OpenCL platforms");

        try
        {
            // Get platform count
            var error = OpenCLRuntime.clGetPlatformIDs(0, null, out var platformCount);
            if (error != OpenCLError.Success || platformCount == 0)
            {
                _logger.LogWarning("No OpenCL platforms found or error occurred: {error}");
                return [];
            }

            _logger.LogDebug("Found {platformCount} OpenCL platforms");

            // Get platform IDs
            var platformIds = new nint[platformCount];
            error = OpenCLRuntime.clGetPlatformIDs(platformCount, platformIds, out _);
            OpenCLException.ThrowIfError(error, "Get platform IDs");

            var platforms = new List<OpenCLPlatformInfo>();

            foreach (var platformId in platformIds)
            {
                try
                {
                    var platform = CreatePlatformInfo(platformId);
                    platforms.Add(platform);
                    _logger.LogDebug($"Added platform: {platform.Name} with {platform.Devices.Count} devices");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create platform info for platform {PlatformId:X}", platformId);
                }
            }

            _logger.LogInformation($"Discovered {platforms.Count} platforms with {platforms.Sum(p => p.Devices.Count)} total devices");

            return platforms.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover OpenCL platforms");
            return [];
        }
    }

    /// <summary>
    /// Creates platform information from a platform ID.
    /// </summary>
    private OpenCLPlatformInfo CreatePlatformInfo(nint platformId)
    {
        var platform = new OpenCLPlatformInfo
        {
            PlatformId = platformId,
            Name = OpenCLRuntimeHelpers.GetPlatformInfoString(platformId, PlatformInfo.Name),
            Vendor = OpenCLRuntimeHelpers.GetPlatformInfoString(platformId, PlatformInfo.Vendor),
            Version = OpenCLRuntimeHelpers.GetPlatformInfoString(platformId, PlatformInfo.Version),
            Profile = OpenCLRuntimeHelpers.GetPlatformInfoString(platformId, PlatformInfo.Profile),
            Extensions = OpenCLRuntimeHelpers.GetPlatformInfoString(platformId, PlatformInfo.Extensions),
            Devices = DiscoverDevices(platformId)
        };

        return platform;
    }

    /// <summary>
    /// Discovers devices for a specific platform.
    /// </summary>
    private IReadOnlyList<OpenCLDeviceInfo> DiscoverDevices(nint platformId)
    {
        try
        {
            // Get device count
            var error = OpenCLRuntime.clGetDeviceIDs(platformId, DeviceType.All, 0, null, out var deviceCount);
            if (error != OpenCLError.Success || deviceCount == 0)
            {
                return [];
            }

            // Get device IDs
            var deviceIds = new nint[deviceCount];
            error = OpenCLRuntime.clGetDeviceIDs(platformId, DeviceType.All, deviceCount, deviceIds, out _);
            OpenCLException.ThrowIfError(error, "Get device IDs");

            var devices = new List<OpenCLDeviceInfo>();

            foreach (var deviceId in deviceIds)
            {
                try
                {
                    var device = CreateDeviceInfo(deviceId);
                    devices.Add(device);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create device info for device {DeviceId:X}", deviceId);
                }
            }

            return devices.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover devices for platform {PlatformId:X}", platformId);
            return [];
        }
    }

    /// <summary>
    /// Creates device information from a device ID.
    /// </summary>
    private OpenCLDeviceInfo CreateDeviceInfo(nint deviceId)
    {
        // Get maximum work item sizes
        var maxWorkItemSizes = GetWorkItemSizes(deviceId);

        var device = new OpenCLDeviceInfo
        {
            DeviceId = deviceId,
            Type = OpenCLRuntimeHelpers.GetDeviceInfo<DeviceType>(deviceId, DeviceInfo.Type),
            Name = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.Name),
            Vendor = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.Vendor),
            DriverVersion = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.DriverVersion),
            OpenCLVersion = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.Version),
            Profile = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.Profile),
            Extensions = OpenCLRuntimeHelpers.GetDeviceInfoString(deviceId, DeviceInfo.Extensions),
            VendorId = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.VendorId),
            MaxComputeUnits = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MaxComputeUnits),
            MaxWorkGroupSize = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.MaxWorkGroupSize),
            MaxWorkItemDimensions = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MaxWorkItemDimensions),
            MaxWorkItemSizes = maxWorkItemSizes,
            MaxClockFrequency = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MaxClockFrequency),
            AddressBits = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.AddressBits),
            MaxMemoryAllocationSize = OpenCLRuntimeHelpers.GetDeviceInfo<ulong>(deviceId, DeviceInfo.MaxMemAllocSize),
            GlobalMemorySize = OpenCLRuntimeHelpers.GetDeviceInfo<ulong>(deviceId, DeviceInfo.GlobalMemSize),
            LocalMemorySize = OpenCLRuntimeHelpers.GetDeviceInfo<ulong>(deviceId, DeviceInfo.LocalMemSize),
            MaxConstantBufferSize = OpenCLRuntimeHelpers.GetDeviceInfo<ulong>(deviceId, DeviceInfo.MaxConstantBufferSize),
            MaxConstantArgs = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MaxConstantArgs),
            ImageSupport = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.ImageSupport) != 0,
            MaxImage2DWidth = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.Image2DMaxWidth),
            MaxImage2DHeight = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.Image2DMaxHeight),
            MaxImage3DWidth = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.Image3DMaxWidth),
            MaxImage3DHeight = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.Image3DMaxHeight),
            MaxImage3DDepth = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.Image3DMaxDepth),
            Available = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.Available) != 0,
            CompilerAvailable = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.CompilerAvailable) != 0,
            ErrorCorrectionSupport = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.ErrorCorrectionSupport) != 0,
            EndianLittle = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.EndianLittle) != 0,
            ProfilingTimerResolution = OpenCLRuntimeHelpers.GetDeviceInfo<nuint>(deviceId, DeviceInfo.ProfilingTimerResolution),
            MemoryBaseAddressAlignment = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MemBaseAddrAlign),
            MinDataTypeAlignmentSize = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MinDataTypeAlignSize),
            GlobalMemoryCacheLineSize = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.GlobalMemCachelineSize),
            GlobalMemoryCacheSize = OpenCLRuntimeHelpers.GetDeviceInfo<ulong>(deviceId, DeviceInfo.GlobalMemCacheSize)
        };

        return device;
    }

    /// <summary>
    /// Gets the maximum work item sizes for each dimension.
    /// </summary>
    private nuint[] GetWorkItemSizes(nint deviceId)
    {
        try
        {
            var dimensions = OpenCLRuntimeHelpers.GetDeviceInfo<uint>(deviceId, DeviceInfo.MaxWorkItemDimensions);
            if (dimensions == 0) return [];

            var sizes = new nuint[dimensions];
            unsafe
            {
                fixed (nuint* ptr = sizes)
                {
                    var error = OpenCLRuntime.clGetDeviceInfo(
                        deviceId, 
                        DeviceInfo.MaxWorkItemSizes,
                        (nuint)(sizeof(nuint) * dimensions),
                        (nint)ptr,
                        out _);
                    
                    if (error != OpenCLError.Success)
                        return [];
                }
            }
            return sizes;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Gets device type priority for ranking (lower values are higher priority).
    /// </summary>
    private static int GetDeviceTypePriority(DeviceType deviceType)
    {
        return deviceType switch
        {
            var t when t.HasFlag(DeviceType.GPU) => 1,
            var t when t.HasFlag(DeviceType.Accelerator) => 2,
            var t when t.HasFlag(DeviceType.CPU) => 3,
            _ => 4
        };
    }
}