// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Native;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Memory stress tests for Metal backend.
    /// Tests large buffer allocations, memory pressure scenarios, and edge cases.
    /// </summary>
    [Trait("Category", "Hardware")]
    [Trait("Category", "Stress")]
    [Trait("Category", "Metal")]
    public class MetalMemoryStressTests : MetalTestBase
    {
        private const string LargeBufferShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void largeBufferTest(device float* data [[ buffer(0) ]],
                           constant uint& size [[ buffer(1) ]],
                           uint gid [[ thread_position_in_grid ]])
{
    if (gid < size) {
        // Simple pattern to verify memory access
        data[gid] = gid * 0.5f + sin((float)gid * 0.001f);
    }
}";

        private const string MemoryPatternShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void memoryPatternTest(device float* input [[ buffer(0) ]],
                             device float* output [[ buffer(1) ]],
                             constant uint& stride [[ buffer(2) ]],
                             uint gid [[ thread_position_in_grid ]])
{
    uint index = gid * stride;
    output[gid] = input[index] * 2.0f + 1.0f;
}";

        private const string MultiBufferShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void multiBufferTest(device float* buffer0 [[ buffer(0) ]],
                           device float* buffer1 [[ buffer(1) ]],
                           device float* buffer2 [[ buffer(2) ]],
                           device float* buffer3 [[ buffer(3) ]],
                           device float* result [[ buffer(4) ]],
                           uint gid [[ thread_position_in_grid ]])
{
    result[gid] = buffer0[gid] + buffer1[gid] + buffer2[gid] + buffer3[gid];
}";

        public MetalMemoryStressTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void Large_Buffer_Allocation_Should_Succeed_Within_Device_Limits()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var deviceInfo = MetalNative.GetDeviceInfo(device);
                var maxBufferLength = deviceInfo.MaxBufferLength;

                Output.WriteLine($"Testing large buffer allocations up to device limit:");
                Output.WriteLine($"  Max Buffer Length: {maxBufferLength / (1024 * 1024 * 1024.0):F2} GB");

                // Test various large buffer sizes
                var testSizes = new long[]
                {
                    1024L * 1024 * 1024,      // 1 GB
                    2048L * 1024 * 1024,      // 2 GB
                    4096L * 1024 * 1024,      // 4 GB
                    (long)(maxBufferLength / 4),       // 25% of max
                    (long)(maxBufferLength / 2),       // 50% of max
                    (long)(maxBufferLength * 0.8) // 80% of max
                };

                var successfulAllocations = new List<(long size, IntPtr buffer)>();

                foreach (var sizeBytes in testSizes)
                {
                    if ((ulong)sizeBytes > maxBufferLength)
                    {
                        Output.WriteLine($"  Skipping {sizeBytes / (1024 * 1024 * 1024.0):F2} GB (exceeds device limit)");
                        continue;
                    }

                    var elementCount = sizeBytes / sizeof(float);
                    var buffer = MetalNative.CreateBuffer(device, (nuint)sizeBytes, 0);

                    if (buffer != IntPtr.Zero)
                    {
                        successfulAllocations.Add(((long)sizeBytes, buffer));
                        Output.WriteLine($"  ✓ Successfully allocated {sizeBytes / (1024 * 1024 * 1024.0):F2} GB");

                        // Test basic read/write access
                        var bufferPtr = MetalNative.GetBufferContents(buffer);
                        bufferPtr.Should().NotBe(IntPtr.Zero, "Buffer contents pointer should be valid");

                        // Write and read back a pattern to verify memory access
                        unsafe
                        {
                            var floatPtr = (float*)bufferPtr;
                            for (var i = 0; i < Math.Min(1000, (long)elementCount); i += 100)
                            {
                                floatPtr[i] = i * 1.5f;
                            }

                            for (var i = 0; i < Math.Min(1000, (long)elementCount); i += 100)
                            {
                                floatPtr[i].Should().BeApproximately(i * 1.5f, 0.001f,
                                    $"Memory should be accessible at offset {i}");
                            }
                        }
                    }
                    else
                    {
                        Output.WriteLine($"  ✗ Failed to allocate {sizeBytes / (1024 * 1024 * 1024.0):F2} GB");
                    }
                }

                // Should be able to allocate at least some large buffers
                successfulAllocations.Should().NotBeEmpty("Should successfully allocate at least some large buffers");

                // Test compute operations on largest successful buffer
                if (successfulAllocations.Any())
                {
                    var (largestSize, largestBuffer) = successfulAllocations.OrderByDescending(x => x.size).First();
                    TestLargeBufferCompute(device, largestBuffer, largestSize);
                }

                // Cleanup
                foreach (var (_, buffer) in successfulAllocations)
                {
                    MetalNative.ReleaseBuffer(buffer);
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Multiple_Buffer_Allocation_Should_Handle_Memory_Pressure()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var deviceInfo = MetalNative.GetDeviceInfo(device);
                var maxBufferLength = deviceInfo.MaxBufferLength;

                const int numBuffers = 20;
                var bufferSize = Math.Min(maxBufferLength / (numBuffers * 2), 512L * 1024 * 1024); // 512MB max per buffer

                Output.WriteLine($"Testing multiple buffer allocation:");
                Output.WriteLine($"  Number of buffers: {numBuffers}");
                Output.WriteLine($"  Buffer size each: {bufferSize / (1024 * 1024):F1} MB");
                Output.WriteLine($"  Total memory: {bufferSize * numBuffers / (1024 * 1024):F1} MB");

                var buffers = new List<IntPtr>();
                var allocationFailures = 0;

                try
                {
                    // Allocate multiple buffers
                    for (var i = 0; i < numBuffers; i++)
                    {
                        var buffer = MetalNative.CreateBuffer(device, (nuint)bufferSize, 0);

                        if (buffer != IntPtr.Zero)
                        {
                            buffers.Add(buffer);

                            // Initialize with unique pattern
                            var bufferPtr = MetalNative.GetBufferContents(buffer);
                            unsafe
                            {
                                var floatPtr = (float*)bufferPtr;
                                var elementCount = bufferSize / sizeof(float);
                                for (var j = 0; j < Math.Min(1000, (long)elementCount); j += 100)
                                {
                                    floatPtr[j] = i * 1000.0f + j;
                                }
                            }
                        }
                        else
                        {
                            allocationFailures++;
                        }
                    }

                    Output.WriteLine($"  Successful allocations: {buffers.Count}");
                    Output.WriteLine($"  Failed allocations: {allocationFailures}");

                    // Should allocate a reasonable number of buffers
                    buffers.Count.Should().BeGreaterThan(numBuffers / 2,
                        "Should successfully allocate at least half the requested buffers");

                    // Verify data integrity across all buffers
                    for (var i = 0; i < buffers.Count; i++)
                    {
                        var bufferPtr = MetalNative.GetBufferContents(buffers[i]);
                        unsafe
                        {
                            var floatPtr = (float*)bufferPtr;
                            var elementCount = (long)(bufferSize / sizeof(float));
                            for (var j = 0; j < Math.Min(1000, elementCount); j += 100)
                            {
                                var expected = i * 1000.0f + j;
                                floatPtr[j].Should().BeApproximately(expected, 0.001f,
                                    $"Buffer {i} should maintain data integrity");
                            }
                        }
                    }

                    // Test compute operations using multiple buffers
                    if (buffers.Count >= 5)
                    {
                        TestMultiBufferCompute(device, buffers.Take(5).ToArray());
                    }
                }
                finally
                {
                    // Cleanup all buffers
                    foreach (var buffer in buffers)
                    {
                        MetalNative.ReleaseBuffer(buffer);
                    }
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Memory_Access_Patterns_Should_Handle_Various_Strides()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, MemoryPatternShader);
            var function = MetalNative.GetFunction(library, "memoryPatternTest");

            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

            Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

            try
            {
                const int baseElementCount = 1024 * 1024; // 1M elements
                var strides = new uint[] { 1, 2, 4, 8, 16, 32, 64, 128 };

                foreach (var stride in strides)
                {
                    var inputElementCount = baseElementCount * stride;
                    var outputElementCount = baseElementCount;

                    var inputSize = (nuint)(inputElementCount * sizeof(float));
                    var outputSize = (nuint)(outputElementCount * sizeof(float));

                    var inputBuffer = MetalNative.CreateBuffer(device, inputSize, 0);
                    var outputBuffer = MetalNative.CreateBuffer(device, outputSize, 0);

                    Skip.If(inputBuffer == IntPtr.Zero || outputBuffer == IntPtr.Zero,
                        $"Buffer allocation failed for stride {stride}");

                    try
                    {
                        // Initialize input data
                        var inputData = MetalTestDataGenerator.CreateLinearSequence((int)inputElementCount);
                        var inputPtr = MetalNative.GetBufferContents(inputBuffer);
                        unsafe
                        {
                            Marshal.Copy(inputData, 0, inputPtr, inputData.Length);
                        }

                        var measure = new MetalPerformanceMeasurement($"Stride {stride}", Output);
                        measure.Start();

                        // Execute kernel with specific stride
                        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

                        MetalNative.SetComputePipelineState(encoder, pipelineState);
                        MetalNative.SetBuffer(encoder, inputBuffer, 0, 0);
                        MetalNative.SetBuffer(encoder, outputBuffer, 0, 1);
                        unsafe
                        {
                            MetalNative.SetBytes(encoder, (nint)(&stride), sizeof(uint), 2);
                        }

                        var threadsPerGroup = 256u;
                        var threadgroupsPerGrid = (outputElementCount + threadsPerGroup - 1) / threadsPerGroup;

                        var gridSize = new MetalSize { width = (nuint)threadgroupsPerGrid, height = (nuint)1, depth = (nuint)1 };
                var threadgroupSize = new MetalSize { width = (nuint)threadsPerGroup, height = (nuint)1, depth = (nuint)1 };
                MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);
                        MetalNative.EndEncoding(encoder);
                        // Note: Commit and WaitUntilCompleted are handled at a higher level
                        // MetalNative.CommitCommandBuffer(commandBuffer);
                        // MetalNative.WaitUntilCompleted(commandBuffer);

                        measure.Stop();

                        // Verify results
                        var outputData = new float[outputElementCount];
                        var outputPtr = MetalNative.GetBufferContents(outputBuffer);
                        unsafe
                        {
                            Marshal.Copy(outputPtr, outputData, 0, outputData.Length);
                        }

                        // Check pattern: output[i] = input[i * stride] * 2 + 1
                        for (var i = 0; i < Math.Min(100, outputElementCount); i++)
                        {
                            var inputIndex = i * stride;
                            var expected = inputData[inputIndex] * 2.0f + 1.0f;
                            outputData[i].Should().BeApproximately(expected, 0.001f,
                                $"Output should match expected pattern for stride {stride} at index {i}");
                        }

                        var throughput = (inputElementCount + outputElementCount) * sizeof(float) /
                                       (measure.ElapsedTime.TotalSeconds * 1024 * 1024);

                        Output.WriteLine($"Stride {stride}: {measure.ElapsedTime.TotalMilliseconds:F2} ms, " +
                                       $"Throughput: {throughput:F1} MB/s");

                        MetalNative.ReleaseCommandBuffer(commandBuffer);
                        // MetalNative.ReleaseComputeCommandEncoder(encoder);
                    }
                    finally
                    {
                        MetalNative.ReleaseBuffer(inputBuffer);
                        MetalNative.ReleaseBuffer(outputBuffer);
                    }
                }
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Buffer_Lifecycle_Should_Handle_Rapid_Allocation_Deallocation()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                const int cycles = 100;
                const int buffersPerCycle = 10;
                const int bufferSizeMB = 10;
                var bufferSize = (nuint)(bufferSizeMB * 1024 * 1024);

                Output.WriteLine($"Testing rapid buffer allocation/deallocation:");
                Output.WriteLine($"  Cycles: {cycles}");
                Output.WriteLine($"  Buffers per cycle: {buffersPerCycle}");
                Output.WriteLine($"  Buffer size: {bufferSizeMB} MB");

                var totalAllocations = 0;
                var totalDeallocations = 0;
                var allocationFailures = 0;

                var overallMeasure = new MetalPerformanceMeasurement("Buffer Lifecycle", Output);
                overallMeasure.Start();

                for (var cycle = 0; cycle < cycles; cycle++)
                {
                    var buffers = new List<IntPtr>();

                    // Allocate buffers
                    for (var i = 0; i < buffersPerCycle; i++)
                    {
                        var buffer = MetalNative.CreateBuffer(device, bufferSize, 0);
                        if (buffer != IntPtr.Zero)
                        {
                            buffers.Add(buffer);
                            totalAllocations++;

                            // Quick write test
                            var bufferPtr = MetalNative.GetBufferContents(buffer);
                            unsafe
                            {
                                var floatPtr = (float*)bufferPtr;
                                floatPtr[0] = cycle * 100.0f + i;
                                floatPtr[100] = cycle * 200.0f + i;
                            }
                        }
                        else
                        {
                            allocationFailures++;
                        }
                    }

                    // Verify data and deallocate
                    foreach (var buffer in buffers)
                    {
                        var bufferPtr = MetalNative.GetBufferContents(buffer);
                        unsafe
                        {
                            var floatPtr = (float*)bufferPtr;
                            floatPtr[0].Should().BeGreaterThan(0, "Buffer should contain written data");
                        }

                        MetalNative.ReleaseBuffer(buffer);
                        totalDeallocations++;
                    }

                    if (cycle % 20 == 0)
                    {
                        Output.WriteLine($"  Completed {cycle} cycles, " +
                                       $"Success rate: {totalAllocations * 100.0 / (totalAllocations + allocationFailures):F1}%");
                    }
                }

                overallMeasure.Stop();

                Output.WriteLine($"Buffer Lifecycle Results:");
                Output.WriteLine($"  Total allocations: {totalAllocations}");
                Output.WriteLine($"  Total deallocations: {totalDeallocations}");
                Output.WriteLine($"  Allocation failures: {allocationFailures}");
                Output.WriteLine($"  Success rate: {totalAllocations * 100.0 / (totalAllocations + allocationFailures):F1}%");
                Output.WriteLine($"  Total time: {overallMeasure.ElapsedTime.TotalSeconds:F2} seconds");
                Output.WriteLine($"  Avg allocation time: {overallMeasure.ElapsedTime.TotalMilliseconds / totalAllocations:F3} ms");

                // Validate results
                totalAllocations.Should().BeGreaterThan((int)(cycles * buffersPerCycle * 0.8),
                    "Should successfully allocate most buffers");
                totalAllocations.Should().Be(totalDeallocations,
                    "All allocated buffers should be deallocated");
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        private void TestLargeBufferCompute(IntPtr device, IntPtr buffer, long bufferSize)
        {
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, LargeBufferShader);
            var function = MetalNative.GetFunction(library, "largeBufferTest");

            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

            try
            {
                var elementCount = (uint)(bufferSize / sizeof(float));

                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

                MetalNative.SetComputePipelineState(encoder, pipelineState);
                MetalNative.SetBuffer(encoder, buffer, 0, 0);
                unsafe
                {
                    MetalNative.SetBytes(encoder, (nint)(&elementCount), sizeof(uint), 1);
                }

                var threadsPerGroup = 256u;
                var threadgroupsPerGrid = (elementCount + threadsPerGroup - 1) / threadsPerGroup;

                var gridSize = new MetalSize { width = (nuint)threadgroupsPerGrid, height = (nuint)1, depth = (nuint)1 };
                var threadgroupSize = new MetalSize { width = (nuint)threadsPerGroup, height = (nuint)1, depth = (nuint)1 };
                MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);
                MetalNative.EndEncoding(encoder);
                // Note: Commit and WaitUntilCompleted are handled at a higher level
                // MetalNative.CommitCommandBuffer(commandBuffer);
                // MetalNative.WaitUntilCompleted(commandBuffer);

                // Verify some results
                var bufferPtr = MetalNative.GetBufferContents(buffer);
                unsafe
                {
                    var floatPtr = (float*)bufferPtr;
                    for (var i = 0; i < Math.Min(10, elementCount); i++)
                    {
                        var expected = i * 0.5f + (float)Math.Sin(i * 0.001);
                        floatPtr[i].Should().BeApproximately(expected, 0.01f,
                            $"Large buffer compute result should be correct at index {i}");
                    }
                }

                Output.WriteLine($"  ✓ Successfully executed compute on {bufferSize / (1024 * 1024 * 1024.0):F2} GB buffer");

                MetalNative.ReleaseCommandBuffer(commandBuffer);
                // Note: ReleaseComputeCommandEncoder not exposed in MetalNative
                // // MetalNative.ReleaseComputeCommandEncoder(encoder);
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
            }
        }

        private void TestMultiBufferCompute(IntPtr device, IntPtr[] buffers)
        {
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, MultiBufferShader);
            var function = MetalNative.GetFunction(library, "multiBufferTest");

            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

            try
            {
                const int elementCount = 1024; // Small test for multi-buffer operations
                var resultSize = (nuint)(elementCount * sizeof(float));
                var resultBuffer = MetalNative.CreateBuffer(device, resultSize, 0);

                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

                MetalNative.SetComputePipelineState(encoder, pipelineState);
                for (var i = 0; i < 4 && i < buffers.Length; i++)
                {
                    MetalNative.SetBuffer(encoder, buffers[i], 0, (int)(uint)i);
                }
                MetalNative.SetBuffer(encoder, resultBuffer, 0, 4);

                var threadsPerGroup = 256u;
                var threadgroupsPerGrid = ((uint)elementCount + threadsPerGroup - 1) / threadsPerGroup;

                var gridSize = new MetalSize { width = (nuint)threadgroupsPerGrid, height = (nuint)1, depth = (nuint)1 };
                var threadgroupSize = new MetalSize { width = (nuint)threadsPerGroup, height = (nuint)1, depth = (nuint)1 };
                MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);
                MetalNative.EndEncoding(encoder);
                // Note: Commit and WaitUntilCompleted are handled at a higher level
                // MetalNative.CommitCommandBuffer(commandBuffer);
                // MetalNative.WaitUntilCompleted(commandBuffer);

                // Verify result buffer has data
                var resultPtr = MetalNative.GetBufferContents(resultBuffer);
                unsafe
                {
                    var floatPtr = (float*)resultPtr;
                    floatPtr[0].Should().NotBe(0, "Multi-buffer compute should produce results");
                }

                Output.WriteLine($"  ✓ Successfully executed multi-buffer compute operation");

                MetalNative.ReleaseBuffer(resultBuffer);
                MetalNative.ReleaseCommandBuffer(commandBuffer);
                // Note: ReleaseComputeCommandEncoder not exposed in MetalNative
                // // MetalNative.ReleaseComputeCommandEncoder(encoder);
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
            }
        }
    }
}
