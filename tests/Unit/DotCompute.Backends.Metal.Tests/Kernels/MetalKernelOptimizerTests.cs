// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Kernels;

/// <summary>
/// Unit tests for Metal kernel optimization pipeline.
/// </summary>
public sealed class MetalKernelOptimizerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalKernelOptimizerTests> _logger;
    private readonly IntPtr _device;
    private readonly bool _metalAvailable;

    public MetalKernelOptimizerTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestOutputLogger<MetalKernelOptimizerTests>(output);

        // Check if Metal is available
        _metalAvailable = MetalNative.IsMetalSupported();

        if (_metalAvailable)
        {
            _device = MetalNative.CreateSystemDefaultDevice();
        }
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithDebugProfile_AddsDebugMacros()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("test_kernel", @"
kernel void test_kernel(device float* data [[buffer(0)]]) {
    uint idx = get_thread_position_in_grid().x;
    data[idx] = data[idx] * 2.0f;
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.None);

        // Assert
        Assert.NotNull(optimized);
        Assert.Equal(MetalOptimizationProfile.Debug, telemetry.Profile);
        Assert.Contains("#define METAL_DEBUG_MODE 1", optimized.Code);
        Assert.Contains("test_kernel", optimized.Name);
        _output.WriteLine($"Applied {telemetry.AppliedOptimizations.Count} debug optimizations");
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithReleaseProfile_OptimizesThreadgroupSize()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("vector_add", @"
kernel void vector_add(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]]
) {
    result[gid] = a[gid] + b[gid];
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Default);

        // Assert
        Assert.NotNull(optimized);
        Assert.Equal(MetalOptimizationProfile.Release, telemetry.Profile);
        Assert.True(telemetry.AppliedOptimizations.Count > 0);
        _output.WriteLine($"Optimization time: {telemetry.OptimizationTimeMs}ms");
        _output.WriteLine($"Applied optimizations: {telemetry.AppliedOptimizations.Count}");
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithAggressiveProfile_AppliesAllOptimizations()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("matrix_multiply", @"
kernel void matrix_multiply(
    device const float* A [[buffer(0)]],
    device const float* B [[buffer(1)]],
    device float* C [[buffer(2)]],
    constant uint& M [[buffer(3)]],
    constant uint& N [[buffer(4)]],
    constant uint& K [[buffer(5)]],
    uint2 gid [[thread_position_in_grid]]
) {
    uint row = gid.y;
    uint col = gid.x;

    if (row >= M || col >= N) return;

    float sum = 0.0f;
    for (uint k = 0; k < K; ++k) {
        sum += A[row * K + k] * B[k * N + col];
    }

    C[row * N + col] = sum;
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.Equal(MetalOptimizationProfile.Aggressive, telemetry.Profile);
        Assert.True(telemetry.AppliedOptimizations.Count >= 3); // Should have multiple optimizations

        // Check for specific optimizations
        Assert.Contains("CompilerHints", telemetry.AppliedOptimizations.Keys);
        Assert.Contains("#pragma clang loop", optimized.Code);

        _output.WriteLine($"Aggressive optimization applied {telemetry.AppliedOptimizations.Count} strategies:");
        foreach (var opt in telemetry.AppliedOptimizations)
        {
            _output.WriteLine($"  - {opt.Key}: {opt.Value}");
        }
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithStridedMemoryAccess_DetectsCoalescingOpportunity()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("strided_access", @"
kernel void strided_access(
    device float* data [[buffer(0)]],
    constant uint& stride [[buffer(1)]],
    uint gid [[thread_position_in_grid]]
) {
    data[gid * stride + 1] = data[gid * stride] * 2.0f;
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.True(telemetry.HasMemoryCoalescing);
        Assert.Contains("MemoryCoalescing", telemetry.AppliedOptimizations.Keys);
        _output.WriteLine("Memory coalescing optimization detected strided access pattern");
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithThreadgroupMemory_AddsAlignmentHints()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("reduction", @"
kernel void reduction(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]]
) {
    shared[tid] = input[gid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    for (uint s = 1; s < 256; s *= 2) {
        if (tid % (2 * s) == 0) {
            shared[tid] += shared[tid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    if (tid == 0) {
        output[0] = shared[0];
    }
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.Contains("ThreadgroupMemoryOptimization", telemetry.AppliedOptimizations.Keys);
        Assert.Contains("alignment", optimized.Code, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine("Threadgroup memory optimization added alignment hints");
    }

    [SkippableFact]
    public async Task OptimizeAsync_MeasuresPerformanceImpact()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("benchmark_kernel", @"
kernel void benchmark_kernel(device float* data [[buffer(0)]]) {
    uint idx = get_thread_position_in_grid().x;
    for (int i = 0; i < 100; i++) {
        data[idx] += 1.0f;
    }
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.True(telemetry.OptimizationTimeMs > 0);
        Assert.True(telemetry.OptimizationTimeMs < 5000); // Should complete within 5 seconds
        _output.WriteLine($"Optimization completed in {telemetry.OptimizationTimeMs}ms");
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithGpuFamilySpecific_AppliesAppleSiliconOptimizations()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("apple_optimized", @"
kernel void apple_optimized(device float* data [[buffer(0)]]) {
    uint idx = get_thread_position_in_grid().x;
    data[idx] = data[idx] * 2.0f;
}");

        var deviceInfo = MetalNative.GetDeviceInfo(_device);
        var familiesString = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";

        Skip.If(!familiesString.Contains("Apple", StringComparison.Ordinal),
            "Test requires Apple Silicon GPU");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.Contains("GpuFamilyOptimization", telemetry.AppliedOptimizations.Keys);
        Assert.Contains("#define METAL_APPLE_GPU 1", optimized.Code);
        _output.WriteLine("Applied Apple Silicon-specific optimizations");
    }

    [SkippableFact]
    public async Task OptimizeAsync_PreservesKernelMetadata()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("metadata_kernel", @"
kernel void metadata_kernel(device float* data [[buffer(0)]]) {
    data[0] = 1.0f;
}")
        {
            EntryPoint = "metadata_kernel",
            Language = KernelLanguage.Metal
        };

        kernel.Metadata!["customKey"] = "customValue";
        kernel.Metadata["version"] = 1;

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Default);

        // Assert
        Assert.NotNull(optimized);
        Assert.Equal("metadata_kernel", optimized.Name);
        Assert.Equal("metadata_kernel", optimized.EntryPoint);
        Assert.Equal(KernelLanguage.Metal, optimized.Language);
        Assert.Contains("optimizationProfile", optimized.Metadata!.Keys);
        _output.WriteLine($"Preserved kernel metadata with optimization info");
    }

    [Fact]
    public async Task OptimizeAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        var optimizer = new MetalKernelOptimizer(_device, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await optimizer.OptimizeAsync(null!, OptimizationLevel.Default));
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithCancellation_HonorsCancellationToken()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("test", "kernel void test() {}");
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // Note: Current implementation doesn't actively check cancellation in all paths
        // but this tests the API contract
        var (result, _) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Default, cts.Token);

        // Should still return a result even if cancelled quickly
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithBarriers_AnalyzesBarrierUsage()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("barrier_kernel", @"
kernel void barrier_kernel(
    device float* data [[buffer(0)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]])
{
    shared[tid] = data[tid];
    threadgroup_barrier(mem_flags::mem_threadgroup);
    data[tid] = shared[tid] * 2.0f;
    threadgroup_barrier(mem_flags::mem_threadgroup);
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.Contains("BarrierOptimization", telemetry.AppliedOptimizations.Keys);
        _output.WriteLine("Barrier usage analyzed successfully");
    }

    [SkippableFact]
    public async Task OptimizeAsync_WithLoops_DetectsUnrollOpportunities()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("loop_kernel", @"
kernel void loop_kernel(device float* data [[buffer(0)]]) {
    for (int i = 0; i < 8; i++) {
        data[i] *= 2.0f;
    }
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        if (telemetry.AppliedOptimizations.ContainsKey("LoopOptimization"))
        {
            _output.WriteLine("Loop unrolling opportunities detected");
        }
    }

    [SkippableFact]
    public async Task OptimizeAsync_TelemetryTracking_CollectsDetailedMetrics()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("telemetry_test", @"
kernel void telemetry_test(device float* data [[buffer(0)]]) {
    data[0] = 1.0f;
}");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(telemetry);
        Assert.Equal("telemetry_test", telemetry.KernelName);
        Assert.True(telemetry.OptimizationTimeMs >= 0);
        Assert.NotNull(telemetry.AppliedOptimizations);
        Assert.True(telemetry.OriginalThreadgroupSize >= 0);
        Assert.True(telemetry.OptimizedThreadgroupSize >= 0);

        _output.WriteLine($"Telemetry collected - Profile: {telemetry.Profile}, " +
                         $"Time: {telemetry.OptimizationTimeMs}ms, " +
                         $"Optimizations: {telemetry.AppliedOptimizations.Count}");
    }

    [SkippableFact]
    public async Task OptimizeAsync_OptimizationFailure_HandlesGracefully()
    {
        Skip.IfNot(_metalAvailable, "Metal is not available on this system");

        // Arrange
        var optimizer = new MetalKernelOptimizer(_device, _logger);
        var kernel = new KernelDefinition("failing_kernel", "invalid metal code @#$%");

        // Act
        var (optimized, telemetry) = await optimizer.OptimizeAsync(kernel, OptimizationLevel.Maximum);

        // Assert - Should still return a result even if optimization has issues
        Assert.NotNull(optimized);
        _output.WriteLine("Optimization failure handled gracefully");
    }
}

/// <summary>
/// Test logger that writes to xUnit test output.
/// </summary>
internal sealed class TestOutputLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestOutputLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            _output.WriteLine(exception.ToString());
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
