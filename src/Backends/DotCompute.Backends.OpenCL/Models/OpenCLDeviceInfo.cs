// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Types.Native;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Models;

/// <summary>
/// Information about an OpenCL compute device.
/// Contains device capabilities, limits, and performance characteristics.
/// </summary>
public sealed class OpenCLDeviceInfo
{
    /// <summary>
    /// Gets the native device ID handle.
    /// </summary>
    public DeviceId DeviceId { get; init; }

    /// <summary>
    /// Gets the device type (GPU, CPU, Accelerator, etc.).
    /// </summary>
    public DeviceType Type { get; init; }

    /// <summary>
    /// Gets the device name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the device vendor name.
    /// </summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>
    /// Gets the device driver version.
    /// </summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the OpenCL version supported by the device.
    /// </summary>
    public string OpenCLVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the device profile (FULL_PROFILE or EMBEDDED_PROFILE).
    /// </summary>
    public string Profile { get; init; } = string.Empty;

    /// <summary>
    /// Gets the supported extensions as a space-separated string.
    /// </summary>
    public string Extensions { get; init; } = string.Empty;

    /// <summary>
    /// Gets the vendor ID.
    /// </summary>
    public uint VendorId { get; init; }

    /// <summary>
    /// Gets the maximum number of compute units available on the device.
    /// </summary>
    public uint MaxComputeUnits { get; init; }

    /// <summary>
    /// Gets the maximum number of work-items in a work-group.
    /// </summary>
    public nuint MaxWorkGroupSize { get; init; }

    /// <summary>
    /// Gets the maximum work-item dimensions.
    /// </summary>
    public uint MaxWorkItemDimensions { get; init; }

    /// <summary>
    /// Gets the maximum work-item sizes for each dimension.
    /// </summary>
    public IReadOnlyList<nuint> MaxWorkItemSizes { get; init; } = [];

    /// <summary>
    /// Gets the maximum clock frequency in MHz.
    /// </summary>
    public uint MaxClockFrequency { get; init; }

    /// <summary>
    /// Gets the device address space size in bits (32 or 64).
    /// </summary>
    public uint AddressBits { get; init; }

    /// <summary>
    /// Gets the maximum size of memory object allocation in bytes.
    /// </summary>
    public ulong MaxMemoryAllocationSize { get; init; }

    /// <summary>
    /// Gets the size of global device memory in bytes.
    /// </summary>
    public ulong GlobalMemorySize { get; init; }

    /// <summary>
    /// Gets the size of local memory in bytes.
    /// </summary>
    public ulong LocalMemorySize { get; init; }

    /// <summary>
    /// Gets the size of constant buffer in bytes.
    /// </summary>
    public ulong MaxConstantBufferSize { get; init; }

    /// <summary>
    /// Gets the maximum number of constant arguments.
    /// </summary>
    public uint MaxConstantArgs { get; init; }

    /// <summary>
    /// Gets whether the device supports images.
    /// </summary>
    public bool ImageSupport { get; init; }

    /// <summary>
    /// Gets the maximum 2D image width.
    /// </summary>
    public nuint MaxImage2DWidth { get; init; }

    /// <summary>
    /// Gets the maximum 2D image height.
    /// </summary>
    public nuint MaxImage2DHeight { get; init; }

    /// <summary>
    /// Gets the maximum 3D image width.
    /// </summary>
    public nuint MaxImage3DWidth { get; init; }

    /// <summary>
    /// Gets the maximum 3D image height.
    /// </summary>
    public nuint MaxImage3DHeight { get; init; }

    /// <summary>
    /// Gets the maximum 3D image depth.
    /// </summary>
    public nuint MaxImage3DDepth { get; init; }

    /// <summary>
    /// Gets whether the device is available for use.
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Gets whether a compiler is available for the device.
    /// </summary>
    public bool CompilerAvailable { get; init; }

    /// <summary>
    /// Gets whether the device supports error correction.
    /// </summary>
    public bool ErrorCorrectionSupport { get; init; }

    /// <summary>
    /// Gets whether the device uses little-endian byte ordering.
    /// </summary>
    public bool EndianLittle { get; init; }

    /// <summary>
    /// Gets the profiling timer resolution in nanoseconds.
    /// </summary>
    public nuint ProfilingTimerResolution { get; init; }

    /// <summary>
    /// Gets the alignment requirement for memory base addresses.
    /// </summary>
    public uint MemoryBaseAddressAlignment { get; init; }

    /// <summary>
    /// Gets the minimum alignment for any data type.
    /// </summary>
    public uint MinDataTypeAlignmentSize { get; init; }

    /// <summary>
    /// Gets the global memory cache line size in bytes.
    /// </summary>
    public uint GlobalMemoryCacheLineSize { get; init; }

    /// <summary>
    /// Gets the global memory cache size in bytes.
    /// </summary>
    public ulong GlobalMemoryCacheSize { get; init; }

    /// <summary>
    /// Creates a string representation of the device information.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Vendor}) - {Type}, {MaxComputeUnits} CUs, {GlobalMemorySize / (1024 * 1024 * 1024)} GB";
    }

    /// <summary>
    /// Gets whether this device supports double-precision floating point.
    /// </summary>
    public bool SupportsDoublePrecision => Extensions.Contains("cl_khr_fp64", StringComparison.Ordinal);

    /// <summary>
    /// Gets whether this device supports half-precision floating point.
    /// </summary>
    public bool SupportsHalfPrecision => Extensions.Contains("cl_khr_fp16", StringComparison.Ordinal);

    /// <summary>
    /// Gets whether this device supports 3D image writes.
    /// </summary>
    public bool Supports3DImageWrites => Extensions.Contains("cl_khr_3d_image_writes", StringComparison.Ordinal);

    /// <summary>
    /// Gets the estimated compute capability based on compute units and clock frequency.
    /// </summary>
    public double EstimatedGFlops => (double)MaxComputeUnits * MaxClockFrequency / 1000.0;
}
