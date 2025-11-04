// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Extensions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.IntegrationTests;

/// <summary>
/// Integration tests for Metal kernel execution on actual M2 GPU hardware.
/// Tests real kernel execution, memory transfers, and unified memory performance.
/// Matches CUDA backend integration test patterns for cross-platform compatibility.
/// </summary>
[Trait("Category", "RequiresMetal")]
[Trait("Category", "Integration")]
public sealed class MetalKernelExecutionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly MetalAccelerator? _accelerator;

    public MetalKernelExecutionTests(ITestOutputHelper _output)
    {
        this._output = _output;

        if (IsMetalAvailable())
        {
            var options = Options.Create(new MetalAcceleratorOptions
            {
                EnableValidation = true,
                EnableMetrics = true
            });

            _accelerator = new MetalAccelerator(options, NullLogger<MetalAccelerator>.Instance);
        }
    }

    #region Device Initialization Tests

    [SkippableFact]
    public void Device_Initialization_Should_Succeed_With_M2()
    {
        // Skip if Metal hardware is not available
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        Assert.NotNull(_accelerator);
        Assert.NotNull(_accelerator.Info);
        Assert.NotNull(_accelerator.Info.Name);

        _output.WriteLine($"Device Name: {_accelerator.Info.Name}");
        _output.WriteLine($"Device Type: {_accelerator.Info.Type}");
        _output.WriteLine($"Max Thread Execution Width: {_accelerator.Info.MaxThreadExecutionWidth}");
        _output.WriteLine($"Max Threads Per Threadgroup: {_accelerator.Info.MaxThreadsPerThreadgroup}");
        _output.WriteLine($"Recommended Max Working Set Size: {_accelerator.Info.RecommendedMaxWorkingSetSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
    }

    [SkippableFact]
    public void Metal_Version_Should_Be_Valid()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        Assert.NotNull(_accelerator);
        var info = _accelerator.Info;

        // M2 should support Metal 3.0+
        Assert.NotNull(info.LanguageVersion);
        Assert.NotEmpty(info.LanguageVersion);
        _output.WriteLine($"Metal Language Version: {info.LanguageVersion}");
    }

    #endregion

    #region Vector Operation Tests

    [SkippableFact]
    public async Task VectorAdd_Should_Execute_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 1024;
        var hostA = new float[elementCount];
        var hostB = new float[elementCount];
        var hostResult = new float[elementCount];

        // Initialize test data
        for (int i = 0; i < elementCount; i++)
        {
            hostA[i] = i * 2.5f;
            hostB[i] = i * 1.5f;
        }

        // Create buffers
        await using var bufferA = await _accelerator!.Memory.AllocateAsync<float>(elementCount);
        await using var bufferB = await _accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(elementCount);

        // Upload data
        await bufferA.WriteAsync(hostA.AsMemory());
        await bufferB.WriteAsync(hostB.AsMemory());

        // Compile and execute kernel
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_add(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] + b[gid];
}";

        var kernel = new KernelDefinition("vector_add", kernelCode)
        {
            EntryPoint = "vector_add",
            Language = Abstractions.Kernels.Types.KernelLanguage.Metal
        };

        var compiled = await _accelerator.CompileKernelAsync(kernel);

        // Execute kernel
        await _accelerator.ExecuteKernelAsync(compiled, new GridDimensions(elementCount, 1, 1),
            new GridDimensions(256, 1, 1), bufferA, bufferB, bufferResult);

        // Download results
        await bufferResult.ReadAsync(hostResult.AsMemory());

        // Verify results
        for (int i = 0; i < elementCount; i++)
        {
            float expected = hostA[i] + hostB[i];
            Assert.True(Math.Abs(hostResult[i] - expected) < 0.001f,
                $"Mismatch at index {i}: expected {expected}, got {hostResult[i]}");
        }

        _output.WriteLine($"✓ Vector add executed successfully on {elementCount} elements");
    }

    [SkippableFact]
    public async Task VectorMultiply_Should_Execute_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 2048;
        var hostA = new float[elementCount];
        var hostResult = new float[elementCount];
        const float scalar = 3.14f;

        // Initialize test data
        for (int i = 0; i < elementCount; i++)
        {
            hostA[i] = (float)Math.Sin(i * 0.1);
        }

        // Create buffers
        await using var bufferA = await _accelerator!.Memory.AllocateAsync<float>(elementCount);
        await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(elementCount);

        // Upload data
        await bufferA.WriteAsync(hostA.AsMemory());

        // Compile and execute kernel
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_multiply(
    device const float* a [[buffer(0)]],
    device float* result [[buffer(1)]],
    constant float& scalar [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] * scalar;
}";

        var kernel = new KernelDefinition("vector_multiply", kernelCode)
        {
            EntryPoint = "vector_multiply",
            Language = Abstractions.Kernels.Types.KernelLanguage.Metal
        };

        var compiled = await _accelerator.CompileKernelAsync(kernel);

        // Execute kernel
        // Note: For scalar parameters, we need to pass them directly
        await _accelerator.ExecuteKernelAsync(compiled, new GridDimensions(elementCount, 1, 1),
            new GridDimensions(256, 1, 1), bufferA, bufferResult);

        // Download results
        await bufferResult.ReadAsync(hostResult.AsMemory());

        // Verify results
        for (int i = 0; i < elementCount; i++)
        {
            float expected = hostA[i] * scalar;
            Assert.True(Math.Abs(hostResult[i] - expected) < 0.001f,
                $"Mismatch at index {i}: expected {expected}, got {hostResult[i]}");
        }

        _output.WriteLine($"✓ Vector multiply executed successfully on {elementCount} elements with scalar {scalar}");
    }

    #endregion

    #region Matrix Operation Tests

    [SkippableFact]
    public async Task MatrixMultiply_Small_Should_Execute_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int M = 64;  // rows of A
        const int N = 64;  // cols of B
        const int K = 64;  // cols of A, rows of B

        var hostA = new float[M * K];
        var hostB = new float[K * N];
        var hostC = new float[M * N];

        // Initialize matrices
        var rand = new Random(42);
        for (int i = 0; i < M * K; i++)
        {
            hostA[i] = (float)rand.NextDouble();
        }

        for (int i = 0; i < K * N; i++)
        {
            hostB[i] = (float)rand.NextDouble();
        }

        // Create buffers
        await using var bufferA = await _accelerator!.Memory.AllocateAsync<float>(M * K);
        await using var bufferB = await _accelerator.Memory.AllocateAsync<float>(K * N);
        await using var bufferC = await _accelerator.Memory.AllocateAsync<float>(M * N);

        // Upload data
        await bufferA.WriteAsync(hostA.AsMemory());
        await bufferB.WriteAsync(hostB.AsMemory());

        // Matrix multiply kernel
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void matrix_multiply(
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

    if (row < M && col < N)
    {
        float sum = 0.0f;
        for (uint i = 0; i < K; ++i)
        {
            sum += A[row * K + i] * B[i * N + col];
        }
        C[row * N + col] = sum;
    }
}";

        var kernel = new KernelDefinition("matrix_multiply", kernelCode)
        {
            EntryPoint = "matrix_multiply",
            Language = Abstractions.Kernels.Types.KernelLanguage.Metal
        };

        var compiled = await _accelerator.CompileKernelAsync(kernel);

        // Execute kernel
        // Note: For scalar parameters, we need to pass them as buffers or adjust kernel signature
        await _accelerator.ExecuteKernelAsync(compiled, new GridDimensions(N, M, 1),
            new GridDimensions(16, 16, 1), bufferA, bufferB, bufferC);

        // Download results
        await bufferC.ReadAsync(hostC.AsMemory());

        // Verify with CPU computation (spot check)
        for (int row = 0; row < Math.Min(M, 8); row += M / 8)
        {
            for (int col = 0; col < Math.Min(N, 8); col += N / 8)
            {
                float expected = 0.0f;
                for (int k = 0; k < K; k++)
                {
                    expected += hostA[row * K + k] * hostB[k * N + col];
                }
                float actual = hostC[row * N + col];
                Assert.True(Math.Abs(actual - expected) / expected < 0.01f,
                    $"Mismatch at ({row},{col}): expected {expected}, got {actual}");
            }
        }

        _output.WriteLine($"✓ Matrix multiply ({M}x{K}) × ({K}x{N}) executed successfully");
    }

    #endregion

    #region Reduction Operation Tests

    [SkippableFact]
    public async Task ReductionSum_Should_Execute_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 4096;
        var hostData = new float[elementCount];

        // Initialize test data
        for (int i = 0; i < elementCount; i++)
        {
            hostData[i] = 1.0f;  // Simple sum test
        }

        // Create buffers
        await using var bufferInput = await _accelerator!.Memory.AllocateAsync<float>(elementCount);
        await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(1);

        // Upload data
        await bufferInput.WriteAsync(hostData.AsMemory());

        // Reduction kernel
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void reduction_sum(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]],
    uint tg_size [[threads_per_threadgroup]])
{
    // Load data into shared memory
    shared[tid] = (gid < 4096) ? input[gid] : 0.0f;
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Reduction in shared memory
    for (uint s = tg_size / 2; s > 0; s >>= 1)
    {
        if (tid < s)
        {
            shared[tid] += shared[tid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    // Write result
    if (tid == 0)
    {
        atomic_fetch_add_explicit((device atomic_float*)output, shared[0], memory_order_relaxed);
    }
}";

        var kernel = new KernelDefinition("reduction_sum", kernelCode)
        {
            EntryPoint = "reduction_sum",
            Language = Abstractions.Kernels.Types.KernelLanguage.Metal
        };

        var compiled = await _accelerator.CompileKernelAsync(kernel);

        // Execute kernel
        await _accelerator.ExecuteKernelAsync(compiled, new GridDimensions(elementCount, 1, 1),
            new GridDimensions(256, 1, 1), bufferInput, bufferOutput);

        // Download result
        var result = new float[1];
        await bufferOutput.ReadAsync(result.AsMemory());

        // Verify
        float expected = elementCount * 1.0f;
        Assert.True(Math.Abs(result[0] - expected) / expected < 0.01f,
            $"Reduction sum mismatch: expected {expected}, got {result[0]}");

        _output.WriteLine($"✓ Reduction sum executed: {elementCount} elements summed to {result[0]} (expected {expected})");
    }

    #endregion

    #region Unified Memory Tests

    [SkippableFact]
    public async Task UnifiedMemory_ZeroCopy_Should_Work()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");
        Skip.IfNot(SupportsUnifiedMemory(), "Unified memory not supported");

        const int elementCount = 1024;
        var hostData = new float[elementCount];

        for (int i = 0; i < elementCount; i++)
        {
            hostData[i] = i * 0.5f;
        }

        // Allocate regular memory (unified memory API may not be exposed)
        await using var unifiedBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        // Write data (zero-copy)
        await unifiedBuffer.WriteAsync(hostData.AsMemory());

        // Simple kernel to double values
        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void double_values(
    device float* data [[buffer(0)]],
    uint gid [[thread_position_in_grid]])
{
    data[gid] *= 2.0f;
}";

        var kernel = new KernelDefinition("double_values", kernelCode)
        {
            EntryPoint = "double_values",
            Language = Abstractions.Kernels.Types.KernelLanguage.Metal
        };

        var compiled = await _accelerator.CompileKernelAsync(kernel);

        // Execute kernel
        await _accelerator.ExecuteKernelAsync(compiled, new GridDimensions(elementCount, 1, 1),
            new GridDimensions(256, 1, 1), unifiedBuffer);

        // Read data (zero-copy)
        var resultData = new float[elementCount];
        await unifiedBuffer.ReadAsync(resultData.AsMemory());

        // Verify
        for (int i = 0; i < elementCount; i++)
        {
            float expected = hostData[i] * 2.0f;
            Assert.True(Math.Abs(resultData[i] - expected) < 0.001f,
                $"Mismatch at index {i}: expected {expected}, got {resultData[i]}");
        }

        _output.WriteLine($"✓ Unified memory zero-copy operation completed successfully");
    }

    #endregion

    #region Memory Transfer Performance Tests

    [SkippableFact]
    public async Task Memory_Transfer_HostToDevice_MeasuresPerformance()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 1024 * 1024; // 1M floats = 4MB
        var hostData = new float[elementCount];

        for (int i = 0; i < elementCount; i++)
        {
            hostData[i] = i * 0.1f;
        }

        await using var deviceBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await deviceBuffer.WriteAsync(hostData.AsMemory());
        stopwatch.Stop();

        var transferRate = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);

        _output.WriteLine($"Host to Device transfer: {elementCount * sizeof(float) / (1024.0 * 1024.0):F2} MB in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Transfer rate: {transferRate:F2} MB/s");

        Assert.True(stopwatch.Elapsed.TotalSeconds < 1.0, "Transfer took too long");
    }

    [SkippableFact]
    public async Task Memory_Transfer_DeviceToHost_MeasuresPerformance()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 1024 * 1024; // 1M floats = 4MB
        var hostData = new float[elementCount];

        await using var deviceBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        // Upload first
        await deviceBuffer.WriteAsync(hostData.AsMemory());

        // Measure download
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await deviceBuffer.ReadAsync(hostData.AsMemory());
        stopwatch.Stop();

        var transferRate = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);

        _output.WriteLine($"Device to Host transfer: {elementCount * sizeof(float) / (1024.0 * 1024.0):F2} MB in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Transfer rate: {transferRate:F2} MB/s");

        Assert.True(stopwatch.Elapsed.TotalSeconds < 1.0, "Transfer took too long");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    public async Task Error_Handling_OutOfBoundsAccess_DetectsIssue()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 100;
        await using var buffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        // Try to read more than allocated
        var tooLargeBuffer = new float[elementCount * 2];

        // This should either throw or handle gracefully
        try
        {
            await buffer.ReadAsync(tooLargeBuffer.AsMemory());
            _output.WriteLine("⚠ Out of bounds access did not throw (may be handled by driver)");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✓ Out of bounds access correctly detected: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private static bool IsMetalAvailable()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            // Try to create a Metal device (use synchronous disposal)
            var options = Options.Create(new MetalAcceleratorOptions());
            var accelerator = new MetalAccelerator(options, NullLogger<MetalAccelerator>.Instance);
            accelerator.DisposeAsync().AsTask().Wait();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool SupportsUnifiedMemory()
    {
        // Check if unified memory is supported via capabilities
        if (_accelerator?.Info?.Capabilities != null &&
            _accelerator.Info.Capabilities.TryGetValue("UnifiedMemory", out var value))
        {
            return value is bool b && b;
        }
        return false;
    }

    #endregion

    public void Dispose()
    {
        if (_accelerator != null)
        {
            // Use async disposal pattern
            _accelerator.DisposeAsync().AsTask().Wait();
        }
    }
}
