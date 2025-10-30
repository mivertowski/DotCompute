// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Execution;
using DotCompute.Backends.OpenCL.Models;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Defines vendor-specific adaptations for OpenCL implementations.
/// Enables optimizations and workarounds for NVIDIA, AMD, Intel, and other vendors.
/// </summary>
/// <remarks>
/// Different OpenCL vendors have varying characteristics:
/// - NVIDIA: Warp-based execution (32 threads), CUDA heritage
/// - AMD: Wavefront-based (32 or 64 threads depending on architecture)
/// - Intel: SIMD-16/32 based execution, varying by generation
///
/// This interface allows DotCompute to optimize for each vendor while maintaining
/// a unified programming model.
/// </remarks>
public interface IOpenCLVendorAdapter
{
    /// <summary>
    /// Gets the vendor type this adapter handles.
    /// </summary>
    public OpenCLVendor Vendor { get; }

    /// <summary>
    /// Gets the vendor's display name.
    /// </summary>
    public string VendorName { get; }

    /// <summary>
    /// Determines if this adapter can handle the specified platform.
    /// </summary>
    /// <param name="platform">The OpenCL platform to evaluate.</param>
    /// <returns><c>true</c> if this adapter can handle the platform; otherwise, <c>false</c>.</returns>
    public bool CanHandle(OpenCLPlatformInfo platform);

    /// <summary>
    /// Gets the optimal work group size for a kernel on this vendor's hardware.
    /// </summary>
    /// <param name="device">The device to optimize for.</param>
    /// <param name="defaultSize">The default work group size to use as a baseline.</param>
    /// <returns>The optimal work group size for this vendor.</returns>
    /// <remarks>
    /// Work group sizing is critical for GPU performance:
    /// - NVIDIA: Prefer multiples of 32 (warp size)
    /// - AMD: Prefer multiples of 32/64 (wavefront size)
    /// - Intel: Prefer multiples of 16 (SIMD width)
    /// </remarks>
    public int GetOptimalWorkGroupSize(OpenCLDeviceInfo device, int defaultSize);

    /// <summary>
    /// Gets the optimal local memory size for this vendor.
    /// </summary>
    /// <param name="device">The device to query.</param>
    /// <returns>The recommended local memory size in bytes.</returns>
    /// <remarks>
    /// Local memory (shared memory in CUDA terms) has vendor-specific limits:
    /// - NVIDIA: 48KB per SM (configurable with L1 cache)
    /// - AMD: 64KB per CU
    /// - Intel: 64KB per subslice
    /// </remarks>
    public long GetOptimalLocalMemorySize(OpenCLDeviceInfo device);

    /// <summary>
    /// Applies vendor-specific queue properties.
    /// </summary>
    /// <param name="properties">The base queue properties.</param>
    /// <param name="device">The device the queue will operate on.</param>
    /// <returns>Modified queue properties optimized for this vendor.</returns>
    /// <remarks>
    /// Some vendors benefit from out-of-order execution, while others work
    /// better with in-order queues depending on the workload characteristics.
    /// </remarks>
    public QueueProperties ApplyVendorOptimizations(QueueProperties properties, OpenCLDeviceInfo device);

    /// <summary>
    /// Gets vendor-specific compiler options.
    /// </summary>
    /// <param name="enableOptimizations">Whether to enable aggressive optimizations.</param>
    /// <returns>Compiler options string suitable for clBuildProgram.</returns>
    /// <remarks>
    /// Vendors support different compiler flags:
    /// - Common: -cl-mad-enable, -cl-fast-relaxed-math
    /// - NVIDIA: -cl-denorms-are-zero
    /// - AMD: -cl-unsafe-math-optimizations
    /// - Intel: Conservative optimizations for better compatibility
    /// </remarks>
    public string GetCompilerOptions(bool enableOptimizations);

    /// <summary>
    /// Checks if a specific extension is reliably supported by this vendor.
    /// Some vendors report extensions but have bugs/limitations.
    /// </summary>
    /// <param name="extension">The extension name (e.g., "cl_khr_fp64").</param>
    /// <param name="device">The device to check.</param>
    /// <returns><c>true</c> if the extension is reliably supported; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Not all advertised extensions work correctly on all hardware.
    /// This method allows vendors to blacklist problematic extensions.
    /// </remarks>
    public bool IsExtensionReliable(string extension, OpenCLDeviceInfo device);

    /// <summary>
    /// Gets recommended buffer alignment for optimal memory access.
    /// </summary>
    /// <param name="device">The device to optimize for.</param>
    /// <returns>The recommended alignment in bytes.</returns>
    /// <remarks>
    /// Proper alignment ensures coalesced memory access:
    /// - NVIDIA: 128-byte alignment for coalescing
    /// - AMD: 256-byte alignment for optimal access
    /// - Intel: 64-byte alignment (cache line size)
    /// </remarks>
    public int GetRecommendedBufferAlignment(OpenCLDeviceInfo device);

    /// <summary>
    /// Indicates if this vendor benefits from persistent kernels.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <returns><c>true</c> if persistent kernels are beneficial; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Persistent kernels keep work groups alive across multiple kernel invocations,
    /// which can improve performance for streaming workloads on high-end GPUs.
    /// </remarks>
    public bool SupportsPersistentKernels(OpenCLDeviceInfo device);
}

/// <summary>
/// OpenCL vendor types.
/// </summary>
public enum OpenCLVendor
{
    /// <summary>
    /// Unknown or unidentified vendor.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// NVIDIA Corporation (CUDA-based OpenCL).
    /// </summary>
    NVIDIA = 1,

    /// <summary>
    /// Advanced Micro Devices (ROCm/GCN/RDNA-based OpenCL).
    /// </summary>
    AMD = 2,

    /// <summary>
    /// Intel Corporation (Xe/Gen graphics-based OpenCL).
    /// </summary>
    Intel = 3,

    /// <summary>
    /// Apple Inc. (Metal-based OpenCL, deprecated on modern macOS).
    /// </summary>
    Apple = 4,

    /// <summary>
    /// ARM Holdings (Mali GPU OpenCL).
    /// </summary>
    ARM = 5,

    /// <summary>
    /// Qualcomm (Adreno GPU OpenCL).
    /// </summary>
    Qualcomm = 6,

    /// <summary>
    /// Generic OpenCL implementation (fallback).
    /// </summary>
    Generic = 99
}
