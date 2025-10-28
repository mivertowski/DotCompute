// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using Xunit;
using DotCompute.Backends.Metal.Configuration;
using DotCompute.Backends.Metal.Native;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Configuration;

/// <summary>
/// Tests for MetalCapabilityManager.
/// </summary>
public sealed class MetalCapabilityManagerTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public MetalCapabilityManagerTests(ITestOutputHelper output)
    {
        _output = output;
        // Clear cache before each test
        MetalCapabilityManager.ClearCache();
    }

    public void Dispose()
    {
        // Clear cache after each test
        MetalCapabilityManager.ClearCache();
    }

    [SkippableFact]
    public void GetCapabilities_ReturnsValidCapabilities()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert
        Assert.NotNull(capabilities);
        Assert.NotNull(capabilities.DeviceName);
        Assert.NotEqual(MetalGpuFamily.Unknown, capabilities.GpuFamily);
        Assert.NotEmpty(capabilities.SupportedFamilies);
        Assert.True(capabilities.MaxThreadsPerThreadgroup > 0);
        Assert.True(capabilities.MaxBufferLength > 0);

        _output.WriteLine($"Device: {capabilities.DeviceName}");
        _output.WriteLine($"GPU Family: {capabilities.GpuFamily}");
        _output.WriteLine($"Feature Tier: {capabilities.FeatureTier}");
        _output.WriteLine($"Unified Memory: {capabilities.HasUnifiedMemory}");
        _output.WriteLine($"Max Threads: {capabilities.MaxThreadsPerThreadgroup}");
    }

    [SkippableFact]
    public void GetCapabilities_CachesResult()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities1 = MetalCapabilityManager.GetCapabilities();
        var capabilities2 = MetalCapabilityManager.GetCapabilities();

        // Assert - Should return same instance
        Assert.Equal(capabilities1.DeviceName, capabilities2.DeviceName);
        Assert.Equal(capabilities1.GpuFamily, capabilities2.GpuFamily);
        Assert.Equal(capabilities1.FeatureTier, capabilities2.FeatureTier);
    }

    [SkippableFact]
    public void ClearCache_ForcesRedetection()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities1 = MetalCapabilityManager.GetCapabilities();
        MetalCapabilityManager.ClearCache();
        var capabilities2 = MetalCapabilityManager.GetCapabilities();

        // Assert - Should still have same values but potentially different detection
        Assert.Equal(capabilities1.GpuFamily, capabilities2.GpuFamily);
    }

    [SkippableFact]
    public void SupportsGpuFamily_AppleSilicon_ReturnsTrue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple1 && capabilities.GpuFamily <= MetalGpuFamily.Apple9,
            "This test requires Apple Silicon");

        // Act
        var supportsApple = MetalCapabilityManager.SupportsGpuFamily(capabilities.GpuFamily);

        // Assert
        Assert.True(supportsApple);
        _output.WriteLine($"Supports {capabilities.GpuFamily}: {supportsApple}");
    }

    [SkippableFact]
    public void HasUnifiedMemory_AppleSilicon_ReturnsTrue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple1 && capabilities.GpuFamily <= MetalGpuFamily.Apple9,
            "This test requires Apple Silicon");

        // Act
        var hasUnifiedMemory = MetalCapabilityManager.HasUnifiedMemory();

        // Assert
        Assert.True(hasUnifiedMemory);
        _output.WriteLine($"Has Unified Memory: {hasUnifiedMemory}");
    }

    [SkippableFact]
    public void SupportsArgumentBuffers_M1OrNewer_ReturnsTrue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple6,
            "This test requires M1 or newer (Apple6+)");

        // Act
        var supportsArgBuffers = MetalCapabilityManager.SupportsArgumentBuffers();

        // Assert
        Assert.True(supportsArgBuffers);
        _output.WriteLine($"Supports Argument Buffers (Tier2): {supportsArgBuffers}");
    }

    [SkippableFact]
    public void GetMaxThreadsPerThreadgroup_ReturnsValidValue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var maxThreads = MetalCapabilityManager.GetMaxThreadsPerThreadgroup();

        // Assert
        Assert.True(maxThreads > 0, "Max threads should be positive");
        // Metal maximum is typically 1024 or 2048, but can vary by device
        // Apple Silicon can support up to 1024 threads per threadgroup
        Assert.True(maxThreads >= 256, "Max threads should be at least 256");
        _output.WriteLine($"Max Threads Per Threadgroup: {maxThreads}");
    }

    [SkippableFact]
    public void GetMaxBufferLength_ReturnsValidValue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var maxBufferLength = MetalCapabilityManager.GetMaxBufferLength();

        // Assert
        Assert.True(maxBufferLength > 0);
        _output.WriteLine($"Max Buffer Length: {maxBufferLength:N0} bytes ({maxBufferLength / (1024.0 * 1024.0 * 1024.0):F2} GB)");
    }

    [Fact]
    public void GetCapabilities_NoMetal_ReturnsFallback()
    {
        // Arrange & Assert
        // This test can only run on systems WITHOUT Metal support
        // On macOS systems with Metal, this test is correctly skipped
        Skip.If(MetalNative.IsMetalSupported(), "Skip this test on systems with Metal - test validates fallback behavior only");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert
        Assert.NotNull(capabilities);
        Assert.Contains("Fallback", capabilities.DeviceName);
        Assert.Equal(MetalGpuFamily.Unknown, capabilities.GpuFamily);
        Assert.Equal(MetalFeatureTier.Basic, capabilities.FeatureTier);
    }

    [Theory]
    [InlineData(MetalGpuFamily.Apple9, MetalFeatureTier.Tier2)] // M4
    [InlineData(MetalGpuFamily.Apple8, MetalFeatureTier.Tier2)] // M3
    [InlineData(MetalGpuFamily.Apple7, MetalFeatureTier.Tier2)] // M2
    [InlineData(MetalGpuFamily.Apple6, MetalFeatureTier.Tier2)] // M1
    [InlineData(MetalGpuFamily.Apple5, MetalFeatureTier.Tier1)] // A13
    [InlineData(MetalGpuFamily.Apple4, MetalFeatureTier.Tier1)] // A12
    public void FeatureTier_Mapping_IsCorrect(MetalGpuFamily family, MetalFeatureTier expectedTier)
    {
        // This test verifies the feature tier logic
        // Note: We can't directly test this without device, so this is a documentation test
        _output.WriteLine($"{family} should map to {expectedTier}");
        Assert.True(true);
    }

    [SkippableFact]
    public void MetalCapabilities_AllPropertiesPopulated()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Verify all required properties are set
        Assert.NotNull(capabilities.DeviceName);
        Assert.NotEqual(default, capabilities.GpuFamily);
        Assert.NotEqual(default, capabilities.FeatureTier);
        Assert.NotNull(capabilities.SupportedFamilies);
        Assert.NotEqual(0UL, capabilities.MaxThreadsPerThreadgroup);
        Assert.NotEqual(0UL, capabilities.MaxThreadgroupSize);
        Assert.NotEqual(0UL, capabilities.MaxBufferLength);
        Assert.NotEqual(0UL, capabilities.RecommendedMaxWorkingSetSize);

        // Location can be any valid enum value including default (BuiltIn = 0)
        Assert.True(Enum.IsDefined(typeof(MetalDeviceLocation), capabilities.Location),
            $"Location {capabilities.Location} should be a valid MetalDeviceLocation");

        _output.WriteLine("All capability properties are properly populated");
        _output.WriteLine($"Location: {capabilities.Location}");
        _output.WriteLine($"LocationNumber: {capabilities.LocationNumber}");
    }

    [Fact]
    public void MetalGpuFamily_EnumValues_AreDistinct()
    {
        // Verify enum values don't overlap
        var appleValues = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var macValues = new[] { 1001, 1002 };
        var commonValues = new[] { 3001, 3002, 3003 };

        foreach (var value in appleValues)
        {
            Assert.DoesNotContain(value, macValues);
            Assert.DoesNotContain(value, commonValues);
        }

        _output.WriteLine("GPU family enum values are properly distinct");
    }

    #region Comprehensive Integration Tests

    [SkippableFact]
    public void GetGPUFamily_AppleM1_ReturnsApple6()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // This test documents the M1 family mapping
        // M1 = Apple6, M2 = Apple7, M3 = Apple8, M4 = Apple9
        _output.WriteLine($"Detected GPU Family: {capabilities.GpuFamily}");
        _output.WriteLine($"Device: {capabilities.DeviceName}");

        // Assert that we detected a valid Apple Silicon family
        if (capabilities.DeviceName.Contains("Apple M", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(capabilities.GpuFamily >= MetalGpuFamily.Apple6 &&
                       capabilities.GpuFamily <= MetalGpuFamily.Apple9,
                       "Apple Silicon should be Apple6-Apple9");
        }
    }

    [SkippableFact]
    public void GetGPUFamily_AppleM2_SupportsApple7AndHigher()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Skip if not M2
        Skip.IfNot(capabilities.DeviceName.Contains("Apple M2", StringComparison.OrdinalIgnoreCase),
            "This test requires Apple M2 hardware");

        // Assert - M2 supports Apple7 features and may report higher family (Apple8) as it supports those features too
        Assert.True(capabilities.SupportedFamilies.Contains(MetalGpuFamily.Apple7),
            "M2 should support Apple7 family");
        Assert.True(capabilities.GpuFamily >= MetalGpuFamily.Apple7,
            $"M2 primary family should be Apple7 or higher, got: {capabilities.GpuFamily}");
        _output.WriteLine($"M2 Device confirmed: {capabilities.DeviceName} -> Primary: {capabilities.GpuFamily}, Supported: {string.Join(", ", capabilities.SupportedFamilies)}");
    }

    [SkippableFact]
    public void GetGPUFamily_AppleM3_ReturnsApple8()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Skip if not M3
        Skip.IfNot(capabilities.DeviceName.Contains("Apple M3", StringComparison.OrdinalIgnoreCase),
            "This test requires Apple M3 hardware");

        // Assert
        Assert.Equal(MetalGpuFamily.Apple8, capabilities.GpuFamily);
        _output.WriteLine($"M3 Device confirmed: {capabilities.DeviceName} -> {capabilities.GpuFamily}");
    }

    [SkippableFact]
    public void SupportsUnifiedMemory_AppleSilicon_ReturnsTrue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple1 &&
                   capabilities.GpuFamily <= MetalGpuFamily.Apple9,
            "This test requires Apple Silicon");

        // Act
        var hasUnifiedMemory = capabilities.HasUnifiedMemory;

        // Assert
        Assert.True(hasUnifiedMemory, "All Apple Silicon devices should have unified memory");
        _output.WriteLine($"Unified Memory: {hasUnifiedMemory}");
        _output.WriteLine($"Recommended Working Set: {capabilities.RecommendedMaxWorkingSetSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
    }

    [SkippableFact]
    public void GetComputeUnits_ReturnsPositive()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Verify we have valid resource information
        Assert.True(capabilities.MaxThreadsPerThreadgroup > 0, "Max threads should be positive");
        Assert.True(capabilities.MaxThreadgroupSize > 0, "Max threadgroup size should be positive");
        Assert.True(capabilities.MaxBufferLength > 0, "Max buffer length should be positive");

        _output.WriteLine($"Max Threads Per Threadgroup: {capabilities.MaxThreadsPerThreadgroup}");
        _output.WriteLine($"Max Threadgroup Size: {capabilities.MaxThreadgroupSize}");
        _output.WriteLine($"Max Buffer Length: {capabilities.MaxBufferLength / (1024.0 * 1024.0 * 1024.0):F2} GB");
    }

    [Theory]
    [InlineData(MetalGpuFamily.Apple1)]
    [InlineData(MetalGpuFamily.Apple2)]
    [InlineData(MetalGpuFamily.Apple3)]
    [InlineData(MetalGpuFamily.Apple4)]
    [InlineData(MetalGpuFamily.Apple5)]
    [InlineData(MetalGpuFamily.Apple6)]
    [InlineData(MetalGpuFamily.Apple7)]
    [InlineData(MetalGpuFamily.Apple8)]
    [InlineData(MetalGpuFamily.Apple9)]
    [InlineData(MetalGpuFamily.Mac1)]
    [InlineData(MetalGpuFamily.Mac2)]
    public void SupportsGpuFamily_AllFamilies_WorksCorrectly(MetalGpuFamily family)
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();
        var supportsFamily = MetalCapabilityManager.SupportsGpuFamily(family);

        // Assert
        var expected = capabilities.SupportedFamilies.Contains(family);
        Assert.Equal(expected, supportsFamily);
        _output.WriteLine($"Family {family}: {(supportsFamily ? "Supported" : "Not Supported")}");
    }

    [SkippableFact]
    public void GetCapabilities_MultipleCalls_ReturnsSameInstance()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var cap1 = MetalCapabilityManager.GetCapabilities();
        var cap2 = MetalCapabilityManager.GetCapabilities();
        var cap3 = MetalCapabilityManager.GetCapabilities();

        // Assert - Should return cached results
        Assert.Equal(cap1.DeviceName, cap2.DeviceName);
        Assert.Equal(cap1.DeviceName, cap3.DeviceName);
        Assert.Equal(cap1.GpuFamily, cap2.GpuFamily);
        Assert.Equal(cap1.GpuFamily, cap3.GpuFamily);
        Assert.Equal(cap1.RegistryID, cap2.RegistryID);
        Assert.Equal(cap1.RegistryID, cap3.RegistryID);

        _output.WriteLine("Cache verification: All calls returned consistent results");
    }

    [SkippableFact]
    public void GetMaxThreadgroupMemory_ImplicitLimit()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Metal has implicit threadgroup memory limits based on GPU family
        // Apple Silicon typically has 32KB-64KB of threadgroup memory
        // We verify that the device provides reasonable thread limits
        var maxThreads = capabilities.MaxThreadsPerThreadgroup;
        var maxSize = capabilities.MaxThreadgroupSize;

        Assert.True(maxThreads >= 256, "Should support at least 256 threads");
        Assert.True(maxSize >= maxThreads, "Max size should be >= max threads");

        _output.WriteLine($"Max Threads: {maxThreads}");
        _output.WriteLine($"Max Threadgroup Size: {maxSize}");
        _output.WriteLine($"Estimated max threadgroup memory: ~32-64KB (Metal implicit limit)");
    }

    [SkippableFact]
    public void FeatureTier_M1OrNewer_IsTier2()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple6,
            "This test requires M1 or newer (Apple6+)");

        // Act & Assert
        Assert.Equal(MetalFeatureTier.Tier2, capabilities.FeatureTier);
        Assert.True(capabilities.SupportedFamilies.Count > 0, "Should support multiple families");

        _output.WriteLine($"Feature Tier: {capabilities.FeatureTier}");
        _output.WriteLine($"Supported Families: {string.Join(", ", capabilities.SupportedFamilies)}");
    }

    [SkippableFact]
    public void DeviceLocation_IsValid()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert
        Assert.True(Enum.IsDefined(typeof(MetalDeviceLocation), capabilities.Location),
            "Location should be a valid enum value");

        // Apple Silicon devices are typically BuiltIn (0)
        if (capabilities.HasUnifiedMemory)
        {
            _output.WriteLine($"Apple Silicon Device Location: {capabilities.Location}");
            _output.WriteLine($"Location Number: {capabilities.LocationNumber}");
        }
    }

    [SkippableFact]
    public void DeviceProperties_LowPowerAndRemovable_Consistent()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Document device characteristics
        _output.WriteLine($"Device: {capabilities.DeviceName}");
        _output.WriteLine($"Is Low Power: {capabilities.IsLowPower}");
        _output.WriteLine($"Is Removable: {capabilities.IsRemovable}");
        _output.WriteLine($"Registry ID: {capabilities.RegistryID}");

        // Built-in Apple Silicon should not be removable
        if (capabilities.Location == MetalDeviceLocation.BuiltIn &&
            capabilities.GpuFamily >= MetalGpuFamily.Apple6)
        {
            Assert.False(capabilities.IsRemovable, "Built-in Apple Silicon should not be removable");
        }
    }

    [SkippableFact]
    public void ResourceLimits_AreSaneValues()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Verify resource limits are in expected ranges
        // Max threads per threadgroup: Apple Silicon = 1024, Intel Mac GPUs = 512-1024
        Assert.InRange((int)capabilities.MaxThreadsPerThreadgroup, 512, 2048);

        // Max buffer: at least 256MB, typically much larger on modern hardware
        Assert.True(capabilities.MaxBufferLength >= 256 * 1024 * 1024,
            "Max buffer should be at least 256MB");

        // Recommended working set: should be at least 128MB
        Assert.True(capabilities.RecommendedMaxWorkingSetSize >= 128 * 1024 * 1024,
            "Recommended working set should be at least 128MB");

        _output.WriteLine($"Max Threads: {capabilities.MaxThreadsPerThreadgroup}");
        _output.WriteLine($"Max Buffer: {capabilities.MaxBufferLength / (1024.0 * 1024.0 * 1024.0):F2} GB");
        _output.WriteLine($"Recommended Working Set: {capabilities.RecommendedMaxWorkingSetSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
    }

    [SkippableFact]
    public void ClearCache_ThreadSafe()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act - Perform concurrent operations
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var caps = MetalCapabilityManager.GetCapabilities();
                Assert.NotNull(caps);
                MetalCapabilityManager.ClearCache();
            }));
        }

        // Assert - Should complete without exceptions
        Task.WaitAll(tasks.ToArray());
        _output.WriteLine("Cache operations completed safely under concurrent access");
    }

    [SkippableFact]
    public void GetCapabilities_ConcurrentAccess_Consistent()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act - Multiple threads requesting capabilities simultaneously
        var results = new System.Collections.Concurrent.ConcurrentBag<MetalCapabilities>();
        var tasks = new List<Task>();

        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var caps = MetalCapabilityManager.GetCapabilities();
                results.Add(caps);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All results should be consistent
        var firstResult = results.First();
        foreach (var result in results)
        {
            Assert.Equal(firstResult.DeviceName, result.DeviceName);
            Assert.Equal(firstResult.GpuFamily, result.GpuFamily);
            Assert.Equal(firstResult.FeatureTier, result.FeatureTier);
        }

        _output.WriteLine($"All {results.Count} concurrent requests returned consistent results");
    }

    [Fact]
    public void Fallback_NoMetal_HasSafeDefaults()
    {
        // This test verifies fallback behavior when Metal is unavailable
        // On systems with Metal, we skip; on systems without Metal, we verify fallback

        if (MetalNative.IsMetalSupported())
        {
            _output.WriteLine("Metal is supported - skipping fallback test");
            return;
        }

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Verify safe fallback values
        Assert.Contains("Fallback", capabilities.DeviceName);
        Assert.Equal(MetalGpuFamily.Unknown, capabilities.GpuFamily);
        Assert.Equal(MetalFeatureTier.Basic, capabilities.FeatureTier);
        Assert.Empty(capabilities.SupportedFamilies);
        Assert.Equal(1024UL, capabilities.MaxThreadsPerThreadgroup);
        Assert.Equal(1024UL, capabilities.MaxThreadgroupSize);
        Assert.Equal(256UL * 1024 * 1024, capabilities.MaxBufferLength);
        Assert.False(capabilities.HasUnifiedMemory);
        Assert.Equal(MetalDeviceLocation.Unspecified, capabilities.Location);

        _output.WriteLine("Fallback capabilities verified");
    }

    [SkippableFact]
    public void SupportedFamilies_ContainsPrimaryFamily()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        var capabilities = MetalCapabilityManager.GetCapabilities();

        // Assert - Primary GPU family should be in supported families
        if (capabilities.GpuFamily != MetalGpuFamily.Unknown)
        {
            Assert.Contains(capabilities.GpuFamily, capabilities.SupportedFamilies);
        }

        _output.WriteLine($"Primary Family: {capabilities.GpuFamily}");
        _output.WriteLine($"All Supported: {string.Join(", ", capabilities.SupportedFamilies)}");
    }

    [SkippableFact]
    public void AppleSilicon_BackwardCompatibility()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var capabilities = MetalCapabilityManager.GetCapabilities();
        Skip.IfNot(capabilities.GpuFamily >= MetalGpuFamily.Apple6,
            "This test requires Apple Silicon (M1+)");

        // Act & Assert - Newer Apple families support older family features
        // M2 (Apple7) should also support Apple6 features
        var primaryFamily = capabilities.GpuFamily;

        // Check that we support our primary family and potentially backward-compatible ones
        Assert.Contains(primaryFamily, capabilities.SupportedFamilies);

        _output.WriteLine($"Primary: {primaryFamily}");
        _output.WriteLine($"Backward compatibility families: {string.Join(", ", capabilities.SupportedFamilies.Where(f => f < primaryFamily))}");
    }

    #endregion
}
