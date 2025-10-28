// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.Execution;

/// <summary>
/// Advanced tests for Metal kernel execution including async execution,
/// graph capture/replay, multi-queue concurrency, and event synchronization.
/// </summary>
[Trait("Category", "Hardware")]
public class MetalKernelExecutionAdvancedTests : MetalTestBase
{
    public MetalKernelExecutionAdvancedTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task AsyncExecution_MultipleConcurrentKernels_ShouldSucceed()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 10000;

        var kernel1 = await CompileSimpleKernel(accelerator!, "kernel1", 2.0f);
        var kernel2 = await CompileSimpleKernel(accelerator!, "kernel2", 3.0f);
        var kernel3 = await CompileSimpleKernel(accelerator!, "kernel3", 4.0f);

        var buffer1 = await accelerator.Memory.AllocateAsync<float>(size);
        var buffer2 = await accelerator.Memory.AllocateAsync<float>(size);
        var buffer3 = await accelerator.Memory.AllocateAsync<float>(size);

        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);
        await buffer1.CopyFromAsync(data.AsMemory());
        await buffer2.CopyFromAsync(data.AsMemory());
        await buffer3.CopyFromAsync(data.AsMemory());

        // Act - Execute concurrently
        var task1 = ExecuteKernel(kernel1, buffer1, size);
        var task2 = ExecuteKernel(kernel2, buffer2, size);
        var task3 = ExecuteKernel(kernel3, buffer3, size);

        await Task.WhenAll(task1, task2, task3);

        // Assert
        var result1 = new float[size];
        var result2 = new float[size];
        var result3 = new float[size];

        await buffer1.CopyToAsync(result1.AsMemory());
        await buffer2.CopyToAsync(result2.AsMemory());
        await buffer3.CopyToAsync(result3.AsMemory());

        result1[0].Should().BeApproximately(2.0f, 0.001f);
        result2[0].Should().BeApproximately(3.0f, 0.001f);
        result3[0].Should().BeApproximately(4.0f, 0.001f);

        Output.WriteLine($"Successfully executed 3 kernels concurrently on {size} elements each");

        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
        await buffer3.DisposeAsync();
    }

    [SkippableFact]
    public async Task CancellationToken_ShouldCancelExecution()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1000000; // Large enough to allow cancellation

        var kernel = await CompileSimpleKernel(accelerator!, "cancellable", 1.0f);
        var buffer = await accelerator.Memory.AllocateAsync<float>(size);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(1)); // Cancel almost immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await ExecuteKernel(kernel, buffer, size, cts.Token);
            }
        });

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task MultiDimensionalExecution_2D_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernelDef = new KernelDefinition
        {
            Name = "matrix_scale",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void matrix_scale(
                    device float* matrix [[buffer(0)]],
                    constant uint& width [[buffer(1)]],
                    constant float& scale [[buffer(2)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    uint idx = id.y * width + id.x;
                    matrix[idx] *= scale;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        const int width = 256;
        const int height = 256;
        const int size = width * height;

        var buffer = await accelerator.Memory.AllocateAsync<float>(size);
        var data = MetalTestDataGenerator.CreateConstantData(size, 5.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(buffer);
        args.AddScalar(width);
        args.AddScalar(2.0f);

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new float[size];
        await buffer.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(10.0f, 0.001f);
        result[size - 1].Should().BeApproximately(10.0f, 0.001f);

        Output.WriteLine($"2D kernel executed on {width}x{height} matrix");

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task SequentialExecution_ShouldMaintainOrder()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1000;

        var kernel1 = await CompileSimpleKernel(accelerator!, "seq1", 2.0f);
        var kernel2 = await CompileSimpleKernel(accelerator!, "seq2", 3.0f);

        var buffer = await accelerator.Memory.AllocateAsync<float>(size);
        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act - Execute sequentially
        await ExecuteKernel(kernel1, buffer, size); // 1.0 * 2.0 = 2.0
        await ExecuteKernel(kernel2, buffer, size); // 2.0 * 3.0 = 6.0

        // Assert
        var result = new float[size];
        await buffer.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(6.0f, 0.001f);

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task LargeWorkload_ShouldExecuteEfficiently()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 10_000_000; // 10 million elements

        var kernel = await CompileSimpleKernel(accelerator!, "large_workload", 2.0f);
        var buffer = await accelerator.Memory.AllocateAsync<float>(size);

        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act
        var perf = new MetalPerformanceMeasurement("Large Workload", Output);
        perf.Start();

        await ExecuteKernel(kernel, buffer, size);

        perf.Stop();

        // Assert
        var result = new float[Math.Min(1000, size)];
        await buffer.CopyToAsync(result.AsMemory(), 0, Math.Min(1000, size));

        result[0].Should().BeApproximately(2.0f, 0.001f);

        perf.LogResults(size * sizeof(float) * 2); // Read + Write

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task KernelWithMultipleScalars_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernelDef = new KernelDefinition
        {
            Name = "multi_scalar",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void multi_scalar(
                    device float* data [[buffer(0)]],
                    constant float& a [[buffer(1)]],
                    constant float& b [[buffer(2)]],
                    constant float& c [[buffer(3)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = data[id] * a + b * c;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        const int size = 1000;
        var buffer = await accelerator.Memory.AllocateAsync<float>(size);
        var data = MetalTestDataGenerator.CreateConstantData(size, 2.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(buffer);
        args.AddScalar(3.0f); // a
        args.AddScalar(4.0f); // b
        args.AddScalar(5.0f); // c

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new float[size];
        await buffer.CopyToAsync(result.AsMemory());

        // 2.0 * 3.0 + 4.0 * 5.0 = 6.0 + 20.0 = 26.0
        result[0].Should().BeApproximately(26.0f, 0.001f);

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task EmptyKernel_ShouldExecuteWithoutError()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernelDef = new KernelDefinition
        {
            Name = "empty_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void empty_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    // Intentionally empty
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        const int size = 100;
        var buffer = await accelerator.Memory.AllocateAsync<float>(size);

        // Act
        var args = new KernelArguments();
        args.AddBuffer(buffer);

        await kernel.ExecuteAsync(args);

        // Assert - Should not throw
        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task RepeatedExecution_ShouldMaintainConsistency()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1000;
        const int iterations = 100;

        var kernel = await CompileSimpleKernel(accelerator!, "repeated", 1.5f);
        var buffer = await accelerator.Memory.AllocateAsync<float>(size);

        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act - Execute multiple times
        for (int i = 0; i < iterations; i++)
        {
            await ExecuteKernel(kernel, buffer, size);
        }

        // Assert
        var result = new float[size];
        await buffer.CopyToAsync(result.AsMemory());

        var expected = Math.Pow(1.5, iterations);
        result[0].Should().BeApproximately((float)expected, (float)(expected * 0.01)); // 1% tolerance

        Output.WriteLine($"After {iterations} iterations: {result[0]} (expected ~{expected})");

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task DifferentDataTypes_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernelDef = new KernelDefinition
        {
            Name = "int_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void int_kernel(
                    device int* data [[buffer(0)]],
                    constant int& multiplier [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] *= multiplier;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        const int size = 1000;
        var buffer = await accelerator.Memory.AllocateAsync<int>(size);
        var data = MetalTestDataGenerator.CreateIntegerSequence(size, 1, 1);
        await buffer.CopyFromAsync(data.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(buffer);
        args.AddScalar(2);

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new int[size];
        await buffer.CopyToAsync(result.AsMemory());

        result[0].Should().Be(2);
        result[10].Should().Be(22); // 11 * 2

        await buffer.DisposeAsync();
    }

    // Helper methods

    private async Task<ICompiledKernel> CompileSimpleKernel(IAccelerator accelerator, string name, float multiplier)
    {
        var kernelDef = new KernelDefinition
        {
            Name = name,
            Language = KernelLanguage.Metal,
            Code = $@"
                #include <metal_stdlib>
                using namespace metal;

                kernel void {name}(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {{
                    data[id] *= {multiplier}f;
                }}"
        };

        return await accelerator.CompileKernelAsync(kernelDef);
    }

    private static async Task ExecuteKernel(ICompiledKernel kernel, IUnifiedBuffer<float> buffer, int size, CancellationToken cancellationToken = default)
    {
        var args = new KernelArguments();
        args.AddBuffer(buffer);

        await kernel.ExecuteAsync(args, cancellationToken);
    }
}
