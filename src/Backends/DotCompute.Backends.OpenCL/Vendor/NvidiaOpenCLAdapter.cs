// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Execution;
using DotCompute.Backends.OpenCL.Models;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Vendor adapter for NVIDIA GPUs with CUDA-specific optimizations.
/// </summary>
/// <remarks>
/// NVIDIA's OpenCL implementation is built on top of CUDA:
/// - Warp-based execution (32 threads execute in lockstep)
/// - 48KB shared memory per SM (configurable with L1 cache)
/// - Excellent support for out-of-order queues
/// - Strong FP64 support on compute-capable cards
/// - Coalesced memory access critical for performance
///
/// Optimization priorities:
/// 1. Align work groups to warp boundaries (multiples of 32)
/// 2. Use 128-byte alignment for global memory buffers
/// 3. Enable aggressive math optimizations
/// 4. Leverage out-of-order execution
/// </remarks>
public sealed class NvidiaOpenCLAdapter : IOpenCLVendorAdapter
{
    /// <inheritdoc/>
    public OpenCLVendor Vendor => OpenCLVendor.NVIDIA;

    /// <inheritdoc/>
    public string VendorName => "NVIDIA Corporation";

    /// <inheritdoc/>
    public bool CanHandle(OpenCLPlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return platform.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public int GetOptimalWorkGroupSize(OpenCLDeviceInfo device, int defaultSize)
    {
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA GPUs work best with warp-aligned work groups (multiples of 32)
        var maxWorkGroupSize = device.MaxWorkGroupSize;

        // Prefer 256 (8 warps) for compute-heavy kernels
        // This balances occupancy with register pressure
        if (maxWorkGroupSize >= 256)
        {
            return 256;
        }

        // Fall back to 128 (4 warps) for smaller devices
        if (maxWorkGroupSize >= 128)
        {
            return 128;
        }

        // Round down to nearest warp (32)
        // This ensures we don't waste GPU resources on partial warps
        var warpCount = (int)(maxWorkGroupSize / 32);
        return warpCount > 0 ? warpCount * 32 : 32;
    }

    /// <inheritdoc/>
    public long GetOptimalLocalMemorySize(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA GPUs have 48KB shared memory per SM (configurable)
        // Use conservative 32KB to leave room for registers and L1 cache
        // On Ampere and newer, 100KB is available but we stay conservative
        const long conservativeLimit = 32 * 1024;
        return Math.Min(conservativeLimit, (long)device.LocalMemorySize);
    }

    /// <inheritdoc/>
    public QueueProperties ApplyVendorOptimizations(QueueProperties properties, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA GPUs benefit significantly from out-of-order execution
        // This allows the GPU to find more parallelism across kernel invocations
        return properties with
        {
            InOrderExecution = false  // Out-of-order for better GPU utilization
        };
    }

    /// <inheritdoc/>
    public string GetCompilerOptions(bool enableOptimizations)
    {
        if (!enableOptimizations)
        {
            return string.Empty;
        }

        // NVIDIA-specific optimizations:
        // -cl-mad-enable: Allows a * b + c to be replaced by a mad instruction
        // -cl-fast-relaxed-math: Allows aggressive math optimizations
        // -cl-denorms-are-zero: Treat denormalized numbers as zero (performance boost)
        return "-cl-mad-enable -cl-fast-relaxed-math -cl-denorms-are-zero";
    }

    /// <inheritdoc/>
    public bool IsExtensionReliable(string extension, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA's OpenCL implementation is mature and well-tested
        // All standard extensions are reliable
        return extension switch
        {
            // Double precision supported on all compute-capable cards
            "cl_khr_fp64" => true,

            // Half precision supported on Maxwell and newer
            "cl_khr_fp16" => true,

            // NVIDIA-specific extensions
            "cl_nv_device_attribute_query" => true,
            "cl_nv_pragma_unroll" => true,
            "cl_nv_compiler_options" => true,

            // All other extensions are generally reliable
            _ => true
        };
    }

    /// <inheritdoc/>
    public int GetRecommendedBufferAlignment(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA prefers 128-byte alignment for coalesced global memory access
        // This matches the cache line size and ensures optimal memory transactions
        return 128;
    }

    /// <inheritdoc/>
    public bool SupportsPersistentKernels(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // NVIDIA GPUs support persistent kernels well
        // Require at least 8 SMs for effective persistent kernel usage
        // This ensures enough parallelism to keep the GPU busy
        return device.MaxComputeUnits >= 8;
    }
}
