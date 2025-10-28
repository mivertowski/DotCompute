// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Algorithms.Types;


/// <summary>
/// Represents hardware capabilities and characteristics for optimization purposes.
/// </summary>
public sealed record HardwareInfo
{
    /// <summary>
    /// Gets or sets the device type (CPU, GPU, etc.).
    /// </summary>
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the compute units count.
    /// </summary>
    public int ComputeUnits { get; init; }

    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; init; } = 256;

    /// <summary>
    /// Gets or sets the maximum work item dimensions.
    /// </summary>
    public int MaxWorkItemDimensions { get; init; } = 3;

    /// <summary>
    /// Gets or sets the global memory size in bytes.
    /// </summary>
    public long GlobalMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the local memory size in bytes.
    /// </summary>
    public long LocalMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public long SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the cache line size in bytes.
    /// </summary>
    public int CacheLineSize { get; init; } = 64;

    /// <summary>
    /// Gets or sets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidth { get; init; }

    /// <summary>
    /// Gets or sets the peak compute performance in FLOPS.
    /// </summary>
    public double PeakFlops { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports double precision.
    /// </summary>
    public bool SupportsDoublePrecision { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports half precision.
    /// </summary>
    public bool SupportsHalfPrecision { get; init; }

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the device vendor.
    /// </summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the driver version.
    /// </summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>
    /// Creates hardware info from an accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to analyze.</param>
    /// <returns>Hardware information.</returns>
    public static HardwareInfo FromAccelerator(IAccelerator accelerator)
    {
        var info = accelerator.Info;

        return new HardwareInfo
        {
            DeviceType = info.DeviceType,
            ComputeUnits = info.ComputeUnits,
            MaxWorkGroupSize = info.MaxThreadsPerBlock,
            MaxWorkItemDimensions = 3, // Standard OpenCL/CUDA default
            GlobalMemorySize = info.TotalMemory,
            LocalMemorySize = info.LocalMemorySize,
            SharedMemorySize = info.MaxSharedMemoryPerBlock,
            CacheLineSize = EstimateCacheLineSize(info),
            MemoryBandwidth = EstimateMemoryBandwidth(info),
            PeakFlops = EstimatePeakFlops(info),
            SupportsDoublePrecision = info.SupportsFloat64,
            SupportsHalfPrecision = EstimateHalfPrecisionSupport(info),
            Name = info.Name,
            Vendor = info.Vendor,
            DriverVersion = info.DriverVersion ?? "Unknown"
        };
    }

    private static double EstimateMemoryBandwidth(AcceleratorInfo info)
    {
        // Rough estimates based on device type and generation
        return info.DeviceType.ToUpperInvariant() switch
        {
            "GPU" => info.ComputeUnits * 25.0, // ~25 GB/s per compute unit (very rough)
            "CPU" => 50.0, // Modern CPU memory bandwidth
            _ => 10.0 // Conservative fallback
        };
    }

    private static double EstimatePeakFlops(AcceleratorInfo info)
    {
        // Very rough FLOPS estimates
        return info.DeviceType.ToUpperInvariant() switch
        {
            "GPU" => info.ComputeUnits * 2e9, // ~2 GFLOPS per compute unit
            "CPU" => info.ComputeUnits * 1e9, // ~1 GFLOPS per core
            _ => 1e9 // 1 GFLOPS fallback
        };
    }

    private static int EstimateCacheLineSize(AcceleratorInfo info)
    {
        // Cache line size estimates based on device type
        return info.DeviceType.ToUpperInvariant() switch
        {
            "GPU" => 128, // NVIDIA GPUs typically use 128-byte cache lines
            "CPU" => 64,  // Most modern CPUs use 64-byte cache lines
            _ => 64       // Conservative fallback
        };
    }

    private static bool EstimateHalfPrecisionSupport(AcceleratorInfo info)
    {
        // Half precision support estimation
        if (string.Equals(info.DeviceType, "GPU", StringComparison.OrdinalIgnoreCase))
        {
            // Most modern GPUs support half precision
            // Check compute capability for NVIDIA GPUs
            if (info.ComputeCapability != null && info.ComputeCapability >= new Version(5, 3))
            {
                return true;
            }

            // For other GPUs, assume support if they're relatively modern
            return info.ComputeUnits > 8;
        }

        // CPU half precision is less common, depends on specific instruction sets
        return false;
    }
}
