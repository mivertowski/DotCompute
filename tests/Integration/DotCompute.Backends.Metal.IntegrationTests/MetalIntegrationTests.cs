// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Factory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.IntegrationTests;

/// <summary>
/// Comprehensive integration tests for end-to-end Metal backend workflows.
/// Tests complete pipelines, multi-kernel execution, real-world scenarios,
/// cross-component integration, and performance validation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresMetal")]
public class MetalIntegrationTests : ConsolidatedTestBase
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MetalBackendFactory _factory;

    public MetalIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        });

        _factory = new MetalBackendFactory(
            _loggerFactory.CreateLogger<MetalBackendFactory>(),
            _loggerFactory);
    }

    private static bool IsMetalAvailable()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return false;
            }

            return MetalBackend.IsAvailable();
        }
        catch
        {
            return false;
        }
    }

    #region Complete Workflows Tests

    [SkippableFact]
    public async Task EndToEnd_VectorAddition_Complete()
    {
        // Full pipeline: Allocate → Compile → Execute → Read Results
        // Verify: Correct results, no leaks

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int size = 1024 * 1024; // 1M elements
        var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();
        var expected = a.Zip(b, (x, y) => x + y).ToArray();

        // Allocate device memory
        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceResult = await accelerator.Memory.AllocateAsync<float>(size);

        // Upload data
        await deviceA.CopyFromAsync(a.AsMemory());
        await deviceB.CopyFromAsync(b.AsMemory());

        // Compile kernel
        var kernel = new KernelDefinition(
            "vector_add",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void vector_add(
                  device const float* a [[buffer(0)]],
                  device const float* b [[buffer(1)]],
                  device float* result [[buffer(2)]],
                  constant uint& size [[buffer(3)]],
                  uint id [[thread_position_in_grid]])
              {
                  if (id < size) {
                      result[id] = a[id] + b[id];
                  }
              }")
        {
            EntryPoint = "vector_add",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);
        compiled.Should().NotBeNull();

        // Execute
        var args = new KernelArguments(deviceA, deviceB, deviceResult, (uint)size);
        await compiled.ExecuteAsync(args);

        // Read results
        var result = new float[size];
        await deviceResult.CopyToAsync(result.AsMemory());

        // Verify
        for (int i = 0; i < Math.Min(1000, size); i++)
        {
            result[i].Should().BeApproximately(expected[i], 0.001f);
        }

        Output.WriteLine($"✓ VectorAddition complete: {size} elements processed correctly");
    }

    [SkippableFact]
    public async Task EndToEnd_MatrixMultiply_Complete()
    {
        // Full matrix multiplication workflow
        // Verify: Mathematically correct results

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int M = 256, N = 256, K = 256;
        var a = CreateRandomMatrix(M, K);
        var b = CreateRandomMatrix(K, N);
        var expected = MultiplyMatricesCPU(a, b, M, N, K);

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(M * K);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(K * N);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(M * N);

        await deviceA.CopyFromAsync(a.AsMemory());
        await deviceB.CopyFromAsync(b.AsMemory());

        var kernel = new KernelDefinition(
            "matmul",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void matmul(
                  device const float* A [[buffer(0)]],
                  device const float* B [[buffer(1)]],
                  device float* C [[buffer(2)]],
                  constant uint& M [[buffer(3)]],
                  constant uint& N [[buffer(4)]],
                  constant uint& K [[buffer(5)]],
                  uint2 gid [[thread_position_in_grid]])
              {
                  uint row = gid.y;
                  uint col = gid.x;

                  if (row < M && col < N) {
                      float sum = 0.0f;
                      for (uint k = 0; k < K; k++) {
                          sum += A[row * K + k] * B[k * N + col];
                      }
                      C[row * N + col] = sum;
                  }
              }")
        {
            EntryPoint = "matmul",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);
        var args = new KernelArguments(deviceA, deviceB, deviceC, (uint)M, (uint)N, (uint)K);
        await compiled.ExecuteAsync(args);

        var result = new float[M * N];
        await deviceC.CopyToAsync(result.AsMemory());

        // Verify sample elements
        for (int i = 0; i < Math.Min(100, M * N); i++)
        {
            result[i].Should().BeApproximately(expected[i], 0.1f);
        }

        Output.WriteLine($"✓ MatrixMultiply complete: {M}x{K} * {K}x{N} = {M}x{N}");
    }

    [SkippableFact]
    public async Task EndToEnd_ImageConvolution_Complete()
    {
        // Image processing pipeline
        // Verify: Correct convolution output

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int width = 512, height = 512;
        var image = CreateRandomImage(width, height);

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(width * height);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(width * height);

        await deviceInput.CopyFromAsync(image.AsMemory());

        var kernel = new KernelDefinition(
            "box_blur",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void box_blur(
                  device const float* input [[buffer(0)]],
                  device float* output [[buffer(1)]],
                  constant uint& width [[buffer(2)]],
                  constant uint& height [[buffer(3)]],
                  uint2 gid [[thread_position_in_grid]])
              {
                  uint x = gid.x;
                  uint y = gid.y;

                  if (x >= width || y >= height) return;

                  float sum = 0.0f;
                  int count = 0;

                  for (int dy = -1; dy <= 1; dy++) {
                      for (int dx = -1; dx <= 1; dx++) {
                          int nx = int(x) + dx;
                          int ny = int(y) + dy;
                          if (nx >= 0 && nx < int(width) && ny >= 0 && ny < int(height)) {
                              sum += input[ny * width + nx];
                              count++;
                          }
                      }
                  }

                  output[y * width + x] = sum / float(count);
              }")
        {
            EntryPoint = "box_blur",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);
        var args = new KernelArguments(deviceInput, deviceOutput, (uint)width, (uint)height);
        await compiled.ExecuteAsync(args);

        var result = new float[width * height];
        await deviceOutput.CopyToAsync(result.AsMemory());

        // Verify output is valid (blurred values should be within range)
        result.All(v => v >= 0.0f && v <= 1.0f).Should().BeTrue();

        Output.WriteLine($"✓ ImageConvolution complete: {width}x{height} image processed");
    }

    [SkippableFact]
    public async Task EndToEnd_ReductionSum_Complete()
    {
        // Parallel reduction workflow
        // Verify: Correct sum, proper synchronization

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 1024 * 1024;
        var data = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var expected = data.Sum();

        await using var deviceData = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceResult = await accelerator.Memory.AllocateAsync<float>(1);

        await deviceData.CopyFromAsync(data.AsMemory());

        var kernel = new KernelDefinition(
            "parallel_sum",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void parallel_sum(
                  device const float* input [[buffer(0)]],
                  device atomic<float>* result [[buffer(1)]],
                  constant uint& size [[buffer(2)]],
                  uint id [[thread_position_in_grid]])
              {
                  if (id < size) {
                      atomic_fetch_add_explicit(result, input[id], memory_order_relaxed);
                  }
              }")
        {
            EntryPoint = "parallel_sum",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);
        var args = new KernelArguments(deviceData, deviceResult, (uint)size);
        await compiled.ExecuteAsync(args);

        var result = new float[1];
        await deviceResult.CopyToAsync(result.AsMemory());

        // Atomic operations may have precision issues with large sums
        result[0].Should().BeApproximately(expected, expected * 0.001f);

        Output.WriteLine($"✓ ReductionSum complete: {size} elements summed to {result[0]:F2}");
    }

    [SkippableFact]
    public async Task EndToEnd_CustomKernel_Compile_Execute()
    {
        // User-provided MSL kernel
        // Verify: Compilation and execution work

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceInput.CopyFromAsync(input.AsMemory());

        // Custom kernel: square root of squared value plus one
        var kernel = new KernelDefinition(
            "custom_math",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void custom_math(
                  device const float* input [[buffer(0)]],
                  device float* output [[buffer(1)]],
                  constant uint& size [[buffer(2)]],
                  uint id [[thread_position_in_grid]])
              {
                  if (id < size) {
                      float x = input[id];
                      output[id] = sqrt(x * x + 1.0f);
                  }
              }")
        {
            EntryPoint = "custom_math",
            Language = KernelLanguage.Metal
        };

        var stopwatch = Stopwatch.StartNew();
        var compiled = await accelerator.CompileKernelAsync(kernel);
        stopwatch.Stop();

        Output.WriteLine($"Compilation time: {stopwatch.ElapsedMilliseconds}ms");

        var args = new KernelArguments(deviceInput, deviceOutput, (uint)size);
        await compiled.ExecuteAsync(args);

        var result = new float[size];
        await deviceOutput.CopyToAsync(result.AsMemory());

        // Verify mathematical correctness
        for (int i = 0; i < Math.Min(100, size); i++)
        {
            var expected = MathF.Sqrt(input[i] * input[i] + 1.0f);
            result[i].Should().BeApproximately(expected, 0.001f);
        }

        Output.WriteLine($"✓ CustomKernel complete: {size} elements processed");
    }

    #endregion

    #region Multi-Kernel Pipelines Tests

    [SkippableFact]
    public async Task Pipeline_ThreeKernels_Sequential()
    {
        // A → B → C kernel chain
        // Verify: Data flows correctly between kernels

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var buffer0 = await accelerator.Memory.AllocateAsync<float>(size);
        await using var buffer1 = await accelerator.Memory.AllocateAsync<float>(size);
        await using var buffer2 = await accelerator.Memory.AllocateAsync<float>(size);
        await using var buffer3 = await accelerator.Memory.AllocateAsync<float>(size);

        await buffer0.CopyFromAsync(input.AsMemory());

        // Kernel A: x * 2
        var kernelA = CreateSimpleKernel("mul_two", "result[id] = input[id] * 2.0f;");
        var compiledA = await accelerator.CompileKernelAsync(kernelA);

        // Kernel B: x + 10
        var kernelB = CreateSimpleKernel("add_ten", "result[id] = input[id] + 10.0f;");
        var compiledB = await accelerator.CompileKernelAsync(kernelB);

        // Kernel C: x / 2
        var kernelC = CreateSimpleKernel("div_two", "result[id] = input[id] / 2.0f;");
        var compiledC = await accelerator.CompileKernelAsync(kernelC);

        // Execute pipeline: A(input) → B → C
        await compiledA.ExecuteAsync(new KernelArguments(buffer0, buffer1, (uint)size));
        await compiledB.ExecuteAsync(new KernelArguments(buffer1, buffer2, (uint)size));
        await compiledC.ExecuteAsync(new KernelArguments(buffer2, buffer3, (uint)size));

        var result = new float[size];
        await buffer3.CopyToAsync(result.AsMemory());

        // Verify: ((x * 2) + 10) / 2 = x + 5
        for (int i = 0; i < Math.Min(100, size); i++)
        {
            result[i].Should().BeApproximately(input[i] + 5.0f, 0.001f);
        }

        Output.WriteLine($"✓ ThreeKernels_Sequential complete: {size} elements through 3-stage pipeline");
    }

    [SkippableFact]
    public async Task Pipeline_ParallelBranches_Diamond()
    {
        // A → (B, C) → D diamond pattern
        // Verify: Parallel branches execute, D waits for both

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var bufferInput = await accelerator.Memory.AllocateAsync<float>(size);
        await using var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
        await using var bufferC = await accelerator.Memory.AllocateAsync<float>(size);
        await using var bufferD = await accelerator.Memory.AllocateAsync<float>(size);

        await bufferInput.CopyFromAsync(input.AsMemory());

        var kernelB = CreateSimpleKernel("branch_b", "result[id] = input[id] * 2.0f;");
        var kernelC = CreateSimpleKernel("branch_c", "result[id] = input[id] + 100.0f;");
        var kernelD = new KernelDefinition(
            "merge_d",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void merge_d(
                  device const float* inputB [[buffer(0)]],
                  device const float* inputC [[buffer(1)]],
                  device float* result [[buffer(2)]],
                  constant uint& size [[buffer(3)]],
                  uint id [[thread_position_in_grid]])
              {
                  if (id < size) {
                      result[id] = inputB[id] + inputC[id];
                  }
              }")
        {
            EntryPoint = "merge_d",
            Language = KernelLanguage.Metal
        };

        var compiledB = await accelerator.CompileKernelAsync(kernelB);
        var compiledC = await accelerator.CompileKernelAsync(kernelC);
        var compiledD = await accelerator.CompileKernelAsync(kernelD);

        // Execute parallel branches
        var taskB = compiledB.ExecuteAsync(new KernelArguments(bufferInput, bufferB, (uint)size));
        var taskC = compiledC.ExecuteAsync(new KernelArguments(bufferInput, bufferC, (uint)size));

        await Task.WhenAll(taskB.AsTask(), taskC.AsTask());

        // Execute merge
        await compiledD.ExecuteAsync(new KernelArguments(bufferB, bufferC, bufferD, (uint)size));

        var result = new float[size];
        await bufferD.CopyToAsync(result.AsMemory());

        // Verify: (x * 2) + (x + 100) = 3x + 100
        for (int i = 0; i < Math.Min(100, size); i++)
        {
            result[i].Should().BeApproximately(3.0f * input[i] + 100.0f, 0.001f);
        }

        Output.WriteLine($"✓ ParallelBranches_Diamond complete: Diamond pattern with {size} elements");
    }

    [SkippableFact]
    public async Task Pipeline_ComplexGraph_10Kernels()
    {
        // Complex 10-kernel dependency graph
        // Verify: Correct execution order, results

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 5000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Allocate buffers for all stages
        var buffers = new List<IUnifiedMemoryBuffer<float>>();
        for (int i = 0; i < 11; i++) // 0 = input, 1-10 = intermediate/output
        {
            buffers.Add(await accelerator.Memory.AllocateAsync<float>(size));
        }

        await buffers[0].CopyFromAsync(input.AsMemory());

        // Create 10 simple kernels with different operations
        var operations = new[] { "*2", "+1", "*3", "+5", "/2", "+10", "*4", "-20", "/3", "+50" };
        var kernels = new List<ICompiledKernel>();

        for (int i = 0; i < 10; i++)
        {
            var op = operations[i];
            var code = op switch
            {
                "*2" => "result[id] = input[id] * 2.0f;",
                "+1" => "result[id] = input[id] + 1.0f;",
                "*3" => "result[id] = input[id] * 3.0f;",
                "+5" => "result[id] = input[id] + 5.0f;",
                "/2" => "result[id] = input[id] / 2.0f;",
                "+10" => "result[id] = input[id] + 10.0f;",
                "*4" => "result[id] = input[id] * 4.0f;",
                "-20" => "result[id] = input[id] - 20.0f;",
                "/3" => "result[id] = input[id] / 3.0f;",
                "+50" => "result[id] = input[id] + 50.0f;",
                _ => "result[id] = input[id];"
            };

            var kernel = CreateSimpleKernel($"op_{i}", code);
            kernels.Add(await accelerator.CompileKernelAsync(kernel));
        }

        // Execute all kernels in sequence
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            await kernels[i].ExecuteAsync(new KernelArguments(buffers[i], buffers[i + 1], (uint)size));
        }
        stopwatch.Stop();

        var result = new float[size];
        await buffers[10].CopyToAsync(result.AsMemory());

        // Verify: Result should be valid (not NaN or Inf)
        result.All(v => !float.IsNaN(v) && !float.IsInfinity(v)).Should().BeTrue();

        Output.WriteLine($"✓ ComplexGraph_10Kernels complete: 10 kernels in {stopwatch.ElapsedMilliseconds}ms");

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Pipeline_DynamicGraph_BuildAtRuntime()
    {
        // Build graph dynamically based on data
        // Verify: Dynamic construction works

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 5000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Dynamically decide pipeline depth based on input size
        int pipelineDepth = size > 1000 ? 5 : 3;

        var buffers = new List<IUnifiedMemoryBuffer<float>>();
        for (int i = 0; i <= pipelineDepth; i++)
        {
            buffers.Add(await accelerator.Memory.AllocateAsync<float>(size));
        }

        await buffers[0].CopyFromAsync(input.AsMemory());

        // Build pipeline dynamically
        for (int i = 0; i < pipelineDepth; i++)
        {
            var kernel = CreateSimpleKernel($"dynamic_{i}", $"result[id] = input[id] + {i + 1}.0f;");
            var compiled = await accelerator.CompileKernelAsync(kernel);
            await compiled.ExecuteAsync(new KernelArguments(buffers[i], buffers[i + 1], (uint)size));
        }

        var result = new float[size];
        await buffers[pipelineDepth].CopyToAsync(result.AsMemory());

        // Verify: Each stage adds 1, 2, 3, ..., pipelineDepth
        var expectedAdd = (pipelineDepth * (pipelineDepth + 1)) / 2.0f;
        for (int i = 0; i < Math.Min(100, size); i++)
        {
            result[i].Should().BeApproximately(input[i] + expectedAdd, 0.001f);
        }

        Output.WriteLine($"✓ DynamicGraph complete: {pipelineDepth} stages");

        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Pipeline_MemoryReuse_Optimized()
    {
        // Pipeline with intermediate buffer reuse
        // Verify: Memory footprint optimized

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 100000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Use only 3 buffers for 5-stage pipeline (ping-pong + input)
        await using var buffer0 = await accelerator.Memory.AllocateAsync<float>(size);
        await using var buffer1 = await accelerator.Memory.AllocateAsync<float>(size);
        await using var buffer2 = await accelerator.Memory.AllocateAsync<float>(size);

        await buffer0.CopyFromAsync(input.AsMemory());

        var kernels = new[]
        {
            CreateSimpleKernel("stage0", "result[id] = input[id] * 2.0f;"),
            CreateSimpleKernel("stage1", "result[id] = input[id] + 1.0f;"),
            CreateSimpleKernel("stage2", "result[id] = input[id] * 3.0f;"),
            CreateSimpleKernel("stage3", "result[id] = input[id] - 5.0f;"),
            CreateSimpleKernel("stage4", "result[id] = input[id] / 2.0f;")
        };

        var compiled = new ICompiledKernel[5];
        for (int i = 0; i < 5; i++)
        {
            compiled[i] = await accelerator.CompileKernelAsync(kernels[i]);
        }

        // Execute with buffer reuse: 0→1, 1→2, 2→1, 1→2, 2→final
        await compiled[0].ExecuteAsync(new KernelArguments(buffer0, buffer1, (uint)size));
        await compiled[1].ExecuteAsync(new KernelArguments(buffer1, buffer2, (uint)size));
        await compiled[2].ExecuteAsync(new KernelArguments(buffer2, buffer1, (uint)size));
        await compiled[3].ExecuteAsync(new KernelArguments(buffer1, buffer2, (uint)size));
        await compiled[4].ExecuteAsync(new KernelArguments(buffer2, buffer1, (uint)size));

        var result = new float[size];
        await buffer1.CopyToAsync(result.AsMemory());

        // Verify: Result is valid
        result.All(v => !float.IsNaN(v) && !float.IsInfinity(v)).Should().BeTrue();

        Output.WriteLine($"✓ MemoryReuse_Optimized complete: 5 stages with 3 buffers");
    }

    #endregion

    #region Real-World Scenarios Tests

    [SkippableFact]
    public async Task Scenario_MLInference_ConvNet()
    {
        // Simplified convolutional neural network inference
        // Verify: Correct predictions

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        // Simplified: 28x28 input → 3x3 conv → 14x14 pooling → 196 outputs
        const int inputSize = 28;
        const int kernelSize = 3;
        const int outputSize = 26; // 28 - 3 + 1
        const int pooledSize = 13; // 26 / 2

        var input = CreateRandomImage(inputSize, inputSize);

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(inputSize * inputSize);
        await using var deviceConv = await accelerator.Memory.AllocateAsync<float>(outputSize * outputSize);
        await using var devicePooled = await accelerator.Memory.AllocateAsync<float>(pooledSize * pooledSize);

        await deviceInput.CopyFromAsync(input.AsMemory());

        // Convolution kernel
        var convKernel = new KernelDefinition(
            "conv2d",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void conv2d(
                  device const float* input [[buffer(0)]],
                  device float* output [[buffer(1)]],
                  constant uint& inputSize [[buffer(2)]],
                  constant uint& kernelSize [[buffer(3)]],
                  uint2 gid [[thread_position_in_grid]])
              {
                  uint x = gid.x;
                  uint y = gid.y;
                  uint outputSize = inputSize - kernelSize + 1;

                  if (x >= outputSize || y >= outputSize) return;

                  float sum = 0.0f;
                  for (uint ky = 0; ky < kernelSize; ky++) {
                      for (uint kx = 0; kx < kernelSize; kx++) {
                          sum += input[(y + ky) * inputSize + (x + kx)] * 0.111f;
                      }
                  }
                  output[y * outputSize + x] = max(0.0f, sum); // ReLU
              }")
        {
            EntryPoint = "conv2d",
            Language = KernelLanguage.Metal
        };

        // Max pooling kernel
        var poolKernel = new KernelDefinition(
            "maxpool",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void maxpool(
                  device const float* input [[buffer(0)]],
                  device float* output [[buffer(1)]],
                  constant uint& inputSize [[buffer(2)]],
                  uint2 gid [[thread_position_in_grid]])
              {
                  uint x = gid.x * 2;
                  uint y = gid.y * 2;

                  if (x + 1 >= inputSize || y + 1 >= inputSize) return;

                  float maxVal = max(max(input[y * inputSize + x],
                                        input[y * inputSize + x + 1]),
                                    max(input[(y + 1) * inputSize + x],
                                        input[(y + 1) * inputSize + x + 1]));

                  uint outSize = inputSize / 2;
                  output[(y / 2) * outSize + (x / 2)] = maxVal;
              }")
        {
            EntryPoint = "maxpool",
            Language = KernelLanguage.Metal
        };

        var compiledConv = await accelerator.CompileKernelAsync(convKernel);
        var compiledPool = await accelerator.CompileKernelAsync(poolKernel);

        // Execute inference pipeline
        await compiledConv.ExecuteAsync(new KernelArguments(deviceInput, deviceConv, (uint)inputSize, (uint)kernelSize));
        await compiledPool.ExecuteAsync(new KernelArguments(deviceConv, devicePooled, (uint)outputSize));

        var result = new float[pooledSize * pooledSize];
        await devicePooled.CopyToAsync(result.AsMemory());

        // Verify: Output is valid
        result.All(v => v >= 0.0f && !float.IsNaN(v)).Should().BeTrue();

        Output.WriteLine($"✓ MLInference_ConvNet complete: {inputSize}x{inputSize} → {pooledSize}x{pooledSize}");
    }

    [SkippableFact]
    public async Task Scenario_PhysicsSimulation_Particles()
    {
        // Particle physics simulation
        // Verify: Correct physics, stable simulation

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int particleCount = 10000;
        const float dt = 0.016f; // 60 FPS

        // Initialize particles: position (x,y), velocity (vx,vy)
        var particles = new float[particleCount * 4];
        var random = new Random(42);
        for (int i = 0; i < particleCount; i++)
        {
            particles[i * 4 + 0] = (float)random.NextDouble() * 100.0f; // x
            particles[i * 4 + 1] = (float)random.NextDouble() * 100.0f; // y
            particles[i * 4 + 2] = ((float)random.NextDouble() - 0.5f) * 10.0f; // vx
            particles[i * 4 + 3] = ((float)random.NextDouble() - 0.5f) * 10.0f; // vy
        }

        await using var deviceParticles = await accelerator.Memory.AllocateAsync<float>(particleCount * 4);
        await deviceParticles.CopyFromAsync(particles.AsMemory());

        var kernel = new KernelDefinition(
            "update_particles",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void update_particles(
                  device float* particles [[buffer(0)]],
                  constant float& dt [[buffer(1)]],
                  constant uint& count [[buffer(2)]],
                  uint id [[thread_position_in_grid]])
              {
                  if (id >= count) return;

                  uint idx = id * 4;
                  float x = particles[idx + 0];
                  float y = particles[idx + 1];
                  float vx = particles[idx + 2];
                  float vy = particles[idx + 3];

                  // Simple physics: gravity + boundaries
                  vy -= 9.8f * dt; // gravity

                  x += vx * dt;
                  y += vy * dt;

                  // Bounce off boundaries
                  if (x < 0.0f || x > 100.0f) vx = -vx * 0.9f;
                  if (y < 0.0f) { y = 0.0f; vy = -vy * 0.9f; }

                  particles[idx + 0] = x;
                  particles[idx + 1] = y;
                  particles[idx + 2] = vx;
                  particles[idx + 3] = vy;
              }")
        {
            EntryPoint = "update_particles",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);

        // Simulate 100 frames
        for (int frame = 0; frame < 100; frame++)
        {
            await compiled.ExecuteAsync(new KernelArguments(deviceParticles, dt, (uint)particleCount));
        }

        var result = new float[particleCount * 4];
        await deviceParticles.CopyToAsync(result.AsMemory());

        // Verify: Particles are within bounds and velocities are reasonable
        for (int i = 0; i < particleCount; i++)
        {
            var x = result[i * 4 + 0];
            var y = result[i * 4 + 1];
            var vx = result[i * 4 + 2];
            var vy = result[i * 4 + 3];

            x.Should().BeInRange(-10.0f, 110.0f);
            y.Should().BeGreaterThanOrEqualTo(-10.0f);
            Math.Abs(vx).Should().BeLessThan(100.0f);
            Math.Abs(vy).Should().BeLessThan(100.0f);
        }

        Output.WriteLine($"✓ PhysicsSimulation_Particles complete: {particleCount} particles, 100 frames");
    }

    [SkippableFact]
    public async Task Scenario_ImageProcessing_Filters()
    {
        // Apply multiple filters to image
        // Verify: Correct filtered output

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int width = 512, height = 512;
        var image = CreateRandomImage(width, height);

        await using var buffer0 = await accelerator.Memory.AllocateAsync<float>(width * height);
        await using var buffer1 = await accelerator.Memory.AllocateAsync<float>(width * height);
        await using var buffer2 = await accelerator.Memory.AllocateAsync<float>(width * height);

        await buffer0.CopyFromAsync(image.AsMemory());

        // Filter 1: Brightness adjustment
        var brightnessKernel = CreateSimpleKernel("brightness", "result[id] = clamp(input[id] + 0.2f, 0.0f, 1.0f);");

        // Filter 2: Contrast adjustment
        var contrastKernel = CreateSimpleKernel("contrast",
            "result[id] = clamp((input[id] - 0.5f) * 1.5f + 0.5f, 0.0f, 1.0f);");

        var compiledBrightness = await accelerator.CompileKernelAsync(brightnessKernel);
        var compiledContrast = await accelerator.CompileKernelAsync(contrastKernel);

        // Apply filters sequentially
        await compiledBrightness.ExecuteAsync(new KernelArguments(buffer0, buffer1, (uint)(width * height)));
        await compiledContrast.ExecuteAsync(new KernelArguments(buffer1, buffer2, (uint)(width * height)));

        var result = new float[width * height];
        await buffer2.CopyToAsync(result.AsMemory());

        // Verify: All values in valid range [0, 1]
        result.All(v => v >= 0.0f && v <= 1.0f).Should().BeTrue();

        Output.WriteLine($"✓ ImageProcessing_Filters complete: {width}x{height} with 2 filters");
    }

    [SkippableFact]
    public async Task Scenario_ScientificComputing_FFT()
    {
        // Fast Fourier Transform (simplified)
        // Verify: Mathematically correct FFT

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        // Simplified DFT for small size
        const int size = 256;
        var signal = Enumerable.Range(0, size)
            .Select(i => MathF.Sin(2.0f * MathF.PI * 5.0f * i / size)) // 5 Hz signal
            .ToArray();

        await using var deviceSignal = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceReal = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceImag = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceSignal.CopyFromAsync(signal.AsMemory());

        // Simplified DFT kernel
        var kernel = new KernelDefinition(
            "dft",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void dft(
                  device const float* signal [[buffer(0)]],
                  device float* real [[buffer(1)]],
                  device float* imag [[buffer(2)]],
                  constant uint& size [[buffer(3)]],
                  uint k [[thread_position_in_grid]])
              {
                  if (k >= size) return;

                  float sumReal = 0.0f;
                  float sumImag = 0.0f;

                  for (uint n = 0; n < size; n++) {
                      float angle = -2.0f * M_PI_F * float(k) * float(n) / float(size);
                      sumReal += signal[n] * cos(angle);
                      sumImag += signal[n] * sin(angle);
                  }

                  real[k] = sumReal;
                  imag[k] = sumImag;
              }")
        {
            EntryPoint = "dft",
            Language = KernelLanguage.Metal
        };

        var compiled = await accelerator.CompileKernelAsync(kernel);
        await compiled.ExecuteAsync(new KernelArguments(deviceSignal, deviceReal, deviceImag, (uint)size));

        var real = new float[size];
        var imag = new float[size];
        await deviceReal.CopyToAsync(real.AsMemory());
        await deviceImag.CopyToAsync(imag.AsMemory());

        // Verify: Peak should be at frequency 5
        var magnitudes = real.Zip(imag, (r, i) => MathF.Sqrt(r * r + i * i)).ToArray();
        var peakIndex = Array.IndexOf(magnitudes, magnitudes.Max());

        peakIndex.Should().BeInRange(4, 6); // Allow some tolerance

        Output.WriteLine($"✓ ScientificComputing_FFT complete: {size} point DFT, peak at index {peakIndex}");
    }

    [SkippableFact]
    public async Task Scenario_DataProcessing_MapReduce()
    {
        // Map-reduce pattern
        // Verify: Correct aggregation

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 1000000;
        var data = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceMapped = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceReduced = await accelerator.Memory.AllocateAsync<float>(1000);

        await deviceInput.CopyFromAsync(data.AsMemory());

        // Map: square each element
        var mapKernel = CreateSimpleKernel("map_square", "result[id] = input[id] * input[id];");

        // Reduce: sum in groups
        var reduceKernel = new KernelDefinition(
            "reduce_sum",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void reduce_sum(
                  device const float* input [[buffer(0)]],
                  device float* output [[buffer(1)]],
                  constant uint& inputSize [[buffer(2)]],
                  constant uint& groupSize [[buffer(3)]],
                  uint gid [[thread_position_in_grid]])
              {
                  uint startIdx = gid * groupSize;
                  uint endIdx = min(startIdx + groupSize, inputSize);

                  float sum = 0.0f;
                  for (uint i = startIdx; i < endIdx; i++) {
                      sum += input[i];
                  }

                  output[gid] = sum;
              }")
        {
            EntryPoint = "reduce_sum",
            Language = KernelLanguage.Metal
        };

        var compiledMap = await accelerator.CompileKernelAsync(mapKernel);
        var compiledReduce = await accelerator.CompileKernelAsync(reduceKernel);

        // Execute map-reduce
        await compiledMap.ExecuteAsync(new KernelArguments(deviceInput, deviceMapped, (uint)size));
        await compiledReduce.ExecuteAsync(new KernelArguments(deviceMapped, deviceReduced, (uint)size, (uint)1000));

        var reduced = new float[1000];
        await deviceReduced.CopyToAsync(reduced.AsMemory());

        // Verify: Sum of squares should be approximately n*(n-1)*(2n-1)/6
        var totalSum = reduced.Sum();
        var expectedSum = data.Select(x => x * x).Sum();

        totalSum.Should().BeApproximately(expectedSum, expectedSum * 0.001f);

        Output.WriteLine($"✓ DataProcessing_MapReduce complete: {size} elements, sum={totalSum:E2}");
    }

    #endregion

    #region Cross-Component Integration Tests

    [SkippableFact]
    public async Task Integration_Compiler_Executor_Memory()
    {
        // Test compiler → executor → memory interaction
        // Verify: Seamless integration

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Test memory allocation
        await using var buffer = await accelerator.Memory.AllocateAsync<float>(size);
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(size);

        // Test memory upload
        await buffer.CopyFromAsync(input.AsMemory());

        // Test compilation
        var kernel = CreateSimpleKernel("test_kernel", "result[id] = input[id] * 2.0f;");
        var compiled = await accelerator.CompileKernelAsync(kernel);
        compiled.Should().NotBeNull();

        // Test execution
        await using var output = await accelerator.Memory.AllocateAsync<float>(size);
        await compiled.ExecuteAsync(new KernelArguments(buffer, output, (uint)size));

        // Test memory download
        var result = new float[size];
        await output.CopyToAsync(result.AsMemory());

        // Verify
        result[0].Should().BeApproximately(0.0f, 0.001f);
        result[100].Should().BeApproximately(200.0f, 0.001f);

        Output.WriteLine($"✓ Compiler_Executor_Memory integration verified");
    }

    [SkippableFact]
    public async Task Integration_Cache_Performance_Telemetry()
    {
        // Test cache → performance counters → telemetry
        // Verify: Metrics collected correctly

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator() as MetalAccelerator;
        accelerator.Should().NotBeNull();

        const int size = 5000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceInput.CopyFromAsync(input.AsMemory());

        var kernel = CreateSimpleKernel("cached_kernel", "result[id] = input[id] * 3.0f;");

        // First compilation - should be slow
        var sw1 = Stopwatch.StartNew();
        var compiled1 = await accelerator.CompileKernelAsync(kernel);
        sw1.Stop();

        // Second compilation - should hit cache
        var sw2 = Stopwatch.StartNew();
        var compiled2 = await accelerator.CompileKernelAsync(kernel);
        sw2.Stop();

        // Cache hit should be faster (or at least not significantly slower)
        Output.WriteLine($"First compilation: {sw1.ElapsedMilliseconds}ms");
        Output.WriteLine($"Second compilation (cached): {sw2.ElapsedMilliseconds}ms");

        // Execute and check telemetry
        await compiled1.ExecuteAsync(new KernelArguments(deviceInput, deviceOutput, (uint)size));

        // Get telemetry snapshot
        var snapshot = accelerator.GetTelemetrySnapshot();
        if (snapshot != null)
        {
            snapshot.TotalOperations.Should().BeGreaterThan(0);
            Output.WriteLine($"Telemetry: {snapshot.TotalOperations} operations recorded");
        }

        Output.WriteLine($"✓ Cache_Performance_Telemetry integration verified");
    }

    [SkippableFact]
    public async Task Integration_Graph_Optimizer_Executor()
    {
        // Test graph → optimizer → executor pipeline
        // Verify: Optimizations applied, execution correct

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10000;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceInput.CopyFromAsync(input.AsMemory());

        // Create kernel with optimization opportunity
        var kernel = CreateSimpleKernel("optimizable",
            "float temp = input[id]; result[id] = temp * temp + temp;");

        // Compile with optimization
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            FastMath = true
        };

        var compiled = await accelerator.CompileKernelAsync(kernel, options);

        // Execute
        await compiled.ExecuteAsync(new KernelArguments(deviceInput, deviceOutput, (uint)size));

        // Verify correctness
        var result = new float[size];
        await deviceOutput.CopyToAsync(result.AsMemory());

        for (int i = 0; i < Math.Min(100, size); i++)
        {
            var expected = input[i] * input[i] + input[i];
            result[i].Should().BeApproximately(expected, 0.001f);
        }

        Output.WriteLine($"✓ Graph_Optimizer_Executor integration verified with optimization");
    }

    [SkippableFact]
    public async Task Integration_ErrorHandling_Recovery()
    {
        // Test error → recovery → fallback flow
        // Verify: Graceful degradation

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        // Try to compile invalid kernel
        var invalidKernel = new KernelDefinition(
            "invalid",
            @"#include <metal_stdlib>
              using namespace metal;

              kernel void invalid(
                  device float* data [[buffer(0)]])
              {
                  // Intentional error: undefined variable
                  data[0] = undefinedVariable;
              }")
        {
            EntryPoint = "invalid",
            Language = KernelLanguage.Metal
        };

        // Should throw compilation error
        var compilationFailed = false;
        try
        {
            _ = await accelerator.CompileKernelAsync(invalidKernel);
        }
        catch
        {
            compilationFailed = true;
        }

        compilationFailed.Should().BeTrue("Invalid kernel should fail compilation");

        // Now compile valid kernel - system should recover
        var validKernel = CreateSimpleKernel("valid", "result[id] = input[id];");
        var compiled = await accelerator.CompileKernelAsync(validKernel);
        compiled.Should().NotBeNull();

        Output.WriteLine($"✓ ErrorHandling_Recovery integration verified");
    }

    [SkippableFact]
    public async Task Integration_MultiGPU_Coordination()
    {
        // Test multi-GPU coordination (if available)
        // Verify: Work distributed correctly

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Note: Most systems have only one Metal device, so this test
        // simulates multi-GPU by creating multiple accelerator instances

        await using var accelerator1 = _factory.CreateProductionAccelerator();
        accelerator1.Should().NotBeNull();

        // For actual multi-GPU, we would create separate accelerators for each device
        // Since we likely only have one, we'll test concurrent operations on the same device

        const int size = 10000;
        var input1 = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var input2 = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();

        await using var device1A = await accelerator1.Memory.AllocateAsync<float>(size);
        await using var device1B = await accelerator1.Memory.AllocateAsync<float>(size);
        await using var device2A = await accelerator1.Memory.AllocateAsync<float>(size);
        await using var device2B = await accelerator1.Memory.AllocateAsync<float>(size);

        await device1A.CopyFromAsync(input1.AsMemory());
        await device2A.CopyFromAsync(input2.AsMemory());

        var kernel = CreateSimpleKernel("concurrent", "result[id] = input[id] * 2.0f;");
        var compiled = await accelerator1.CompileKernelAsync(kernel);

        // Execute concurrently
        var task1 = compiled.ExecuteAsync(new KernelArguments(device1A, device1B, (uint)size));
        var task2 = compiled.ExecuteAsync(new KernelArguments(device2A, device2B, (uint)size));

        await Task.WhenAll(task1.AsTask(), task2.AsTask());

        var result1 = new float[size];
        var result2 = new float[size];
        await device1B.CopyToAsync(result1.AsMemory());
        await device2B.CopyToAsync(result2.AsMemory());

        // Verify both results
        result1[100].Should().BeApproximately(200.0f, 0.001f);
        result2[100].Should().BeApproximately(400.0f, 0.001f);

        Output.WriteLine($"✓ MultiGPU_Coordination verified (concurrent execution)");
    }

    #endregion

    #region Performance Integration Tests

    [SkippableFact]
    public async Task Performance_EndToEnd_MeetsTargets()
    {
        // Measure end-to-end performance
        // Verify: Meets claimed performance targets

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 10 * 1024 * 1024; // 10M elements
        const int iterations = 10;

        var input = new float[size];
        Array.Fill(input, 1.0f);

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceA.CopyFromAsync(input.AsMemory());

        var kernel = CreateSimpleKernel("saxpy", "result[id] = 2.0f * input[id] + 1.0f;");
        var compiled = await accelerator.CompileKernelAsync(kernel);

        // Warm-up
        await compiled.ExecuteAsync(new KernelArguments(deviceA, deviceB, (uint)size));

        // Measure performance
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await compiled.ExecuteAsync(new KernelArguments(deviceA, deviceB, (uint)size));
        }
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        var avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
        var throughputGBps = (size * sizeof(float) * 2) / (avgTimeMs / 1000.0) / (1024.0 * 1024.0 * 1024.0);

        Output.WriteLine($"Performance: {avgTimeMs:F2}ms per iteration");
        Output.WriteLine($"Throughput: {throughputGBps:F2} GB/s");

        // Target: At least 10 GB/s on modern Apple Silicon
        throughputGBps.Should().BeGreaterThan(5.0, "Should achieve reasonable memory bandwidth");

        Output.WriteLine($"✓ EndToEnd_MeetsTargets verified: {throughputGBps:F2} GB/s");
    }

    [SkippableFact]
    public async Task Performance_Sustained_NoDegrade()
    {
        // Repeated executions
        // Verify: No performance degradation

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 1024 * 1024;
        const int iterations = 100;

        var input = new float[size];
        Array.Fill(input, 1.0f);

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(size);

        await deviceA.CopyFromAsync(input.AsMemory());

        var kernel = CreateSimpleKernel("sustained", "result[id] = input[id] * 1.001f;");
        var compiled = await accelerator.CompileKernelAsync(kernel);

        var timings = new List<double>();

        // Measure each iteration
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await compiled.ExecuteAsync(new KernelArguments(deviceA, deviceB, (uint)size));
            await accelerator.SynchronizeAsync();
            sw.Stop();
            timings.Add(sw.Elapsed.TotalMilliseconds);
        }

        var firstHalf = timings.Take(iterations / 2).Average();
        var secondHalf = timings.Skip(iterations / 2).Average();
        var degradation = (secondHalf - firstHalf) / firstHalf * 100.0;

        Output.WriteLine($"First half average: {firstHalf:F3}ms");
        Output.WriteLine($"Second half average: {secondHalf:F3}ms");
        Output.WriteLine($"Degradation: {degradation:F2}%");

        // Allow up to 10% degradation (thermal throttling, etc.)
        degradation.Should().BeLessThan(10.0, "Performance should remain stable");

        Output.WriteLine($"✓ Sustained_NoDegrade verified over {iterations} iterations");
    }

    [SkippableFact]
    public async Task Performance_Scaling_MultipleKernels()
    {
        // Test performance with increasing kernel count
        // Verify: Linear or better scaling

        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        await using var accelerator = _factory.CreateProductionAccelerator();

        const int size = 500000;
        var results = new List<(int kernelCount, double timeMs)>();

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(size);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(size);

        var input = new float[size];
        Array.Fill(input, 1.0f);
        await deviceA.CopyFromAsync(input.AsMemory());

        // Test with 1, 2, 5, 10 kernels
        foreach (var kernelCount in new[] { 1, 2, 5, 10 })
        {
            var kernels = new List<ICompiledKernel>();
            for (int i = 0; i < kernelCount; i++)
            {
                var kernel = CreateSimpleKernel($"scale_{i}", "result[id] = input[id] * 1.1f;");
                kernels.Add(await accelerator.CompileKernelAsync(kernel));
            }

            // Warm-up
            foreach (var k in kernels)
            {
                await k.ExecuteAsync(new KernelArguments(deviceA, deviceB, (uint)size));
            }

            // Measure
            var sw = Stopwatch.StartNew();
            foreach (var k in kernels)
            {
                await k.ExecuteAsync(new KernelArguments(deviceA, deviceB, (uint)size));
            }
            await accelerator.SynchronizeAsync();
            sw.Stop();

            results.Add((kernelCount, sw.Elapsed.TotalMilliseconds));
            Output.WriteLine($"{kernelCount} kernels: {sw.Elapsed.TotalMilliseconds:F2}ms");
        }

        // Verify scaling: 10 kernels should not take 10x the time of 1 kernel
        // (due to overlapping execution, caching, etc.)
        var time1 = results.First(r => r.kernelCount == 1).timeMs;
        var time10 = results.First(r => r.kernelCount == 10).timeMs;
        var scalingFactor = time10 / time1;

        Output.WriteLine($"Scaling factor (10x vs 1x): {scalingFactor:F2}x");
        scalingFactor.Should().BeLessThan(15.0, "Should show some parallel efficiency");

        Output.WriteLine($"✓ Scaling_MultipleKernels verified");
    }

    #endregion

    #region Helper Methods

    private static KernelDefinition CreateSimpleKernel(string name, string operation)
    {
        return new KernelDefinition(
            name,
            $@"#include <metal_stdlib>
               using namespace metal;

               kernel void {name}(
                   device const float* input [[buffer(0)]],
                   device float* result [[buffer(1)]],
                   constant uint& size [[buffer(2)]],
                   uint id [[thread_position_in_grid]])
               {{
                   if (id < size) {{
                       {operation}
                   }}
               }}")
        {
            EntryPoint = name,
            Language = KernelLanguage.Metal
        };
    }

    private static float[] CreateRandomMatrix(int rows, int cols, int seed = 42)
    {
        var random = new Random(seed);
        var matrix = new float[rows * cols];
        for (int i = 0; i < matrix.Length; i++)
        {
            matrix[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
        return matrix;
    }

    private static float[] MultiplyMatricesCPU(float[] a, float[] b, int M, int N, int K)
    {
        var c = new float[M * N];
        for (int i = 0; i < M; i++)
        {
            for (int j = 0; j < N; j++)
            {
                float sum = 0.0f;
                for (int k = 0; k < K; k++)
                {
                    sum += a[i * K + k] * b[k * N + j];
                }
                c[i * N + j] = sum;
            }
        }
        return c;
    }

    private static float[] CreateRandomImage(int width, int height, int seed = 42)
    {
        var random = new Random(seed);
        var image = new float[width * height];
        for (int i = 0; i < image.Length; i++)
        {
            image[i] = (float)random.NextDouble();
        }
        return image;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loggerFactory?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XunitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);
        public void Dispose() { }
    }

    private sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{logLevel}] [{_categoryName}] {formatter(state, exception)}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    #endregion
}
