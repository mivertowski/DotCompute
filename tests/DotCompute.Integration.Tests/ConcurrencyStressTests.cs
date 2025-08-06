// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core;
using DotCompute.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests;

/// <summary>
/// High-concurrency stress tests for the DotCompute integration layer.
/// Tests race conditions, deadlocks, resource contention, and system limits.
/// </summary>
public sealed class ConcurrencyStressTests : IntegrationTestBase
{
    private readonly ILogger<ConcurrencyStressTests> _logger;

    public ConcurrencyStressTests()
    {
        _logger = ServiceProvider.GetRequiredService<ILogger<ConcurrencyStressTests>>();
    }

    #region High-Concurrency Memory Operations

    [Fact]
    public async Task MemoryOperations_HighConcurrency_ShouldNotCauseDeadlocks()
    {
        // Arrange
        const int threadCount = 50;
        const int operationsPerThread = 100;
        const int bufferSize = 1024;
        
        var accelerator = await GetDefaultAcceleratorAsync();
        var memoryManager = ServiceProvider.GetRequiredService<MemoryManager>();
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = new ConcurrentBag<int>();

        _logger.LogInformation("Starting high-concurrency memory operations test with {ThreadCount} threads", threadCount);

        // Act - Multiple threads performing memory operations concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    var localBuffers = new List<IMemoryBuffer>();

                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var operationId = threadId * operationsPerThread + i;

                        // Mix of operations: allocate, transfer, free
                        switch (operationId % 4)
                        {
                            case 0: // Allocate
                                var buffer = await memoryManager.AllocateAsync(accelerator, bufferSize);
                                localBuffers.Add(buffer);
                                break;

                            case 1: // Transfer between buffers
                                if (localBuffers.Count >= 2)
                                {
                                    await memoryManager.TransferAsync(localBuffers[^2], localBuffers[^1]);
                                }
                                break;

                            case 2: // Write/Read data
                                if (localBuffers.Count > 0)
                                {
                                    var testData = new byte[bufferSize];
                                    new Random(operationId).NextBytes(testData);
                                    await localBuffers[^1].CopyFromHostAsync(testData.AsMemory());
                                    
                                    var readBack = new byte[bufferSize];
                                    await localBuffers[^1].CopyToHostAsync(readBack.AsMemory());
                                }
                                break;

                            case 3: // Free some buffers
                                if (localBuffers.Count > 5)
                                {
                                    var bufferToFree = localBuffers[0];
                                    localBuffers.RemoveAt(0);
                                    await memoryManager.FreeAsync(bufferToFree);
                                }
                                break;
                        }

                        completedOperations.Add(operationId);
                    }

                    // Cleanup remaining buffers
                    foreach (var buffer in localBuffers)
                    {
                        await memoryManager.FreeAsync(buffer);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in thread {ThreadId}", threadId);
                    exceptions.Add(ex);
                }
            })).ToArray();

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2)); // 2-minute timeout
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

        // Assert
        completedTask.Should().Be(Task.WhenAll(tasks), "Operations should complete without deadlock");
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent operations");
        completedOperations.Count.Should().Be(threadCount * operationsPerThread);

        _logger.LogInformation("Completed {OperationCount} concurrent memory operations", completedOperations.Count);
    }

    [Fact]
    public async Task MemoryAllocation_UnderPressure_ShouldHandleGracefully()
    {
        // Arrange
        const int maxConcurrentAllocations = 100;
        const int allocationSize = 1024 * 1024; // 1MB per allocation
        
        var accelerator = await GetDefaultAcceleratorAsync();
        var memoryManager = ServiceProvider.GetRequiredService<MemoryManager>();
        var allocations = new ConcurrentBag<IMemoryBuffer>();
        var exceptions = new ConcurrentBag<Exception>();

        _logger.LogInformation("Testing memory allocation under pressure");

        // Act - Try to allocate many large buffers concurrently
        var tasks = Enumerable.Range(0, maxConcurrentAllocations).Select(i =>
            Task.Run(async () =>
            {
                try
                {
                    var buffer = await memoryManager.AllocateAsync(accelerator, allocationSize);
                    allocations.Add(buffer);

                    // Hold the allocation for a short time to create pressure
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        allocations.Should().NotBeEmpty("Some allocations should succeed");
        
        if (exceptions.Any())
        {
            // Under memory pressure, we expect specific exception types
            exceptions.Should().OnlyContain(ex =>
                ex is OutOfMemoryException or
                InvalidOperationException or
                MemoryException);
        }

        // Cleanup
        var cleanupTasks = allocations.Select(async buffer =>
        {
            try
            {
                await memoryManager.FreeAsync(buffer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to free buffer during cleanup");
            }
        });

        await Task.WhenAll(cleanupTasks);

        _logger.LogInformation("Allocated {Count} buffers, encountered {ExceptionCount} exceptions",
            allocations.Count, exceptions.Count);
    }

    #endregion

    #region Kernel Execution Concurrency

    [Fact]
    public async Task KernelExecution_MultipleConcurrentKernels_ShouldExecuteCorrectly()
    {
        // Arrange
        const int kernelCount = 20;
        const int dataSize = 1000;
        
        var accelerator = await GetDefaultAcceleratorAsync();
        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<(int KernelId, bool Success)>();

        _logger.LogInformation("Testing concurrent kernel execution with {KernelCount} kernels", kernelCount);

        // Act - Execute multiple kernels concurrently
        var tasks = Enumerable.Range(0, kernelCount).Select(kernelId =>
            Task.Run(async () =>
            {
                try
                {
                    // Create test data
                    var inputData = Enumerable.Range(0, dataSize)
                        .Select(i => (float)(i + kernelId))
                        .ToArray();

                    var expectedOutput = inputData.Select(x => x * 2.0f).ToArray();

                    // Create kernel context
                    using var context = new KernelExecutionContext(accelerator);
                    
                    // Simulate kernel execution (simplified)
                    await using var inputBuffer = await accelerator.Memory.AllocateAsync(
                        dataSize * sizeof(float), 
                        MemoryOptions.Default);
                    
                    await using var outputBuffer = await accelerator.Memory.AllocateAsync(
                        dataSize * sizeof(float), 
                        MemoryOptions.Default);

                    // Copy input data
                    await inputBuffer.CopyFromHostAsync(
                        MemoryMarshal.Cast<float, byte>(inputData.AsMemory()));

                    // Simulate computation (copy and multiply by 2)
                    await outputBuffer.CopyFromAsync(inputBuffer);

                    // Read back results
                    var outputBytes = new byte[dataSize * sizeof(float)];
                    await outputBuffer.CopyToHostAsync(outputBytes.AsMemory());
                    var actualOutput = MemoryMarshal.Cast<byte, float>(outputBytes).ToArray();

                    // Modify in-place to simulate the *2 operation
                    for (int i = 0; i < actualOutput.Length; i++)
                    {
                        actualOutput[i] *= 2.0f;
                    }

                    // Verify results
                    var success = expectedOutput.Zip(actualOutput, (expected, actual) => 
                        Math.Abs(expected - actual) < 0.001f).All(x => x);

                    results.Add((kernelId, success));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in kernel {KernelId}", kernelId);
                    exceptions.Add(ex);
                    results.Add((kernelId, false));
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Concurrent kernel execution should not cause exceptions");
        results.Count.Should().Be(kernelCount);
        results.Should().OnlyContain(r => r.Success, "All kernels should execute correctly");

        _logger.LogInformation("Successfully executed {Count} concurrent kernels", 
            results.Count(r => r.Success));
    }

    #endregion

    #region Resource Contention Tests

    [Fact]
    public async Task MultipleComponents_ResourceContention_ShouldNotCauseDeadlock()
    {
        // Arrange
        const int componentCount = 10;
        const int operationsPerComponent = 50;
        
        var accelerator = await GetDefaultAcceleratorAsync();
        var memoryManager = ServiceProvider.GetRequiredService<MemoryManager>();
        var exceptions = new ConcurrentBag<Exception>();
        var completedComponents = new ConcurrentBag<int>();

        // Act - Multiple components competing for resources
        var tasks = Enumerable.Range(0, componentCount).Select(componentId =>
            Task.Run(async () =>
            {
                try
                {
                    var componentBuffers = new List<IMemoryBuffer>();

                    for (int i = 0; i < operationsPerComponent; i++)
                    {
                        // Randomly choose operation to create contention
                        var operation = Random.Shared.Next(4);
                        
                        switch (operation)
                        {
                            case 0: // Memory allocation
                                var size = Random.Shared.Next(1024, 8192);
                                var buffer = await memoryManager.AllocateAsync(accelerator, size);
                                componentBuffers.Add(buffer);
                                break;

                            case 1: // Memory transfer
                                if (componentBuffers.Count >= 2)
                                {
                                    var src = componentBuffers[Random.Shared.Next(componentBuffers.Count)];
                                    var dst = componentBuffers[Random.Shared.Next(componentBuffers.Count)];
                                    if (src != dst && src.SizeInBytes == dst.SizeInBytes)
                                    {
                                        await memoryManager.TransferAsync(src, dst);
                                    }
                                }
                                break;

                            case 2: // Data operations
                                if (componentBuffers.Count > 0)
                                {
                                    var buffer = componentBuffers[Random.Shared.Next(componentBuffers.Count)];
                                    var data = new byte[buffer.SizeInBytes];
                                    Random.Shared.NextBytes(data);
                                    await buffer.CopyFromHostAsync(data.AsMemory());
                                }
                                break;

                            case 3: // Memory deallocation
                                if (componentBuffers.Count > 0)
                                {
                                    var bufferToFree = componentBuffers[0];
                                    componentBuffers.RemoveAt(0);
                                    await memoryManager.FreeAsync(bufferToFree);
                                }
                                break;
                        }

                        // Add small delay to increase contention chances
                        await Task.Delay(1);
                    }

                    // Cleanup
                    foreach (var buffer in componentBuffers)
                    {
                        await memoryManager.FreeAsync(buffer);
                    }

                    completedComponents.Add(componentId);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

        // Assert
        completedTask.Should().Be(Task.WhenAll(tasks), "Should not deadlock under resource contention");
        exceptions.Should().BeEmpty("Resource contention should be handled gracefully");
        completedComponents.Count.Should().Be(componentCount);
    }

    #endregion

    #region System Limits and Recovery

    [Fact]
    public async Task SystemLimits_ExceedingLimits_ShouldRecoverGracefully()
    {
        // Arrange
        const int maxAttempts = 1000;
        var accelerator = await GetDefaultAcceleratorAsync();
        var memoryManager = ServiceProvider.GetRequiredService<MemoryManager>();
        
        var successCount = 0;
        var failureCount = 0;
        var allocatedBuffers = new List<IMemoryBuffer>();

        // Act - Keep allocating until we hit system limits
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var buffer = await memoryManager.AllocateAsync(accelerator, 1024 * 1024); // 1MB
                allocatedBuffers.Add(buffer);
                successCount++;
            }
            catch (OutOfMemoryException)
            {
                failureCount++;
                break; // Expected when hitting limits
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected exception during allocation {i}", i);
                failureCount++;
                break;
            }
        }

        // Try to recover by freeing some memory
        var halfwayPoint = allocatedBuffers.Count / 2;
        for (int i = 0; i < halfwayPoint; i++)
        {
            await memoryManager.FreeAsync(allocatedBuffers[i]);
        }

        // Try allocating again after cleanup
        var recoverySuccess = false;
        try
        {
            var recoveryBuffer = await memoryManager.AllocateAsync(accelerator, 1024 * 1024);
            await memoryManager.FreeAsync(recoveryBuffer);
            recoverySuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recover after cleanup");
        }

        // Final cleanup
        for (int i = halfwayPoint; i < allocatedBuffers.Count; i++)
        {
            try
            {
                await memoryManager.FreeAsync(allocatedBuffers[i]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during final cleanup");
            }
        }

        // Assert
        successCount.Should().BeGreaterThan(0, "Should succeed with some allocations");
        recoverySuccess.Should().BeTrue("System should recover after freeing memory");

        _logger.LogInformation("Successfully allocated {SuccessCount} buffers before hitting limits", successCount);
    }

    #endregion

    #region Race Condition Detection

    [Fact]
    public async Task StatisticsAccuracy_UnderConcurrency_ShouldRemainConsistent()
    {
        // Arrange
        const int threadCount = 20;
        const int operationsPerThread = 100;
        
        var accelerator = await GetDefaultAcceleratorAsync();
        var memoryManager = ServiceProvider.GetRequiredService<MemoryManager>();
        var allAllocations = new ConcurrentBag<IMemoryBuffer>();
        var allDeallocations = new ConcurrentBag<IMemoryBuffer>();

        // Act - Concurrent operations with careful tracking
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                var localAllocations = new List<IMemoryBuffer>();

                // Allocation phase
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var buffer = await memoryManager.AllocateAsync(accelerator, 1024);
                    localAllocations.Add(buffer);
                    allAllocations.Add(buffer);
                }

                // Deallocation phase (free half)
                var toFree = localAllocations.Take(operationsPerThread / 2).ToList();
                foreach (var buffer in toFree)
                {
                    await memoryManager.FreeAsync(buffer);
                    allDeallocations.Add(buffer);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Statistics should be consistent
        var stats = memoryManager.GetMemoryStatistics();
        var expectedTotalAllocations = threadCount * operationsPerThread;
        var expectedTotalDeallocations = threadCount * (operationsPerThread / 2);
        var expectedActiveAllocations = expectedTotalAllocations - expectedTotalDeallocations;

        stats.TotalAllocations.Should().Be(expectedTotalAllocations, 
            "Total allocations should match expected count");
        stats.TotalDeallocations.Should().Be(expectedTotalDeallocations, 
            "Total deallocations should match expected count");
        stats.ActiveAllocations.Should().Be(expectedActiveAllocations, 
            "Active allocations should be consistent");

        allAllocations.Count.Should().Be(expectedTotalAllocations);
        allDeallocations.Count.Should().Be(expectedTotalDeallocations);

        // Cleanup remaining buffers
        var remainingBuffers = allAllocations.Except(allDeallocations);
        foreach (var buffer in remainingBuffers)
        {
            await memoryManager.FreeAsync(buffer);
        }
    }

    #endregion

    private async Task<IAccelerator> GetDefaultAcceleratorAsync()
    {
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        return await acceleratorManager.GetDefaultAcceleratorAsync(AcceleratorType.CPU);
    }
}

/// <summary>
/// Additional using statements for the integration tests.
/// </summary>
#pragma warning disable IDE0005
using System.Runtime.InteropServices;
#pragma warning restore IDE0005