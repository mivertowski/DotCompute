// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Execution;
using DotCompute.Backends.OpenCL.Models;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Vendor adapter for AMD GPUs with ROCm-specific optimizations.
/// </summary>
/// <remarks>
/// AMD's OpenCL implementation has evolved through several GPU architectures:
/// - GCN (Graphics Core Next): 64-wide wavefronts, 64KB LDS per CU
/// - RDNA/RDNA2: 32-wide wavefronts (dual-issue), improved performance
/// - RDNA3: Enhanced ray tracing, AI acceleration
///
/// Architecture detection:
/// - RX 6000/7000 series: RDNA/RDNA2/RDNA3 (32-wide wavefronts)
/// - RX 5000 and older: GCN (64-wide wavefronts)
/// - MI series: GCN/CDNA (data center focused)
///
/// Optimization priorities:
/// 1. Align work groups to wavefront boundaries
/// 2. Use 256-byte alignment for optimal memory access
/// 3. Leverage out-of-order execution
/// 4. Enable aggressive math optimizations
/// </remarks>
public sealed class AmdOpenCLAdapter : IOpenCLVendorAdapter
{
    /// <inheritdoc/>
    public OpenCLVendor Vendor => OpenCLVendor.AMD;

    /// <inheritdoc/>
    public string VendorName => "Advanced Micro Devices, Inc.";

    /// <inheritdoc/>
    public bool CanHandle(OpenCLPlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return platform.Vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
               platform.Vendor.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public int GetOptimalWorkGroupSize(OpenCLDeviceInfo device, int defaultSize)
    {
        ArgumentNullException.ThrowIfNull(device);

        // AMD GPUs work best with wavefront-aligned work groups
        // RDNA: 32-wide wavefronts (RX 6000/7000 series)
        // GCN: 64-wide wavefronts (RX 5000 and older)
        var maxWorkGroupSize = device.MaxWorkGroupSize;

        // Detect architecture based on device name
        bool isRDNA = device.Name.Contains("RX 6", StringComparison.OrdinalIgnoreCase) ||
                      device.Name.Contains("RX 7", StringComparison.OrdinalIgnoreCase) ||
                      device.Name.Contains("RDNA", StringComparison.OrdinalIgnoreCase);

        int wavefrontSize = isRDNA ? 32 : 64;

        // Prefer 256 work items:
        // - GCN: 4 wavefronts (4 × 64 = 256)
        // - RDNA: 8 wavefronts (8 × 32 = 256)
        if (maxWorkGroupSize >= 256)
        {
            return 256;
        }

        // Fall back to smaller sizes aligned to wavefront
        if (maxWorkGroupSize >= 128)
        {
            return 128;
        }

        // Round down to nearest wavefront
        int wavefrontCount = (int)maxWorkGroupSize / wavefrontSize;
        return wavefrontCount > 0 ? wavefrontCount * wavefrontSize : wavefrontSize;
    }

    /// <inheritdoc/>
    public long GetOptimalLocalMemorySize(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // AMD GPUs have 64KB LDS (Local Data Share) per CU
        // Use conservative 48KB to leave room for other resources
        // RDNA3 has enhanced LDS but we stay conservative
        const long conservativeLimit = 48 * 1024;
        return Math.Min(conservativeLimit, (long)device.LocalMemorySize);
    }

    /// <inheritdoc/>
    public QueueProperties ApplyVendorOptimizations(QueueProperties properties, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // AMD GPUs benefit from out-of-order execution
        // ROCm runtime is optimized for asynchronous dispatch
        return properties with
        {
            InOrderExecution = false
        };
    }

    /// <inheritdoc/>
    public string GetCompilerOptions(bool enableOptimizations)
    {
        if (!enableOptimizations)
        {
            return string.Empty;
        }

        // AMD-specific optimizations:
        // -cl-mad-enable: Enable multiply-add fusion
        // -cl-fast-relaxed-math: Aggressive math optimizations
        // -cl-unsafe-math-optimizations: Additional optimizations (may affect precision)
        return "-cl-mad-enable -cl-fast-relaxed-math -cl-unsafe-math-optimizations";
    }

    /// <inheritdoc/>
    public bool IsExtensionReliable(string extension, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(device);

        return extension switch
        {
            // Double precision: Well supported on GCN and newer
            "cl_khr_fp64" => true,

            // Half precision: Supported on RDNA and newer
            "cl_khr_fp16" => true,

            // AMD-specific extensions
            "cl_amd_device_attribute_query" => true,
            "cl_amd_media_ops" => true,
            "cl_amd_media_ops2" => true,

            // AMD's printf can be buggy on older drivers
            // Check driver version for reliability
            "cl_amd_printf" => CheckDriverVersion(device),

            // Generally reliable
            _ => true
        };
    }

    /// <summary>
    /// Checks if the driver version is recent enough for reliable operation.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <returns><c>true</c> if the driver is reliable; otherwise, <c>false</c>.</returns>
    private static bool CheckDriverVersion(OpenCLDeviceInfo device)
    {
        // In a production system, we would parse the driver version string
        // and compare against known problematic versions
        // For now, we assume modern drivers (ROCm 5.0+) are reliable

        // Example version parsing:
        // var version = device.DriverVersion;
        // if (version.StartsWith("3.", StringComparison.Ordinal))
        //     return false; // Old driver

        return true;
    }

    /// <inheritdoc/>
    public int GetRecommendedBufferAlignment(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // AMD prefers 256-byte alignment for optimal memory access
        // This matches the memory controller granularity on RDNA
        return 256;
    }

    /// <inheritdoc/>
    public bool SupportsPersistentKernels(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // AMD GPUs support persistent kernels effectively
        // Require at least 16 CUs for good performance
        // High-end cards (RX 6800/7900) have 60-96 CUs
        return device.MaxComputeUnits >= 16;
    }
}
