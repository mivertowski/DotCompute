using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA;
using DotCompute.Core.Abstractions;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests;

public class CudaMemoryTests : IDisposable
{
    private readonly CudaBackend _backend;
    private readonly bool _isCudaAvailable;

    public CudaMemoryTests()
    {
        _backend = new CudaBackend(null!);
        _isCudaAvailable = CudaBackend.IsAvailable();
    }

    public void Dispose()
    {
        _backend?.Dispose();
    }

    [SkippableFact]
    public void CudaDeviceMemory_Creation_ValidatesParameters()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange & Act & Assert
        var action = () => new CudaDeviceMemory(IntPtr.Zero, 1024);
        action.Should().Throw<ArgumentException>();

        action = () => new CudaDeviceMemory(new IntPtr(1), 0);
        action.Should().Throw<ArgumentOutOfRangeException>();

        action = () => new CudaDeviceMemory(new IntPtr(1), -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [SkippableFact]
    public void CudaDeviceMemory_Properties_ReturnCorrectValues()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var memory = _backend.AllocateMemory(2048);

        // Assert
        memory.Size.Should().Be(2048);
        memory.IsDisposed.Should().BeFalse();
        memory.Handle.Should().NotBe(IntPtr.Zero);
    }

    [SkippableFact]
    public void CudaDeviceMemory_Dispose_DisposesCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var memory = _backend.AllocateMemory(1024);

        // Act
        memory.Dispose();

        // Assert
        memory.IsDisposed.Should().BeTrue();
        memory.Handle.Should().Be(IntPtr.Zero);
        
        // Double dispose should not throw
        var action = () => memory.Dispose();
        action.Should().NotThrow();
    }

    [SkippableFact]
    public async Task MemoryOperations_AfterDispose_ThrowException()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var memory = _backend.AllocateMemory(1024);
        var data = new float[256];
        memory.Dispose();

        // Act & Assert
        var action = async () => await _backend.CopyToDeviceAsync(data, memory);
        await action.Should().ThrowAsync<ObjectDisposedException>();

        action = async () => await _backend.CopyFromDeviceAsync(memory, data);
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [SkippableFact]
    public async Task CopyOperations_DifferentDataTypes_WorkCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Test with different data types
        await TestCopyOperations<byte>();
        await TestCopyOperations<short>();
        await TestCopyOperations<int>();
        await TestCopyOperations<long>();
        await TestCopyOperations<float>();
        await TestCopyOperations<double>();
    }

    private async Task TestCopyOperations<T>() where T : struct
    {
        // Arrange
        const int count = 100;
        var size = count * Marshal.SizeOf<T>();
        var data = new T[count];
        var memory = _backend.AllocateMemory(size);

        // Initialize data
        for (int i = 0; i < count; i++)
        {
            data[i] = (T)Convert.ChangeType(i, typeof(T));
        }

        // Act
        await _backend.CopyToDeviceAsync(data, memory);
        var result = new T[count];
        await _backend.CopyFromDeviceAsync(memory, result);

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    [SkippableFact]
    public async Task MemoryCopy_PartialCopy_WorksCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int totalSize = 1000;
        const int partialSize = 250;
        var fullData = new float[totalSize];
        var partialData = new float[partialSize];
        
        for (int i = 0; i < totalSize; i++)
        {
            fullData[i] = i;
        }

        var memory = _backend.AllocateMemory(totalSize * sizeof(float));

        // Act - Copy full data
        await _backend.CopyToDeviceAsync(fullData, memory);
        
        // Copy partial data back
        await _backend.CopyFromDeviceAsync(memory, partialData, 0, partialSize * sizeof(float));

        // Assert
        for (int i = 0; i < partialSize; i++)
        {
            partialData[i].Should().Be(fullData[i]);
        }
    }

    [SkippableFact]
    public async Task MemoryCopy_WithOffset_WorksCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int size = 100;
        const int offset = 25;
        var sourceData = new float[size];
        var targetData = new float[size - offset];
        
        for (int i = 0; i < size; i++)
        {
            sourceData[i] = i * 2;
        }

        var memory = _backend.AllocateMemory(size * sizeof(float));

        // Act
        await _backend.CopyToDeviceAsync(sourceData, memory);
        await _backend.CopyFromDeviceAsync(memory, targetData, offset * sizeof(float), targetData.Length * sizeof(float));

        // Assert
        for (int i = 0; i < targetData.Length; i++)
        {
            targetData[i].Should().Be(sourceData[i + offset]);
        }
    }

    [SkippableFact]
    public void MemoryAllocation_Alignment_IsCorrect()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // CUDA typically aligns to 256 bytes
        var sizes = new[] { 1, 17, 255, 256, 257, 1024, 4096 };

        foreach (var size in sizes)
        {
            var memory = _backend.AllocateMemory(size);
            
            // Memory should be allocated successfully regardless of size
            memory.Should().NotBeNull();
            memory.Size.Should().Be(size);
            
            // Check if handle is aligned (implementation specific)
            var address = memory.Handle.ToInt64();
            (address % 256).Should().Be(0, $"Memory should be aligned for size {size}");
        }
    }

    [SkippableFact]
    public async Task ConcurrentMemoryOperations_WorkCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int operationCount = 10;
        const int dataSize = 1000;
        var tasks = new Task[operationCount];

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                var data = new float[dataSize];
                for (int j = 0; j < dataSize; j++)
                {
                    data[j] = index * dataSize + j;
                }

                var memory = _backend.AllocateMemory(dataSize * sizeof(float));
                await _backend.CopyToDeviceAsync(data, memory);
                
                var result = new float[dataSize];
                await _backend.CopyFromDeviceAsync(memory, result);
                
                result.Should().BeEquivalentTo(data);
                memory.Dispose();
            });
        }

        // Assert
        await Task.WhenAll(tasks);
    }

    [SkippableFact]
    public void MemoryPressure_AllocationAndDeallocation_HandlesCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var (_, total) = _backend.GetMemoryInfo();
        var chunkSize = Math.Min(total / 100, 10 * 1024 * 1024); // 10MB chunks or 1% of total
        var allocations = new List<IDeviceMemory>();

        try
        {
            // Act - Allocate until we can't
            while (true)
            {
                try
                {
                    var memory = _backend.AllocateMemory(chunkSize);
                    allocations.Add(memory);
                    
                    if (allocations.Count > 1000) // Safety limit
                        break;
                }
                catch (ComputeException)
                {
                    // Expected when we run out of memory
                    break;
                }
            }

            // Assert
            allocations.Should().NotBeEmpty();
            
            // Free half the allocations
            for (int i = 0; i < allocations.Count / 2; i++)
            {
                allocations[i].Dispose();
            }

            // Should be able to allocate again
            var newMemory = _backend.AllocateMemory(chunkSize);
            newMemory.Should().NotBeNull();
            newMemory.Dispose();
        }
        finally
        {
            // Cleanup
            foreach (var allocation in allocations)
            {
                if (!allocation.IsDisposed)
                    allocation.Dispose();
            }
        }
    }

    [SkippableFact]
    public async Task ZeroCopy_PinnedMemory_WorksCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int size = 1000;
        var data = new float[size];
        for (int i = 0; i < size; i++)
        {
            data[i] = i * 3.14f;
        }

        // Pin the memory
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var memory = _backend.AllocateMemory(size * sizeof(float));

            // Act
            await _backend.CopyToDeviceAsync(data, memory);
            
            // Modify original array
            data[0] = 999.0f;
            
            // Copy back
            var result = new float[size];
            await _backend.CopyFromDeviceAsync(memory, result);

            // Assert - Device memory should have original value
            result[0].Should().NotBe(999.0f);
            result[0].Should().Be(0.0f);
        }
        finally
        {
            handle.Free();
        }
    }
}