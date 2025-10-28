// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Execution;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Vendor adapter for Intel GPUs with Xe architecture optimizations.
/// </summary>
/// <remarks>
/// Intel's GPU architecture has evolved significantly:
/// - Gen9 (Skylake/Kaby Lake): Integrated graphics, limited compute
/// - Gen11 (Ice Lake): Enhanced compute capabilities
/// - Gen12/Xe-LP (Tiger Lake/Alder Lake): Modern integrated graphics
/// - Xe-HPG (Arc Alchemist): Discrete gaming GPUs (Arc A-series)
/// - Xe-HPC (Ponte Vecchio): Data center GPUs
///
/// Execution model:
/// - SIMD-8, SIMD-16, or SIMD-32 depending on hardware generation
/// - EU (Execution Unit) based parallelism
/// - 64KB SLM (Shared Local Memory) per subslice
///
/// Optimization priorities:
/// 1. Align work groups to SIMD width (16 or 32)
/// 2. Use 64-byte alignment (cache line size)
/// 3. Conservative optimizations for compatibility
/// 4. Consider integrated vs discrete GPU characteristics
/// </remarks>
public sealed class IntelOpenCLAdapter : IOpenCLVendorAdapter
{
    /// <inheritdoc/>
    public OpenCLVendor Vendor => OpenCLVendor.Intel;

    /// <inheritdoc/>
    public string VendorName => "Intel Corporation";

    /// <inheritdoc/>
    public bool CanHandle(OpenCLPlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return platform.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public int GetOptimalWorkGroupSize(OpenCLDeviceInfo device, int defaultSize)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Intel GPUs work with SIMD-16 or SIMD-32 depending on architecture
        var maxWorkGroupSize = device.MaxWorkGroupSize;

        // Detect if this is a discrete GPU (Arc series)
        bool isDiscrete = device.MaxComputeUnits >= 96; // Arc A770 has 512 EUs

        // Xe architecture (Arc, Tiger Lake, etc.) prefers 256 work items
        if (maxWorkGroupSize >= 256 && isDiscrete)
        {
            return 256;
        }

        // Older Gen graphics or integrated GPUs prefer 128
        if (maxWorkGroupSize >= 128)
        {
            return 128;
        }

        // Fall back to 64 for very limited hardware
        if (maxWorkGroupSize >= 64)
        {
            return 64;
        }

        // Round down to nearest 16 (SIMD width)
        int simdGroups = (int)(maxWorkGroupSize / 16);
        return simdGroups > 0 ? simdGroups * 16 : 16;
    }

    /// <inheritdoc/>
    public long GetOptimalLocalMemorySize(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Intel GPUs have 64KB SLM (Shared Local Memory) per subslice
        // Use conservative 32KB to account for compiler usage
        const long conservativeLimit = 32 * 1024;
        return Math.Min(conservativeLimit, (long)device.LocalMemorySize);
    }

    /// <inheritdoc/>
    public QueueProperties ApplyVendorOptimizations(QueueProperties properties, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Intel GPUs work well with both in-order and out-of-order queues
        // For integrated GPUs, in-order can be more predictable
        // For discrete GPUs (Arc), out-of-order provides better performance
        bool isDiscrete = device.MaxComputeUnits >= 96;

        if (isDiscrete)
        {
            return properties with
            {
                InOrderExecution = false  // Out-of-order for Arc series
            };
        }

        // Keep as-is for integrated GPUs (don't force changes)
        return properties;
    }

    /// <inheritdoc/>
    public string GetCompilerOptions(bool enableOptimizations)
    {
        if (!enableOptimizations)
        {
            return string.Empty;
        }

        // Intel-specific optimizations are more conservative
        // Avoid aggressive optimizations that might cause compatibility issues
        // -cl-mad-enable: Safe multiply-add fusion
        // -cl-fast-relaxed-math: Relaxed math optimizations
        return "-cl-mad-enable -cl-fast-relaxed-math";
    }

    /// <inheritdoc/>
    public bool IsExtensionReliable(string extension, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(device);

        return extension switch
        {
            // Double precision: Limited on integrated GPUs, full on discrete
            "cl_khr_fp64" => CheckFP64Support(device),

            // Half precision: Well supported on modern Intel GPUs
            "cl_khr_fp16" => true,

            // Intel-specific extensions
            "cl_intel_advanced_motion_estimation" => true,
            "cl_intel_subgroups" => true,
            "cl_intel_required_subgroup_size" => true,
            "cl_intel_planar_yuv" => true,

            // Generally reliable on Intel hardware
            _ => true
        };
    }

    /// <summary>
    /// Checks if double precision floating point is reliably supported.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <returns><c>true</c> if FP64 is reliably supported; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Intel integrated GPUs may have limited FP64 support (emulated, slow).
    /// Discrete Arc GPUs have hardware FP64 support but at reduced throughput.
    /// </remarks>
    private static bool CheckFP64Support(OpenCLDeviceInfo device)
    {
        // Check if the device actually supports double precision
        if (!device.SupportsDoublePrecision)
        {
            return false;
        }

        // Discrete GPUs (Arc series) have better FP64 support
        bool isDiscrete = device.MaxComputeUnits >= 96;
        return isDiscrete;
    }

    /// <inheritdoc/>
    public int GetRecommendedBufferAlignment(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Intel prefers 64-byte alignment matching CPU cache line size
        // This is beneficial for integrated GPUs sharing system memory
        return 64;
    }

    /// <inheritdoc/>
    public bool SupportsPersistentKernels(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Persistent kernels are beneficial on discrete Intel GPUs
        // Integrated GPUs share resources with CPU, less optimal for persistent workloads

        // Arc A770 has 512 EUs, A750 has 448 EUs
        bool isDiscrete = device.Type.HasFlag(DeviceType.GPU) &&
                          device.MaxComputeUnits >= 96;

        return isDiscrete;
    }
}
