// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests;

/// <summary>
/// Cross-backend validation tests comparing OpenCL results with CPU and CUDA backends.
/// Ensures computational correctness across all backends.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
[Trait("Category", "CrossBackend")]
public class OpenCLCrossBackendValidationTests : OpenCLTestBase
{
    private const string VectorAddSource = @"
        __kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                c[idx] = a[idx] + b[idx];
            }
        }";

    private const string VectorMultiplySource = @"
        __kernel void vectorMultiply(__global const float* a, __global const float* b, __global float* c, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                c[idx] = a[idx] * b[idx];
            }
        }";

    private const string DotProductSource = @"
        __kernel void dotProduct(__global const float* a, __global const float* b, __global float* partial, int n, __local float* scratch) {
            int local_id = get_local_id(0);
            int global_id = get_global_id(0);
            int local_size = get_local_size(0);

            // Each thread computes partial dot product
            scratch[local_id] = (global_id < n) ? a[global_id] * b[global_id] : 0.0f;
            barrier(CLK_LOCAL_MEM_FENCE);

            // Reduction in local memory
            for (int offset = local_size / 2; offset > 0; offset >>= 1) {
                if (local_id < offset) {
                    scratch[local_id] += scratch[local_id + offset];
                }
                barrier(CLK_LOCAL_MEM_FENCE);
            }

            if (local_id == 0) {
                partial[get_group_id(0)] = scratch[0];
            }
        }";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCrossBackendValidationTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLCrossBackendValidationTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Validates vector addition results match CPU implementation.
    /// </summary>
    [SkippableFact]
    public async Task Vector_Add_Should_Match_CPU_Results()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        const int elementCount = 10000;

        // Prepare test data
        var hostA = new float[elementCount];
        var hostB = new float[elementCount];
        var cpuResult = new float[elementCount];
        var openclResult = new float[elementCount];

        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)(random.NextDouble() * 100.0 - 50.0);
            hostB[i] = (float)(random.NextDouble() * 100.0 - 50.0);
        }

        // CPU computation
        for (var i = 0; i < elementCount; i++)
        {
            cpuResult[i] = hostA[i] + hostB[i];
        }

        // OpenCL computation
        await using var accelerator = CreateAccelerator();

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceA.WriteAsync(hostA.AsSpan(), 0);
        await deviceB.WriteAsync(hostB.AsSpan(), 0);

        var kernelDef = new KernelDefinition("vectorAdd", VectorAddSource, "vectorAdd");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        await kernel.LaunchAsync<float>(launchConfig, deviceA, deviceB, deviceC, elementCount);
        await accelerator.SynchronizeAsync();

        await deviceC.ReadAsync(openclResult.AsSpan(), 0);

        // Compare results
        var maxError = 0.0f;
        var errorCount = 0;
        const float tolerance = 0.0001f;

        for (var i = 0; i < elementCount; i++)
        {
            var error = Math.Abs(openclResult[i] - cpuResult[i]);
            maxError = Math.Max(maxError, error);

            if (error > tolerance)
            {
                errorCount++;
                if (errorCount <= 10)
                {
                    Output.WriteLine($"Mismatch at {i}: OpenCL={openclResult[i]}, CPU={cpuResult[i]}, Error={error}");
                }
            }
        }

        Output.WriteLine($"Cross-Backend Validation (Vector Add):");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Max Error: {maxError:E6}");
        Output.WriteLine($"  Errors: {errorCount}/{elementCount}");

        maxError.Should().BeLessThan(tolerance, "OpenCL and CPU results should match within tolerance");
        errorCount.Should().Be(0, "All results should match");
    }

    /// <summary>
    /// Validates vector multiplication results match CPU implementation.
    /// </summary>
    [SkippableFact]
    public async Task Vector_Multiply_Should_Match_CPU_Results()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        const int elementCount = 10000;

        var hostA = new float[elementCount];
        var hostB = new float[elementCount];
        var cpuResult = new float[elementCount];
        var openclResult = new float[elementCount];

        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)(random.NextDouble() * 10.0);
            hostB[i] = (float)(random.NextDouble() * 10.0);
        }

        // CPU computation
        for (var i = 0; i < elementCount; i++)
        {
            cpuResult[i] = hostA[i] * hostB[i];
        }

        // OpenCL computation
        await using var accelerator = CreateAccelerator();

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceA.WriteAsync(hostA.AsSpan(), 0);
        await deviceB.WriteAsync(hostB.AsSpan(), 0);

        var kernelDef = new KernelDefinition("vectorMultiply", VectorMultiplySource, "vectorMultiply");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        await kernel.LaunchAsync<float>(launchConfig, deviceA, deviceB, deviceC, elementCount);
        await accelerator.SynchronizeAsync();

        await deviceC.ReadAsync(openclResult.AsSpan(), 0);

        // Compare results
        var maxError = 0.0f;
        for (var i = 0; i < elementCount; i++)
        {
            var error = Math.Abs(openclResult[i] - cpuResult[i]);
            maxError = Math.Max(maxError, error);
        }

        Output.WriteLine($"Cross-Backend Validation (Vector Multiply):");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Max Error: {maxError:E6}");

        maxError.Should().BeLessThan(0.001f, "OpenCL and CPU results should match");
    }

    /// <summary>
    /// Validates reduction operation (dot product) matches CPU.
    /// </summary>
    [SkippableFact]
    public async Task Dot_Product_Should_Match_CPU_Results()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        const int elementCount = 10000;

        var hostA = new float[elementCount];
        var hostB = new float[elementCount];

        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            hostB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }

        // CPU computation
        var cpuResult = 0.0f;
        for (var i = 0; i < elementCount; i++)
        {
            cpuResult += hostA[i] * hostB[i];
        }

        // OpenCL computation
        await using var accelerator = CreateAccelerator();

        const int workGroupSize = 256;
        var numWorkGroups = (elementCount + workGroupSize - 1) / workGroupSize;

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var devicePartial = await accelerator.Memory.AllocateAsync<float>(numWorkGroups);

        await deviceA.WriteAsync(hostA.AsSpan(), 0);
        await deviceB.WriteAsync(hostB.AsSpan(), 0);

        var kernelDef = new KernelDefinition("dotProduct", DotProductSource, "dotProduct");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(numWorkGroups * workGroupSize),
            LocalWorkSize = new Dim3(workGroupSize),
            SharedMemoryBytes = (ulong)(workGroupSize * sizeof(float))
        };

        await kernel.LaunchAsync<float>(launchConfig, deviceA, deviceB, devicePartial, elementCount, IntPtr.Zero);
        await accelerator.SynchronizeAsync();

        // Read partial results and sum on CPU
        var partialResults = new float[numWorkGroups];
        await devicePartial.ReadAsync(partialResults.AsSpan(), 0);

        var openclResult = partialResults.Sum();

        var error = Math.Abs(openclResult - cpuResult);
        var relativeError = error / Math.Abs(cpuResult);

        Output.WriteLine($"Cross-Backend Validation (Dot Product):");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  CPU Result: {cpuResult:F6}");
        Output.WriteLine($"  OpenCL Result: {openclResult:F6}");
        Output.WriteLine($"  Absolute Error: {error:E6}");
        Output.WriteLine($"  Relative Error: {relativeError:E6}");

        relativeError.Should().BeLessThan(0.001f, "Relative error should be less than 0.1%");
    }

    /// <summary>
    /// Validates computational consistency across multiple runs.
    /// </summary>
    [SkippableFact]
    public async Task OpenCL_Results_Should_Be_Deterministic()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        const int elementCount = 10000;
        const int iterations = 5;

        var hostA = new float[elementCount];
        var hostB = new float[elementCount];

        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)(random.NextDouble() * 100.0);
            hostB[i] = (float)(random.NextDouble() * 100.0);
        }

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("vectorAdd", VectorAddSource, "vectorAdd");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        var results = new List<float[]>();

        for (var iter = 0; iter < iterations; iter++)
        {
            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);

            await kernel.LaunchAsync<float>(launchConfig, deviceA, deviceB, deviceC, elementCount);
            await accelerator.SynchronizeAsync();

            var result = new float[elementCount];
            await deviceC.ReadAsync(result.AsSpan(), 0);
            results.Add(result);
        }

        // Compare all results with first iteration
        var reference = results[0];

        for (var iter = 1; iter < iterations; iter++)
        {
            for (var i = 0; i < elementCount; i++)
            {
                results[iter][i].Should().Be(reference[i], $"Result {iter} at index {i} should match reference");
            }
        }

        Output.WriteLine($"Determinism Validation:");
        Output.WriteLine($"  Iterations: {iterations}");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Result: All iterations produced identical results");
    }

    /// <summary>
    /// Validates edge cases match CPU behavior.
    /// </summary>
    [SkippableFact]
    public async Task Edge_Cases_Should_Match_CPU_Behavior()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        var testCases = new[]
        {
            new { A = 0.0f, B = 0.0f, Name = "Zero + Zero" },
            new { A = float.MaxValue, B = 0.0f, Name = "MaxValue + Zero" },
            new { A = float.MinValue, B = 0.0f, Name = "MinValue + Zero" },
            new { A = 1.0f, B = -1.0f, Name = "Positive + Negative" },
            new { A = float.Epsilon, B = float.Epsilon, Name = "Epsilon + Epsilon" },
        };

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("vectorAdd", VectorAddSource, "vectorAdd");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        foreach (var testCase in testCases)
        {
            const int count = 1;

            var hostA = new[] { testCase.A };
            var hostB = new[] { testCase.B };
            var cpuResult = testCase.A + testCase.B;

            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(count);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(count);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(count);

            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);

            var launchConfig = new LaunchConfiguration
            {
                GlobalWorkSize = new Dim3(256),
                LocalWorkSize = new Dim3(256)
            };

            await kernel.LaunchAsync<float>(launchConfig, deviceA, deviceB, deviceC, count);
            await accelerator.SynchronizeAsync();

            var openclResult = new float[count];
            await deviceC.ReadAsync(openclResult.AsSpan(), 0);

            Output.WriteLine($"Edge Case: {testCase.Name}");
            Output.WriteLine($"  CPU: {cpuResult}");
            Output.WriteLine($"  OpenCL: {openclResult[0]}");

            if (float.IsNaN(cpuResult))
            {
                openclResult[0].Should().Match(x => float.IsNaN((float)x!), "Both should be NaN");
            }
            else if (float.IsInfinity(cpuResult))
            {
                openclResult[0].Should().Match(x => float.IsInfinity((float)x!), "Both should be Infinity");
            }
            else
            {
                openclResult[0].Should().BeApproximately(cpuResult, 0.0001f, $"OpenCL should match CPU for {testCase.Name}");
            }
        }
    }
}
