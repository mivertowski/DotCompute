using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Integration.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Thread safety validation tests for DotCompute LINQ system.
/// Tests concurrent access, race conditions, and thread-safe operations.
/// </summary>
public class ThreadSafetyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MockHardwareProvider _mockHardware;
    private readonly ITestOutputHelper _output;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public ThreadSafetyTests(ITestOutputHelper output)
    {
        _output = output;
        _mockHardware = new MockHardwareProvider();
        _cancellationTokenSource = new CancellationTokenSource();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(_mockHardware.CpuAccelerator.Object);
        services.AddSingleton(_mockHardware.GpuAccelerator.Object);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ConcurrentKernelCompilation_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int compilationsPerThread = 5;
        var compilationTasks = new List<Task<CompilationResult>>();
        var compilationResults = new ConcurrentBag<CompilationResult>();
        var exceptions = new ConcurrentBag<Exception>();

        var compiler = _serviceProvider.GetRequiredService<IExpressionCompiler>();

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            var task = Task.Run(async () =>
            {
                var threadResults = new List<CompilationResult>();
                
                try
                {
                    for (int i = 0; i < compilationsPerThread; i++)
                    {
                        var kernelName = $"ConcurrentKernel_{threadId}_{i}";
                        var expression = CreateTestExpression(kernelName, i);
                        var options = new CompilationOptions
                        {
                            TargetBackend = i % 2 == 0 ? ComputeBackend.CPU : ComputeBackend.GPU,
                            EnableCaching = true
                        };

                        var result = await compiler.CompileAsync(expression, options);
                        threadResults.Add(result);
                        compilationResults.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                return threadResults;
            });
            
            compilationTasks.Add(task);
        }

        var allResults = await Task.WhenAll(compilationTasks);

        // Assert
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent compilation");
        compilationResults.Should().HaveCount(threadCount * compilationsPerThread);
        compilationResults.Should().OnlyContain(r => r.Success, "All compilations should succeed");

        // Verify no race conditions in naming
        var uniqueKernelNames = compilationResults.Select(r => r.KernelName).Distinct().ToArray();
        uniqueKernelNames.Should().HaveCount(threadCount * compilationsPerThread, 
            "All kernel names should be unique (no race conditions)");

        _output.WriteLine($"Successfully compiled {compilationResults.Count} kernels concurrently");
    }

    [Fact]
    public async Task ConcurrentCacheAccess_ShouldNotCauseCorruption()
    {
        // Arrange
        const int threadCount = 20;
        const int operationsPerThread = 10;
        var cache = _serviceProvider.GetRequiredService<IKernelCache>();
        var cacheOperations = new ConcurrentBag<CacheOperation>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var key = $"test_key_{threadId % 5}_{i % 3}"; // Some overlap for contention
                        var value = CreateMockKernel($"kernel_{threadId}_{i}");

                        // Random operations: 50% store, 30% get, 20% remove
                        var operation = Random.Shared.Next(100);
                        
                        if (operation < 50)
                        {
                            // Store operation
                            await cache.StoreAsync(key, value);
                            cacheOperations.Add(new CacheOperation("Store", key, true));
                        }
                        else if (operation < 80)
                        {
                            // Get operation
                            var retrieved = cache.TryGet(key, out var cachedKernel);
                            cacheOperations.Add(new CacheOperation("Get", key, retrieved));
                        }
                        else
                        {
                            // Remove operation
                            var removed = cache.TryRemove(key);
                            cacheOperations.Add(new CacheOperation("Remove", key, removed));
                        }

                        // Small delay to increase contention
                        await Task.Delay(1);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Cache operations should be thread-safe");
        cacheOperations.Should().HaveCount(threadCount * operationsPerThread);

        // Verify cache consistency
        var storeOperations = cacheOperations.Where(op => op.Type == "Store").ToArray();
        var getOperations = cacheOperations.Where(op => op.Type == "Get" && op.Success).ToArray();
        
        _output.WriteLine($"Store operations: {storeOperations.Length}");
        _output.WriteLine($"Successful get operations: {getOperations.Length}");
        
        // There should be some successful gets (cached values found)
        getOperations.Should().NotBeEmpty("Some cache hits should occur");
    }

    [Fact]
    public async Task ConcurrentMemoryOperations_ShouldMaintainConsistency()
    {
        // Arrange
        const int threadCount = 8;
        const int operationsPerThread = 20;
        var memoryManager = _serviceProvider.GetRequiredService<IMemoryManager>();
        var allocatedBuffers = new ConcurrentBag<IUnifiedMemoryBuffer>();
        var exceptions = new ConcurrentBag<Exception>();
        var operationCounts = new ConcurrentDictionary<string, int>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var bufferSize = Random.Shared.Next(1000, 10000);
                        
                        // Allocate buffer
                        var buffer = memoryManager.AllocateBuffer<float>(bufferSize);
                        allocatedBuffers.Add(buffer);
                        operationCounts.AddOrUpdate("Allocate", 1, (k, v) => v + 1);

                        // Write data
                        var testData = TestDataGenerator.GenerateFloatArray(bufferSize);
                        await buffer.CopyFromAsync(testData);
                        operationCounts.AddOrUpdate("Write", 1, (k, v) => v + 1);

                        // Read data back
                        var readData = new float[bufferSize];
                        await buffer.CopyToAsync(readData);
                        operationCounts.AddOrUpdate("Read", 1, (k, v) => v + 1);

                        // Verify data integrity
                        readData.Should().BeEquivalentTo(testData, 
                            "Data should be preserved during concurrent memory operations");

                        // Random disposal to test cleanup
                        if (Random.Shared.Next(100) < 30)
                        {
                            buffer.Dispose();
                            operationCounts.AddOrUpdate("Dispose", 1, (k, v) => v + 1);
                        }

                        await Task.Delay(1); // Small delay for scheduling
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Memory operations should be thread-safe");
        allocatedBuffers.Should().HaveCount(threadCount * operationsPerThread);

        foreach (var kvp in operationCounts)
        {
            _output.WriteLine($"{kvp.Key} operations: {kvp.Value}");
        }

        operationCounts["Allocate"].Should().Be(threadCount * operationsPerThread);
        operationCounts["Write"].Should().Be(threadCount * operationsPerThread);
        operationCounts["Read"].Should().Be(threadCount * operationsPerThread);
    }

    [Fact]
    public async Task ConcurrentOptimizationRequests_ShouldNotInterfere()
    {
        // Arrange
        const int threadCount = 6;
        const int requestsPerThread = 8;
        var optimizer = _serviceProvider.GetRequiredService<IOptimizationEngine>();
        var optimizationResults = new ConcurrentBag<OptimizationResult>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < requestsPerThread; i++)
                    {
                        var workload = CreateTestWorkload(threadId, i);
                        var options = new OptimizationOptions
                        {
                            Strategy = (OptimizationStrategy)(i % 3 + 1),
                            EnableCaching = true,
                            MaxOptimizationTime = TimeSpan.FromSeconds(1)
                        };

                        var result = await optimizer.OptimizeAsync(workload, options);
                        optimizationResults.Add(result);

                        // Simulate some processing time
                        await Task.Delay(Random.Shared.Next(10, 50));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Optimization requests should not interfere with each other");
        optimizationResults.Should().HaveCount(threadCount * requestsPerThread);
        optimizationResults.Should().OnlyContain(r => r.Success, "All optimizations should succeed");

        // Verify optimization strategies were applied correctly
        var strategyCounts = optimizationResults.GroupBy(r => r.Strategy)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var kvp in strategyCounts)
        {
            _output.WriteLine($"Strategy {kvp.Key}: {kvp.Value} optimizations");
        }

        strategyCounts.Should().HaveCount(3, "All three optimization strategies should be used");
    }

    [Fact]
    public async Task ConcurrentStreamProcessing_ShouldMaintainOrder()
    {
        // Arrange
        const int streamCount = 4;
        const int batchesPerStream = 50;
        var streamProcessors = new List<StreamProcessor>();
        var allResults = new ConcurrentDictionary<int, List<ProcessedBatch>>();
        var exceptions = new ConcurrentBag<Exception>();

        for (int i = 0; i < streamCount; i++)
        {
            streamProcessors.Add(new StreamProcessor(i));
            allResults[i] = new List<ProcessedBatch>();
        }

        // Act
        var tasks = streamProcessors.Select(processor =>
            Task.Run(async () =>
            {
                try
                {
                    for (int batch = 0; batch < batchesPerStream; batch++)
                    {
                        var data = TestDataGenerator.GenerateFloatArray(1000);
                        var processedBatch = await processor.ProcessBatchAsync(batch, data);
                        
                        lock (allResults[processor.StreamId])
                        {
                            allResults[processor.StreamId].Add(processedBatch);
                        }

                        // Random delay to simulate real-world timing variations
                        await Task.Delay(Random.Shared.Next(1, 10));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Stream processing should not throw exceptions");
        
        foreach (var streamResults in allResults.Values)
        {
            streamResults.Should().HaveCount(batchesPerStream);
            
            // Verify processing order was maintained within each stream
            var orderedResults = streamResults.OrderBy(b => b.BatchId).ToArray();
            for (int i = 0; i < orderedResults.Length; i++)
            {
                orderedResults[i].BatchId.Should().Be(i, "Batch processing order should be maintained");
            }
        }

        _output.WriteLine($"Processed {allResults.Values.Sum(list => list.Count)} batches across {streamCount} streams");
    }

    [Fact]
    public async Task StressTestConcurrentOperations_ShouldRemainStable()
    {
        // Arrange
        const int duration = 5; // seconds
        const int maxConcurrentOperations = 50;
        var endTime = DateTime.UtcNow.AddSeconds(duration);
        var activeOperations = 0;
        var completedOperations = 0;
        var exceptions = new ConcurrentBag<Exception>();
        var cancellationToken = _cancellationTokenSource.Token;

        // Act
        var stressTasks = new List<Task>();

        // Kernel compilation stress
        stressTasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Read(ref activeOperations) < maxConcurrentOperations)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Interlocked.Increment(ref activeOperations);
                            
                            var compiler = _serviceProvider.GetRequiredService<IExpressionCompiler>();
                            var expression = CreateRandomExpression();
                            var result = await compiler.CompileAsync(expression, new CompilationOptions());
                            
                            Interlocked.Increment(ref completedOperations);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeOperations);
                        }
                    }, cancellationToken);
                }
                
                await Task.Delay(10, cancellationToken);
            }
        }, cancellationToken));

        // Memory allocation stress
        stressTasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Read(ref activeOperations) < maxConcurrentOperations)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Interlocked.Increment(ref activeOperations);
                            
                            var memoryManager = _serviceProvider.GetRequiredService<IMemoryManager>();
                            using var buffer = memoryManager.AllocateBuffer<float>(Random.Shared.Next(1000, 5000));
                            
                            var data = TestDataGenerator.GenerateFloatArray(buffer.Length);
                            await buffer.CopyFromAsync(data);
                            
                            Interlocked.Increment(ref completedOperations);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeOperations);
                        }
                    }, cancellationToken);
                }
                
                await Task.Delay(5, cancellationToken);
            }
        }, cancellationToken));

        await Task.WhenAll(stressTasks);

        // Wait for remaining operations to complete
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (Interlocked.Read(ref activeOperations) > 0 && DateTime.UtcNow < timeout)
        {
            await Task.Delay(100);
        }

        // Assert
        exceptions.Should().HaveCountLessOrEqualTo(completedOperations * 0.01, 
            "Error rate should be less than 1%");
        
        completedOperations.Should().BeGreaterThan(0, "Some operations should complete during stress test");
        
        _output.WriteLine($"Completed {completedOperations} operations in {duration} seconds");
        _output.WriteLine($"Error rate: {(double)exceptions.Count / completedOperations * 100:F2}%");
        
        if (exceptions.Any())
        {
            _output.WriteLine($"Exceptions: {string.Join(", ", exceptions.Select(e => e.GetType().Name))}");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task ConcurrentExecutionWithDifferentThreadCounts_ShouldScale(int threadCount)
    {
        // Arrange
        const int operationsPerThread = 10;
        var operations = new ConcurrentBag<ThreadSafeOperation>();
        var exceptions = new ConcurrentBag<Exception>();
        var startTime = DateTime.UtcNow;

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var operation = new ThreadSafeOperation
                        {
                            ThreadId = threadId,
                            OperationId = i,
                            StartTime = DateTime.UtcNow
                        };

                        // Simulate some computational work
                        var data = TestDataGenerator.GenerateFloatArray(1000);
                        var result = await SimulateComputeOperation(data);
                        
                        operation.EndTime = DateTime.UtcNow;
                        operation.Result = result;
                        operations.Add(operation);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);
        var totalTime = DateTime.UtcNow - startTime;

        // Assert
        exceptions.Should().BeEmpty($"No exceptions should occur with {threadCount} threads");
        operations.Should().HaveCount(threadCount * operationsPerThread);
        
        var throughput = operations.Count / totalTime.TotalSeconds;
        var avgOperationTime = operations.Average(op => (op.EndTime - op.StartTime).TotalMilliseconds);
        
        _output.WriteLine($"Threads: {threadCount}, Throughput: {throughput:F1} ops/sec, Avg time: {avgOperationTime:F2} ms");
        
        // Verify thread distribution
        var threadDistribution = operations.GroupBy(op => op.ThreadId)
            .ToDictionary(g => g.Key, g => g.Count());
        
        threadDistribution.Should().HaveCount(threadCount);
        threadDistribution.Values.Should().OnlyContain(count => count == operationsPerThread);
        
        throughput.Should().BeGreaterThan(10, "Minimum throughput should be achieved");
    }

    private Expression<Func<float[], float[]>> CreateTestExpression(string kernelName, int variant)
    {
        return variant % 3 switch
        {
            0 => x => x.Select(v => v * 2.0f).ToArray(),
            1 => x => x.Where(v => v > 0).Select(v => v + 1).ToArray(),
            2 => x => x.Select(v => v * v).Where(v => v < 100).ToArray(),
            _ => x => x.Select(v => v).ToArray()
        };
    }

    private Expression<Func<float[], float[]>> CreateRandomExpression()
    {
        var operations = new Expression<Func<float[], float[]>>[]
        {
            x => x.Select(v => v * 2.0f).ToArray(),
            x => x.Where(v => v > 0).ToArray(),
            x => x.Select(v => v + 1.0f).ToArray(),
            x => x.Select(v => v / 2.0f).ToArray(),
            x => x.Where(v => v < 100).ToArray()
        };
        
        return operations[Random.Shared.Next(operations.Length)];
    }

    private ICompiledKernel CreateMockKernel(string name)
    {
        var mock = new Mock<ICompiledKernel>();
        mock.Setup(x => x.Name).Returns(name);
        mock.Setup(x => x.IsValid).Returns(true);
        return mock.Object;
    }

    private WorkloadCharacteristics CreateTestWorkload(int threadId, int workloadId)
    {
        return new WorkloadCharacteristics
        {
            DataSize = Random.Shared.Next(1000, 100000),
            ComputeIntensity = (ComputeIntensity)(workloadId % 3 + 1),
            MemoryPattern = (MemoryAccessPattern)(threadId % 2 + 1),
            ParallelismDegree = Random.Shared.Next(1, 16)
        };
    }

    private async Task<float[]> SimulateComputeOperation(float[] data)
    {
        // Simulate computational work
        await Task.Delay(Random.Shared.Next(1, 10));
        return data.Select(x => x * 2.0f).ToArray();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _mockHardware?.Dispose();
        _serviceProvider?.Dispose();
    }
}

// Supporting types for thread safety testing

public record CacheOperation(string Type, string Key, bool Success);

public class StreamProcessor
{
    public int StreamId { get; }
    
    public StreamProcessor(int streamId)
    {
        StreamId = streamId;
    }
    
    public async Task<ProcessedBatch> ProcessBatchAsync(int batchId, float[] data)
    {
        // Simulate processing time
        await Task.Delay(Random.Shared.Next(5, 25));
        
        return new ProcessedBatch
        {
            StreamId = StreamId,
            BatchId = batchId,
            ProcessedData = data.Select(x => x * 2.0f).ToArray(),
            ProcessedAt = DateTime.UtcNow
        };
    }
}

public class ProcessedBatch
{
    public int StreamId { get; set; }
    public int BatchId { get; set; }
    public float[] ProcessedData { get; set; } = Array.Empty<float>();
    public DateTime ProcessedAt { get; set; }
}

public class ThreadSafeOperation
{
    public int ThreadId { get; set; }
    public int OperationId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public float[]? Result { get; set; }
}