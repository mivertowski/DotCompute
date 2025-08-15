// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using FluentAssertions;
// Memory types are in the main namespace
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.Diagnostics;
using FluentAssertions;

namespace DotCompute.Backends.CPU.Tests.Performance;

/// <summary>
/// Performance and stress tests for CPU backend components.
/// Tests memory operations, kernel compilation performance, and concurrent access patterns.
/// </summary>
public class CpuBackendPerformanceTests : IDisposable
{
    private readonly Mock<ILogger<CpuAccelerator>> _mockLogger;
    private readonly Mock<IOptions<CpuAcceleratorOptions>> _mockOptions;
    private readonly Mock<IOptions<CpuThreadPoolOptions>> _mockThreadPoolOptions;
    private bool _disposed;

    public CpuBackendPerformanceTests()
    {
        _mockLogger = new Mock<ILogger<CpuAccelerator>>();
        _mockOptions = new Mock<IOptions<CpuAcceleratorOptions>>();
        _mockThreadPoolOptions = new Mock<IOptions<CpuThreadPoolOptions>>();

        _mockOptions.Setup(o => o.Value).Returns(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true
        });

        _mockThreadPoolOptions.Setup(o => o.Value).Returns(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount,
            MaxQueuedItems = 10000,
            EnableWorkStealing = true
        });
    }

    #region Memory Performance Tests

    [Theory]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(10 * 1024 * 1024)]
    public async Task MemoryManager_LargeBufferAllocation_ShouldCompleteWithinReasonableTime(int elementCount)
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        var stopwatch = Stopwatch.StartNew();

        // Act
        using var buffer = await memoryManager.CreateBufferAsync<float>(
            elementCount, MemoryLocation.Host, MemoryAccess.ReadWrite);

        stopwatch.Stop();

        // Assert
        Assert.NotNull(buffer);
        buffer.ElementCount.Should().Be(elementCount);
        stopwatch.ElapsedMilliseconds .Should().BeLessThan(1000,); // Should complete within 1 second
    }

    [Fact]
    public async Task MemoryManager_ManySmallAllocations_ShouldMaintainPerformance()
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        const int allocationCount = 1000;
        const int elementCount = 1024;
        var buffers = new List<IBuffer<int>>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Act
            for(int i = 0; i < allocationCount; i++)
            {
                var buffer = await memoryManager.CreateBufferAsync<int>(
                    elementCount, MemoryLocation.Host, MemoryAccess.ReadWrite);
                buffers.Add(buffer);
            }

            stopwatch.Stop();

            // Assert
            Assert.Equal(allocationCount, buffers.Count());
            stopwatch.ElapsedMilliseconds .Should().BeLessThan(5000,); // Should complete within 5 seconds
           (stopwatch.ElapsedMilliseconds /double)allocationCount).Should().BeLessThan(10); // Average <10ms per allocation
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Theory]
    [InlineData(1024 * 1024)]
    [InlineData(10 * 1024 * 1024)]
    public async Task MemoryManager_DataCopy_ShouldAchieveReasonableThroughput(int elementCount)
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        var sourceData = GenerateTestData<float>(elementCount);

        using var sourceBuffer = await memoryManager.CreateBufferAsync(
            sourceData, MemoryLocation.Host, MemoryAccess.ReadOnly);
        using var destBuffer = await memoryManager.CreateBufferAsync<float>(
            elementCount, MemoryLocation.Host, MemoryAccess.WriteOnly);

        var stopwatch = Stopwatch.StartNew();

        // Act
        await memoryManager.CopyAsync(sourceBuffer, destBuffer);

        stopwatch.Stop();

        // Assert
        var dataSizeMB =(elementCount * sizeof(float)) /(1024.0 * 1024.0);
        var throughputMBps = dataSizeMB /(stopwatch.ElapsedMilliseconds / 1000.0);
        
        Assert.True(throughputMBps > 100); // Should achieve >100 MB/s
    }

    [Fact]
    public async Task MemoryManager_ConcurrentAllocations_ShouldHandleParallelAccess()
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        const int concurrentTasks = 10;
        const int elementCount = 1024 * 1024;

        var tasks = new List<Task<IBuffer<float>>>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for(int i = 0; i < concurrentTasks; i++)
        {
            tasks.Add(memoryManager.CreateBufferAsync<float>(
                elementCount, MemoryLocation.Host, MemoryAccess.ReadWrite).AsTask());
        }

        var buffers = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        try
        {
            Assert.Equal(concurrentTasks, buffers.Count());
            buffers.Should().AllSatisfy(b => b.NotBeNull());
            stopwatch.ElapsedMilliseconds .Should().BeLessThan(10000,); // Should complete within 10 seconds
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    #endregion

    #region Kernel Compilation Performance Tests

    [Fact]
    public async Task Accelerator_KernelCompilation_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var definition = CreateComplexKernelDefinition("PerformanceTestKernel");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(definition);

        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiledKernel);
        stopwatch.ElapsedMilliseconds .Should().BeLessThan(5000,); // Should compile within 5 seconds
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Accelerator_ConcurrentKernelCompilation_ShouldScaleEffectively(int concurrentCompilations)
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var compilationTasks = new List<Task<ICompiledKernel>>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for(int i = 0; i < concurrentCompilations; i++)
        {
            var definition = CreateComplexKernelDefinition($"ConcurrentKernel{i}");
            compilationTasks.Add(accelerator.CompileKernelAsync(definition).AsTask());
        }

        var compiledKernels = await Task.WhenAll(compilationTasks);
        stopwatch.Stop();

        // Assert
        Assert.Equal(concurrentCompilations, compiledKernels.Count());
        compiledKernels.Should().AllSatisfy(k => k.NotBeNull());
        
        // Should scale reasonably - not linear due to CPU cores, but should be better than sequential
        var averageTimePerKernel = stopwatch.ElapsedMilliseconds /(double)concurrentCompilations;
        Assert.True(averageTimePerKernel < 1000); // Average <1 second per kernel
    }

    [Fact]
    public async Task Accelerator_RepeatedKernelCompilation_ShouldMaintainPerformance()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var definition = CreateComplexKernelDefinition("RepeatedTestKernel");
        const int repetitions = 100;
        var compilationTimes = new List<double>();

        // Act
        for(int i = 0; i < repetitions; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var compiledKernel = await accelerator.CompileKernelAsync(definition);
            stopwatch.Stop();

            Assert.NotNull(compiledKernel);
            compilationTimes.Add(stopwatch.ElapsedMilliseconds);

            await compiledKernel.DisposeAsync();
        }

        // Assert
        var averageTime = compilationTimes.Average();
        var maxTime = compilationTimes.Max();
        var minTime = compilationTimes.Min();

        Assert.True(averageTime < 1000); // Average <1 second
       (maxTime - minTime).Should().BeLessThan(averageTime); // Variance should be reasonable
    }

    #endregion

    #region Stress Tests

    [Fact]
    public async Task Accelerator_MemoryStressTest_ShouldHandleHighMemoryPressure()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var memoryManager = accelerator.Memory;
        const int largeBufferCount = 100;
        const int elementsPerBuffer = 1024 * 1024; // 1M floats = 4MB per buffer
        var buffers = new List<IBuffer<float>>();

        try
        {
            // Act - Allocate many large buffers
            for(int i = 0; i < largeBufferCount; i++)
            {
                var buffer = await memoryManager.CreateBufferAsync<float>(
                    elementsPerBuffer, MemoryLocation.Host, MemoryAccess.ReadWrite);
                buffers.Add(buffer);
            }

            // Assert
            Assert.Equal(largeBufferCount, buffers.Count());
            
            // Verify memory statistics are reasonable
            var statistics = memoryManager.GetStatistics();
            (statistics.TotalAllocatedBytes > 0).Should().BeTrue();
            (statistics.AllocationCount >= largeBufferCount).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Accelerator_ConcurrentOperationsStressTest_ShouldMaintainStability()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        const int operationsPerType = 25;
        var allTasks = new List<Task>();

        // Act - Mix of concurrent operations
        
        // Memory allocations
        for(int i = 0; i < operationsPerType; i++)
        {
            allTasks.Add(Task.Run(async () =>
            {
                using var buffer = await accelerator.Memory.CreateBufferAsync<int>(
                    1024, MemoryLocation.Host, MemoryAccess.ReadWrite);
            }));
        }

        // Kernel compilations
        for(int i = 0; i < operationsPerType; i++)
        {
            allTasks.Add(Task.Run(async () =>
            {
                var definition = CreateSimpleKernelDefinition($"StressKernel{i}");
                using var kernel = await accelerator.CompileKernelAsync(definition);
            }));
        }

        // Synchronization calls
        for(int i = 0; i < operationsPerType; i++)
        {
            allTasks.Add(Task.Run(async () =>
            {
                await accelerator.SynchronizeAsync();
            }));
        }

        // Memory copies
        for(int i = 0; i < operationsPerType; i++)
        {
            allTasks.Add(Task.Run(async () =>
            {
                var data = GenerateTestData<float>(1024);
                using var source = await accelerator.Memory.CreateBufferAsync(
                    data, MemoryLocation.Host);
                using var dest = await accelerator.Memory.CreateBufferAsync<float>(
                    1024, MemoryLocation.Host);
                await accelerator.Memory.CopyAsync(source, dest);
            }));
        }

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(allTasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds .Should().BeLessThan(30000,); // Should complete within 30 seconds
    }

    #endregion

    #region Disposal and Resource Management Tests

    [Fact]
    public async Task Accelerator_DisposalUnderLoad_ShouldCompleteCleanly()
    {
        // Arrange
        var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var buffers = new List<IBuffer<float>>();
        var kernels = new List<ICompiledKernel>();

        // Create some resources
        for(int i = 0; i < 10; i++)
        {
            var buffer = await accelerator.Memory.CreateBufferAsync<float>(
                1024, MemoryLocation.Host, MemoryAccess.ReadWrite);
            buffers.Add(buffer);

            var kernel = await accelerator.CompileKernelAsync(
                CreateSimpleKernelDefinition($"DisposalKernel{i}"));
            kernels.Add(kernel);
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        await accelerator.DisposeAsync();

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds .Should().BeLessThan(10000,); // Should dispose within 10 seconds

        // Clean up remaining resources
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
        foreach (var kernel in kernels)
        {
            await kernel.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryManager_MemoryFragmentation_ShouldHandlePatternedAllocations()
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        var buffers = new List<IBuffer<byte>>();
        
        const int iterations = 50;
        var random = new Random(42); // Fixed seed for reproducibility

        try
        {
            // Act - Create and destroy buffers in random patterns to test fragmentation handling
            for(int i = 0; i < iterations; i++)
            {
                // Allocate some buffers
                for(int j = 0; j < 5; j++)
                {
                    var size = random.Next(1024, 1024 * 1024);
                    var buffer = await memoryManager.CreateBufferAsync<byte>(
                        size, MemoryLocation.Host, MemoryAccess.ReadWrite);
                    buffers.Add(buffer);
                }

                // Randomly dispose some buffers
                while(buffers.Count > 10 && random.NextDouble() < 0.3)
                {
                    var index = random.Next(buffers.Count);
                    await buffers[index].DisposeAsync();
                    buffers.RemoveAt(index);
                }
            }

            // Assert
            var statistics = memoryManager.GetStatistics();
            (statistics.FragmentationPercentage < 50).Should().BeTrue(); // Should maintain reasonable fragmentation
            (statistics.TotalAllocatedBytes > 0).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    #endregion

    #region Helper Methods

    private ReadOnlyMemory<T> GenerateTestData<T>(int count) where T : struct
    {
        if(typeof(T) == typeof(float))
        {
            var data = new float[count];
            var random = new Random(42);
            for(int i = 0; i < count; i++)
            {
                data[i] =(float)random.NextDouble();
            }
            return(ReadOnlyMemory<T>)(object)new ReadOnlyMemory<float>(data);
        }
        else if(typeof(T) == typeof(int))
        {
            var data = new int[count];
            var random = new Random(42);
            for(int i = 0; i < count; i++)
            {
                data[i] = random.Next();
            }
            return(ReadOnlyMemory<T>)(object)new ReadOnlyMemory<int>(data);
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} not supported in test data generation");
        }
    }

    private KernelDefinition CreateComplexKernelDefinition(string name)
    {
        var complexCode = @"
__kernel void complexComputation(__global const float* input, __global float* output, const int n) {
    int id = get_global_id(0);
    if(id < n) {
        float value = input[id];
        
        // Complex mathematical operations
        for(int i = 0; i < 10; i++) {
            value = sqrt(value * value + 1.0f);
            value = sin(value) * cos(value);
            value = exp(value * 0.1f);
            value = log(fabs(value) + 1.0f);
        }
        
        output[id] = value;
    }
}";
        return new KernelDefinition(
            name,
            System.Text.Encoding.UTF8.GetBytes(complexCode));
    }

    private KernelDefinition CreateSimpleKernelDefinition(string name)
    {
        var simpleCode = "__kernel void simple(__global float* data) { int id = get_global_id(0); data[id] *= 2.0f; }";
        return new KernelDefinition(
            name,
            System.Text.Encoding.UTF8.GetBytes(simpleCode));
    }

    #endregion

    public void Dispose()
    {
        if(!_disposed)
        {
            _disposed = true;
        }
    }
}
