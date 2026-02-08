// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Ports;

/// <summary>
/// Port interface for device discovery operations.
/// Part of hexagonal architecture - defines the contract that backend adapters must implement.
/// </summary>
public interface IDeviceDiscoveryPort
{
    /// <summary>
    /// Discovers all available devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered devices.</returns>
    public ValueTask<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed capabilities for a specific device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Device capabilities.</returns>
    public ValueTask<DeviceCapabilities> GetCapabilitiesAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific device is available.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>True if the device is available.</returns>
    public bool IsDeviceAvailable(string deviceId);

    /// <summary>
    /// Gets the backend type.
    /// </summary>
    public BackendType BackendType { get; }
}

/// <summary>
/// Information about a discovered device.
/// </summary>
public sealed record DiscoveredDevice
{
    /// <summary>Unique device identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable device name.</summary>
    public required string Name { get; init; }

    /// <summary>Device vendor.</summary>
    public required string Vendor { get; init; }

    /// <summary>Backend type.</summary>
    public required BackendType Backend { get; init; }

    /// <summary>Device index (for multi-device systems).</summary>
    public int DeviceIndex { get; init; }

    /// <summary>Total memory in bytes.</summary>
    public long TotalMemory { get; init; }

    /// <summary>Whether the device is currently available.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Driver version.</summary>
    public string? DriverVersion { get; init; }
}

/// <summary>
/// Detailed device capabilities.
/// </summary>
public sealed record DeviceCapabilities
{
    /// <summary>Device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Compute capability version.</summary>
    public Version? ComputeCapability { get; init; }

    /// <summary>Maximum threads per block.</summary>
    public int MaxThreadsPerBlock { get; init; }

    /// <summary>Maximum grid dimensions.</summary>
    public Dim3 MaxGridDimensions { get; init; }

    /// <summary>Warp/wavefront size.</summary>
    public int WarpSize { get; init; }

    /// <summary>Number of streaming multiprocessors/compute units.</summary>
    public int MultiprocessorCount { get; init; }

    /// <summary>Maximum shared memory per block.</summary>
    public int MaxSharedMemoryPerBlock { get; init; }

    /// <summary>Maximum registers per block.</summary>
    public int MaxRegistersPerBlock { get; init; }

    /// <summary>Memory clock rate in MHz.</summary>
    public int MemoryClockRate { get; init; }

    /// <summary>Memory bus width in bits.</summary>
    public int MemoryBusWidth { get; init; }

    /// <summary>L2 cache size in bytes.</summary>
    public int L2CacheSize { get; init; }

    /// <summary>Supports unified memory.</summary>
    public bool SupportsUnifiedMemory { get; init; }

    /// <summary>Supports concurrent kernel execution.</summary>
    public bool SupportsConcurrentKernels { get; init; }

    /// <summary>Supports peer-to-peer access.</summary>
    public bool SupportsP2PAccess { get; init; }

    /// <summary>Supported features.</summary>
    public IReadOnlyList<string> SupportedFeatures { get; init; } = [];
}

/// <summary>
/// Backend/accelerator type.
/// </summary>
public enum BackendType
{
    /// <summary>CPU backend (SIMD).</summary>
    Cpu,

    /// <summary>NVIDIA CUDA backend.</summary>
    Cuda,

    /// <summary>Apple Metal backend.</summary>
    Metal,

    /// <summary>OpenCL backend.</summary>
    OpenCL,

    /// <summary>AMD ROCm/HIP backend (future).</summary>
    Rocm,

    /// <summary>Vulkan compute backend (future).</summary>
    Vulkan
}
