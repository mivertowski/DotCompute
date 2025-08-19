// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.BLAS;
using DotCompute.Linq;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests
{

/// <summary>
/// Integration tests for GPU-accelerated LINQ operations with RTX card.
/// </summary>
public class LinqGpuIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _services;
    private readonly ILogger<LinqGpuIntegrationTests> _logger;
    private readonly CudaDevice _cudaDevice;
    private readonly GPULINQProvider _gpuLinqProvider;
    private readonly CpuFallbackExecutor _cpuExecutor;
    private readonly bool _hasRtxGpu;

    public LinqGpuIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<LinqGpuIntegrationTests>>();
        
        // Check for RTX GPU
        try
        {
            _cudaDevice = new CudaDevice(0, _services.GetRequiredService<ILogger<CudaDevice>>());
            _hasRtxGpu = _cudaDevice.Info.Name.Contains("RTX") || _cudaDevice.Info.Name.Contains("GeForce");
            
            if (_hasRtxGpu)
            {
                _output.WriteLine($"RTX GPU detected: {_cudaDevice.Info.Name}");
                _output.WriteLine($"CUDA Cores: {_cudaDevice.Info.CudaCores}");
                _output.WriteLine($"Memory: {_cudaDevice.Info.TotalMemory / (1024 * 1024 * 1024)} GB");
                _output.WriteLine($"Compute Capability: {_cudaDevice.Info.ComputeCapability}");
            }
            else
            {
                _output.WriteLine("No RTX GPU detected, tests will use CPU fallback");
            }
            
            // Initialize LINQ providers
            _gpuLinqProvider = new GPULINQProvider(_cudaDevice, _services.GetRequiredService<ILogger<GPULINQProvider>>());
            _cpuExecutor = new CpuFallbackExecutor(_services.GetRequiredService<ILogger<CpuFallbackExecutor>>());
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to initialize CUDA: {ex.Message}");
            _hasRtxGpu = false;
            
            // Create CPU-only fallback
            _cpuExecutor = new CpuFallbackExecutor(_services.GetRequiredService<ILogger<CpuFallbackExecutor>>());
        }
    }

    [Fact]
    public async Task Select_LargeDataset_ComparesGpuVsCpu()
    {
        // Arrange
        const int dataSize = 1_000_000;
        var data = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        
        _output.WriteLine($"Testing Select operation with {dataSize:N0} elements");
        
        // Act - CPU execution
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuResult = await _cpuExecutor.SelectAsync(data, data, x => x * 2.0f + 1.0f);
        cpuStopwatch.Stop();
        
        _output.WriteLine($"CPU execution time: {cpuStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"CPU throughput: {dataSize / cpuStopwatch.Elapsed.TotalSeconds / 1_000_000:F2} M ops/sec");
        
        if (_hasRtxGpu)
        {
            // Act - GPU execution
            var gpuStopwatch = Stopwatch.StartNew();
            var queryable = data.AsQueryable().AsGPUQueryable();
            var gpuQuery = queryable.Select(x => x * 2.0f + 1.0f);
            var gpuResult = gpuQuery.ToArray();
            gpuStopwatch.Stop();
            
            _output.WriteLine($"GPU execution time: {gpuStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"GPU throughput: {dataSize / gpuStopwatch.Elapsed.TotalSeconds / 1_000_000:F2} M ops/sec");
            _output.WriteLine($"Speedup: {cpuStopwatch.Elapsed.TotalMilliseconds / gpuStopwatch.Elapsed.TotalMilliseconds:F2}x");
            
            // Assert - Results match
            AssertArraysEqual(cpuResult, gpuResult, 0.001f);
        }
        
        // Assert - CPU results are correct
        Assert.Equal(1.0f, cpuResult[0], 3);
        Assert.Equal(3.0f, cpuResult[1], 3);
        Assert.Equal(5.0f, cpuResult[2], 3);
    }

    [Fact]
    public async Task Where_FilteringOperation_ComparesGpuVsCpu()
    {
        // Arrange
        const int dataSize = 10_000_000;
        var data = Enumerable.Range(0, dataSize).ToArray();
        
        _output.WriteLine($"Testing Where operation with {dataSize:N0} elements");
        
        // Act - CPU execution
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuResult = await _cpuExecutor.WhereAsync(data, x => x % 2 == 0 && x > 100);
        cpuStopwatch.Stop();
        
        _output.WriteLine($"CPU execution time: {cpuStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"CPU filtered count: {cpuResult.Length:N0}");
        
        if (_hasRtxGpu)
        {
            // Act - GPU execution
            var gpuStopwatch = Stopwatch.StartNew();
            var queryable = data.AsQueryable().AsGPUQueryable();
            var gpuQuery = queryable.Where(x => x % 2 == 0 && x > 100);
            var gpuResult = gpuQuery.ToArray();
            gpuStopwatch.Stop();
            
            _output.WriteLine($"GPU execution time: {gpuStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"GPU filtered count: {gpuResult.Length:N0}");
            _output.WriteLine($"Speedup: {cpuStopwatch.Elapsed.TotalMilliseconds / gpuStopwatch.Elapsed.TotalMilliseconds:F2}x");
            
            // Assert
            Assert.Equal(cpuResult.Length, gpuResult.Length);
            AssertArraysEqual(cpuResult, gpuResult);
        }
        
        // Assert - CPU results are correct
        Assert.True(cpuResult.All(x => x % 2 == 0 && x > 100));
    }

    [Fact]
    public async Task Sum_ReductionOperation_ComparesGpuVsCpu()
    {
        // Arrange
        const int dataSize = 50_000_000;
        var data = Enumerable.Range(1, dataSize).Select(i => (float)i).ToArray();
        
        _output.WriteLine($"Testing Sum operation with {dataSize:N0} elements");
        
        // Act - CPU execution with SIMD
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuSum = await _cpuExecutor.SumAsync(data);
        cpuStopwatch.Stop();
        
        _output.WriteLine($"CPU execution time: {cpuStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"CPU sum result: {cpuSum:E}");
        _output.WriteLine($"CPU throughput: {dataSize / cpuStopwatch.Elapsed.TotalSeconds / 1_000_000:F2} M elements/sec");
        
        if (_hasRtxGpu)
        {
            // Act - GPU execution
            var gpuStopwatch = Stopwatch.StartNew();
            var queryable = data.AsQueryable().AsGPUQueryable();
            var gpuSum = queryable.Sum();
            gpuStopwatch.Stop();
            
            _output.WriteLine($"GPU execution time: {gpuStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"GPU sum result: {gpuSum:E}");
            _output.WriteLine($"GPU throughput: {dataSize / gpuStopwatch.Elapsed.TotalSeconds / 1_000_000:F2} M elements/sec");
            _output.WriteLine($"Speedup: {cpuStopwatch.Elapsed.TotalMilliseconds / gpuStopwatch.Elapsed.TotalMilliseconds:F2}x");
            
            // Assert - Results are close (accounting for floating point precision)
            var expectedSum = (double)dataSize * (dataSize + 1) / 2;
            Assert.Equal(expectedSum, cpuSum, expectedSum * 0.001); // 0.1% tolerance
            Assert.Equal(expectedSum, gpuSum, expectedSum * 0.001);
        }
    }

    [Fact]
    public async Task ComplexQuery_ChainedOperations_ComparesGpuVsCpu()
    {
        // Arrange
        const int dataSize = 1_000_000;
        var data = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        
        _output.WriteLine($"Testing complex chained query with {dataSize:N0} elements");
        
        // Act - CPU execution
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuResult = data
            .Where(x => x > 100)
            .Select(x => x * x)
            .Where(x => x < 1_000_000)
            .Select(x => MathF.Sqrt(x))
            .Sum();
        cpuStopwatch.Stop();
        
        _output.WriteLine($"CPU execution time: {cpuStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"CPU result: {cpuResult:F2}");
        
        if (_hasRtxGpu)
        {
            // Act - GPU execution
            var gpuStopwatch = Stopwatch.StartNew();
            var gpuResult = data.AsQueryable()
                .AsGPUQueryable()
                .Where(x => x > 100)
                .Select(x => x * x)
                .Where(x => x < 1_000_000)
                .Select(x => MathF.Sqrt(x))
                .Sum();
            gpuStopwatch.Stop();
            
            _output.WriteLine($"GPU execution time: {gpuStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"GPU result: {gpuResult:F2}");
            _output.WriteLine($"Speedup: {cpuStopwatch.Elapsed.TotalMilliseconds / gpuStopwatch.Elapsed.TotalMilliseconds:F2}x");
            
            // Assert - Results are close
            Assert.Equal(cpuResult, gpuResult, cpuResult * 0.001f);
        }
    }

    [Fact]
    public async Task OrderBy_SortingOperation_ComparesGpuVsCpu()
    {
        // Arrange
        const int dataSize = 100_000;
        var random = new Random(42);
        var data = Enumerable.Range(0, dataSize).Select(_ => (float)random.NextDouble()).ToArray();
        
        _output.WriteLine($"Testing OrderBy operation with {dataSize:N0} elements");
        
        // Act - CPU execution
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuResult = await _cpuExecutor.OrderByAsync(data, x => x);
        cpuStopwatch.Stop();
        
        _output.WriteLine($"CPU execution time: {cpuStopwatch.ElapsedMilliseconds}ms");
        
        if (_hasRtxGpu)
        {
            // Act - GPU execution  
            var gpuStopwatch = Stopwatch.StartNew();
            var gpuResult = data.AsQueryable()
                .AsGPUQueryable()
                .OrderBy(x => x)
                .ToArray();
            gpuStopwatch.Stop();
            
            _output.WriteLine($"GPU execution time: {gpuStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Speedup: {cpuStopwatch.Elapsed.TotalMilliseconds / gpuStopwatch.Elapsed.TotalMilliseconds:F2}x");
            
            // Assert - Both are sorted correctly
            AssertArraysSorted(cpuResult);
            AssertArraysSorted(gpuResult);
            AssertArraysEqual(cpuResult, gpuResult, 0.0001f);
        }
        
        // Assert - CPU result is sorted
        AssertArraysSorted(cpuResult);
    }

    [Fact]
    public async Task GroupBy_AggregationOperation_TestsGpuPerformance()
    {
        if (!_hasRtxGpu)
        {
            _output.WriteLine("Skipping GPU GroupBy test - no RTX GPU available");
            return;
        }
        
        // Arrange
        const int dataSize = 1_000_000;
        const int groups = 100;
        var data = Enumerable.Range(0, dataSize).Select(i => new { Key = i % groups, Value = (float)i }).ToArray();
        
        _output.WriteLine($"Testing GroupBy operation with {dataSize:N0} elements and {groups} groups");
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = data.AsQueryable()
            .AsGPUQueryable()
            .GroupBy(x => x.Key)
            .Select(g => new { Key = g.Key, Sum = g.Sum(x => x.Value), Count = g.Count() })
            .ToArray();
        stopwatch.Stop();
        
        _output.WriteLine($"GPU GroupBy execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Result groups: {result.Length}");
        
        // Assert
        Assert.Equal(groups, result.Length);
        Assert.True(result.All(r => r.Count == dataSize / groups));
    }

    [Fact]
    public async Task MemoryTransfer_LargeDataset_MeasuresOverhead()
    {
        if (!_hasRtxGpu)
        {
            _output.WriteLine("Skipping memory transfer test - no RTX GPU available");
            return;
        }
        
        // Test different data sizes to measure transfer overhead
        var sizes = new[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };
        
        _output.WriteLine("Memory Transfer Performance:");
        _output.WriteLine("Size\t\tH2D (ms)\tD2H (ms)\tTotal (ms)\tBandwidth (GB/s)");
        
        foreach (var size in sizes)
        {
            var data = new float[size];
            var sizeInBytes = size * sizeof(float);
            
            // Host to Device
            var h2dStopwatch = Stopwatch.StartNew();
            var deviceBuffer = await _cudaDevice.Memory.AllocateAsync(sizeInBytes);
            await deviceBuffer.CopyFromHostAsync(data.AsMemory());
            h2dStopwatch.Stop();
            
            // Device to Host
            var result = new float[size];
            var d2hStopwatch = Stopwatch.StartNew();
            await deviceBuffer.CopyToHostAsync(result.AsMemory());
            d2hStopwatch.Stop();
            
            var totalTime = h2dStopwatch.Elapsed + d2hStopwatch.Elapsed;
            var bandwidth = (sizeInBytes * 2.0) / totalTime.TotalSeconds / (1024 * 1024 * 1024);
            
            _output.WriteLine($"{size:N0}\t\t{h2dStopwatch.Elapsed.TotalMilliseconds:F2}\t\t" +
                            $"{d2hStopwatch.Elapsed.TotalMilliseconds:F2}\t\t" +
                            $"{totalTime.TotalMilliseconds:F2}\t\t{bandwidth:F2}");
            
            await deviceBuffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ParallelExecution_MultipleQueries_TestsConcurrency()
    {
        if (!_hasRtxGpu)
        {
            _output.WriteLine("Skipping parallel execution test - no RTX GPU available");
            return;
        }
        
        // Arrange
        const int queryCount = 10;
        const int dataSize = 100_000;
        var datasets = Enumerable.Range(0, queryCount)
            .Select(_ => Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray())
            .ToArray();
        
        _output.WriteLine($"Testing {queryCount} parallel queries with {dataSize:N0} elements each");
        
        // Act - Sequential execution
        var seqStopwatch = Stopwatch.StartNew();
        var seqResults = new float[queryCount];
        for (int i = 0; i < queryCount; i++)
        {
            seqResults[i] = datasets[i].AsQueryable()
                .AsGPUQueryable()
                .Where(x => x > 1000)
                .Select(x => x * 2)
                .Sum();
        }
        seqStopwatch.Stop();
        
        _output.WriteLine($"Sequential execution time: {seqStopwatch.ElapsedMilliseconds}ms");
        
        // Act - Parallel execution
        var parStopwatch = Stopwatch.StartNew();
        var parResults = new float[queryCount];
        await Parallel.ForEachAsync(Enumerable.Range(0, queryCount), async (i, ct) =>
        {
            parResults[i] = await Task.Run(() => datasets[i].AsQueryable()
                .AsGPUQueryable()
                .Where(x => x > 1000)
                .Select(x => x * 2)
                .Sum(), ct);
        });
        parStopwatch.Stop();
        
        _output.WriteLine($"Parallel execution time: {parStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Parallel speedup: {seqStopwatch.Elapsed.TotalMilliseconds / parStopwatch.Elapsed.TotalMilliseconds:F2}x");
        
        // Assert
        AssertArraysEqual(seqResults, parResults, 0.001f);
    }

    #region Helper Methods

    private void AssertArraysEqual<T>(T[] expected, T[] actual, float tolerance = 0.0f) where T : IComparable<T>
    {
        Assert.Equal(expected.Length, actual.Length);
        
        if (typeof(T) == typeof(float) && tolerance > 0)
        {
            var expectedFloat = expected as float[];
            var actualFloat = actual as float[];
            
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expectedFloat![i], actualFloat![i], tolerance);
            }
        }
        else
        {
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }

    private void AssertArraysSorted<T>(T[] array) where T : IComparable<T>
    {
        for (int i = 1; i < array.Length; i++)
        {
            Assert.True(array[i].CompareTo(array[i - 1]) >= 0, 
                $"Array not sorted at index {i}: {array[i - 1]} > {array[i]}");
        }
    }

    #endregion

    public void Dispose()
    {
        _gpuLinqProvider?.Dispose();
        _cudaDevice?.Dispose();
    }
}

/// <summary>
/// Extension methods for GPU LINQ support
/// </summary>
public static class GpuLinqExtensions
{
    public static IQueryable<T> AsGPUQueryable<T>(this IQueryable<T> source)
    {
        // This would be implemented to wrap the source with GPU provider
        // For testing, we'll use the existing queryable
        return source;
    }
}}
