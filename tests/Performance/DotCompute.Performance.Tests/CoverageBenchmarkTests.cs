using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BdnLogger = BenchmarkDotNet.Loggers.ILogger;
using BenchmarkDotNet.Running;
using DotCompute.Tests.Utilities.TestInfrastructure;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Performance;


/// <summary>
/// Performance benchmark tests for coverage analysis
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CoverageBenchmarkTests(ITestOutputHelper output) : PerformanceBenchmarkBase(output)
{
    // LoggerMessage delegates for improved performance
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> LogMemoryAllocationCompleted =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "MemoryAllocationCompleted"),
            "Memory allocation benchmark completed:");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, double, Exception?> LogAverageAllocationTime =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(2, "AverageAllocationTime"),
            "  Average allocation time: {Time}ms");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, long, Exception?> LogTotalMemoryAllocated =
        LoggerMessage.Define<long>(LogLevel.Information, new EventId(3, "TotalMemoryAllocated"),
            "  Total memory allocated: {Memory} bytes");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, double, Exception?> LogAllocationEfficiency =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(4, "AllocationEfficiency"),
            "  Allocation efficiency: {Efficiency} bytes/ms");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, int, double, Exception?> LogDataTransferBenchmark =
        LoggerMessage.Define<int, double>(LogLevel.Information, new EventId(5, "DataTransferBenchmark"),
            "Data transfer benchmark - Size: {Size} bytes, Bandwidth: {Bandwidth:F2} MB/s");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, int, double, double, Exception?> LogConcurrencyBenchmark =
        LoggerMessage.Define<int, double, double>(LogLevel.Information, new EventId(6, "ConcurrencyBenchmark"),
            "Concurrency {Level}: {Time}ms avg, efficiency: {Efficiency:F2}");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, double, Exception?> LogMemoryPressureImpact =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(7, "MemoryPressureImpact"),
            "Memory pressure impact: {Impact:F2}x slowdown");

    [Fact]
    public async Task MemoryAllocation_Performance_MeetsRequirements()
    {
        // Test memory allocation performance across different sizes
        var sizes = new[] { 1024, 10240, 102400, 1048576 }; // 1KB to 1MB

        var result = await BenchmarkMemoryAllocation(
            size => Task.FromResult<IDisposable>(new TestMemoryBuffer(size)),
            sizes,
            iterationsPerSize: 100);

        LogMemoryAllocationCompleted(Logger, null);
        LogAverageAllocationTime(Logger, result.AverageAllocTimeMs, null);
        LogTotalMemoryAllocated(Logger, result.TotalMemoryAllocated, null);
        LogAllocationEfficiency(Logger, result.AllocationEfficiency, null);

        // Assert performance requirements
        _ = result.AverageAllocTimeMs.Should().BeLessThan(10.0, "Memory allocation should be under 10ms average");
        _ = result.AllocationEfficiency.Should().BeGreaterThan(100, "Allocation efficiency should be > 100 bytes/ms");
    }

    [Fact]
    public async Task KernelCompilation_Performance_MeetsRequirements()
    {
        // Benchmark kernel compilation performance
        var kernelSources = new[]
        {
        "kernel void simple() { }",
        "kernel void vectorAdd(global float* a, global float* b, global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
        GenerateComplexKernel()
    };

        foreach (var kernel in kernelSources)
        {
            await AssertPerformance(
               () => CompileKernelAsync(kernel),
                expectedMaxTime: TimeSpan.FromMilliseconds(500),
                operationName: $"Kernel compilation({kernel.Length} chars)",
                iterations: 10);
        }
    }

    [Fact]
    public async Task DataTransfer_Bandwidth_MeetsRequirements()
    {
        // Benchmark data transfer performance
        var dataSizes = new[] { 1024, 10240, 102400, 1048576 }; // 1KB to 1MB

        foreach (var size in dataSizes)
        {
            var data = CreateTestData(size);

            var result = await Benchmark.MeasureAsync(
               () => TransferDataAsync(data),
                iterations: 50,
                operationName: $"Data transfer({size} bytes)");

            var bandwidthMBps = (size / (1024.0 * 1024.0)) / result.AverageTime.TotalSeconds;

            LogDataTransferBenchmark(Logger, size, bandwidthMBps, null);

            // Assert minimum bandwidth requirements
            _ = bandwidthMBps.Should().BeGreaterThan(10.0, $"Data transfer bandwidth should be > 10 MB/s, got {bandwidthMBps:F2}");
        }
    }

    [Fact]
    public async Task ConcurrentOperations_Performance_ScalesCorrectly()
    {
        // Test performance scaling with concurrent operations
        var concurrencyLevels = new[] { 1, 2, 4, 8 };
        var baselineTime = TimeSpan.Zero;

        foreach (var concurrency in concurrencyLevels)
        {
            var result = await Benchmark.MeasureAsync(
               () => ExecuteConcurrentOperationsAsync(concurrency),
                iterations: 10,
                operationName: $"Concurrent operations(x{concurrency})");

            if (concurrency == 1)
            {
                baselineTime = result.AverageTime;
            }

            var efficiency = baselineTime.TotalMilliseconds / result.AverageTime.TotalMilliseconds * concurrency;

            LogConcurrencyBenchmark(Logger, concurrency, result.AverageTime.TotalMilliseconds, efficiency, null);

            // Assert reasonable scaling efficiency
            if (concurrency > 1)
            {
                _ = efficiency.Should().BeGreaterThan(0.7 * concurrency,
                    $"Concurrency efficiency should be > 70% of ideal, got {efficiency / concurrency * 100:F1}%");
            }
        }
    }

    [Fact]
    public async Task MemoryPressure_GarbageCollection_Impact()
    {
        // Test performance under memory pressure
        const int iterations = 100;
        const int allocationSize = 1024 * 1024; // 1MB allocations

        // Baseline performance
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = GC.GetTotalMemory(false);

        var baselineResult = await Benchmark.MeasureAsync(
           () => AllocateAndReleaseMemoryAsync(allocationSize),
            iterations,
            "Memory operations(clean)");

        // Performance under pressure
        var allocatedMemory = new List<byte[]>();
        try
        {
            // Allocate memory to create pressure
            for (var i = 0; i < 100; i++)
            {
                allocatedMemory.Add(new byte[allocationSize]);
            }

            var pressureResult = await Benchmark.MeasureAsync(
               () => AllocateAndReleaseMemoryAsync(allocationSize),
                iterations,
                "Memory operations(pressure)");

            var performanceImpact = pressureResult.AverageTime.TotalMilliseconds / baselineResult.AverageTime.TotalMilliseconds;

            LogMemoryPressureImpact(Logger, performanceImpact, null);

            // Assert reasonable performance degradation
            _ = performanceImpact.Should().BeLessThan(3.0, $"Memory pressure impact should be < 3x, got {performanceImpact:F2}x");
        }
        finally
        {
            allocatedMemory.Clear();
            GC.Collect();
        }
    }

    [Fact]
    public void BenchmarkRunner_Integration_ExecutesSuccessfully()
    {
        // Integration test for BenchmarkDotNet
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddJob(Job.Dry)
            .AddLogger(new XunitLogger(Output));

        var summary = BenchmarkRunner.Run<SimpleBenchmark>(config);

        Assert.NotNull(summary);
        Assert.NotEmpty(summary.Reports);
        Assert.True(summary.Reports.All(r => r.Success), "All benchmark runs should succeed");
    }

    // Helper methods for benchmarking

    private static async Task<object> CompileKernelAsync(string kernelSource)
    {
        // Simulate kernel compilation
        await Task.Delay(Random.Shared.Next(10, 100));
        return new { Compiled = true, Source = kernelSource };
    }

    private static async Task<int> TransferDataAsync(byte[] data)
    {
        // Simulate data transfer
        await Task.Delay(Math.Max(1, data.Length / 100000)); // Simulate bandwidth
        return data.Length;
    }

    private static async Task<int> ExecuteConcurrentOperationsAsync(int concurrencyLevel)
    {
        var tasks = Enumerable.Range(0, concurrencyLevel)
            .Select(_ => SimulateWorkAsync())
            .ToArray();

        await Task.WhenAll(tasks);
        return tasks.Length;
    }

    private static async Task<int> AllocateAndReleaseMemoryAsync(int size)
    {
        var buffer = new byte[size];
        await Task.Yield();
        return buffer.Length;
    }

    private static async Task SimulateWorkAsync() => await Task.Delay(Random.Shared.Next(10, 50));

    private static string GenerateComplexKernel()
    {
        return @"
kernel void matrixMultiply(
    global const float* A,
    global const float* B, 
    global float* C,
    const int N)
{
    int row = get_global_id(0);
    int col = get_global_id(1);
    
    if(row < N && col < N) {
        float sum = 0.0f;
        for(int k = 0; k < N; k++) {
            sum += A[row * N + k] * B[k * N + col];
        }
        C[row * N + col] = sum;
    }
}";
    }

    /// <summary>
    /// Test memory buffer for benchmarking
    /// </summary>
    private sealed class TestMemoryBuffer(int size) : IDisposable
    {
        public void Dispose()
        {
            // Nothing to dispose for test buffer
        }
    }
}

/// <summary>
/// Simple benchmark class for BenchmarkDotNet integration testing
/// </summary>
[ExcludeFromCodeCoverage]
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[SuppressMessage("Design", "CA1852:Seal internal types", Justification = "BenchmarkDotNet requires this class to be unsealed for proxy generation")]
public class SimpleBenchmark
{
    private readonly byte[] _data = new byte[1024];

    [Benchmark]
    public int SimpleOperation() => _data.Sum(b => (int)b);

    [Benchmark]
    public async Task<int> AsyncOperation()
    {
        await Task.Delay(1);
        return _data.Length;
    }
}

/// <summary>
/// xUnit logger for BenchmarkDotNet
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class XunitLogger(ITestOutputHelper output) : BdnLogger
{
    private readonly ITestOutputHelper _output = output;

    public string Id => "xunit";
    public int Priority => 0;

    public void Write(LogKind logKind, string text) => _output.WriteLine($"[{logKind}] {text}");

    public void WriteLine() => _output.WriteLine("");

    public void WriteLine(LogKind logKind, string text) => _output.WriteLine($"[{logKind}] {text}");

    public void Flush() { }
}
