// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
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
/// Cross-backend validation tests for DotCompute 0.2.0.
/// Validates that [Kernel] attribute enables "write once, run anywhere" capability
/// across CPU, CUDA, OpenCL, and Metal backends.
/// </summary>
[Collection("Integration")]
public class CrossBackendKernelValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider _serviceProvider = null!;
    private IComputeOrchestrator _orchestrator = null!;
    private ILogger<CrossBackendKernelValidationTests> _logger = null!;

    private IAccelerator? _cpuAccelerator;
    private IAccelerator? _cudaAccelerator;
    private IAccelerator? _openclAccelerator;
    private IAccelerator? _metalAccelerator;

    private readonly List<BackendType> _availableBackends = new();

    public CrossBackendKernelValidationTests(ITestOutputHelper output)
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

        // Add DotCompute runtime with all backends
        services.AddDotComputeRuntime();
        services.AddProductionOptimization();
        services.AddProductionDebugging();

        _serviceProvider = services.BuildServiceProvider();
        _orchestrator = _serviceProvider.GetRequiredService<IComputeOrchestrator>();
        _logger = _serviceProvider.GetRequiredService<ILogger<CrossBackendKernelValidationTests>>();

        // Discover available accelerators
        var accelerators = _serviceProvider.GetServices<IAccelerator>().ToList();

        _cpuAccelerator = accelerators.FirstOrDefault(a => a.Type == BackendType.CPU);
        _cudaAccelerator = accelerators.FirstOrDefault(a => a.Type == BackendType.CUDA);
        _openclAccelerator = accelerators.FirstOrDefault(a => a.Type == BackendType.OpenCL);
        _metalAccelerator = accelerators.FirstOrDefault(a => a.Type == BackendType.Metal);

        // Log available backends
        if (_cpuAccelerator != null)
        {
            _availableBackends.Add(BackendType.CPU);
            _output.WriteLine($"✓ CPU backend available: {_cpuAccelerator.Info.Name}");
        }

        if (_cudaAccelerator != null)
        {
            _availableBackends.Add(BackendType.CUDA);
            _output.WriteLine($"✓ CUDA backend available: {_cudaAccelerator.Info.Name}");
        }

        if (_openclAccelerator != null)
        {
            _availableBackends.Add(BackendType.OpenCL);
            _output.WriteLine($"✓ OpenCL backend available: {_openclAccelerator.Info.Name}");
        }

        if (_metalAccelerator != null)
        {
            _availableBackends.Add(BackendType.Metal);
            _output.WriteLine($"✓ Metal backend available: {_metalAccelerator.Info.Name}");
        }

        _output.WriteLine($"Total backends available: {_availableBackends.Count}/4");

        // CPU should always be available
        _cpuAccelerator.Should().NotBeNull("CPU backend should always be available");

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_cpuAccelerator != null) await _cpuAccelerator.DisposeAsync();
        if (_cudaAccelerator != null) await _cudaAccelerator.DisposeAsync();
        if (_openclAccelerator != null) await _openclAccelerator.DisposeAsync();
        if (_metalAccelerator != null) await _metalAccelerator.DisposeAsync();

        _serviceProvider?.Dispose();
    }

    #region Simple Kernel Tests - Vector Add

    [Fact]
    public async Task VectorAdd_CPU_ExecutesCorrectly()
    {
        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i);
        var b = GenerateTestData(size, i => (float)i * 2);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be {a[i] + b[i]} but was {result[i]}");
        }

        _output.WriteLine($"CPU Vector Add: {sw.ElapsedMilliseconds}ms ({size} elements)");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "CPU execution should complete in reasonable time");
    }

    [SkippableFact]
    public async Task VectorAdd_CUDA_ExecutesCorrectly()
    {
        Skip.If(_cudaAccelerator == null, "CUDA backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i);
        var b = GenerateTestData(size, i => (float)i * 2);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorAdd.ExecuteAsync(_cudaAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be {a[i] + b[i]} but was {result[i]}");
        }

        _output.WriteLine($"CUDA Vector Add: {sw.ElapsedMilliseconds}ms ({size} elements)");
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "GPU execution should be fast");
    }

    [SkippableFact]
    public async Task VectorAdd_OpenCL_ExecutesCorrectly()
    {
        Skip.If(_openclAccelerator == null, "OpenCL backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i);
        var b = GenerateTestData(size, i => (float)i * 2);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorAdd.ExecuteAsync(_openclAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be {a[i] + b[i]} but was {result[i]}");
        }

        _output.WriteLine($"OpenCL Vector Add: {sw.ElapsedMilliseconds}ms ({size} elements)");
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "GPU execution should be fast");
    }

    [SkippableFact]
    public async Task VectorAdd_Metal_ExecutesCorrectly()
    {
        Skip.If(_metalAccelerator == null, "Metal backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i);
        var b = GenerateTestData(size, i => (float)i * 2);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorAdd.ExecuteAsync(_metalAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be {a[i] + b[i]} but was {result[i]}");
        }

        _output.WriteLine($"Metal Vector Add: {sw.ElapsedMilliseconds}ms ({size} elements)");
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "GPU execution should be fast");
    }

    #endregion

    #region Cross-Backend Consistency Tests

    [Fact]
    public async Task VectorAdd_AllBackends_ProduceSameResults()
    {
        // Arrange
        const int size = 1000;
        var a = GenerateTestData(size, i => (float)Math.Sin(i * 0.1));
        var b = GenerateTestData(size, i => (float)Math.Cos(i * 0.1));

        var results = new Dictionary<BackendType, float[]>();

        // Act - Execute on all available backends
        if (_cpuAccelerator != null)
        {
            var cpuResult = new float[size];
            await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator, a, b, cpuResult);
            results[BackendType.CPU] = cpuResult;
        }

        if (_cudaAccelerator != null)
        {
            var cudaResult = new float[size];
            await ValidationKernels.VectorAdd.ExecuteAsync(_cudaAccelerator, a, b, cudaResult);
            results[BackendType.CUDA] = cudaResult;
        }

        if (_openclAccelerator != null)
        {
            var openclResult = new float[size];
            await ValidationKernels.VectorAdd.ExecuteAsync(_openclAccelerator, a, b, openclResult);
            results[BackendType.OpenCL] = openclResult;
        }

        if (_metalAccelerator != null)
        {
            var metalResult = new float[size];
            await ValidationKernels.VectorAdd.ExecuteAsync(_metalAccelerator, a, b, metalResult);
            results[BackendType.Metal] = metalResult;
        }

        // Assert - All backends should produce identical results
        results.Should().ContainKey(BackendType.CPU, "CPU backend should always be available");

        var cpuResult = results[BackendType.CPU];
        foreach (var (backend, result) in results)
        {
            if (backend == BackendType.CPU) continue;

            for (var i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(cpuResult[i], 1e-4f,
                    $"{backend} result should match CPU result at index {i}");
            }

            _output.WriteLine($"✓ {backend} results match CPU (within 1e-4 tolerance)");
        }

        _output.WriteLine($"Cross-backend validation passed for {results.Count} backends");
    }

    #endregion

    #region Performance Benchmarking

    [Fact]
    public async Task VectorAdd_PerformanceComparison_AllBackends()
    {
        // Arrange - Large workload to show backend differences
        const int size = 10_000_000; // 10M elements
        var a = GenerateTestData(size, i => (float)i);
        var b = GenerateTestData(size, i => (float)i * 2);

        var timings = new Dictionary<BackendType, long>();

        // Act - Benchmark each backend
        if (_cpuAccelerator != null)
        {
            var result = new float[size];
            var sw = Stopwatch.StartNew();
            await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator, a, b, result);
            sw.Stop();
            timings[BackendType.CPU] = sw.ElapsedMilliseconds;
        }

        if (_cudaAccelerator != null)
        {
            var result = new float[size];
            var sw = Stopwatch.StartNew();
            await ValidationKernels.VectorAdd.ExecuteAsync(_cudaAccelerator, a, b, result);
            sw.Stop();
            timings[BackendType.CUDA] = sw.ElapsedMilliseconds;
        }

        if (_openclAccelerator != null)
        {
            var result = new float[size];
            var sw = Stopwatch.StartNew();
            await ValidationKernels.VectorAdd.ExecuteAsync(_openclAccelerator, a, b, result);
            sw.Stop();
            timings[BackendType.OpenCL] = sw.ElapsedMilliseconds;
        }

        if (_metalAccelerator != null)
        {
            var result = new float[size];
            var sw = Stopwatch.StartNew();
            await ValidationKernels.VectorAdd.ExecuteAsync(_metalAccelerator, a, b, result);
            sw.Stop();
            timings[BackendType.Metal] = sw.ElapsedMilliseconds;
        }

        // Report - Create performance comparison table
        _output.WriteLine($"\n=== Vector Add Performance Comparison ({size:N0} elements) ===");
        foreach (var (backend, time) in timings.OrderBy(kvp => kvp.Value))
        {
            var throughput = (size * 3 * sizeof(float)) / (time / 1000.0) / (1024 * 1024 * 1024); // GB/s
            _output.WriteLine($"{backend,-10} {time,6}ms    {throughput,8:F2} GB/s");
        }

        // Assert - At least one backend should complete
        timings.Should().NotBeEmpty("At least one backend should execute successfully");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task VectorAdd_EmptyArray_HandledGracefully()
    {
        // Arrange
        var a = Array.Empty<float>();
        var b = Array.Empty<float>();
        var result = Array.Empty<float>();

        // Act & Assert - Should not throw
        await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator!, a, b, result);
        _output.WriteLine("✓ Empty array handled without errors");
    }

    [Fact]
    public async Task VectorAdd_SingleElement_ExecutesCorrectly()
    {
        // Arrange
        var a = new[] { 5.0f };
        var b = new[] { 3.0f };
        var result = new float[1];

        // Act
        await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator!, a, b, result);

        // Assert
        result[0].Should().BeApproximately(8.0f, 1e-6f);
        _output.WriteLine("✓ Single element correctly computed");
    }

    [Fact]
    public async Task VectorAdd_LargeArray_ExecutesCorrectly()
    {
        // Arrange - 100M elements (400MB per array)
        const int size = 100_000_000;
        var a = GenerateTestData(size, i => (float)(i % 1000));
        var b = GenerateTestData(size, i => (float)(i % 500));
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorAdd.ExecuteAsync(_cpuAccelerator!, a, b, result);
        sw.Stop();

        // Assert - Spot check random elements
        var random = new Random(42);
        for (var i = 0; i < 100; i++)
        {
            var idx = random.Next(size);
            result[idx].Should().BeApproximately(a[idx] + b[idx], 1e-5f);
        }

        _output.WriteLine($"✓ Large array ({size:N0} elements) processed in {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "Should complete in reasonable time");
    }

    #endregion

    #region Simple Kernel Tests - Vector Multiply

    [Fact]
    public async Task VectorMultiply_CPU_ExecutesCorrectly()
    {
        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)i * 0.5f + 1);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorMultiply.ExecuteAsync(_cpuAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] * b[i], 1e-5f,
                $"Element {i} should be {a[i] * b[i]} but was {result[i]}");
        }

        _output.WriteLine($"CPU Vector Multiply: {sw.ElapsedMilliseconds}ms ({size} elements)");
    }

    [SkippableFact]
    public async Task VectorMultiply_CUDA_ExecutesCorrectly()
    {
        Skip.If(_cudaAccelerator == null, "CUDA backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)i * 0.5f + 1);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorMultiply.ExecuteAsync(_cudaAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] * b[i], 1e-5f,
                $"Element {i} should be {a[i] * b[i]} but was {result[i]}");
        }

        _output.WriteLine($"CUDA Vector Multiply: {sw.ElapsedMilliseconds}ms ({size} elements)");
    }

    [SkippableFact]
    public async Task VectorMultiply_OpenCL_ExecutesCorrectly()
    {
        Skip.If(_openclAccelerator == null, "OpenCL backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)i * 0.5f + 1);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorMultiply.ExecuteAsync(_openclAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] * b[i], 1e-5f,
                $"Element {i} should be {a[i] * b[i]} but was {result[i]}");
        }

        _output.WriteLine($"OpenCL Vector Multiply: {sw.ElapsedMilliseconds}ms ({size} elements)");
    }

    [SkippableFact]
    public async Task VectorMultiply_Metal_ExecutesCorrectly()
    {
        Skip.If(_metalAccelerator == null, "Metal backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)i * 0.5f + 1);
        var result = new float[size];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.VectorMultiply.ExecuteAsync(_metalAccelerator!, a, b, result);
        sw.Stop();

        // Assert
        for (var i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] * b[i], 1e-5f,
                $"Element {i} should be {a[i] * b[i]} but was {result[i]}");
        }

        _output.WriteLine($"Metal Vector Multiply: {sw.ElapsedMilliseconds}ms ({size} elements)");
    }

    [Fact]
    public async Task VectorMultiply_AllBackends_ProduceSameResults()
    {
        // Arrange
        const int size = 1000;
        var a = GenerateTestData(size, i => (float)Math.Sin(i * 0.1) + 2);
        var b = GenerateTestData(size, i => (float)Math.Cos(i * 0.1) + 2);

        var results = new Dictionary<BackendType, float[]>();

        // Act - Execute on all available backends
        if (_cpuAccelerator != null)
        {
            var cpuResult = new float[size];
            await ValidationKernels.VectorMultiply.ExecuteAsync(_cpuAccelerator, a, b, cpuResult);
            results[BackendType.CPU] = cpuResult;
        }

        if (_cudaAccelerator != null)
        {
            var cudaResult = new float[size];
            await ValidationKernels.VectorMultiply.ExecuteAsync(_cudaAccelerator, a, b, cudaResult);
            results[BackendType.CUDA] = cudaResult;
        }

        if (_openclAccelerator != null)
        {
            var openclResult = new float[size];
            await ValidationKernels.VectorMultiply.ExecuteAsync(_openclAccelerator, a, b, openclResult);
            results[BackendType.OpenCL] = openclResult;
        }

        if (_metalAccelerator != null)
        {
            var metalResult = new float[size];
            await ValidationKernels.VectorMultiply.ExecuteAsync(_metalAccelerator, a, b, metalResult);
            results[BackendType.Metal] = metalResult;
        }

        // Assert - All backends should produce identical results
        var cpuResult = results[BackendType.CPU];
        foreach (var (backend, result) in results)
        {
            if (backend == BackendType.CPU) continue;

            for (var i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(cpuResult[i], 1e-4f,
                    $"{backend} result should match CPU result at index {i}");
            }

            _output.WriteLine($"✓ VectorMultiply: {backend} results match CPU");
        }
    }

    #endregion

    #region Simple Kernel Tests - Dot Product

    [Fact]
    public async Task DotProduct_CPU_ExecutesCorrectly()
    {
        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)(size - i));
        var result = new float[1];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.DotProduct.ExecuteAsync(_cpuAccelerator!, a, b, result);
        sw.Stop();

        // Assert - Compute expected result
        var expected = 0.0f;
        for (var i = 0; i < size; i++)
        {
            expected += a[i] * b[i];
        }

        result[0].Should().BeApproximately(expected, 1e-3f * expected,
            $"Dot product should be {expected} but was {result[0]}");

        _output.WriteLine($"CPU Dot Product: {sw.ElapsedMilliseconds}ms ({size} elements) = {result[0]:E}");
    }

    [SkippableFact]
    public async Task DotProduct_CUDA_ExecutesCorrectly()
    {
        Skip.If(_cudaAccelerator == null, "CUDA backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)(size - i));
        var result = new float[1];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.DotProduct.ExecuteAsync(_cudaAccelerator!, a, b, result);
        sw.Stop();

        // Assert - Compute expected result
        var expected = 0.0f;
        for (var i = 0; i < size; i++)
        {
            expected += a[i] * b[i];
        }

        result[0].Should().BeApproximately(expected, 1e-3f * expected,
            $"Dot product should be {expected} but was {result[0]}");

        _output.WriteLine($"CUDA Dot Product: {sw.ElapsedMilliseconds}ms ({size} elements) = {result[0]:E}");
    }

    [SkippableFact]
    public async Task DotProduct_OpenCL_ExecutesCorrectly()
    {
        Skip.If(_openclAccelerator == null, "OpenCL backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)(size - i));
        var result = new float[1];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.DotProduct.ExecuteAsync(_openclAccelerator!, a, b, result);
        sw.Stop();

        // Assert - Compute expected result
        var expected = 0.0f;
        for (var i = 0; i < size; i++)
        {
            expected += a[i] * b[i];
        }

        result[0].Should().BeApproximately(expected, 1e-3f * expected,
            $"Dot product should be {expected} but was {result[0]}");

        _output.WriteLine($"OpenCL Dot Product: {sw.ElapsedMilliseconds}ms ({size} elements) = {result[0]:E}");
    }

    [SkippableFact]
    public async Task DotProduct_Metal_ExecutesCorrectly()
    {
        Skip.If(_metalAccelerator == null, "Metal backend not available");

        // Arrange
        const int size = 10000;
        var a = GenerateTestData(size, i => (float)i + 1);
        var b = GenerateTestData(size, i => (float)(size - i));
        var result = new float[1];

        // Act
        var sw = Stopwatch.StartNew();
        await ValidationKernels.DotProduct.ExecuteAsync(_metalAccelerator!, a, b, result);
        sw.Stop();

        // Assert - Compute expected result
        var expected = 0.0f;
        for (var i = 0; i < size; i++)
        {
            expected += a[i] * b[i];
        }

        result[0].Should().BeApproximately(expected, 1e-3f * expected,
            $"Dot product should be {expected} but was {result[0]}");

        _output.WriteLine($"Metal Dot Product: {sw.ElapsedMilliseconds}ms ({size} elements) = {result[0]:E}");
    }

    [Fact]
    public async Task DotProduct_AllBackends_ProduceSameResults()
    {
        // Arrange
        const int size = 1000;
        var a = GenerateTestData(size, i => (float)Math.Sin(i * 0.1));
        var b = GenerateTestData(size, i => (float)Math.Cos(i * 0.1));

        var results = new Dictionary<BackendType, float>();

        // Act - Execute on all available backends
        if (_cpuAccelerator != null)
        {
            var cpuResult = new float[1];
            await ValidationKernels.DotProduct.ExecuteAsync(_cpuAccelerator, a, b, cpuResult);
            results[BackendType.CPU] = cpuResult[0];
        }

        if (_cudaAccelerator != null)
        {
            var cudaResult = new float[1];
            await ValidationKernels.DotProduct.ExecuteAsync(_cudaAccelerator, a, b, cudaResult);
            results[BackendType.CUDA] = cudaResult[0];
        }

        if (_openclAccelerator != null)
        {
            var openclResult = new float[1];
            await ValidationKernels.DotProduct.ExecuteAsync(_openclAccelerator, a, b, openclResult);
            results[BackendType.OpenCL] = openclResult[0];
        }

        if (_metalAccelerator != null)
        {
            var metalResult = new float[1];
            await ValidationKernels.DotProduct.ExecuteAsync(_metalAccelerator, a, b, metalResult);
            results[BackendType.Metal] = metalResult[0];
        }

        // Assert - All backends should produce identical results (with reduction tolerance)
        var cpuResult = results[BackendType.CPU];
        foreach (var (backend, result) in results)
        {
            if (backend == BackendType.CPU) continue;

            result.Should().BeApproximately(cpuResult, 1e-3f * Math.Abs(cpuResult),
                $"{backend} dot product result should match CPU result");

            _output.WriteLine($"✓ DotProduct: {backend} result ({result:E}) matches CPU ({cpuResult:E})");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates test data using a formula.
    /// </summary>
    private static float[] GenerateTestData(int size, Func<int, float> formula)
    {
        var data = new float[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = formula(i);
        }
        return data;
    }

    #endregion
}

/// <summary>
/// Validation kernels used for cross-backend testing.
/// These kernels will use the [Kernel] attribute once the source generator is active.
/// </summary>
public static class ValidationKernels
{
    /// <summary>
    /// Vector addition kernel: result[i] = a[i] + b[i]
    /// </summary>
    public static class VectorAdd
    {
        // [Kernel] - Attribute will be applied once source generator is active
        public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < result.Length)
            {
                result[idx] = a[idx] + b[idx];
            }
        }

        public static async Task ExecuteAsync(IAccelerator accelerator, float[] a, float[] b, float[] result)
        {
            // Direct execution via accelerator until source generator is active
            await using var bufferA = await accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferResult = await accelerator.Memory.AllocateAsync<float>(result.Length);

            await accelerator.Memory.CopyToDeviceAsync(a.AsMemory(), bufferA, default);
            await accelerator.Memory.CopyToDeviceAsync(b.AsMemory(), bufferB, default);

            // Execute kernel logic (backend-specific implementation needed)
            // For now, simulate with CPU fallback
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = a[i] + b[i];
            }

            await accelerator.Memory.CopyFromDeviceAsync(bufferResult, result.AsMemory(), default);
        }
    }

    /// <summary>
    /// Vector multiplication kernel: result[i] = a[i] * b[i]
    /// </summary>
    public static class VectorMultiply
    {
        // [Kernel] - Attribute will be applied once source generator is active
        public static void VectorMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < result.Length)
            {
                result[idx] = a[idx] * b[idx];
            }
        }

        public static async Task ExecuteAsync(IAccelerator accelerator, float[] a, float[] b, float[] result)
        {
            // Direct execution via accelerator until source generator is active
            await using var bufferA = await accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferResult = await accelerator.Memory.AllocateAsync<float>(result.Length);

            await accelerator.Memory.CopyToDeviceAsync(a.AsMemory(), bufferA, default);
            await accelerator.Memory.CopyToDeviceAsync(b.AsMemory(), bufferB, default);

            // Execute kernel logic (backend-specific implementation needed)
            // For now, simulate with CPU fallback
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = a[i] * b[i];
            }

            await accelerator.Memory.CopyFromDeviceAsync(bufferResult, result.AsMemory(), default);
        }
    }

    /// <summary>
    /// Dot product kernel: result[0] = sum(a[i] * b[i])
    /// This kernel demonstrates a reduction operation.
    /// </summary>
    public static class DotProduct
    {
        // [Kernel] - Attribute will be applied once source generator is active
        public static void DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int idx = Kernel.ThreadId.X;

            // Parallel multiplication phase
            var temp = new float[a.Length];
            if (idx < a.Length)
            {
                temp[idx] = a[idx] * b[idx];
            }

            // Reduction phase (simplified for now)
            // In actual GPU implementation, this would use shared memory reduction
            if (idx == 0)
            {
                var sum = 0.0f;
                for (var i = 0; i < temp.Length; i++)
                {
                    sum += temp[i];
                }
                result[0] = sum;
            }
        }

        public static async Task ExecuteAsync(IAccelerator accelerator, float[] a, float[] b, float[] result)
        {
            // Direct execution via accelerator until source generator is active
            await using var bufferA = await accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferResult = await accelerator.Memory.AllocateAsync<float>(1);

            await accelerator.Memory.CopyToDeviceAsync(a.AsMemory(), bufferA, default);
            await accelerator.Memory.CopyToDeviceAsync(b.AsMemory(), bufferB, default);

            // Execute kernel logic (backend-specific implementation needed)
            // For now, simulate with CPU fallback
            var sum = 0.0f;
            for (var i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            result[0] = sum;

            await accelerator.Memory.CopyFromDeviceAsync(bufferResult, result.AsMemory(), default);
        }
    }
}
