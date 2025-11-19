// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Compilation;

/// <summary>
/// Tests for <see cref="RingKernelDiscovery"/>.
/// </summary>
public class RingKernelDiscoveryTests
{
    #region Discovery Tests

    [Fact]
    public void DiscoverKernels_WithValidKernel_FindsKernel()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernels = discovery.DiscoverKernels(new[] { assembly });

        // Assert
        Assert.NotEmpty(kernels);
        Assert.Contains(kernels, k => k.KernelId == "TestKernel");
    }

    [Fact]
    public void DiscoverKernels_WithNoAssemblies_ScansAllLoadedAssemblies()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);

        // Act
        var kernels = discovery.DiscoverKernels();

        // Assert
        // Should find kernels in current assembly since it's loaded
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void DiscoverKernelById_WithValidId_FindsKernel()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("TestKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal("TestKernel", kernel.KernelId);
    }

    [Fact]
    public void DiscoverKernelById_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("NonExistentKernel", new[] { assembly });

        // Assert
        Assert.Null(kernel);
    }

    [Fact]
    public void DiscoverKernelById_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => discovery.DiscoverKernelById(null!));
        Assert.Throws<ArgumentException>(() => discovery.DiscoverKernelById(string.Empty));
        Assert.Throws<ArgumentException>(() => discovery.DiscoverKernelById("   "));
    }

    #endregion

    #region Registry Tests

    [Fact]
    public void GetKernelFromRegistry_AfterDiscovery_ReturnsKernel()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();
        discovery.DiscoverKernels(new[] { assembly });

        // Act
        var kernel = discovery.GetKernelFromRegistry("TestKernel");

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal("TestKernel", kernel.KernelId);
    }

    [Fact]
    public void GetKernelFromRegistry_BeforeDiscovery_ReturnsNull()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);

        // Act
        var kernel = discovery.GetKernelFromRegistry("TestKernel");

        // Assert
        Assert.Null(kernel);
    }

    [Fact]
    public void GetAllRegisteredKernels_AfterDiscovery_ReturnsAllKernels()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();
        discovery.DiscoverKernels(new[] { assembly });

        // Act
        var kernels = discovery.GetAllRegisteredKernels();

        // Assert
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void ClearRegistry_AfterDiscovery_ClearsAllKernels()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();
        discovery.DiscoverKernels(new[] { assembly });

        // Act
        discovery.ClearRegistry();
        var kernels = discovery.GetAllRegisteredKernels();

        // Assert
        Assert.Empty(kernels);
    }

    [Fact]
    public void DiscoverKernelById_UsesRegistryCache_OnSecondCall()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel1 = discovery.DiscoverKernelById("TestKernel", new[] { assembly });
        var kernel2 = discovery.DiscoverKernelById("TestKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel1);
        Assert.NotNull(kernel2);
        Assert.Same(kernel1, kernel2); // Should be same instance from cache
    }

    #endregion

    #region Metadata Extraction Tests

    [Fact]
    public void DiscoverKernels_ExtractsAttributeProperties_Correctly()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("ConfiguredKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal(2048, kernel.Capacity);
        Assert.Equal(512, kernel.InputQueueSize);
        Assert.Equal(512, kernel.OutputQueueSize);
        Assert.Equal(RingKernelMode.EventDriven, kernel.Mode);
        Assert.Equal(MessagePassingStrategy.AtomicQueue, kernel.MessagingStrategy);
        Assert.Equal(RingKernelDomain.GraphAnalytics, kernel.Domain);
    }

    [Fact]
    public void DiscoverKernels_UsesDefaultKernelId_WhenNotSpecified()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("TestKernelMethods_NoIdKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal("TestKernelMethods_NoIdKernel", kernel.KernelId);
    }

    [Fact]
    public void DiscoverKernels_ExtractsParameterMetadata_Correctly()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("TestKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.NotEmpty(kernel.Parameters);

        // Check for buffer parameters
        var bufferParams = kernel.Parameters.Where(p => p.IsBuffer).ToList();
        Assert.NotEmpty(bufferParams);

        // Check for read-only parameters
        var readOnlyParams = kernel.Parameters.Where(p => p.IsReadOnly).ToList();
        Assert.NotEmpty(readOnlyParams);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void DiscoverKernels_IgnoresNonStaticMethods()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("NonStaticKernel", new[] { assembly });

        // Assert
        Assert.Null(kernel); // Should not discover non-static methods
    }

    [Fact]
    public void DiscoverKernels_IgnoresNonVoidMethods()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("NonVoidKernel", new[] { assembly });

        // Assert
        Assert.Null(kernel); // Should not discover non-void methods
    }

    [Fact]
    public void DiscoverKernels_IgnoresNonPublicMethods()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("PrivateKernel", new[] { assembly });

        // Assert
        Assert.Null(kernel); // Should not discover non-public methods
    }

    #endregion

    #region Helper Test Classes

    /// <summary>
    /// Test methods with various [RingKernel] configurations.
    /// </summary>
    private class TestKernelMethods
    {
        [RingKernel(KernelId = "TestKernel")]
        public static void ValidKernel(
            Span<int> data,
            ReadOnlySpan<float> input,
            int scalar)
        {
        }

        [RingKernel(
            KernelId = "ConfiguredKernel",
            Capacity = 2048,
            InputQueueSize = 512,
            OutputQueueSize = 512,
            Mode = RingKernelMode.EventDriven,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.GraphAnalytics)]
        public static void ConfiguredKernel(Span<int> data)
        {
        }

        [RingKernel]
        public static void NoIdKernel(Span<int> data)
        {
        }

        // Invalid kernels (should be ignored)

        [RingKernel(KernelId = "NonStaticKernel")]
        public void NonStaticMethod(Span<int> data)
        {
        }

        [RingKernel(KernelId = "NonVoidKernel")]
        public static int NonVoidMethod(Span<int> data)
        {
            return 0;
        }

        [RingKernel(KernelId = "PrivateKernel")]
        private static void PrivateMethod(Span<int> data)
        {
        }
    }

    #endregion
}
