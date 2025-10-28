// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Execution;
using DotCompute.Backends.OpenCL.Models;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Generic fallback adapter for unknown or unsupported vendors.
/// Provides safe defaults without vendor-specific optimizations.
/// </summary>
/// <remarks>
/// This adapter serves as a conservative fallback for:
/// - Unknown vendors (ARM Mali, Qualcomm Adreno, etc.)
/// - Custom OpenCL implementations
/// - Experimental hardware
/// - Software OpenCL implementations (PoCL, etc.)
///
/// Strategy:
/// - Use device-reported capabilities directly
/// - Apply conservative limits (50% of hardware max)
/// - Only trust KHR (Khronos) standard extensions
/// - Avoid aggressive optimizations
/// - Prioritize correctness over performance
///
/// This ensures DotCompute works correctly on any conformant OpenCL implementation,
/// even if performance isn't optimal.
/// </remarks>
public sealed class GenericOpenCLAdapter : IOpenCLVendorAdapter
{
    /// <inheritdoc/>
    public OpenCLVendor Vendor => OpenCLVendor.Generic;

    /// <inheritdoc/>
    public string VendorName => "Generic OpenCL";

    /// <inheritdoc/>
    public bool CanHandle(OpenCLPlatformInfo platform)
    {
        // Generic adapter is the fallback - it handles anything
        return true;
    }

    /// <inheritdoc/>
    public int GetOptimalWorkGroupSize(OpenCLDeviceInfo device, int defaultSize)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Conservative: Use device max or default, whichever is smaller
        // Don't try to guess optimal sizes for unknown hardware
        var safeSize = Math.Min(defaultSize, (int)device.MaxWorkGroupSize);

        // Ensure it's at least a reasonable minimum
        const int minWorkGroupSize = 32;
        return Math.Max(safeSize, minWorkGroupSize);
    }

    /// <inheritdoc/>
    public long GetOptimalLocalMemorySize(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Use 50% of available local memory as safe default
        // This leaves room for compiler-generated usage and other resources
        return (long)device.LocalMemorySize / 2;
    }

    /// <inheritdoc/>
    public QueueProperties ApplyVendorOptimizations(QueueProperties properties, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // No vendor-specific changes
        // Keep whatever the user/system configured
        return properties;
    }

    /// <inheritdoc/>
    public string GetCompilerOptions(bool enableOptimizations)
    {
        if (!enableOptimizations)
        {
            return string.Empty;
        }

        // Only use the most universally supported and safe optimization
        // -cl-mad-enable is widely supported and generally safe
        // Avoid aggressive optimizations that might break on unknown hardware
        return "-cl-mad-enable";
    }

    /// <inheritdoc/>
    public bool IsExtensionReliable(string extension, OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(device);

        // Conservative: Only trust KHR (Khronos) standard extensions
        // Vendor-specific extensions might not work correctly on unknown hardware
        if (extension.StartsWith("cl_khr_", StringComparison.Ordinal))
        {
            return true;
        }

        // Don't trust vendor-specific extensions on generic hardware
        return false;
    }

    /// <inheritdoc/>
    public int GetRecommendedBufferAlignment(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Safe default: 128 bytes
        // This is a reasonable compromise:
        // - Larger than typical cache lines (64 bytes)
        // - Smaller than vendor-specific preferences (256 bytes)
        // - Works well across most architectures
        return 128;
    }

    /// <inheritdoc/>
    public bool SupportsPersistentKernels(OpenCLDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Conservative: Don't assume support for advanced features
        // Persistent kernels require careful resource management
        // and may not work correctly on all hardware
        return false;
    }
}
