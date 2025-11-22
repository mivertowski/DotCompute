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

        // Act & Assert - null throws ArgumentNullException, empty/whitespace throws ArgumentException
        Assert.ThrowsAny<ArgumentException>(() => discovery.DiscoverKernelById(null!));
        Assert.ThrowsAny<ArgumentException>(() => discovery.DiscoverKernelById(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => discovery.DiscoverKernelById("   "));
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

    #region Orleans.GpuBridge.Core Attribute Tests

    [Fact]
    public void DiscoverKernels_WithEnableTimestamps_CapturesProperty()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("TimestampKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.True(kernel.EnableTimestamps);
    }

    [Fact]
    public void DiscoverKernels_WithProcessingMode_CapturesProperty()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var batchKernel = discovery.DiscoverKernelById("BatchProcessingKernel", new[] { assembly });
        var adaptiveKernel = discovery.DiscoverKernelById("AdaptiveProcessingKernel", new[] { assembly });

        // Assert
        Assert.NotNull(batchKernel);
        Assert.Equal(Abstractions.RingKernels.RingProcessingMode.Batch, batchKernel.ProcessingMode);

        Assert.NotNull(adaptiveKernel);
        Assert.Equal(Abstractions.RingKernels.RingProcessingMode.Adaptive, adaptiveKernel.ProcessingMode);
    }

    [Fact]
    public void DiscoverKernels_WithMaxMessagesPerIteration_CapturesProperty()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("FairnessKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal(16, kernel.MaxMessagesPerIteration);
    }

    [Fact]
    public void DiscoverKernels_WithMessageQueueSize_OverridesInputOutputSizes()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("UnifiedQueueKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.Equal(1024, kernel.InputQueueSize);
        Assert.Equal(1024, kernel.OutputQueueSize);
        Assert.Equal(1024, kernel.MessageQueueSize);
    }

    [Fact]
    public void DiscoverKernels_WithBarriers_CapturesProperties()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var threadBlockKernel = discovery.DiscoverKernelById("BarrierThreadBlockKernel", new[] { assembly });
        var gridKernel = discovery.DiscoverKernelById("BarrierGridKernel", new[] { assembly });

        // Assert
        Assert.NotNull(threadBlockKernel);
        Assert.True(threadBlockKernel.UseBarriers);
        Assert.Equal(Abstractions.Barriers.BarrierScope.ThreadBlock, threadBlockKernel.BarrierScope);

        Assert.NotNull(gridKernel);
        Assert.True(gridKernel.UseBarriers);
        Assert.Equal(Abstractions.Barriers.BarrierScope.Grid, gridKernel.BarrierScope);
    }

    [Fact]
    public void DiscoverKernels_WithMemoryConsistency_CapturesProperty()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var releaseAcquireKernel = discovery.DiscoverKernelById("ReleaseAcquireKernel", new[] { assembly });
        var sequentialKernel = discovery.DiscoverKernelById("SequentialKernel", new[] { assembly });

        // Assert
        Assert.NotNull(releaseAcquireKernel);
        Assert.Equal(Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire, releaseAcquireKernel.MemoryConsistency);

        Assert.NotNull(sequentialKernel);
        Assert.Equal(Abstractions.Memory.MemoryConsistencyModel.Sequential, sequentialKernel.MemoryConsistency);
    }

    [Fact]
    public void DiscoverKernels_WithEnableCausalOrdering_CapturesProperty()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("CausalOrderingKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.True(kernel.EnableCausalOrdering);
    }

    [Fact]
    public void DiscoverKernels_WithAllNewAttributes_CapturesAllProperties()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var kernel = discovery.DiscoverKernelById("ComprehensiveKernel", new[] { assembly });

        // Assert
        Assert.NotNull(kernel);
        Assert.True(kernel.EnableTimestamps);
        Assert.Equal(Abstractions.RingKernels.RingProcessingMode.Adaptive, kernel.ProcessingMode);
        Assert.Equal(32, kernel.MaxMessagesPerIteration);
        Assert.Equal(2048, kernel.MessageQueueSize);
        Assert.True(kernel.UseBarriers);
        Assert.Equal(Abstractions.Barriers.BarrierScope.ThreadBlock, kernel.BarrierScope);
        Assert.Equal(Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire, kernel.MemoryConsistency);
        Assert.True(kernel.EnableCausalOrdering);
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

        // Orleans.GpuBridge.Core integration test kernels

        [RingKernel(KernelId = "TimestampKernel", EnableTimestamps = true)]
        public static void TimestampKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "BatchProcessingKernel", ProcessingMode = Abstractions.RingKernels.RingProcessingMode.Batch)]
        public static void BatchProcessingKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "AdaptiveProcessingKernel", ProcessingMode = Abstractions.RingKernels.RingProcessingMode.Adaptive)]
        public static void AdaptiveProcessingKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "FairnessKernel", MaxMessagesPerIteration = 16)]
        public static void FairnessKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "UnifiedQueueKernel", MessageQueueSize = 1024)]
        public static void UnifiedQueueKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "BarrierThreadBlockKernel", UseBarriers = true, BarrierScope = Abstractions.Barriers.BarrierScope.ThreadBlock)]
        public static void BarrierThreadBlockKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "BarrierGridKernel", UseBarriers = true, BarrierScope = Abstractions.Barriers.BarrierScope.Grid)]
        public static void BarrierGridKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "ReleaseAcquireKernel", MemoryConsistency = Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire)]
        public static void ReleaseAcquireKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "SequentialKernel", MemoryConsistency = Abstractions.Memory.MemoryConsistencyModel.Sequential)]
        public static void SequentialKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "CausalOrderingKernel", EnableCausalOrdering = true)]
        public static void CausalOrderingKernel(Span<int> data)
        {
        }

        [RingKernel(
            KernelId = "ComprehensiveKernel",
            EnableTimestamps = true,
            ProcessingMode = Abstractions.RingKernels.RingProcessingMode.Adaptive,
            MaxMessagesPerIteration = 32,
            MessageQueueSize = 2048,
            UseBarriers = true,
            BarrierScope = Abstractions.Barriers.BarrierScope.ThreadBlock,
            MemoryConsistency = Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire,
            EnableCausalOrdering = true)]
        public static void ComprehensiveKernel(Span<int> data)
        {
        }
    }

    #endregion
}
