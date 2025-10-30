// Copyright (c) Michael Ivertowski. Licensed under the MIT License.

using DotCompute.Backends.OpenCL.Models;

namespace DotCompute.Backends.OpenCL.Vendor;

/// <summary>
/// Factory for creating vendor-specific adapters based on platform detection.
/// </summary>
/// <remarks>
/// The factory maintains a registry of all supported vendor adapters and selects
/// the appropriate one based on platform characteristics. Adapters are tried in
/// priority order, with the generic fallback ensuring we always have a valid adapter.
///
/// Adapter priority:
/// 1. NVIDIA - Most common for compute workloads
/// 2. AMD - Growing compute presence
/// 3. Intel - Expanding with Arc discrete GPUs
/// 4. Generic - Universal fallback
///
/// The factory is thread-safe and uses singleton adapter instances for efficiency.
/// </remarks>
public static class VendorAdapterFactory
{
    /// <summary>
    /// Registry of all available vendor adapters, in priority order.
    /// </summary>
    /// <remarks>
    /// Adapters are stateless singletons - they can be safely shared across threads.
    /// The generic adapter is last as it always returns true for CanHandle.
    /// </remarks>
    private static readonly IOpenCLVendorAdapter[] Adapters =
    [
        new NvidiaOpenCLAdapter(),
        new AmdOpenCLAdapter(),
        new IntelOpenCLAdapter(),
        new GenericOpenCLAdapter()  // Fallback - must be last
    ];

    /// <summary>
    /// Gets the appropriate vendor adapter for the specified platform.
    /// </summary>
    /// <param name="platform">The OpenCL platform to get an adapter for.</param>
    /// <returns>The vendor-specific adapter, or generic adapter as fallback.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="platform"/> is null.</exception>
    /// <remarks>
    /// This method is thread-safe and deterministic. For the same platform,
    /// it will always return the same adapter type.
    /// </remarks>
    public static IOpenCLVendorAdapter GetAdapter(OpenCLPlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        // Try each adapter in priority order
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(platform))
            {
                return adapter;
            }
        }

        // Should never reach here due to GenericOpenCLAdapter always returning true
        // But provide a safe fallback just in case
        return new GenericOpenCLAdapter();
    }

    /// <summary>
    /// Detects the vendor type from a platform.
    /// </summary>
    /// <param name="platform">The OpenCL platform to analyze.</param>
    /// <returns>The detected vendor type.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="platform"/> is null.</exception>
    /// <remarks>
    /// This is a convenience method that returns the vendor enum rather than
    /// the full adapter instance. Useful for logging and diagnostics.
    /// </remarks>
    public static OpenCLVendor DetectVendor(OpenCLPlatformInfo platform)
    {
        var adapter = GetAdapter(platform);
        return adapter.Vendor;
    }

    /// <summary>
    /// Gets the vendor display name for a platform.
    /// </summary>
    /// <param name="platform">The OpenCL platform to query.</param>
    /// <returns>The human-readable vendor name.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="platform"/> is null.</exception>
    /// <remarks>
    /// This method is useful for displaying vendor information to users
    /// in a consistent format across all vendors.
    /// </remarks>
    public static string GetVendorName(OpenCLPlatformInfo platform)
    {
        var adapter = GetAdapter(platform);
        return adapter.VendorName;
    }

    /// <summary>
    /// Checks if a specific vendor is supported by the factory.
    /// </summary>
    /// <param name="vendor">The vendor to check.</param>
    /// <returns><c>true</c> if the vendor has a dedicated adapter; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is useful for determining if vendor-specific optimizations
    /// are available. Returns false for Unknown and Generic vendors.
    /// </remarks>
    public static bool IsVendorSupported(OpenCLVendor vendor)
    {
        return vendor switch
        {
            OpenCLVendor.NVIDIA => true,
            OpenCLVendor.AMD => true,
            OpenCLVendor.Intel => true,
            _ => false  // Unknown, Generic, or other
        };
    }

    /// <summary>
    /// Gets all registered vendor adapters.
    /// </summary>
    /// <returns>A read-only collection of all vendor adapters.</returns>
    /// <remarks>
    /// This method is primarily for testing and diagnostics.
    /// The returned collection includes the generic fallback adapter.
    /// </remarks>
    public static IReadOnlyList<IOpenCLVendorAdapter> AllAdapters => Adapters;
}
