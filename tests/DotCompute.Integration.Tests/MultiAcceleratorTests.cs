// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for multi-accelerator scenarios.
/// Tests workload distribution across multiple compute devices.
/// </summary>
public class MultiAcceleratorTests : IntegrationTestBase
{
    public MultiAcceleratorTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task MultiAccelerator_WorkloadDistribution_ShouldDistributeAcrossDevices()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var availableAccelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        // Skip test if not enough accelerators
        if (availableAccelerators.Count < 2)
        {
            Logger.LogInformation("Skipping multi-accelerator test - insufficient devices");
            return;
        }

        const int totalWorkItems = 2048;
        var inputData = GenerateTestData(totalWorkItems);

        // Act
        var results = await DistributeWorkloadAcrossAccelerators(
            availableAccelerators.Take(2).ToList(),
            inputData);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();
        
        var totalProcessed = results.Sum(r => r.ProcessedItems);
        totalProcessed.Should().Be(totalWorkItems);
    }

    [Fact]
    public async Task MultiAccelerator_LoadBalancing_ShouldBalanceWorkloadByCapability()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var availableAccelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        if (availableAccelerators.Count < 2)
        {
            Logger.LogInformation("Skipping load balancing test - insufficient devices");
            return;
        }

        const int totalWorkItems = 4096;
        var inputData = GenerateTestData(totalWorkItems);

        // Act
        var results = await LoadBalanceWorkload(
            availableAccelerators,
            inputData);

        // Assert
        results.Should().NotBeEmpty();
        results.All(r => r.Success).Should().BeTrue();
        
        // Verify work distribution is reasonable (no device should have 0 or all work)
        var workDistribution = results.Select(r => r.ProcessedItems).ToList();
        workDistribution.Should().AllSatisfy(items => items.Should().BeGreaterThan(0));
        
        if (workDistribution.Count > 1)
        {
            var maxWork = workDistribution.Max();
            var minWork = workDistribution.Min();
            (maxWork - minWork).Should().BeLessThan(totalWorkItems / 2); // Reasonable distribution
        }
    }

    [Fact]
    public async Task MultiAccelerator_MemoryCoherence_ShouldMaintainDataConsistency()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var accelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        if (accelerators.Count < 2)
        {
            Logger.LogInformation("Skipping memory coherence test - insufficient devices");
            return;
        }

        const int dataSize = 1024;
        var sharedData = GenerateTestData(dataSize);

        // Act
        var coherenceResults = await TestMemoryCoherence(
            accelerators.Take(2).ToList(),
            sharedData);

        // Assert
        coherenceResults.Should().NotBeEmpty();
        coherenceResults.All(r => r.DataIntegrity).Should().BeTrue();
        
        // All accelerators should see the same final data
        var finalStates = coherenceResults.Select(r => r.FinalChecksum).Distinct().ToList();
        finalStates.Should().HaveCount(1, "All accelerators should see consistent data");
    }

    [Fact]
    public async Task MultiAccelerator_AsyncExecution_ShouldExecuteConcurrently()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var accelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        if (accelerators.Count < 2)
        {
            Logger.LogInformation("Skipping async execution test - insufficient devices");
            return;
        }

        const int workItemsPerAccelerator = 512;
        var inputData = GenerateTestData(workItemsPerAccelerator * accelerators.Count);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var concurrentResults = await ExecuteConcurrentWorkloads(
            accelerators,
            inputData,
            workItemsPerAccelerator);
        stopwatch.Stop();

        // Assert
        concurrentResults.Should().HaveCount(accelerators.Count);
        concurrentResults.All(r => r.Success).Should().BeTrue();
        
        // Concurrent execution should be faster than sequential
        var totalExecutionTime = stopwatch.Elapsed;
        var expectedSequentialTime = concurrentResults.Sum(r => r.ExecutionTime.TotalMilliseconds);
        
        totalExecutionTime.TotalMilliseconds.Should().BeLessThan(
            expectedSequentialTime * 0.8, // Should be at least 20% faster
            "Concurrent execution should show performance benefit");
    }

    [Fact]
    public async Task MultiAccelerator_FaultTolerance_ShouldHandleDeviceFailure()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var accelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        if (accelerators.Count < 3)
        {
            Logger.LogInformation("Skipping fault tolerance test - need at least 3 devices");
            return;
        }

        const int totalWorkItems = 1536;
        var inputData = GenerateTestData(totalWorkItems);

        // Act - Simulate failure of one accelerator
        var faultTolerantResults = await ExecuteWithSimulatedFailure(
            accelerators,
            inputData,
            failureIndex: 1);

        // Assert
        faultTolerantResults.Should().NotBeEmpty();
        faultTolerantResults.Count(r => r.Success).Should().BeGreaterOrEqualTo(accelerators.Count - 1);
        
        var totalProcessed = faultTolerantResults
            .Where(r => r.Success)
            .Sum(r => r.ProcessedItems);
        
        totalProcessed.Should().Be(totalWorkItems, "Work should be redistributed after failure");
    }

    [Theory]
    [InlineData(ComputeBackendType.CPU, ComputeBackendType.CPU)]
    [InlineData(ComputeBackendType.CPU, ComputeBackendType.CUDA)]
    public async Task MultiAccelerator_HeterogeneousExecution_ShouldWorkAcrossBackendTypes(
        ComputeBackendType backend1, ComputeBackendType backend2)
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var accelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        var accelerator1 = accelerators.FirstOrDefault(a => GetBackendType(a) == backend1);
        var accelerator2 = accelerators.FirstOrDefault(a => GetBackendType(a) == backend2);
        
        if (accelerator1 == null || accelerator2 == null)
        {
            Logger.LogInformation($"Skipping heterogeneous test - missing {backend1} or {backend2}");
            return;
        }

        const int workItemsPerDevice = 256;
        var inputData = GenerateTestData(workItemsPerDevice * 2);

        // Act
        var heterogeneousResults = await ExecuteHeterogeneousWorkload(
            [(accelerator1, backend1), (accelerator2, backend2)],
            inputData,
            workItemsPerDevice);

        // Assert
        heterogeneousResults.Should().HaveCount(2);
        heterogeneousResults.All(r => r.Success).Should().BeTrue();
        
        // Results should be equivalent regardless of backend
        var results1 = heterogeneousResults[0].Results;
        var results2 = heterogeneousResults[1].Results;
        
        CompareComputeResults(results1, results2).Should().BeTrue(
            "Different backend types should produce equivalent results");
    }

    [Fact]
    public async Task MultiAccelerator_PeerToPeerTransfer_ShouldTransferDataDirectly()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var accelerators = await acceleratorManager.GetAvailableAcceleratorsAsync();
        
        if (accelerators.Count < 2)
        {
            Logger.LogInformation("Skipping P2P transfer test - insufficient devices");
            return;
        }

        const int transferSize = 1024 * 1024; // 1MB
        var testData = GenerateTestData(transferSize / sizeof(float));

        // Act
        var transferResults = await TestPeerToPeerTransfer(
            accelerators[0],
            accelerators[1],
            testData);

        // Assert
        transferResults.Should().NotBeNull();
        transferResults.Success.Should().BeTrue();
        transferResults.TransferTime.Should().BePositive();
        transferResults.DataIntegrity.Should().BeTrue();
        
        if (transferResults.DirectTransferSupported)
        {
            transferResults.TransferTime.Should().BeLessThan(TimeSpan.FromSeconds(1),
                "P2P transfer should be reasonably fast");
        }
    }

    private async Task<List<AcceleratorResult>> DistributeWorkloadAcrossAccelerators(
        List<IAccelerator> accelerators,
        float[] inputData)
    {
        var results = new List<AcceleratorResult>();
        var workItemsPerAccelerator = inputData.Length / accelerators.Count;

        var tasks = accelerators.Select(async (accelerator, index) =>
        {
            var startIndex = index * workItemsPerAccelerator;
            var endIndex = (index == accelerators.Count - 1) ? inputData.Length : (index + 1) * workItemsPerAccelerator;
            var workData = inputData[startIndex..endIndex];

            try
            {
                var result = await ExecuteWorkOnAccelerator(accelerator, workData);
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = true,
                    ProcessedItems = workData.Length,
                    ExecutionTime = result.ExecutionTime,
                    Results = result.Output
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to execute work on accelerator {accelerator.Info.Id}");
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        return taskResults.ToList();
    }

    private async Task<List<AcceleratorResult>> LoadBalanceWorkload(
        List<IAccelerator> accelerators,
        float[] inputData)
    {
        // Simple load balancing based on relative compute capability
        var weights = accelerators.Select(GetAcceleratorWeight).ToArray();
        var totalWeight = weights.Sum();

        var results = new List<AcceleratorResult>();
        int processedItems = 0;

        var tasks = accelerators.Select(async (accelerator, index) =>
        {
            var weight = weights[index];
            var allocatedItems = (int)(inputData.Length * (weight / totalWeight));
            var workData = inputData[processedItems..(processedItems + allocatedItems)];
            Interlocked.Add(ref processedItems, allocatedItems);

            try
            {
                var result = await ExecuteWorkOnAccelerator(accelerator, workData);
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = true,
                    ProcessedItems = allocatedItems,
                    ExecutionTime = result.ExecutionTime,
                    Results = result.Output
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to execute work on accelerator {accelerator.Info.Id}");
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<List<CoherenceResult>> TestMemoryCoherence(
        List<IAccelerator> accelerators,
        float[] sharedData)
    {
        var results = new List<CoherenceResult>();

        // Create shared buffer
        var sharedBuffer = await CreateSharedBuffer(sharedData);

        var tasks = accelerators.Select(async accelerator =>
        {
            try
            {
                // Each accelerator modifies the shared data
                await ModifySharedData(accelerator, sharedBuffer, accelerator.Info.Id.GetHashCode());
                
                // Read back and verify
                var readData = await ReadSharedData(accelerator, sharedBuffer);
                var checksum = ComputeChecksum(readData);

                return new CoherenceResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    DataIntegrity = true,
                    FinalChecksum = checksum
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Memory coherence test failed for {accelerator.Info.Id}");
                return new CoherenceResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    DataIntegrity = false,
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<List<AcceleratorResult>> ExecuteConcurrentWorkloads(
        List<IAccelerator> accelerators,
        float[] inputData,
        int workItemsPerAccelerator)
    {
        var tasks = accelerators.Select(async (accelerator, index) =>
        {
            var startIndex = index * workItemsPerAccelerator;
            var workData = inputData[startIndex..(startIndex + workItemsPerAccelerator)];
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await ExecuteWorkOnAccelerator(accelerator, workData);
                stopwatch.Stop();

                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = true,
                    ProcessedItems = workData.Length,
                    ExecutionTime = stopwatch.Elapsed,
                    Results = result.Output
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex, $"Concurrent execution failed for {accelerator.Info.Id}");
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    ExecutionTime = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<List<AcceleratorResult>> ExecuteWithSimulatedFailure(
        List<IAccelerator> accelerators,
        float[] inputData,
        int failureIndex)
    {
        var results = new List<AcceleratorResult>();
        var workItemsPerAccelerator = inputData.Length / (accelerators.Count - 1); // Account for failure

        var tasks = accelerators.Select(async (accelerator, index) =>
        {
            if (index == failureIndex)
            {
                // Simulate failure
                await Task.Delay(100); // Simulate some work before failing
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    Error = "Simulated device failure"
                };
            }

            // Redistribute work from failed accelerator
            var adjustedIndex = index > failureIndex ? index - 1 : index;
            var startIndex = adjustedIndex * workItemsPerAccelerator;
            var endIndex = adjustedIndex == accelerators.Count - 2 ? inputData.Length : (adjustedIndex + 1) * workItemsPerAccelerator;
            var workData = inputData[startIndex..endIndex];

            try
            {
                var result = await ExecuteWorkOnAccelerator(accelerator, workData);
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = true,
                    ProcessedItems = workData.Length,
                    ExecutionTime = result.ExecutionTime,
                    Results = result.Output
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Work execution failed for {accelerator.Info.Id}");
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<List<AcceleratorResult>> ExecuteHeterogeneousWorkload(
        List<(IAccelerator Accelerator, ComputeBackendType Backend)> acceleratorBackends,
        float[] inputData,
        int workItemsPerDevice)
    {
        var tasks = acceleratorBackends.Select(async (ab, index) =>
        {
            var (accelerator, backend) = ab;
            var startIndex = index * workItemsPerDevice;
            var workData = inputData[startIndex..(startIndex + workItemsPerDevice)];

            try
            {
                var result = await ExecuteWorkOnAcceleratorWithBackend(accelerator, backend, workData);
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = true,
                    ProcessedItems = workData.Length,
                    ExecutionTime = result.ExecutionTime,
                    Results = result.Output
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Heterogeneous execution failed for {accelerator.Info.Id} on {backend}");
                return new AcceleratorResult
                {
                    AcceleratorId = accelerator.Info.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<PeerToPeerResult> TestPeerToPeerTransfer(
        IAccelerator sourceAccelerator,
        IAccelerator targetAccelerator,
        float[] testData)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Create buffer on source
            var sourceBuffer = await CreateBufferOnAccelerator(sourceAccelerator, testData);
            
            // Transfer to target
            var targetBuffer = await TransferBufferBetweenAccelerators(
                sourceAccelerator, sourceBuffer,
                targetAccelerator);
            
            stopwatch.Stop();
            
            // Verify data integrity
            var transferredData = await ReadBufferFromAccelerator(targetAccelerator, targetBuffer);
            var dataIntegrity = VerifyDataIntegrity(testData, transferredData);

            return new PeerToPeerResult
            {
                Success = true,
                TransferTime = stopwatch.Elapsed,
                DataIntegrity = dataIntegrity,
                DirectTransferSupported = CheckDirectTransferSupport(sourceAccelerator, targetAccelerator)
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "P2P transfer failed");
            return new PeerToPeerResult
            {
                Success = false,
                Error = ex.Message,
                TransferTime = stopwatch.Elapsed
            };
        }
    }

    // Helper methods
    private static float[] GenerateTestData(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(0, size)
                        .Select(_ => (float)random.NextDouble() * 100.0f)
                        .ToArray();
    }

    private static double GetAcceleratorWeight(IAccelerator accelerator)
    {
        // Simple weight calculation based on compute units and memory
        var computeUnits = accelerator.Info.ComputeUnits;
        var memorySize = accelerator.Info.TotalMemory;
        
        return Math.Max(computeUnits * memorySize / (1024.0 * 1024.0 * 1024.0), 1.0); // Normalize by GB
    }

    private static ComputeBackendType GetBackendType(IAccelerator accelerator)
    {
        return accelerator.Info.DeviceType.ToUpperInvariant() switch
        {
            "CPU" => ComputeBackendType.CPU,
            "GPU" when accelerator.Info.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) => ComputeBackendType.CUDA,
            "GPU" when accelerator.Info.Vendor.Contains("Apple", StringComparison.OrdinalIgnoreCase) => ComputeBackendType.Metal,
            _ => ComputeBackendType.CPU
        };
    }

    private static bool CompareComputeResults(object[] results1, object[] results2)
    {
        if (results1.Length != results2.Length) return false;
        
        for (int i = 0; i < results1.Length; i++)
        {
            if (results1[i] is float[] arr1 && results2[i] is float[] arr2)
            {
                if (arr1.Length != arr2.Length) return false;
                
                for (int j = 0; j < arr1.Length; j++)
                {
                    if (Math.Abs(arr1[j] - arr2[j]) > 0.001f) return false;
                }
            }
        }
        
        return true;
    }

    private static uint ComputeChecksum(float[] data)
    {
        uint checksum = 0;
        foreach (var value in data)
        {
            checksum ^= (uint)value.GetHashCode();
        }
        return checksum;
    }

    private static bool VerifyDataIntegrity(float[] original, float[] transferred)
    {
        if (original.Length != transferred.Length) return false;
        
        for (int i = 0; i < original.Length; i++)
        {
            if (Math.Abs(original[i] - transferred[i]) > 0.001f) return false;
        }
        
        return true;
    }

    private static bool CheckDirectTransferSupport(IAccelerator source, IAccelerator target)
    {
        // Simplified check - in real implementation would check for P2P capabilities
        return source.Info.DeviceType == target.Info.DeviceType;
    }

    // Placeholder methods - these would be implemented based on actual accelerator APIs
    private async Task<(TimeSpan ExecutionTime, object[] Output)> ExecuteWorkOnAccelerator(IAccelerator accelerator, float[] workData)
    {
        await Task.Delay(10); // Simulate work
        return (TimeSpan.FromMilliseconds(10), new object[] { workData.Select(x => x * 2).ToArray() });
    }

    private async Task<(TimeSpan ExecutionTime, object[] Output)> ExecuteWorkOnAcceleratorWithBackend(
        IAccelerator accelerator, ComputeBackendType backend, float[] workData)
    {
        await Task.Delay(10); // Simulate work
        return (TimeSpan.FromMilliseconds(10), new object[] { workData.Select(x => x * 2).ToArray() });
    }

    private async Task<IBuffer> CreateSharedBuffer(float[] data)
    {
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        return await CreateInputBuffer(memoryManager, data);
    }

    private async Task ModifySharedData(IAccelerator accelerator, IBuffer sharedBuffer, int modifier)
    {
        // Placeholder - would modify data on accelerator
        await Task.Delay(1);
    }

    private async Task<float[]> ReadSharedData(IAccelerator accelerator, IBuffer sharedBuffer)
    {
        // Placeholder - would read data from accelerator
        await Task.Delay(1);
        return new float[100]; // Dummy data
    }

    private async Task<IBuffer> CreateBufferOnAccelerator(IAccelerator accelerator, float[] data)
    {
        return await CreateInputBuffer(accelerator.Memory, data);
    }

    private async Task<IBuffer> TransferBufferBetweenAccelerators(
        IAccelerator source, IBuffer sourceBuffer,
        IAccelerator target)
    {
        // Placeholder - would perform actual P2P transfer
        await Task.Delay(10);
        return sourceBuffer; // Simplified
    }

    private async Task<float[]> ReadBufferFromAccelerator(IAccelerator accelerator, IBuffer buffer)
    {
        return await ReadBufferAsync<float[]>(buffer);
    }
}

/// <summary>
/// Result of accelerator execution.
/// </summary>
public class AcceleratorResult
{
    public string AcceleratorId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ProcessedItems { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public object[] Results { get; set; } = Array.Empty<object>();
    public string? Error { get; set; }
}

/// <summary>
/// Result of memory coherence test.
/// </summary>
public class CoherenceResult
{
    public string AcceleratorId { get; set; } = string.Empty;
    public bool DataIntegrity { get; set; }
    public uint FinalChecksum { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of peer-to-peer transfer test.
/// </summary>
public class PeerToPeerResult
{
    public bool Success { get; set; }
    public TimeSpan TransferTime { get; set; }
    public bool DataIntegrity { get; set; }
    public bool DirectTransferSupported { get; set; }
    public string? Error { get; set; }
}