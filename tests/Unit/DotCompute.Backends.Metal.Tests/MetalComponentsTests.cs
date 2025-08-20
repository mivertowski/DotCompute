// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Tests for Metal backend component instantiation and basic functionality.
/// These tests verify that components can be created without hardware dependencies.
/// </summary>
public sealed class MetalComponentsTests
{
    private readonly ILogger<MetalBackend> _backendLogger = NullLogger<MetalBackend>.Instance;
    private readonly ILogger<MetalAccelerator> _acceleratorLogger = NullLogger<MetalAccelerator>.Instance;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalBackend_CanBeInstantiated()
    {
        // Act & Assert (should not throw during instantiation)
        var backend = new MetalBackend(_backendLogger);
        
        Assert.NotNull(backend);
        Assert.Equal("Metal", backend.Name);
        Assert.Equal(AcceleratorType.Metal, backend.Type);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalBackend_Properties_HaveExpectedValues()
    {
        // Arrange
        var backend = new MetalBackend(_backendLogger);

        // Act & Assert
        Assert.Equal("Metal", backend.Name);
        Assert.Equal(AcceleratorType.Metal, backend.Type);
        Assert.NotNull(backend.SupportedPlatforms);
        Assert.Contains("macOS", backend.SupportedPlatforms);
        Assert.Contains("iOS", backend.SupportedPlatforms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "NonMacOS")]
    public async Task MetalBackend_DiscoverAccelerators_OnNonMacOS_ReturnsEmptyList()
    {
        // Skip on macOS since it would try to actually create Metal devices
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        // Arrange
        var backend = new MetalBackend(_backendLogger);

        // Act
        var accelerators = await backend.DiscoverAcceleratorsAsync();

        // Assert
        Assert.NotNull(accelerators);
        Assert.Empty(accelerators); // Should be empty on non-macOS platforms
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalAcceleratorOptions_DefaultValues_AreReasonable()
    {
        // Act
        var options = new MetalAcceleratorOptions();

        // Assert
        Assert.Equal(4L * 1024 * 1024 * 1024, options.MaxMemoryAllocation); // 4GB
        Assert.Equal(1024, options.MaxThreadgroupSize);
        Assert.False(options.PreferIntegratedGpu); // Should prefer discrete by default
        Assert.True(options.EnableMetalPerformanceShaders);
        Assert.True(options.EnableGpuFamilySpecialization);
        Assert.Equal(16, options.CommandBufferCacheSize);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalNative_Functions_AreProperlyDeclared()
    {
        // This test verifies that the P/Invoke declarations are syntactically correct
        // It doesn't actually call the native functions since we're not on macOS

        // Act & Assert - Just verify the type exists and has expected members
        var type = typeof(MetalNative);
        Assert.NotNull(type);

        // Check that key functions are declared
        var methods = type.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToHashSet();

        Assert.Contains(nameof(MetalNative.IsMetalSupported), methodNames);
        Assert.Contains(nameof(MetalNative.CreateSystemDefaultDevice), methodNames);
        Assert.Contains(nameof(MetalNative.CreateCommandQueue), methodNames);
        Assert.Contains(nameof(MetalNative.CreateBuffer), methodNames);
        Assert.Contains(nameof(MetalNative.CreateLibraryWithSource), methodNames);
        Assert.Contains(nameof(MetalNative.GetDeviceInfo), methodNames);
        Assert.Contains(nameof(MetalNative.ReleaseDevice), methodNames);
        Assert.Contains(nameof(MetalNative.ReleaseCommandQueue), methodNames);
        Assert.Contains(nameof(MetalNative.ReleaseBuffer), methodNames);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalStorageMode_EnumValues_AreCorrect()
    {
        // Act & Assert
        Assert.Equal(0, (int)MetalStorageMode.Shared);
        Assert.Equal(1, (int)MetalStorageMode.Managed);
        Assert.Equal(2, (int)MetalStorageMode.Private);
        Assert.Equal(3, (int)MetalStorageMode.Memoryless);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalDeviceLocation_EnumValues_AreCorrect()
    {
        // Act & Assert
        Assert.Equal(0, (int)MetalDeviceLocation.BuiltIn);
        Assert.Equal(1, (int)MetalDeviceLocation.Slot);
        Assert.Equal(2, (int)MetalDeviceLocation.External);
        Assert.Equal(3, (int)MetalDeviceLocation.Unspecified);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalCommandBufferStatus_EnumValues_AreCorrect()
    {
        // Act & Assert
        Assert.Equal(0, (int)MetalCommandBufferStatus.NotEnqueued);
        Assert.Equal(1, (int)MetalCommandBufferStatus.Enqueued);
        Assert.Equal(2, (int)MetalCommandBufferStatus.Committed);
        Assert.Equal(3, (int)MetalCommandBufferStatus.Scheduled);
        Assert.Equal(4, (int)MetalCommandBufferStatus.Completed);
        Assert.Equal(5, (int)MetalCommandBufferStatus.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void AcceleratorType_Metal_IsDefinedCorrectly()
    {
        // This verifies the Metal accelerator type is properly defined in the abstraction layer
        Assert.True(Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.Metal));
        Assert.Equal("Metal", AcceleratorType.Metal.ToString());
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    [Trait("Category", "Unit")]
    [Trait("Platform", "All")]
    public void MetalAcceleratorOptions_CanSetValidValues(int threadgroupSize)
    {
        // Arrange
        var options = new MetalAcceleratorOptions();

        // Act
        options.MaxThreadgroupSize = threadgroupSize;
        options.MaxMemoryAllocation = 8L * 1024 * 1024 * 1024; // 8GB
        options.PreferIntegratedGpu = true;
        options.EnableMetalPerformanceShaders = false;
        options.EnableGpuFamilySpecialization = false;
        options.CommandBufferCacheSize = 32;

        // Assert
        Assert.Equal(threadgroupSize, options.MaxThreadgroupSize);
        Assert.Equal(8L * 1024 * 1024 * 1024, options.MaxMemoryAllocation);
        Assert.True(options.PreferIntegratedGpu);
        Assert.False(options.EnableMetalPerformanceShaders);
        Assert.False(options.EnableGpuFamilySpecialization);
        Assert.Equal(32, options.CommandBufferCacheSize);
    }
}