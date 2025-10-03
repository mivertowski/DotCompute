// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Types.Native;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Models;

/// <summary>
/// Information about an OpenCL platform.
/// Contains platform vendor, version, and supported extensions.
/// </summary>
public sealed class OpenCLPlatformInfo
{
    /// <summary>
    /// Gets the native platform ID handle.
    /// </summary>
    public PlatformId PlatformId { get; init; }

    /// <summary>
    /// Gets the platform name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform vendor.
    /// </summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>
    /// Gets the OpenCL version supported by the platform.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform profile (FULL_PROFILE or EMBEDDED_PROFILE).
    /// </summary>
    public string Profile { get; init; } = string.Empty;

    /// <summary>
    /// Gets the supported extensions as a space-separated string.
    /// </summary>
    public string Extensions { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of available devices on this platform.
    /// </summary>
    public IReadOnlyList<OpenCLDeviceInfo> Devices { get; init; } = [];

    /// <summary>
    /// Creates a string representation of the platform information.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Vendor}) - {Version}, {Devices.Count} devices";
    }

    /// <summary>
    /// Gets GPU devices available on this platform.
    /// </summary>
    public IEnumerable<OpenCLDeviceInfo> GpuDevices =>
        Devices.Where(d => d.Type.HasFlag(DeviceType.GPU));

    /// <summary>
    /// Gets CPU devices available on this platform.
    /// </summary>
    public IEnumerable<OpenCLDeviceInfo> CpuDevices =>
        Devices.Where(d => d.Type.HasFlag(DeviceType.CPU));

    /// <summary>
    /// Gets accelerator devices available on this platform.
    /// </summary>
    public IEnumerable<OpenCLDeviceInfo> AcceleratorDevices =>
        Devices.Where(d => d.Type.HasFlag(DeviceType.Accelerator));

    /// <summary>
    /// Gets the total number of compute units across all devices.
    /// </summary>
    public uint TotalComputeUnits => (uint)Devices.Sum(d => d.MaxComputeUnits);

    /// <summary>
    /// Gets the total global memory across all devices in bytes.
    /// </summary>
    public ulong TotalGlobalMemory => (ulong)Devices.Sum(d => (long)d.GlobalMemorySize);

    /// <summary>
    /// Gets whether this platform supports OpenCL 2.0 or later.
    /// </summary>
    public bool SupportsOpenCL20 => Version.Contains("2.") || Version.Contains("3.");

    /// <summary>
    /// Gets whether this platform supports OpenCL 3.0 or later.
    /// </summary>
    public bool SupportsOpenCL30 => Version.Contains("3.");

    /// <summary>
    /// Gets the best device for compute workloads (highest compute units and memory).
    /// </summary>
    public OpenCLDeviceInfo? GetBestComputeDevice()
    {
        return Devices
            .Where(d => d.Available && d.CompilerAvailable)
            .OrderByDescending(d => d.EstimatedGFlops)
            .ThenByDescending(d => d.GlobalMemorySize)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets devices by type with optional filtering.
    /// </summary>
    public IEnumerable<OpenCLDeviceInfo> GetDevices(
        DeviceType deviceType = DeviceType.All,
        bool requireCompiler = true)
    {
        return Devices
            .Where(d => (deviceType == DeviceType.All || d.Type.HasFlag(deviceType)) &&
                       d.Available &&
                       (!requireCompiler || d.CompilerAvailable));
    }
}