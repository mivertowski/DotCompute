// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Memory;

/// <summary>
/// Hardware tests for OpenCL memory operations.
/// Tests device memory allocation, host-device transfers, and memory bandwidth.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
public class OpenCLMemoryTests : OpenCLTestBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMemoryTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLMemoryTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Tests that device memory allocation succeeds for various sizes.
    /// </summary>
    [SkippableFact]
    public async Task Device_Memory_Allocation_Should_Succeed()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        // Test various allocation sizes
        var sizes = new[] { 1024, 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024 };

        foreach (var sizeBytes in sizes)
        {
            var elementCount = sizeBytes / sizeof(float);
            await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

            buffer.Should().NotBeNull();
            buffer.SizeInBytes.Should().Be(sizeBytes);
            buffer.ElementCount.Should().Be(elementCount);

            Output.WriteLine($"Successfully allocated {sizeBytes / (1024 * 1024):F1} MB");
        }
    }

    /// <summary>
    /// Tests large memory allocation within device limits.
    /// </summary>
    [SkippableFact]
    public async Task Large_Memory_Allocation_Should_Work_Within_Limits()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var deviceInfo = accelerator.DeviceInfo;
        var maxAllocSize = deviceInfo?.MaxMemoryAllocationSize ?? (256UL * 1024 * 1024);
        var globalMemSize = deviceInfo?.GlobalMemorySize ?? (1024UL * 1024 * 1024);

        // Try to allocate 25% of available memory, capped at device limits
        var targetSize = Math.Min(globalMemSize / 4, maxAllocSize / 2);
        targetSize = Math.Min(targetSize, 512UL * 1024 * 1024); // Cap at 512MB for test stability
        var elementCount = (long)(targetSize / sizeof(float));

        await using var buffer = await accelerator.Memory.AllocateAsync<float>((int)elementCount);

        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(elementCount * sizeof(float));

        Output.WriteLine($"Large allocation test:");
        Output.WriteLine($"  Global Memory: {globalMemSize / (1024 * 1024 * 1024.0):F2} GB");
        Output.WriteLine($"  Max Allocation: {maxAllocSize / (1024 * 1024 * 1024.0):F2} GB");
        Output.WriteLine($"  Allocated: {buffer.SizeInBytes / (1024 * 1024 * 1024.0):F2} GB");
    }

    /// <summary>
    /// Tests host-to-device transfer performance.
    /// </summary>
    [SkippableFact]
    public async Task Host_To_Device_Transfer_Should_Be_Fast()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var transferSizes = new[] { 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024 };

        foreach (var sizeBytes in transferSizes)
        {
            var elementCount = sizeBytes / sizeof(float);
            var hostData = new float[elementCount];

            // Initialize with test pattern
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = (float)Math.Sin(i * 0.001);
            }

            await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

            // Measure transfer time
            var stopwatch = Stopwatch.StartNew();
            await deviceBuffer.CopyFromAsync(hostData.AsMemory());
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();

            var transferRateGBps = sizeBytes / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

            Output.WriteLine($"H2D Transfer - Size: {sizeBytes / (1024 * 1024):F1} MB, " +
                           $"Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, " +
                           $"Rate: {transferRateGBps:F2} GB/s");

            // Intel Arc GPU: ~0.26 GB/s typical for PCIe 4.0 integrated GPU
            transferRateGBps.Should().BeGreaterThan(0.2, "Host-to-Device transfer should achieve reasonable bandwidth");
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5.0, "Transfer should complete reasonably quickly");
        }
    }

    /// <summary>
    /// Tests device-to-host transfer performance.
    /// </summary>
    [SkippableFact]
    public async Task Device_To_Host_Transfer_Should_Be_Fast()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var transferSizes = new[] { 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024 };

        foreach (var sizeBytes in transferSizes)
        {
            var elementCount = sizeBytes / sizeof(float);
            var hostData = new float[elementCount];
            var resultData = new float[elementCount];

            // Initialize test data
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = (float)(i % 1000) * 0.1f;
            }

            await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

            // Upload data first
            await deviceBuffer.CopyFromAsync(hostData.AsMemory());

            // Measure download time
            var stopwatch = Stopwatch.StartNew();
            await deviceBuffer.CopyToAsync(resultData.AsMemory());
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();

            var transferRateGBps = sizeBytes / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

            Output.WriteLine($"D2H Transfer - Size: {sizeBytes / (1024 * 1024):F1} MB, " +
                           $"Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, " +
                           $"Rate: {transferRateGBps:F2} GB/s");

            // Verify data integrity
            for (var i = 0; i < Math.Min(1000, elementCount); i++)
            {
                resultData[i].Should().BeApproximately(hostData[i], 0.0001f, $"at index {i}");
            }

            // Intel Arc GPU: ~0.26 GB/s typical for PCIe 4.0 integrated GPU
            transferRateGBps.Should().BeGreaterThan(0.2, "Device-to-Host transfer should achieve reasonable bandwidth");
        }
    }

    /// <summary>
    /// Tests concurrent bidirectional transfers.
    /// </summary>
    [SkippableFact]
    public async Task Bidirectional_Transfer_Should_Work_Concurrently()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024;

        var hostDataA = new float[elementCount];
        var hostDataB = new float[elementCount];
        var resultDataA = new float[elementCount];
        var resultDataB = new float[elementCount];

        // Initialize test data
        for (var i = 0; i < elementCount; i++)
        {
            hostDataA[i] = i * 0.5f;
            hostDataB[i] = i * 1.5f;
        }

        await using var deviceBufferA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceBufferB = await accelerator.Memory.AllocateAsync<float>(elementCount);

        // Test concurrent transfers
        var stopwatch = Stopwatch.StartNew();

        var uploadTask = Task.WhenAll(
            deviceBufferA.CopyFromAsync(hostDataA.AsMemory()).AsTask(),
            deviceBufferB.CopyFromAsync(hostDataB.AsMemory()).AsTask()
        );

        await uploadTask;
        await accelerator.SynchronizeAsync();

        var downloadTask = Task.WhenAll(
            deviceBufferA.CopyToAsync(resultDataA.AsMemory()).AsTask(),
            deviceBufferB.CopyToAsync(resultDataB.AsMemory()).AsTask()
        );

        await downloadTask;
        await accelerator.SynchronizeAsync();

        stopwatch.Stop();

        // Verify data integrity
        for (var i = 0; i < Math.Min(1000, elementCount); i++)
        {
            resultDataA[i].Should().BeApproximately(hostDataA[i], 0.0001f);
            resultDataB[i].Should().BeApproximately(hostDataB[i], 0.0001f);
        }

        var totalBytes = elementCount * sizeof(float) * 4; // 2 uploads + 2 downloads
        var throughputGBps = totalBytes / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

        Output.WriteLine($"Bidirectional Transfer:");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"  Total Throughput: {throughputGBps:F2} GB/s");

        throughputGBps.Should().BeGreaterThan(0.2, "Concurrent transfers should achieve reasonable throughput");
    }

    /// <summary>
    /// Tests memory bandwidth using a kernel.
    /// </summary>
    [SkippableFact]
    public async Task Memory_Bandwidth_Benchmark_Should_Be_Realistic()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const string bandwidthKernel = @"
            __kernel void memoryBandwidthTest(__global const float* input, __global float* output, int n) {
                int idx = get_global_id(0);
                if (idx < n) {
                    output[idx] = input[idx] * 2.0f + 1.0f;
                }
            }";

        const int elementCount = 16 * 1024 * 1024;
        const int iterations = 10;

        var hostInput = new float[elementCount];
        var hostOutput = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            hostInput[i] = (float)Math.Sin(i * 0.001);
        }

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceInput.CopyFromAsync(hostInput.AsMemory());

        var kernelDef = new KernelDefinition("memoryBandwidthTest", bandwidthKernel, "memoryBandwidthTest");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Prepare kernel arguments
        var args = new KernelArguments();
        args.Add(deviceInput);
        args.Add(deviceOutput);
        args.Add(elementCount);

        // Warmup
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await kernel.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalSeconds;
        }

        var averageTime = times.Average();
        var minTime = times.Min();

        // Calculate effective bandwidth (read + write)
        var bytesTransferred = elementCount * sizeof(float) * 2;
        var effectiveBandwidthGBps = bytesTransferred / (averageTime * 1024 * 1024 * 1024);
        var peakBandwidthGBps = bytesTransferred / (minTime * 1024 * 1024 * 1024);

        Output.WriteLine($"Memory Bandwidth Benchmark:");
        Output.WriteLine($"  Data Size: {elementCount * sizeof(float) / (1024 * 1024):F1} MB");
        Output.WriteLine($"  Average Time: {averageTime * 1000:F2} ms");
        Output.WriteLine($"  Min Time: {minTime * 1000:F2} ms");
        Output.WriteLine($"  Effective Bandwidth: {effectiveBandwidthGBps:F1} GB/s");
        Output.WriteLine($"  Peak Bandwidth: {peakBandwidthGBps:F1} GB/s");

        effectiveBandwidthGBps.Should().BeGreaterThan(10, "Effective bandwidth should be substantial");
        peakBandwidthGBps.Should().BeGreaterThan(effectiveBandwidthGBps, "Peak should be better than average");
    }

    /// <summary>
    /// Tests memory statistics accuracy.
    /// </summary>
    [SkippableFact(Skip = "GetMemoryStatistics not yet implemented in OpenCL backend")]
    public async Task Memory_Statistics_Should_Be_Accurate()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        // Get initial statistics
        // var initialStats = accelerator.GetMemoryStatistics();
        // Output.WriteLine($"[DEBUG] Initial - Used: {initialStats.UsedMemoryBytes}, Allocations: {initialStats.AllocationCount}");

        const int bufferSize = 16 * 1024 * 1024;
        const int elementCount = bufferSize / sizeof(float);

        // Allocate memory
        Output.WriteLine($"[DEBUG] Allocating {elementCount} floats ({bufferSize} bytes)");
        await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
        Output.WriteLine($"[DEBUG] Buffer allocated");

        // Get statistics after allocation
        // var afterAllocStats = accelerator.GetMemoryStatistics();
        // Output.WriteLine($"[DEBUG] After - Used: {afterAllocStats.UsedMemoryBytes}, Allocations: {afterAllocStats.AllocationCount}");

        // Verify statistics changed
        // afterAllocStats.UsedMemoryBytes.Should().BeGreaterThan(initialStats.UsedMemoryBytes);
        // afterAllocStats.AllocationCount.Should().BeGreaterThanOrEqualTo(initialStats.AllocationCount);

        Output.WriteLine($"Memory Statistics: Feature not yet implemented");
        // Output.WriteLine($"  Initial Used: {initialStats.UsedMemoryBytes / (1024 * 1024):F1} MB");
        // Output.WriteLine($"  After Alloc: {afterAllocStats.UsedMemoryBytes / (1024 * 1024):F1} MB");
        // Output.WriteLine($"  Allocations: {afterAllocStats.AllocationCount}");
        // Output.WriteLine($"  Available: {afterAllocStats.AvailableMemoryBytes / (1024 * 1024 * 1024):F2} GB");
    }

    /// <summary>
    /// Tests buffer copy operations.
    /// </summary>
    [SkippableFact(Skip = "Buffer-to-buffer copy not yet implemented in OpenCL backend")]
    public async Task Buffer_Copy_Should_Work_Correctly()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024;

        var hostData = new float[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            hostData[i] = i * 0.1f;
        }

        await using var sourceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var destBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

        // Write to source
        await sourceBuffer.CopyFromAsync(hostData.AsMemory());

        // Copy (if API supports it, otherwise skip)
        try
        {
            // Note: Buffer-to-buffer copy not implemented yet in OpenCL backend
            // var stopwatch = Stopwatch.StartNew();
            // await sourceBuffer.CopyToAsync(destBuffer, 0, 0, elementCount);
            // await accelerator.SynchronizeAsync();
            // stopwatch.Stop();
            await Task.CompletedTask;
            var stopwatch = new Stopwatch();

            // Read back and verify
            var resultData = new float[elementCount];
            await destBuffer.CopyToAsync(resultData.AsMemory());

            for (var i = 0; i < Math.Min(1000, elementCount); i++)
            {
                resultData[i].Should().BeApproximately(hostData[i], 0.0001f, $"at index {i}");
            }

            var bandwidth = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
            Output.WriteLine($"Buffer Copy:");
            Output.WriteLine($"  Size: {elementCount * sizeof(float) / (1024 * 1024):F1} MB");
            Output.WriteLine($"  Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Bandwidth: {bandwidth:F2} GB/s");
        }
        catch (NotSupportedException)
        {
            Output.WriteLine("Buffer copy not supported, skipping");
        }
    }

    /// <summary>
    /// Tests multiple allocations and deallocations.
    /// </summary>
    [SkippableFact]
    public async Task Multiple_Allocations_Should_Work()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int numAllocations = 10;
        const int sizePerAllocation = 1024 * 1024;

        var buffers = new List<IDisposable>();

        try
        {
            for (var i = 0; i < numAllocations; i++)
            {
                var buffer = await accelerator.Memory.AllocateAsync<float>(sizePerAllocation);
                buffers.Add((IDisposable)buffer);
                Output.WriteLine($"Allocated buffer {i + 1}/{numAllocations}");
            }

            buffers.Count.Should().Be(numAllocations);
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                if (buffer is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    buffer.Dispose();
                }
            }
        }

        Output.WriteLine($"Successfully allocated and deallocated {numAllocations} buffers");
    }
}
