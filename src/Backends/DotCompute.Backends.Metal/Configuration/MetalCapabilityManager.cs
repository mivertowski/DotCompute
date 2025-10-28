// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Configuration;

/// <summary>
/// Centralized manager for Metal GPU family detection and feature availability.
/// Handles device capability queries and feature tier detection.
/// This is the single source of truth for Metal capabilities.
/// </summary>
public static class MetalCapabilityManager
{
    private static readonly ILogger _logger;
    private static CachedCapabilities? _cachedCapabilities;
    private static readonly Lock _lock = new();

    static MetalCapabilityManager()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger("MetalCapabilityManager");
    }

    /// <summary>
    /// Gets the Metal GPU family and features for the system default device.
    /// This is the SINGLE source of truth for Metal capability detection.
    /// </summary>
    public static MetalCapabilities GetCapabilities()
    {
        lock (_lock)
        {
            if (_cachedCapabilities != null)
            {
                return _cachedCapabilities.Capabilities;
            }

            try
            {
                // Check if Metal is supported
                if (!MetalNative.IsMetalSupported())
                {
                    _logger.LogWarning("Metal is not supported on this system");
                    return CreateFallbackCapabilities();
                }

                // Get system default device
                var device = MetalNative.CreateSystemDefaultDevice();
                if (device == IntPtr.Zero)
                {
                    _logger.LogWarning("Failed to create Metal device, using fallback capabilities");
                    return CreateFallbackCapabilities();
                }

                try
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(device);
                    var familiesString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";
                    var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown";

                    var capabilities = BuildCapabilities(deviceInfo, familiesString, deviceName);
                    _cachedCapabilities = new CachedCapabilities { Capabilities = capabilities };

                    _logger.LogInformation(
                        "Metal capabilities detected: Device={DeviceName}, Family={Family}, Tier={Tier}, UnifiedMemory={UnifiedMemory}",
                        deviceName, capabilities.GpuFamily, capabilities.FeatureTier, capabilities.HasUnifiedMemory);

                    return capabilities;
                }
                finally
                {
                    MetalNative.ReleaseDevice(device);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect Metal capabilities, using fallback");
                return CreateFallbackCapabilities();
            }
        }
    }

    /// <summary>
    /// Checks if a specific GPU family is supported.
    /// </summary>
    public static bool SupportsGpuFamily(MetalGpuFamily family)
    {
        var capabilities = GetCapabilities();
        return capabilities.SupportedFamilies.Contains(family);
    }

    /// <summary>
    /// Checks if argument buffers (Tier 2 feature) are supported.
    /// </summary>
    public static bool SupportsArgumentBuffers()
    {
        var capabilities = GetCapabilities();
        return capabilities.FeatureTier >= MetalFeatureTier.Tier2;
    }

    /// <summary>
    /// Checks if the device has unified memory (Apple Silicon).
    /// </summary>
    public static bool HasUnifiedMemory()
    {
        var capabilities = GetCapabilities();
        return capabilities.HasUnifiedMemory;
    }

    /// <summary>
    /// Gets the maximum threads per threadgroup.
    /// </summary>
    public static int GetMaxThreadsPerThreadgroup()
    {
        var capabilities = GetCapabilities();
        return (int)capabilities.MaxThreadsPerThreadgroup;
    }

    /// <summary>
    /// Gets the maximum buffer length.
    /// </summary>
    public static long GetMaxBufferLength()
    {
        var capabilities = GetCapabilities();
        return (long)capabilities.MaxBufferLength;
    }

    /// <summary>
    /// Clears the cached capabilities, forcing re-detection on next access.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cachedCapabilities = null;
            _logger.LogDebug("Cleared Metal capability cache");
        }
    }

    private static MetalCapabilities BuildCapabilities(MetalDeviceInfo deviceInfo, string familiesString, string deviceName)
    {
        var families = ParseGpuFamilies(familiesString);
        var gpuFamily = DetermineGpuFamily(families, familiesString);
        var featureTier = DetermineFeatureTier(families, deviceInfo);

        return new MetalCapabilities
        {
            DeviceName = deviceName,
            GpuFamily = gpuFamily,
            FeatureTier = featureTier,
            SupportedFamilies = families,
            MaxThreadsPerThreadgroup = deviceInfo.MaxThreadsPerThreadgroup,
            MaxThreadgroupSize = deviceInfo.MaxThreadgroupSize,
            MaxBufferLength = deviceInfo.MaxBufferLength,
            RecommendedMaxWorkingSetSize = deviceInfo.RecommendedMaxWorkingSetSize,
            HasUnifiedMemory = deviceInfo.HasUnifiedMemory,
            IsLowPower = deviceInfo.IsLowPower,
            IsRemovable = deviceInfo.IsRemovable,
            RegistryID = deviceInfo.RegistryID,
            Location = deviceInfo.Location,
            LocationNumber = deviceInfo.LocationNumber
        };
    }

    private static HashSet<MetalGpuFamily> ParseGpuFamilies(string familiesString)
    {
        var families = new HashSet<MetalGpuFamily>();

        // Parse Apple families (Apple1-9)
        for (var i = 1; i <= 9; i++)
        {
            if (familiesString.Contains($"Apple{i}", StringComparison.Ordinal))
            {
                _ = families.Add((MetalGpuFamily)i);
            }
        }

        // Parse Mac families (Mac1-2)
        if (familiesString.Contains("Mac1", StringComparison.Ordinal))
        {
            _ = families.Add(MetalGpuFamily.Mac1);
        }
        if (familiesString.Contains("Mac2", StringComparison.Ordinal))
        {
            _ = families.Add(MetalGpuFamily.Mac2);
        }

        // Parse Common families
        if (familiesString.Contains("Common1", StringComparison.Ordinal))
        {
            _ = families.Add(MetalGpuFamily.Common1);
        }
        if (familiesString.Contains("Common2", StringComparison.Ordinal))
        {
            _ = families.Add(MetalGpuFamily.Common2);
        }
        if (familiesString.Contains("Common3", StringComparison.Ordinal))
        {
            _ = families.Add(MetalGpuFamily.Common3);
        }

        return families;
    }

    private static MetalGpuFamily DetermineGpuFamily(HashSet<MetalGpuFamily> families, string familiesString)
    {
        // Check for latest Apple Silicon first (highest to lowest)
        if (families.Contains(MetalGpuFamily.Apple9)) return MetalGpuFamily.Apple9; // M4 family
        if (families.Contains(MetalGpuFamily.Apple8)) return MetalGpuFamily.Apple8; // M3 family
        if (families.Contains(MetalGpuFamily.Apple7)) return MetalGpuFamily.Apple7; // M2 family
        if (families.Contains(MetalGpuFamily.Apple6)) return MetalGpuFamily.Apple6; // M1 family
        if (families.Contains(MetalGpuFamily.Apple5)) return MetalGpuFamily.Apple5; // A13 family
        if (families.Contains(MetalGpuFamily.Apple4)) return MetalGpuFamily.Apple4; // A12 family
        if (families.Contains(MetalGpuFamily.Apple3)) return MetalGpuFamily.Apple3; // A11 family
        if (families.Contains(MetalGpuFamily.Apple2)) return MetalGpuFamily.Apple2; // A10 family
        if (families.Contains(MetalGpuFamily.Apple1)) return MetalGpuFamily.Apple1; // A7-A9 family

        // Check for Mac families
        if (families.Contains(MetalGpuFamily.Mac2)) return MetalGpuFamily.Mac2; // Modern Intel Mac
        if (families.Contains(MetalGpuFamily.Mac1)) return MetalGpuFamily.Mac1; // Older Intel Mac

        // Fallback to Common families
        if (families.Contains(MetalGpuFamily.Common3)) return MetalGpuFamily.Common3;
        if (families.Contains(MetalGpuFamily.Common2)) return MetalGpuFamily.Common2;
        if (families.Contains(MetalGpuFamily.Common1)) return MetalGpuFamily.Common1;

        return MetalGpuFamily.Unknown;
    }

    private static MetalFeatureTier DetermineFeatureTier(HashSet<MetalGpuFamily> families, MetalDeviceInfo deviceInfo)
    {
        // Apple7+ (M2+) and Apple6 (M1) support Tier 2 features
        if (families.Contains(MetalGpuFamily.Apple9) ||
            families.Contains(MetalGpuFamily.Apple8) ||
            families.Contains(MetalGpuFamily.Apple7) ||
            families.Contains(MetalGpuFamily.Apple6))
        {
            return MetalFeatureTier.Tier2;
        }

        // Apple4+ (A12+) supports Tier 1 features
        if (families.Contains(MetalGpuFamily.Apple5) ||
            families.Contains(MetalGpuFamily.Apple4))
        {
            return MetalFeatureTier.Tier1;
        }

        // Mac2 (Modern Intel) supports Tier 1
        if (families.Contains(MetalGpuFamily.Mac2))
        {
            return MetalFeatureTier.Tier1;
        }

        // Everything else is basic
        return MetalFeatureTier.Basic;
    }

    private static MetalCapabilities CreateFallbackCapabilities()
    {
        var capabilities = new MetalCapabilities
        {
            DeviceName = "Fallback (Metal Unavailable)",
            GpuFamily = MetalGpuFamily.Unknown,
            FeatureTier = MetalFeatureTier.Basic,
            SupportedFamilies = [],
            MaxThreadsPerThreadgroup = 1024,
            MaxThreadgroupSize = 1024,
            MaxBufferLength = 256 * 1024 * 1024, // 256MB fallback
            RecommendedMaxWorkingSetSize = 256 * 1024 * 1024,
            HasUnifiedMemory = false,
            IsLowPower = false,
            IsRemovable = false,
            RegistryID = 0,
            Location = MetalDeviceLocation.Unspecified,
            LocationNumber = 0
        };

        _cachedCapabilities = new CachedCapabilities { Capabilities = capabilities };
        return capabilities;
    }

    private class CachedCapabilities
    {
        public required MetalCapabilities Capabilities { get; init; }
    }
}

/// <summary>
/// Represents Metal device capabilities.
/// </summary>
public sealed class MetalCapabilities
{
    /// <summary>Gets the device name.</summary>
    public required string DeviceName { get; init; }

    /// <summary>Gets the primary GPU family.</summary>
    public required MetalGpuFamily GpuFamily { get; init; }

    /// <summary>Gets the feature tier.</summary>
    public required MetalFeatureTier FeatureTier { get; init; }

    /// <summary>Gets all supported GPU families.</summary>
    public required HashSet<MetalGpuFamily> SupportedFamilies { get; init; }

    /// <summary>Gets the maximum threads per threadgroup.</summary>
    public required ulong MaxThreadsPerThreadgroup { get; init; }

    /// <summary>Gets the maximum threadgroup size.</summary>
    public required ulong MaxThreadgroupSize { get; init; }

    /// <summary>Gets the maximum buffer length.</summary>
    public required ulong MaxBufferLength { get; init; }

    /// <summary>Gets the recommended maximum working set size.</summary>
    public required ulong RecommendedMaxWorkingSetSize { get; init; }

    /// <summary>Gets whether the device has unified memory.</summary>
    public required bool HasUnifiedMemory { get; init; }

    /// <summary>Gets whether the device is low power.</summary>
    public required bool IsLowPower { get; init; }

    /// <summary>Gets whether the device is removable.</summary>
    public required bool IsRemovable { get; init; }

    /// <summary>Gets the device registry ID.</summary>
    public required ulong RegistryID { get; init; }

    /// <summary>Gets the device location.</summary>
    public required MetalDeviceLocation Location { get; init; }

    /// <summary>Gets the device location number.</summary>
    public required ulong LocationNumber { get; init; }
}

/// <summary>
/// Metal GPU family enumeration.
/// </summary>
public enum MetalGpuFamily
{
    /// <summary>Unknown or unsupported family.</summary>
    Unknown = 0,

    // Apple Silicon families
    /// <summary>Apple1 (A7-A9 processors).</summary>
    Apple1 = 1,
    /// <summary>Apple2 (A10 processors).</summary>
    Apple2 = 2,
    /// <summary>Apple3 (A11 processors).</summary>
    Apple3 = 3,
    /// <summary>Apple4 (A12 processors).</summary>
    Apple4 = 4,
    /// <summary>Apple5 (A13 processors).</summary>
    Apple5 = 5,
    /// <summary>Apple6 (M1 processors).</summary>
    Apple6 = 6,
    /// <summary>Apple7 (M2 processors).</summary>
    Apple7 = 7,
    /// <summary>Apple8 (M3 processors).</summary>
    Apple8 = 8,
    /// <summary>Apple9 (M4 processors).</summary>
    Apple9 = 9,

    // Mac families (Intel)
    /// <summary>Mac1 (Older Intel Mac GPUs).</summary>
    Mac1 = 1001,
    /// <summary>Mac2 (Modern Intel Mac GPUs).</summary>
    Mac2 = 1002,

    // Common families
    /// <summary>Common1 (Basic Metal features).</summary>
    Common1 = 3001,
    /// <summary>Common2 (Enhanced Metal features).</summary>
    Common2 = 3002,
    /// <summary>Common3 (Advanced Metal features).</summary>
    Common3 = 3003
}

/// <summary>
/// Metal feature tier enumeration.
/// </summary>
public enum MetalFeatureTier
{
    /// <summary>Basic Metal features.</summary>
    Basic = 0,
    /// <summary>Tier 1 features (A12+, Mac2).</summary>
    Tier1 = 1,
    /// <summary>Tier 2 features (M1+, argument buffers).</summary>
    Tier2 = 2
}
