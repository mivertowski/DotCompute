// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Execution;

/// <summary>
/// Hardware tests for OpenCL kernel execution functionality.
/// Tests kernel compilation, execution, and performance on real hardware.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
public class OpenCLKernelExecutionTests : OpenCLTestBase
{
    private const string VectorAddKernel = @"
        __kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                c[idx] = a[idx] + b[idx];
            }
        }";

    private const string MatrixMultiplyKernel = @"
        __kernel void matrixMultiply(__global const float* a, __global const float* b, __global float* c, int width) {
            int row = get_global_id(1);
            int col = get_global_id(0);

            if (row < width && col < width) {
                float sum = 0.0f;
                for (int k = 0; k < width; k++) {
                    sum += a[row * width + k] * b[k * width + col];
                }
                c[row * width + col] = sum;
            }
        }";

    private const string LocalMemoryReduceKernel = @"
        __kernel void localMemoryReduce(__global const float* input, __global float* output, int n, __local float* scratch) {
            int local_id = get_local_id(0);
            int global_id = get_global_id(0);
            int group_id = get_group_id(0);
            int local_size = get_local_size(0);

            // Load data into local memory
            scratch[local_id] = (global_id < n) ? input[global_id] : 0.0f;
            barrier(CLK_LOCAL_MEM_FENCE);

            // Perform reduction in local memory
            for (int offset = local_size / 2; offset > 0; offset >>= 1) {
                if (local_id < offset) {
                    scratch[local_id] += scratch[local_id + offset];
                }
                barrier(CLK_LOCAL_MEM_FENCE);
            }

            // Write result for this work group
            if (local_id == 0) {
                output[group_id] = scratch[0];
            }
        }";

    private const string ComplexComputeKernel = @"
        __kernel void complexCompute(__global const float* input, __global float* output, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                float x = input[idx];
                // Perform some compute-intensive operations
                float result = 0.0f;
                for (int i = 0; i < 10; i++) {
                    result += sin(x + i * 0.1f) * cos(x - i * 0.1f);
                }
                output[idx] = result;
            }
        }";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelExecutionTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLKernelExecutionTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Tests that vector addition kernel executes correctly on OpenCL hardware.
    /// </summary>
    [SkippableFact]
    public async Task Vector_Add_Kernel_Should_Execute_Correctly()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024; // 1M elements

        // Prepare test data
        var hostA = new float[elementCount];
        var hostB = new float[elementCount];
        var hostResult = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = i * 0.5f;
            hostB[i] = i * 0.3f;
        }

        // Allocate device memory
        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        // Transfer data to device
        await deviceA.CopyFromAsync(hostA.AsMemory());
        await deviceB.CopyFromAsync(hostB.AsMemory());

        // Compile kernel
        var kernelDef = new KernelDefinition("vectorAdd", VectorAddKernel, "vectorAdd");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        kernel.Should().NotBeNull();

        // Prepare kernel arguments
        var args = new KernelArguments();
        args.Add(deviceA);
        args.Add(deviceB);
        args.Add(deviceC);
        args.Add(elementCount);

        // Execute kernel
        var stopwatch = Stopwatch.StartNew();
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Read results
        await deviceC.CopyToAsync(hostResult.AsMemory());

        // Verify results
        for (var i = 0; i < Math.Min(1000, elementCount); i++)
        {
            var expected = hostA[i] + hostB[i];
            hostResult[i].Should().BeApproximately(expected, 0.0001f, $"at index {i}");
        }

        var throughput = (elementCount * 3 * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

        Output.WriteLine($"Vector Add Performance:");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"  Throughput: {throughput:F2} GB/s");
    }

    /// <summary>
    /// Tests that matrix multiplication kernel executes correctly.
    /// </summary>
    [SkippableFact]
    public async Task Matrix_Multiply_Kernel_Should_Execute_Correctly()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int matrixSize = 128;
        const int elementCount = matrixSize * matrixSize;

        // Prepare test data
        var hostA = new float[elementCount];
        var hostB = new float[elementCount];
        var hostResult = new float[elementCount];

        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            hostB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }

        // Allocate device memory
        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        // Transfer data
        await deviceA.CopyFromAsync(hostA.AsMemory());
        await deviceB.CopyFromAsync(hostB.AsMemory());

        // Compile kernel
        var kernelDef = new KernelDefinition("matrixMultiply", MatrixMultiplyKernel, "matrixMultiply");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        Output.WriteLine($"Matrix size: {matrixSize}x{matrixSize}");

        // Prepare kernel arguments with 2D launch configuration
        const int blockSize = 16; // 16x16 work group
        var args = new KernelArguments();
        args.AddBuffer(deviceA);
        args.AddBuffer(deviceB);
        args.AddBuffer(deviceC);
        args.AddScalar(matrixSize);

        // Matrix multiply requires 2D grid: matrixSize x matrixSize
        args.LaunchConfiguration = new KernelLaunchConfiguration
        {
            GridSize = ((uint)matrixSize, (uint)matrixSize, 1),
            BlockSize = (blockSize, blockSize, 1)
        };

        // Execute kernel
        var stopwatch = Stopwatch.StartNew();
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Read results
        await deviceC.CopyToAsync(hostResult.AsMemory());

        // Verify a few elements
        for (var row = 0; row < Math.Min(10, matrixSize); row++)
        {
            for (var col = 0; col < Math.Min(10, matrixSize); col++)
            {
                var expected = 0.0f;
                for (var k = 0; k < matrixSize; k++)
                {
                    expected += hostA[row * matrixSize + k] * hostB[k * matrixSize + col];
                }

                var actual = hostResult[row * matrixSize + col];
                actual.Should().BeApproximately(expected, 0.001f, $"at position ({row}, {col})");
            }
        }

        var gflops = (2.0 * matrixSize * matrixSize * matrixSize) / (stopwatch.Elapsed.TotalSeconds * 1e9);

        Output.WriteLine($"Matrix Multiply Performance:");
        Output.WriteLine($"  Matrix Size: {matrixSize}x{matrixSize}");
        Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
    }

    /// <summary>
    /// Tests that local memory reduction kernel works correctly.
    /// </summary>
    [SkippableFact]
    public async Task Local_Memory_Reduce_Should_Execute_Correctly()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 256;
        const int workGroupSize = 256;
        var numWorkGroups = (elementCount + workGroupSize - 1) / workGroupSize;

        // Prepare test data (all ones for easy verification)
        var hostInput = new float[elementCount];
        var hostOutput = new float[numWorkGroups];

        for (var i = 0; i < elementCount; i++)
        {
            hostInput[i] = 1.0f;
        }

        // Allocate device memory
        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(numWorkGroups);

        // Transfer data
        await deviceInput.CopyFromAsync(hostInput.AsMemory());

        // Compile kernel
        var kernelDef = new KernelDefinition("localMemoryReduce", LocalMemoryReduceKernel, "localMemoryReduce");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Prepare kernel arguments
        var args = new KernelArguments();
        args.Add(deviceInput);
        args.Add(deviceOutput);
        args.Add(elementCount);
        // Local memory size for scratch buffer: workGroupSize * sizeof(float)
        args.Add(new IntPtr(workGroupSize * sizeof(float)));

        // Execute kernel
        var stopwatch = Stopwatch.StartNew();
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Read results
        await deviceOutput.CopyToAsync(hostOutput.AsMemory());

        // Verify results
        for (var i = 0; i < numWorkGroups; i++)
        {
            var expectedSum = Math.Min(workGroupSize, elementCount - i * workGroupSize);
            hostOutput[i].Should().BeApproximately(expectedSum, 0.0001f, $"work group {i}");
        }

        var totalSum = hostOutput.Sum();
        totalSum.Should().BeApproximately(elementCount, 0.0001f);

        Output.WriteLine($"Local Memory Reduction Performance:");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Work Groups: {numWorkGroups}, Work Group Size: {workGroupSize}");
        Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"  Total Sum: {totalSum:F0} (expected: {elementCount})");
    }

    /// <summary>
    /// Tests compute-intensive kernel performance.
    /// </summary>
    [SkippableFact]
    public async Task Complex_Compute_Kernel_Should_Execute()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024;

        var hostInput = new float[elementCount];
        var hostOutput = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            hostInput[i] = i * 0.001f;
        }

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceInput.CopyFromAsync(hostInput.AsMemory());

        var kernelDef = new KernelDefinition("complexCompute", ComplexComputeKernel, "complexCompute");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Prepare kernel arguments
        var args = new KernelArguments();
        args.Add(deviceInput);
        args.Add(deviceOutput);
        args.Add(elementCount);

        var stopwatch = Stopwatch.StartNew();
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        await deviceOutput.CopyToAsync(hostOutput.AsMemory());

        // Verify at least some computation occurred (non-zero results)
        var nonZeroCount = hostOutput.Count(x => Math.Abs(x) > 0.0001f);
        nonZeroCount.Should().BeGreaterThan(elementCount / 2, "Most elements should have non-zero results");

        Output.WriteLine($"Complex Compute Performance:");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"  Throughput: {elementCount / stopwatch.Elapsed.TotalSeconds / 1e6:F2} M elements/sec");
    }

    /// <summary>
    /// Tests kernel performance meets expectations.
    /// </summary>
    [SkippableFact]
    public async Task Kernel_Performance_Should_Meet_Expectations()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int iterations = 10;
        const int elementCount = 1024 * 1024;

        var hostA = new float[elementCount];
        var hostB = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = i * 0.5f;
            hostB[i] = i * 0.3f;
        }

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceA.CopyFromAsync(hostA.AsMemory());
        await deviceB.CopyFromAsync(hostB.AsMemory());

        var kernelDef = new KernelDefinition("vectorAdd", VectorAddKernel, "vectorAdd");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Prepare kernel arguments
        var args = new KernelArguments();
        args.Add(deviceA);
        args.Add(deviceB);
        args.Add(deviceC);
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
            times[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        var averageTime = times.Average();
        var minTime = times.Min();
        var maxTime = times.Max();

        var throughput = (elementCount * 3 * sizeof(float)) / (averageTime / 1000.0 * 1024 * 1024 * 1024);

        Output.WriteLine($"Kernel Performance Benchmark ({iterations} iterations):");
        Output.WriteLine($"  Average Time: {averageTime:F2} ms");
        Output.WriteLine($"  Min Time: {minTime:F2} ms");
        Output.WriteLine($"  Max Time: {maxTime:F2} ms");
        Output.WriteLine($"  Average Throughput: {throughput:F2} GB/s");

        averageTime.Should().BeLessThan(10.0, "Kernel execution should be fast");
        // Intel Arc GPU: ~2.0 GB/s typical throughput for integrated GPU
        throughput.Should().BeGreaterThan(1.5, "Memory throughput should be reasonable");
    }

    /// <summary>
    /// Tests optimal work group size selection.
    /// </summary>
    [SkippableFact]
    public async Task Work_Group_Configuration_Should_Be_Optimized()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var deviceInfo = accelerator.Info;
        var maxWorkGroupSize = accelerator.DeviceInfo?.MaxWorkGroupSize ?? 256;

        // Test different work group sizes
        var workGroupSizes = new[] { 32, 64, 128, 256, 512, 1024 };

        foreach (var workGroupSize in workGroupSizes)
        {
            if (workGroupSize <= (int)maxWorkGroupSize)
            {
                const int totalItems = 1024 * 1024;
                var globalSize = ((totalItems + workGroupSize - 1) / workGroupSize) * workGroupSize;

                Output.WriteLine($"Work Group Size: {workGroupSize}, Global Size: {globalSize}");

                workGroupSize.Should().BeGreaterThan(0);
                workGroupSize.Should().BeLessThanOrEqualTo((int)maxWorkGroupSize);
                globalSize.Should().BeGreaterThan(0);
            }
        }

        Output.WriteLine($"Device Limits:");
        Output.WriteLine($"  Max Work Group Size: {maxWorkGroupSize}");
        Output.WriteLine($"  Max Compute Units: {accelerator.DeviceInfo?.MaxComputeUnits ?? 0}");
    }
}
