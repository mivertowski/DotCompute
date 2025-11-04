// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Memory;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.Memory;

/// <summary>
/// Comprehensive tests for Metal memory management including allocation,
/// pooling, unified memory, and memory pressure scenarios.
/// </summary>
[Trait("Category", "Hardware")]
public class MetalMemoryManagementTests : MetalTestBase
{
    public MetalMemoryManagementTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task AllocateBuffer_ShouldSucceed()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1024;

        // Act
        var buffer = await accelerator!.Memory.AllocateAsync<float>(size);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(size);
        buffer.ElementSize.Should().Be(sizeof(float));

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task MultipleAllocations_ShouldSucceed()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int bufferCount = 10;
        const int size = 2048;

        // Act
        var buffers = new List<IUnifiedBuffer<float>>();
        for (int i = 0; i < bufferCount; i++)
        {
            var buffer = await accelerator!.Memory.AllocateAsync<float>(size);
            buffers.Add(buffer);
        }

        // Assert
        buffers.Should().HaveCount(bufferCount);
        foreach (var buffer in buffers)
        {
            buffer.Length.Should().Be(size);
        }

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task UnifiedMemory_OnAppleSilicon_ShouldBeEnabled()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");
        Skip.IfNot(IsAppleSilicon(), "Not running on Apple Silicon");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        // Assert - Apple Silicon should have unified memory
        accelerator!.Info.IsUnifiedMemory.Should().BeTrue("Apple Silicon uses unified memory architecture");
    }

    [SkippableFact]
    public async Task MemoryPooling_ShouldReduceAllocations()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int iterations = 100;
        const int size = 1024;

        // Act - Allocate and deallocate repeatedly
        for (int i = 0; i < iterations; i++)
        {
            var buffer = await accelerator!.Memory.AllocateAsync<float>(size);
            await buffer.DisposeAsync();
        }

        // Assert - Memory pool should have reused allocations
        // This is validated through memory manager statistics
        var memoryManager = accelerator!.Memory as MetalMemoryManager;
        if (memoryManager != null)
        {
            var stats = memoryManager.GetStatistics();
            Output.WriteLine($"Pool Statistics:");
            Output.WriteLine($"  Total Allocations: {stats.TotalAllocations}");
            Output.WriteLine($"  Pool Hits: {stats.PoolHits}");
            Output.WriteLine($"  Pool Hit Rate: {stats.PoolHitRate:P1}");

            // With pooling, not all allocations should result in new memory
            stats.PoolHitRate.Should().BeGreaterThan(0.0, "Memory pooling should reuse allocations");
        }
    }

    [SkippableFact]
    public async Task BufferCopy_HostToDevice_ShouldTransferData()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1000;
        var hostData = MetalTestDataGenerator.CreateLinearSequence(size);
        var buffer = await accelerator!.Memory.AllocateAsync<float>(size);

        // Act
        await buffer.CopyFromAsync(hostData.AsMemory());

        // Copy back to verify
        var result = new float[size];
        await buffer.CopyToAsync(result.AsMemory());

        // Assert
        result.Should().BeEquivalentTo(hostData);

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task BufferCopy_DeviceToHost_ShouldTransferData()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1000;
        var originalData = MetalTestDataGenerator.CreateRandomData(size);

        var buffer = await accelerator!.Memory.AllocateAsync<float>(size);
        await buffer.CopyFromAsync(originalData.AsMemory());

        // Act
        var copiedData = new float[size];
        await buffer.CopyToAsync(copiedData.AsMemory());

        // Assert
        copiedData.Should().BeEquivalentTo(originalData);

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task LargeAllocation_ShouldHandleGigabytes()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const long size = 256 * 1024 * 1024; // 256M floats = 1GB

        // Act
        var buffer = await accelerator!.Memory.AllocateAsync<float>(size);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(size);

        Output.WriteLine($"Successfully allocated {size * sizeof(float) / (1024.0 * 1024.0 * 1024.0):F2} GB");

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task BufferOverflow_ShouldBeDetected()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 100;
        var buffer = await accelerator!.Memory.AllocateAsync<float>(size);

        var oversizedData = new float[size + 10]; // Larger than buffer

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await buffer.CopyFromAsync(oversizedData.AsMemory());
        });

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task MemoryPressure_ManySmallAllocations_ShouldHandle()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int allocationCount = 1000;
        const int smallSize = 256;

        // Act
        var buffers = new List<IUnifiedBuffer<float>>();
        for (int i = 0; i < allocationCount; i++)
        {
            var buffer = await accelerator!.Memory.AllocateAsync<float>(smallSize);
            buffers.Add(buffer);
        }

        // Assert
        buffers.Should().HaveCount(allocationCount);

        var totalMemory = allocationCount * smallSize * sizeof(float);
        Output.WriteLine($"Allocated {allocationCount} buffers totaling {totalMemory / (1024.0 * 1024.0):F2} MB");

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task MemoryPressure_FewLargeAllocations_ShouldHandle()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int allocationCount = 5;
        const int largeSize = 64 * 1024 * 1024; // 64M floats = 256MB each

        // Act
        var buffers = new List<IUnifiedBuffer<float>>();
        for (int i = 0; i < allocationCount; i++)
        {
            var buffer = await accelerator!.Memory.AllocateAsync<float>(largeSize);
            buffers.Add(buffer);
        }

        // Assert
        buffers.Should().HaveCount(allocationCount);

        var totalMemory = (long)allocationCount * largeSize * sizeof(float);
        Output.WriteLine($"Allocated {allocationCount} large buffers totaling {totalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task MemoryStatistics_ShouldTrackUsage()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        var memoryManager = accelerator!.Memory as MetalMemoryManager;

        if (memoryManager == null)
        {
            Skip.If(true, "Memory manager doesn't support statistics");
        }

        // Act
        var buffer1 = await accelerator.Memory.AllocateAsync<float>(1000);
        var buffer2 = await accelerator.Memory.AllocateAsync<float>(2000);

        var stats = memoryManager!.GetStatistics();

        // Assert
        stats.TotalAllocations.Should().BeGreaterOrEqualTo(2);
        stats.TotalAllocatedBytes.Should().BeGreaterThan(0);

        Output.WriteLine($"Memory Statistics:");
        Output.WriteLine($"  Total Allocations: {stats.TotalAllocations}");
        Output.WriteLine($"  Total Bytes: {stats.TotalAllocatedBytes / 1024.0:F2} KB");
        Output.WriteLine($"  Active Buffers: {stats.ActiveBuffers}");

        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
    }

    [SkippableFact]
    public async Task ZeroSizeAllocation_ShouldFail()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await accelerator!.Memory.AllocateAsync<float>(0);
        });
    }

    [SkippableFact]
    public async Task NegativeSizeAllocation_ShouldFail()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await accelerator!.Memory.AllocateAsync<float>(-100);
        });
    }
}
