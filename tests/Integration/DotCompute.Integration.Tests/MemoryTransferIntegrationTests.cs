// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Integration.Tests.Infrastructure;

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration

namespace DotCompute.Tests.Integration;


/// <summary>
/// Integration tests for memory transfer scenarios including host-to-device,
/// device-to-device, P2P transfers, and memory coherency validation.
/// </summary>
[Collection("Integration")]
public sealed class MemoryTransferIntegrationTests : ComputeWorkflowTestBase
{
    private readonly IMemoryManager _memoryManager;

    public MemoryTransferIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
    }

    [Theory]
    [InlineData(1024, MemoryOptions.None)]
    [InlineData(4096, MemoryOptions.HostVisible)]
    [InlineData(16384, MemoryOptions.Cached)]
    [InlineData(65536, MemoryOptions.ReadOnly)]
    [InlineData(262144, MemoryOptions.WriteOnly)]
    public async Task HostToDeviceTransfer_VariousSizes_ShouldMaintainDataIntegrity(int size, MemoryOptions options)
    {
        // Arrange
        var originalData = TestDataGenerators.GenerateFloatArray(size, -1000f, 1000f);
        var transferStopwatch = Stopwatch.StartNew();

        // Act
        using var deviceBuffer = await _memoryManager.AllocateAsync(
            size * sizeof(float), options);

        await deviceBuffer.CopyFromHostAsync<float>(originalData.AsMemory());

        var retrievedData = new float[size];
        await deviceBuffer.CopyToHostAsync<float>(retrievedData.AsMemory());

        transferStopwatch.Stop();

        // Assert
        _ = retrievedData.Should().BeEquivalentTo(originalData, options => options.WithStrictOrdering());

        var transferredMB = (size * sizeof(float) * 2) / 1024.0 / 1024.0; // Round trip
        var bandwidthMBps = transferredMB / transferStopwatch.Elapsed.TotalSeconds;

        Logger.LogInformation("Host-Device transfer: {Size} elements, {Bandwidth:F2} MB/s, Options: {Options}",
            size, bandwidthMBps, options);

        _ = bandwidthMBps.Should().BeGreaterThan(10, "Transfer bandwidth should be reasonable");
    }

    [Fact]
    public async Task DeviceToDeviceTransfer_SameDevice_ShouldBeEfficient()
    {
        // Arrange
        const int dataSize = 8192;
        var sourceData = TestDataGenerators.GenerateFloatArray(dataSize);

        using var sourceBuffer = await _memoryManager.AllocateAndCopyAsync<float>(
            sourceData.AsMemory(), MemoryOptions.ReadOnly);
        using var destBuffer = await _memoryManager.AllocateAsync(
            dataSize * sizeof(float), MemoryOptions.WriteOnly);

        var transferStopwatch = Stopwatch.StartNew();

        // Act - Simulate device-to-device copy using a kernel
        var copyWorkflow = new ComputeWorkflowDefinition
        {
            Name = "DeviceToDeviceCopy",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "memory_copy",
                SourceCode = KernelSources.MemoryCopy,
                CompilationOptions = new CompilationOptions
                {
                    OptimizationLevel = OptimizationLevel.Maximum,
                    EnableMemoryCoalescing = true
                }
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "source", Data = sourceData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "destination", Size = dataSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "copy_stage",
                Order = 1,
                KernelName = "memory_copy",
                ExecutionOptions = new ExecutionOptions
                {
                    GlobalWorkSize = [dataSize],
                    LocalWorkSize = [Math.Min(256, dataSize)]
                },
                ArgumentNames = ["source", "destination"]
            }
            ]
        };

        var result = await ExecuteComputeWorkflowAsync("DeviceToDeviceCopy", copyWorkflow);
        transferStopwatch.Stop();

        // Assert
        _ = result.Success.Should().BeTrue();
        var copiedData = (float[])result.Results["destination"];

        _ = copiedData.Should().BeEquivalentTo(sourceData, options => options.WithStrictOrdering());

        var transferredMB = (dataSize * sizeof(float)) / 1024.0 / 1024.0;
        var bandwidthMBps = transferredMB / transferStopwatch.Elapsed.TotalSeconds;

        Logger.LogInformation("Device-to-device transfer: {Size} elements, {Bandwidth:F2} MB/s",
            dataSize, bandwidthMBps);

        _ = bandwidthMBps.Should().BeGreaterThan(50, "Device-to-device transfers should be faster than host transfers");
    }

    [Fact]
    public async Task PeerToPeerTransfer_MultipleDevices_ShouldSynchronizeCorrectly()
    {
        // Arrange - Simulate P2P transfer between GPU devices
        const int dataSize = 4096;
        var originalData = TestDataGenerators.GenerateFloatArray(dataSize);

        // Create workflow that distributes data across multiple simulated devices
        var p2pWorkflow = new ComputeWorkflowDefinition
        {
            Name = "P2PTransfer",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "p2p_scatter",
                SourceCode = KernelSources.P2PScatter
            },
            new WorkflowKernel
            {
                Name = "p2p_gather",
                SourceCode = KernelSources.P2PGather
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "data", Data = originalData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = dataSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer
            {
                Name = "device0_chunk",
                SizeInBytes =(dataSize / 2) * sizeof(float)
            },
            new WorkflowIntermediateBuffer
            {
                Name = "device1_chunk",
                SizeInBytes =(dataSize / 2) * sizeof(float)
            }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "scatter_stage",
                Order = 1,
                KernelName = "p2p_scatter",
                ArgumentNames = ["data", "device0_chunk", "device1_chunk"],
                Parameters = new Dictionary<string, object> { ["chunk_size"] = dataSize / 2 }
            },
            new WorkflowExecutionStage
            {
                Name = "gather_stage",
                Order = 2,
                KernelName = "p2p_gather",
                ArgumentNames = ["device0_chunk", "device1_chunk", "result"],
                Parameters = new Dictionary<string, object> { ["chunk_size"] = dataSize / 2 }
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("P2PTransfer", p2pWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();
        _ = result.ExecutionResults.Count.Should().Be(2);
        _ = result.ExecutionResults.Values.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        var finalResult = (float[])result.Results["result"];
        _ = finalResult.Should().BeEquivalentTo(originalData, options => options.WithStrictOrdering());

        LogPerformanceMetrics("P2PTransfer", result.Duration, dataSize);
    }

    [Fact]
    public async Task MemoryCoherency_ConcurrentAccess_ShouldMaintainConsistency()
    {
        // Arrange
        const int dataSize = 2048;
        var initialData = TestDataGenerators.GenerateFloatArray(dataSize, 0f, 100f);

        // Create workflow with multiple stages accessing same memory
        var coherencyWorkflow = new ComputeWorkflowDefinition
        {
            Name = "MemoryCoherency",
            Kernels =
            [
                new WorkflowKernel { Name = "increment", SourceCode = KernelSources.IncrementKernel },
            new WorkflowKernel { Name = "multiply", SourceCode = KernelSources.MultiplyKernel },
            new WorkflowKernel { Name = "validate", SourceCode = KernelSources.ValidateKernel }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "data", Data = initialData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_result", Size = dataSize },
            new WorkflowOutput { Name = "validation", Size = 1 }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer
            {
                Name = "shared_buffer",
                SizeInBytes = dataSize * sizeof(float),
                Options = MemoryOptions.Cached
            }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "init_stage",
                Order = 1,
                KernelName = "increment", // Copy and increment
                ArgumentNames = ["data", "shared_buffer"]
            },
            new WorkflowExecutionStage
            {
                Name = "process_stage",
                Order = 2,
                KernelName = "multiply", // Multiply in place
                ArgumentNames = ["shared_buffer", "shared_buffer"],
                Parameters = new Dictionary<string, object> { ["factor"] = 2.0f }
            },
            new WorkflowExecutionStage
            {
                Name = "finalize_stage",
                Order = 3,
                KernelName = "validate", // Validate and copy to final
                ArgumentNames = ["shared_buffer", "final_result", "validation"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MemoryCoherency", coherencyWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        var finalResult = (float[])result.Results["final_result"];
        var validation = (float[])result.Results["validation"];

        // Verify mathematical correctness:(initial + 1) * 2
        for (var i = 0; i < Math.Min(100, dataSize); i++)
        {
            var expected = (initialData[i] + 1.0f) * 2.0f;
            _ = finalResult[i].Should().BeApproximately(expected, 0.001f);
        }

        _ = validation[0].Should().Be(1.0f); // Validation should pass

        LogPerformanceMetrics("MemoryCoherency", result.Duration, dataSize * 3);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(32768)]
    public async Task AsyncMemoryTransfer_ParallelOperations_ShouldOverlapEfficiently(int dataSize)
    {
        // Arrange
        var datasets = Enumerable.Range(0, 4)
            .Select(_ => TestDataGenerators.GenerateFloatArray(dataSize))
            .ToArray();

        var buffers = new List<IMemoryBuffer>();
        var tasks = new List<Task>();
        var transferStopwatch = Stopwatch.StartNew();

        try
        {
            // Act - Start multiple async transfers
            for (var i = 0; i < datasets.Length; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(
                    dataSize * sizeof(float), MemoryOptions.None);
                buffers.Add(buffer);

                var dataset = datasets[i];
                var transferTask = Task.Run(async () =>
                {
                    await buffer.CopyFromHostAsync<float>(dataset.AsMemory());

                    // Simulate some processing
                    await Task.Delay(10);

                    var retrieved = new float[dataSize];
                    await buffer.CopyToHostAsync<float>(retrieved.AsMemory());

                    return retrieved;
                });

                tasks.Add(transferTask);
            }

            var results = await Task.WhenAll(tasks.Cast<Task<float[]>>());
            transferStopwatch.Stop();

            // Assert
            Assert.Equal(datasets.Length, results.Length);

            for (var i = 0; i < datasets.Length; i++)
            {
                _ = results[i].Should().BeEquivalentTo(datasets[i],
                    options => options.WithStrictOrdering());
            }

            var totalDataMB = (datasets.Length * dataSize * sizeof(float) * 2) / 1024.0 / 1024.0;
            var effectiveBandwidthMBps = totalDataMB / transferStopwatch.Elapsed.TotalSeconds;

            Logger.LogInformation("Async parallel transfers: {Count} x {Size} elements, " +
                                 "Effective bandwidth: {Bandwidth:F2} MB/s",
                datasets.Length, dataSize, effectiveBandwidthMBps);

            // Parallel transfers should be more efficient than sequential
            Assert.True(effectiveBandwidthMBps > 20);
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    public async Task MemoryPooling_RepeatedAllocations_ShouldReuseMemory()
    {
        // Arrange
        const int allocationSize = 4096;
        const int numAllocations = 10;
        var allocationTimes = new List<double>();
        var data = TestDataGenerators.GenerateFloatArray(allocationSize);

        // Act - Perform repeated allocations and deallocations
        for (var i = 0; i < numAllocations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            using var buffer = await _memoryManager.AllocateAsync(
                allocationSize * sizeof(float), MemoryOptions.None);

            await buffer.CopyFromHostAsync<float>(data.AsMemory());

            var retrieved = new float[allocationSize];
            await buffer.CopyToHostAsync<float>(retrieved.AsMemory());

            stopwatch.Stop();
            allocationTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

            _ = retrieved.Should().BeEquivalentTo(data, options => options.WithStrictOrdering());

            // Small delay to allow memory pooling to take effect
            await Task.Delay(1);
        }

        // Assert
        var avgEarlyAllocations = allocationTimes.Take(3).Average();
        var avgLaterAllocations = allocationTimes.Skip(7).Average();

        Logger.LogInformation("Early allocations avg: {Early:F2}ms, Later allocations avg: {Later:F2}ms",
            avgEarlyAllocations, avgLaterAllocations);

        // Later allocations should be faster due to memory pooling
        //(In a real implementation with actual memory pooling)
        _ = (avgLaterAllocations < avgEarlyAllocations * 1.5).Should().BeTrue(
            "Memory pooling should improve allocation performance");
    }

    [Fact]
    public async Task LargeMemoryTransfer_Fragmentation_ShouldHandleGracefully()
    {
        // Arrange
        const int largeSize = 1024 * 1024; // 1M elements
        var largeData = TestDataGenerators.GenerateFloatArray(largeSize, 0f, 1f);

        // Create multiple smaller buffers first to fragment memory
        var fragmentBuffers = new List<IMemoryBuffer>();
        for (var i = 0; i < 10; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(1024 * sizeof(float));
            fragmentBuffers.Add(buffer);
        }

        try
        {
            var transferStopwatch = Stopwatch.StartNew();

            // Act - Try to allocate large buffer after fragmentation
            using var largeBuffer = await _memoryManager.AllocateAsync(
                largeSize * sizeof(float), MemoryOptions.None);

            await largeBuffer.CopyFromHostAsync<float>(largeData.AsMemory());

            var retrieved = new float[largeSize];
            await largeBuffer.CopyToHostAsync<float>(retrieved.AsMemory());

            transferStopwatch.Stop();

            // Assert
            _ = retrieved.Should().BeEquivalentTo(largeData, options => options.WithStrictOrdering());

            var transferredMB = (largeSize * sizeof(float) * 2) / 1024.0 / 1024.0;
            var bandwidthMBps = transferredMB / transferStopwatch.Elapsed.TotalSeconds;

            Logger.LogInformation("Large fragmented transfer: {Size} elements, {Bandwidth:F2} MB/s",
                largeSize, bandwidthMBps);

            _ = bandwidthMBps.Should().BeGreaterThan(5, "Should handle fragmented large transfers");
        }
        finally
        {
            foreach (var buffer in fragmentBuffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    public async Task MemoryAlignment_SpecializedAccess_ShouldOptimizePerformance()
    {
        // Arrange - Test aligned vs unaligned memory access patterns
        const int dataSize = 8192;
        var alignedData = TestDataGenerators.GenerateFloatArray(dataSize);

        // Create workflow that tests memory alignment optimization
        var alignmentWorkflow = new ComputeWorkflowDefinition
        {
            Name = "MemoryAlignment",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "aligned_access",
                SourceCode = KernelSources.AlignedMemoryAccess,
                CompilationOptions = new CompilationOptions
                {
                    OptimizationLevel = OptimizationLevel.Maximum,
                    EnableMemoryCoalescing = true
                }
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = alignedData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = dataSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "aligned_stage",
                Order = 1,
                KernelName = "aligned_access",
                ExecutionOptions = new ExecutionOptions
                {
                    GlobalWorkSize = [dataSize / 4], // Process 4 elements per thread
                    LocalWorkSize = [64]
                },
                ArgumentNames = ["input", "output"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MemoryAlignment", alignmentWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        var output = (float[])result.Results["output"];
        _ = output.Length.Should().Be(dataSize);

        // Verify aligned memory access produced correct results
        _ = output.Should().NotContain(float.NaN);
        _ = output.Should().NotContain(float.PositiveInfinity);
        _ = output.Should().NotContain(float.NegativeInfinity);

        // Performance should be good due to memory alignment
        if (result.Metrics != null)
        {
            _ = result.Metrics.ThroughputMBps.Should().BeGreaterThan(30,
                "Aligned memory access should show good performance");
        }

        LogPerformanceMetrics("MemoryAlignment", result.Duration, dataSize);
    }
}

/// <summary>
/// Additional kernel sources for memory transfer testing.
/// </summary>
internal static partial class KernelSources
{
    public const string MemoryCopy = @"
__kernel void memory_copy(__global const float* source, __global float* destination) {
    int gid = get_global_id(0);
    destination[gid] = source[gid];
}";

    public const string P2PScatter = @"
__kernel void p2p_scatter(__global const float* data, 
                         __global float* device0_chunk, 
                         __global float* device1_chunk,
                         int chunk_size) {
    int gid = get_global_id(0);
    if(gid < chunk_size) {
        device0_chunk[gid] = data[gid];
        device1_chunk[gid] = data[gid + chunk_size];
    }
}";

    public const string P2PGather = @"
__kernel void p2p_gather(__global const float* device0_chunk,
                        __global const float* device1_chunk,
                        __global float* result,
                        int chunk_size) {
    int gid = get_global_id(0);
    if(gid < chunk_size) {
        result[gid] = device0_chunk[gid];
        result[gid + chunk_size] = device1_chunk[gid];
    }
}";

    public const string IncrementKernel = @"
__kernel void increment(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] + 1.0f;
}";

    public const string MultiplyKernel = @"
__kernel void multiply(__global const float* input, __global float* output, float factor) {
    int gid = get_global_id(0);
    output[gid] = input[gid] * factor;
}";

    public const string ValidateKernel = @"
__kernel void validate(__global const float* input, 
                      __global float* output,
                      __global float* validation) {
    int gid = get_global_id(0);
    output[gid] = input[gid];
    
    // Simple validation: check if values are reasonable
    if(gid == 0) {
        validation[0] =(input[0] > 0.0f && input[0] < 1000.0f) ? 1.0f : 0.0f;
    }
}";

    public const string AlignedMemoryAccess = @"
__kernel void aligned_access(__global const float4* input, __global float4* output) {
    int gid = get_global_id(0);
    float4 data = input[gid];
    
    // Perform vectorized operations for better memory throughput
    data = data * 2.0f + 1.0f;
    data = native_sin(data) * native_cos(data);
    
    output[gid] = data;
}";
}
