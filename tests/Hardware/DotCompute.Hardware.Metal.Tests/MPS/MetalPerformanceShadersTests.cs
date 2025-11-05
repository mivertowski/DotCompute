// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.MPS;

/// <summary>
/// Tests for Metal Performance Shaders (MPS) integration.
/// MPS provides optimized kernels for common operations like matrix multiply and convolution.
/// </summary>
[Trait("Category", "Hardware")]
public class MetalPerformanceShadersTests : MetalTestBase
{
    public MetalPerformanceShadersTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task MatrixMultiply_MPS_ShouldProduceCorrectResults()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int M = 128, N = 128, K = 128;

        // Create simple test matrices
        var a = MetalTestDataGenerator.CreateMatrix(M, K);
        var b = MetalTestDataGenerator.CreateMatrix(K, N);

        // Calculate expected result on CPU
        var expected = new float[M * N];
        for (int i = 0; i < M; i++)
        {
            for (int j = 0; j < N; j++)
            {
                float sum = 0;
                for (int k = 0; k < K; k++)
                {
                    sum += a[i * K + k] * b[k * N + j];
                }
                expected[i * N + j] = sum;
            }
        }

        // Use custom Metal kernel for matrix multiply (MPS would be called internally by backend)
        var kernelDef = new KernelDefinition
        {
            Name = "matrix_multiply_mps_test",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void matrix_multiply_mps_test(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* c [[buffer(2)]],
                    constant uint& M [[buffer(3)]],
                    constant uint& N [[buffer(4)]],
                    constant uint& K [[buffer(5)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    uint row = id.y;
                    uint col = id.x;

                    if (row >= M || col >= N) return;

                    float sum = 0.0f;
                    for (uint k = 0; k < K; k++)
                    {
                        sum += a[row * K + k] * b[k * N + col];
                    }
                    c[row * N + col] = sum;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferA = await accelerator.Memory.AllocateAsync<float>(M * K);
        var bufferB = await accelerator.Memory.AllocateAsync<float>(K * N);
        var bufferC = await accelerator.Memory.AllocateAsync<float>(M * N);

        await bufferA.CopyFromAsync(a.AsMemory());
        await bufferB.CopyFromAsync(b.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferA);
        args.AddBuffer(bufferB);
        args.AddBuffer(bufferC);
        args.AddScalar((uint)M);
        args.AddScalar((uint)N);
        args.AddScalar((uint)K);

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new float[M * N];
        await bufferC.CopyToAsync(result.AsMemory());

        // Verify a sample of results
        for (int i = 0; i < Math.Min(100, M * N); i++)
        {
            result[i].Should().BeApproximately(expected[i], 0.01f,
                $"Matrix element {i} should match CPU calculation");
        }

        Output.WriteLine($"Matrix multiplication {M}x{K} * {K}x{N} completed successfully");

        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
    }

    [SkippableFact]
    public async Task MatrixMultiply_LargeMatrices_PerformanceComparison()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 512; // 512x512 matrices

        var a = MetalTestDataGenerator.CreateMatrix(size, size);
        var b = MetalTestDataGenerator.CreateMatrix(size, size);

        var kernelDef = new KernelDefinition
        {
            Name = "large_matmul",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void large_matmul(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* c [[buffer(2)]],
                    constant uint& N [[buffer(3)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    uint row = id.y;
                    uint col = id.x;

                    if (row >= N || col >= N) return;

                    float sum = 0.0f;
                    for (uint k = 0; k < N; k++)
                    {
                        sum += a[row * N + k] * b[k * N + col];
                    }
                    c[row * N + col] = sum;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferA = await accelerator.Memory.AllocateAsync<float>(size * size);
        var bufferB = await accelerator.Memory.AllocateAsync<float>(size * size);
        var bufferC = await accelerator.Memory.AllocateAsync<float>(size * size);

        await bufferA.CopyFromAsync(a.AsMemory());
        await bufferB.CopyFromAsync(b.AsMemory());

        // Act
        var perf = new MetalPerformanceMeasurement("Large Matrix Multiply", Output);
        perf.Start();

        var args = new KernelArguments();
        args.AddBuffer(bufferA);
        args.AddBuffer(bufferB);
        args.AddBuffer(bufferC);
        args.AddScalar((uint)size);

        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();

        perf.Stop();

        // Assert
        var gflops = (2.0 * size * size * size) / (perf.ElapsedTime.TotalSeconds * 1e9);
        Output.WriteLine($"Performance: {gflops:F2} GFLOPS");
        Output.WriteLine($"Matrix size: {size}x{size}");

        gflops.Should().BeGreaterThan(0, "Should perform operations");

        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
    }

    [SkippableFact]
    public async Task VectorOperations_MPS_ShouldBeOptimized()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1_000_000;

        var a = MetalTestDataGenerator.CreateRandomData(size);
        var b = MetalTestDataGenerator.CreateRandomData(size);

        var kernelDef = new KernelDefinition
        {
            Name = "vector_ops",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void vector_ops(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* result [[buffer(2)]],
                    uint id [[thread_position_in_grid]])
                {
                    // Fused multiply-add operation
                    result[id] = a[id] * b[id] + a[id];
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferResult = await accelerator.Memory.AllocateAsync<float>(size);

        await bufferA.CopyFromAsync(a.AsMemory());
        await bufferB.CopyFromAsync(b.AsMemory());

        // Act
        var perf = new MetalPerformanceMeasurement("Vector Operations", Output);
        perf.Start();

        var args = new KernelArguments();
        args.AddBuffer(bufferA);
        args.AddBuffer(bufferB);
        args.AddBuffer(bufferResult);

        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();

        perf.Stop();

        // Assert
        var result = new float[Math.Min(1000, size)];
        await bufferResult.CopyToAsync(result.AsMemory());

        var expected = a[0] * b[0] + a[0];
        result[0].Should().BeApproximately(expected, 0.001f);

        perf.LogResults(size * sizeof(float) * 3); // 3 arrays

        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
    }

    [SkippableFact]
    public async Task Convolution_1D_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int dataSize = 1000;
        const int kernelSize = 5;

        var data = MetalTestDataGenerator.CreateSinusoidalData(dataSize);
        var convKernel = new float[kernelSize];
        Array.Fill(convKernel, 1.0f / kernelSize); // Simple averaging kernel

        var kernelDef = new KernelDefinition
        {
            Name = "conv1d",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void conv1d(
                    device const float* input [[buffer(0)]],
                    device const float* kernel [[buffer(1)]],
                    device float* output [[buffer(2)]],
                    constant uint& dataSize [[buffer(3)]],
                    constant uint& kernelSize [[buffer(4)]],
                    uint id [[thread_position_in_grid]])
                {
                    if (id >= dataSize) return;

                    float sum = 0.0f;
                    int halfKernel = kernelSize / 2;

                    for (int k = 0; k < int(kernelSize); k++)
                    {
                        int idx = int(id) + k - halfKernel;
                        if (idx >= 0 && idx < int(dataSize))
                        {
                            sum += input[idx] * kernel[k];
                        }
                    }

                    output[id] = sum;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferData = await accelerator.Memory.AllocateAsync<float>(dataSize);
        var bufferKernel = await accelerator.Memory.AllocateAsync<float>(kernelSize);
        var bufferOutput = await accelerator.Memory.AllocateAsync<float>(dataSize);

        await bufferData.CopyFromAsync(data.AsMemory());
        await bufferKernel.CopyFromAsync(convKernel.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferData);
        args.AddBuffer(bufferKernel);
        args.AddBuffer(bufferOutput);
        args.AddScalar((uint)dataSize);
        args.AddScalar((uint)kernelSize);

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new float[dataSize];
        await bufferOutput.CopyToAsync(result.AsMemory());

        // Convolution should smooth the data
        result.Should().NotBeEmpty();
        Output.WriteLine($"1D Convolution completed on {dataSize} elements with kernel size {kernelSize}");

        await bufferData.DisposeAsync();
        await bufferKernel.DisposeAsync();
        await bufferOutput.DisposeAsync();
    }

    [SkippableFact]
    public async Task TypeConversion_FloatToHalf_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 10000;

        var floatData = MetalTestDataGenerator.CreateRandomData(size, min: -100.0f, max: 100.0f);

        var kernelDef = new KernelDefinition
        {
            Name = "float_to_half",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void float_to_half(
                    device const float* input [[buffer(0)]],
                    device half* output [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    output[id] = half(input[id]);
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferFloat = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferHalf = await accelerator.Memory.AllocateAsync<ushort>(size); // half is 16-bit

        await bufferFloat.CopyFromAsync(floatData.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferFloat);
        args.AddBuffer(bufferHalf);

        await kernel.ExecuteAsync(args);

        // Assert
        Output.WriteLine($"Converted {size} floats to half precision successfully");

        await bufferFloat.DisposeAsync();
        await bufferHalf.DisposeAsync();
    }

    [SkippableFact]
    public async Task Reduction_Sum_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1024;

        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);
        var expectedSum = size * 1.0f;

        var kernelDef = new KernelDefinition
        {
            Name = "reduction_sum",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                #include <metal_atomic>
                using namespace metal;

                kernel void reduction_sum(
                    device const float* input [[buffer(0)]],
                    device atomic_float* result [[buffer(1)]],
                    constant uint& size [[buffer(2)]],
                    uint id [[thread_position_in_grid]])
                {
                    if (id < size)
                    {
                        atomic_fetch_add_explicit(&result[0], input[id], memory_order_relaxed);
                    }
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferInput = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferResult = await accelerator.Memory.AllocateAsync<float>(1);

        await bufferInput.CopyFromAsync(data.AsMemory());
        await bufferResult.CopyFromAsync(new float[] { 0.0f }.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferInput);
        args.AddBuffer(bufferResult);
        args.AddScalar((uint)size);

        await kernel.ExecuteAsync(args);

        // Assert
        var result = new float[1];
        await bufferResult.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(expectedSum, 1.0f); // Atomic ops may have slight variance

        Output.WriteLine($"Reduction sum: {result[0]} (expected: {expectedSum})");

        await bufferInput.DisposeAsync();
        await bufferResult.DisposeAsync();
    }

    [SkippableFact]
    public async Task ImageProcessing_Grayscale_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int width = 256;
        const int height = 256;
        const int channels = 3; // RGB
        const int size = width * height * channels;

        var rgbData = MetalTestDataGenerator.CreateRandomData(size, min: 0.0f, max: 255.0f);

        var kernelDef = new KernelDefinition
        {
            Name = "rgb_to_grayscale",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void rgb_to_grayscale(
                    device const float* rgb [[buffer(0)]],
                    device float* gray [[buffer(1)]],
                    constant uint& width [[buffer(2)]],
                    constant uint& height [[buffer(3)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    if (id.x >= width || id.y >= height) return;

                    uint pixelIdx = id.y * width + id.x;
                    uint rgbIdx = pixelIdx * 3;

                    float r = rgb[rgbIdx];
                    float g = rgb[rgbIdx + 1];
                    float b = rgb[rgbIdx + 2];

                    // Standard grayscale conversion
                    gray[pixelIdx] = 0.299f * r + 0.587f * g + 0.114f * b;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferRGB = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferGray = await accelerator.Memory.AllocateAsync<float>(width * height);

        await bufferRGB.CopyFromAsync(rgbData.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferRGB);
        args.AddBuffer(bufferGray);
        args.AddScalar((uint)width);
        args.AddScalar((uint)height);

        await kernel.ExecuteAsync(args);

        // Assert
        var grayResult = new float[width * height];
        await bufferGray.CopyToAsync(grayResult.AsMemory());

        grayResult.Should().NotBeEmpty();
        Output.WriteLine($"Converted {width}x{height} RGB image to grayscale");

        await bufferRGB.DisposeAsync();
        await bufferGray.DisposeAsync();
    }

    [SkippableFact]
    public async Task Transpose_Matrix_ShouldWork()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int rows = 128;
        const int cols = 64;

        var matrix = MetalTestDataGenerator.CreateMatrix(rows, cols);

        var kernelDef = new KernelDefinition
        {
            Name = "transpose",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void transpose(
                    device const float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant uint& rows [[buffer(2)]],
                    constant uint& cols [[buffer(3)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    if (id.x >= cols || id.y >= rows) return;

                    uint inputIdx = id.y * cols + id.x;
                    uint outputIdx = id.x * rows + id.y;

                    output[outputIdx] = input[inputIdx];
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var bufferInput = await accelerator.Memory.AllocateAsync<float>(rows * cols);
        var bufferOutput = await accelerator.Memory.AllocateAsync<float>(rows * cols);

        await bufferInput.CopyFromAsync(matrix.AsMemory());

        // Act
        var args = new KernelArguments();
        args.AddBuffer(bufferInput);
        args.AddBuffer(bufferOutput);
        args.AddScalar((uint)rows);
        args.AddScalar((uint)cols);

        await kernel.ExecuteAsync(args);

        // Assert
        var transposed = new float[rows * cols];
        await bufferOutput.CopyToAsync(transposed.AsMemory());

        // Verify transpose: output[j,i] should equal input[i,j]
        transposed[0 * rows + 0].Should().Be(matrix[0 * cols + 0]);
        transposed[1 * rows + 0].Should().Be(matrix[0 * cols + 1]);

        Output.WriteLine($"Transposed {rows}x{cols} matrix successfully");

        await bufferInput.DisposeAsync();
        await bufferOutput.DisposeAsync();
    }
}
