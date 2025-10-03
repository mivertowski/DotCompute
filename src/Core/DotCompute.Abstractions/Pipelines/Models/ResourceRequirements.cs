// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Defines resource requirements for pipeline stage execution.
/// </summary>
public sealed class ResourceRequirements
{
    /// <summary>
    /// Gets or sets the minimum memory required in bytes.
    /// </summary>
    public long MinMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the preferred memory amount in bytes.
    /// </summary>
    public long PreferredMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of compute units required.
    /// </summary>
    public int MinComputeUnits { get; set; } = 1;

    /// <summary>
    /// Gets or sets the preferred number of compute units.
    /// </summary>
    public int PreferredComputeUnits { get; set; } = 1;

    /// <summary>
    /// Gets or sets the required compute device type.
    /// </summary>
    public ComputeDeviceType RequiredDeviceType { get; set; } = ComputeDeviceType.Auto;

    /// <summary>
    /// Gets or sets the minimum compute capability required.
    /// </summary>
    public double MinComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets whether shared memory is required.
    /// </summary>
    public bool RequiresSharedMemory { get; set; }

    /// <summary>
    /// Gets or sets the amount of shared memory required in bytes.
    /// </summary>
    public long SharedMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets whether constant memory is required.
    /// </summary>
    public bool RequiresConstantMemory { get; set; }

    /// <summary>
    /// Gets or sets the amount of constant memory required in bytes.
    /// </summary>
    public long ConstantMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets whether texture memory is required.
    /// </summary>
    public bool RequiresTextureMemory { get; set; }

    /// <summary>
    /// Gets or sets the bandwidth requirements in bytes per second.
    /// </summary>
    public long BandwidthRequirement { get; set; }

    /// <summary>
    /// Gets or sets the maximum latency tolerance in milliseconds.
    /// </summary>
    public double MaxLatencyMs { get; set; } = double.MaxValue;

    /// <summary>
    /// Creates minimal resource requirements.
    /// </summary>
    /// <returns>Minimal resource requirements</returns>
    public static ResourceRequirements Minimal()
    {
        return new ResourceRequirements
        {
            MinMemoryBytes = 1024, // 1 KB
            PreferredMemoryBytes = 4096, // 4 KB
            MinComputeUnits = 1,
            PreferredComputeUnits = 1
        };
    }

    /// <summary>
    /// Creates GPU-optimized resource requirements.
    /// </summary>
    /// <returns>GPU-optimized resource requirements</returns>
    public static ResourceRequirements GpuOptimized()
    {
        return new ResourceRequirements
        {
            MinMemoryBytes = 1024 * 1024, // 1 MB
            PreferredMemoryBytes = 64 * 1024 * 1024, // 64 MB
            MinComputeUnits = 32,
            PreferredComputeUnits = 256,
            RequiredDeviceType = ComputeDeviceType.CUDA,
            RequiresSharedMemory = true,
            SharedMemoryBytes = 48 * 1024 // 48 KB
        };
    }
}
