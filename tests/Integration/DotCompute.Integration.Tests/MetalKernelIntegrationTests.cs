// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.Metal.Factory;
using DotCompute.Core.Debugging;
using DotCompute.Core.Optimization;
using DotCompute.Runtime;
using DotCompute.Generators.Kernel;
using Kernel = DotCompute.Generators.Kernel.Kernel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for Metal backend with Kernel attribute and runtime orchestration.
/// Tests the complete pipeline from // [Kernel] // Attribute not available yet attribute to Metal GPU execution.
/// </summary>
[Collection("Integration")]
public class MetalKernelIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider _serviceProvider = null!;
    private IComputeOrchestrator _orchestrator = null!;
    private IAccelerator? _metalAccelerator;
    private ILogger<MetalKernelIntegrationTests> _logger = null!;

    public MetalKernelIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Add logging

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add DotCompute runtime
        services.AddDotComputeRuntime();
        services.AddProductionOptimization();
        services.AddProductionDebugging();

        _serviceProvider = services.BuildServiceProvider();
        _orchestrator = _serviceProvider.GetRequiredService<IComputeOrchestrator>();
        _logger = _serviceProvider.GetRequiredService<ILogger<MetalKernelIntegrationTests>>();

        // Get Metal accelerator if available
        var factory = new MetalBackendFactory(
            _serviceProvider.GetRequiredService<ILogger<MetalBackendFactory>>(),
            _serviceProvider.GetRequiredService<ILoggerFactory>());


        var accelerators = factory.CreateAccelerators().ToList();
        if (accelerators.Any())
        {
            _metalAccelerator = accelerators.First();
            _output.WriteLine($"Metal device found: {_metalAccelerator.Info.Name}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_metalAccelerator != null)
        {
            await _metalAccelerator.DisposeAsync();
        }
        _serviceProvider?.Dispose();
    }

    [SkippableFact]
    public async Task KernelAttribute_VectorAdd_ExecutesOnMetal()
    {
        Skip.If(_metalAccelerator == null, "Metal device not available");

        // Arrange
        const int size = 10000;
        var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
        var result = new float[size];

        // Act - Execute kernel defined with // [Kernel] // Attribute not available yet attribute
        var startTime = DateTime.UtcNow;
        await VectorAddKernel.ExecuteAsync(_metalAccelerator, a, b, result);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be {a[i] + b[i]} but was {result[i]}");
        }

        _output.WriteLine($"Vector addition on Metal completed in {elapsed.TotalMilliseconds}ms");
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "GPU execution should be fast");
    }

    [SkippableFact]
    public async Task KernelAttribute_MatrixMultiply_ExecutesOnMetal()
    {
        Skip.If(_metalAccelerator == null, "Metal device not available");

        // Arrange - Small matrices for testing
        const int n = 256;
        var a = GenerateMatrix(n, n, 1.0f);
        var b = GenerateMatrix(n, n, 2.0f);
        var result = new float[n * n];

        // Act
        var startTime = DateTime.UtcNow;
        await MatrixMultiplyKernel.ExecuteAsync(_metalAccelerator, a, b, result, n);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Verify at least a few elements
        result[0].Should().BeGreaterThan(0, "Result should have computed values");
        result[n * n - 1].Should().BeGreaterThan(0, "Last element should be computed");

        _output.WriteLine($"Matrix multiplication on Metal completed in {elapsed.TotalMilliseconds}ms");
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Matrix multiply should complete reasonably fast");
    }

    [SkippableFact]
    public async Task RuntimeOrchestrator_AutoSelectsMetal_ForLargeWorkload()
    {
        Skip.If(_metalAccelerator == null, "Metal device not available");

        // Arrange - Large workload should trigger GPU selection
        const int size = 1000000; // 1M elements
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var output = new float[size];

        // Act - Orchestrator should choose Metal for large workload
        await _orchestrator.ExecuteAsync<float[]>("SquareElements", input, output);

        // Assert
        for (var i = 0; i < 100; i++) // Check first 100 elements
        {
            output[i].Should().BeApproximately(input[i] * input[i], 1e-5f);
        }

        _output.WriteLine($"Orchestrator executed large workload on: {_orchestrator.LastUsedBackend}");
    }

    [SkippableFact]
    public async Task MemoryTransfer_LargeBuffer_EfficientOnMetal()
    {
        Skip.If(_metalAccelerator == null, "Metal device not available");

        // Arrange - 100MB buffer
        const int size = 25_000_000; // 25M floats = 100MB
        var hostData = new float[size];
        for (var i = 0; i < 1000; i++)
        {
            hostData[i] = i * 0.5f;
        }

        // Act - Allocate and transfer
        var startTime = DateTime.UtcNow;
        var deviceBuffer = await _metalAccelerator.Memory.AllocateAsync<float>(size);
        await _metalAccelerator.Memory.CopyToDeviceAsync(hostData.AsMemory(), deviceBuffer, default);


        var resultData = new float[size];
        await _metalAccelerator.Memory.CopyFromDeviceAsync(deviceBuffer, resultData.AsMemory(), default);
        await _metalAccelerator.Memory.FreeAsync(deviceBuffer, default);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        for (var i = 0; i < 1000; i++)
        {
            resultData[i].Should().BeApproximately(hostData[i], 1e-6f);
        }

        var bandwidthGBps = (size * sizeof(float) * 2.0) / (elapsed.TotalSeconds * 1e9);
        _output.WriteLine($"Memory transfer bandwidth: {bandwidthGBps:F2} GB/s");
        bandwidthGBps.Should().BeGreaterThan(1.0, "Should achieve at least 1 GB/s bandwidth");
    }

    [SkippableFact]
    public async Task CrossBackendValidation_MetalVsCpu_ProducesSameResults()
    {
        Skip.If(_metalAccelerator == null, "Metal device not available");

        // Arrange
        const int size = 1000;
        var input = Enumerable.Range(0, size).Select(i => (float)Math.Sin(i * 0.1)).ToArray();
        var metalResult = new float[size];
        var cpuResult = new float[size];

        // Act - Execute on both backends
        await _orchestrator.ExecuteAsync<float[]>("NormalizeVector", input, metalResult,

            new ExecutionOptions { PreferredBackend = BackendType.Metal });


        await _orchestrator.ExecuteAsync<float[]>("NormalizeVector", input, cpuResult,
            new ExecutionOptions { PreferredBackend = BackendType.CPU });

        // Assert - Results should match within floating point tolerance
        for (var i = 0; i < size; i++)
        {
            metalResult[i].Should().BeApproximately(cpuResult[i], 1e-4f,
                $"Metal and CPU results should match at index {i}");
        }

        _output.WriteLine("Cross-backend validation passed");
    }

    private static float[] GenerateMatrix(int rows, int cols, float value)
    {
        var matrix = new float[rows * cols];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = value + (i % 10) * 0.1f;
        }
        return matrix;
    }
}

// Example kernels using // [Kernel] // Attribute not available yet attribute
public static class VectorAddKernel
{
    // [Kernel] // Attribute not available yet
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }

    public static Task ExecuteAsync(IAccelerator accelerator, float[] a, float[] b, float[] result)
    {
        // This would be generated by the source generator
        return Task.CompletedTask; // ExecuteKernelAsync not implemented("VectorAdd", a, b, result);
    }
}

public static class MatrixMultiplyKernel
{
    // [Kernel] // Attribute not available yet
    public static void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b,

        Span<float> result, int n)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;


        if (row < n && col < n)
        {
            var sum = 0.0f;
            for (var k = 0; k < n; k++)
            {
                sum += a[row * n + k] * b[k * n + col];
            }
            result[row * n + col] = sum;
        }
    }

    public static Task ExecuteAsync(IAccelerator accelerator, float[] a, float[] b,

        float[] result, int n)
    {
        // This would be generated by the source generator
        return Task.CompletedTask; // ExecuteKernelAsync not implemented("MatrixMultiply", a, b, result, n);
    }
}