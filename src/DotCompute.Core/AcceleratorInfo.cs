// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Core;

/// <summary>
/// Contains information about an accelerator device.
/// </summary>
public sealed class AcceleratorInfo
{
    /// <summary>
    /// Gets the unique identifier for this accelerator.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display name of the accelerator.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public required AcceleratorType Type { get; init; }

    /// <summary>
    /// Gets the vendor name.
    /// </summary>
    public required string Vendor { get; init; }

    /// <summary>
    /// Gets the driver version.
    /// </summary>
    public string? DriverVersion { get; init; }

    /// <summary>
    /// Gets the maximum number of compute units.
    /// </summary>
    public required int MaxComputeUnits { get; init; }

    /// <summary>
    /// Gets the maximum work group size.
    /// </summary>
    public required int MaxWorkGroupSize { get; init; }

    /// <summary>
    /// Gets the maximum memory allocation size in bytes.
    /// </summary>
    public required long MaxMemoryAllocation { get; init; }

    /// <summary>
    /// Gets the total global memory size in bytes.
    /// </summary>
    public required long GlobalMemorySize { get; init; }

    /// <summary>
    /// Gets the local memory size per compute unit in bytes.
    /// </summary>
    public required long LocalMemorySize { get; init; }

    /// <summary>
    /// Gets whether the device supports double precision.
    /// </summary>
    public required bool SupportsDoublePrecision { get; init; }

    /// <summary>
    /// Gets device-specific capabilities.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Capabilities { get; init; }
}

/// <summary>
/// Defines the type of accelerator.
/// </summary>
public enum AcceleratorType
{
    /// <summary>
    /// CPU-based accelerator.
    /// </summary>
    Cpu,

    /// <summary>
    /// NVIDIA CUDA GPU.
    /// </summary>
    Cuda,

    /// <summary>
    /// AMD ROCm GPU.
    /// </summary>
    Rocm,

    /// <summary>
    /// Intel GPU.
    /// </summary>
    IntelGpu,

    /// <summary>
    /// Apple Metal GPU.
    /// </summary>
    Metal,

    /// <summary>
    /// Vulkan compute.
    /// </summary>
    Vulkan,

    /// <summary>
    /// OpenCL device.
    /// </summary>
    OpenCl,

    /// <summary>
    /// Custom or unknown accelerator.
    /// </summary>
    Custom
}