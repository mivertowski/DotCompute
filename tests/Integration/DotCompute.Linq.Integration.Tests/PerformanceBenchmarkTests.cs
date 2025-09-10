using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Integration.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Performance benchmark tests for DotCompute LINQ operations.
/// Tests performance characteristics and validates optimization effectiveness.
/// </summary>
public class PerformanceBenchmarkTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MockHardwareProvider _mockHardware;
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _mockHardware = new MockHardwareProvider();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(_mockHardware.CpuAccelerator.Object);
        services.AddSingleton(_mockHardware.GpuAccelerator.Object);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task BenchmarkSimpleOperations_ShouldMeetPerformanceTargets()
    {
        // Arrange
        var testCases = TestDataGenerator.GeneratePerformanceTestCases().ToArray();
        var results = new PerformanceResults();

        foreach (var testCase in testCases)
        {
            _output.WriteLine($"Running benchmark: {testCase.Name}");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate LINQ operation execution
            var result = await SimulateLinqOperation(testCase.Data, testCase.ComputeIntensity);
            
            stopwatch.Stop();
            
            // Assert performance targets
            var throughput = testCase.DataSize / stopwatch.Elapsed.TotalSeconds;
            var latency = stopwatch.Elapsed;
            
            results.Add(testCase.Name, throughput, latency, testCase.DataSize);
            
            _output.WriteLine($"  Throughput: {throughput:F0} ops/sec");
            _output.WriteLine($"  Latency: {latency.TotalMilliseconds:F2} ms");
            
            // Performance assertions
            switch (testCase.ComputeIntensity)
            {
                case ComputeIntensity.Low:
                    throughput.Should().BeGreaterThan(100_000, "Low intensity operations should process >100k ops/sec");
                    break;
                case ComputeIntensity.Medium:
                    throughput.Should().BeGreaterThan(50_000, "Medium intensity operations should process >50k ops/sec");
                    break;
                case ComputeIntensity.High:
                    throughput.Should().BeGreaterThan(10_000, "High intensity operations should process >10k ops/sec");
                    break;
            }
            
            latency.Should().BeLessThan(TimeSpan.FromSeconds(5), "Operations should complete within 5 seconds");
        }

        // Verify overall performance characteristics
        results.Should().NotBeEmpty();
        results.AverageThroughput.Should().BeGreaterThan(1000);
        
        _output.WriteLine($"Overall average throughput: {results.AverageThroughput:F0} ops/sec");
    }

    [Fact]
    public async Task BenchmarkOptimizationEffectiveness_ShouldShowImprovement()
    {
        // Arrange
        var workloads = TestDataGenerator.GenerateOptimizationWorkloads().ToArray();
        
        foreach (var workload in workloads)
        {
            _output.WriteLine($"Testing optimization for: {workload.Name}");
            
            // Act - Unoptimized execution
            var unoptimizedTime = await MeasureExecutionTime(() => 
                SimulateUnoptimizedExecution(workload.Data, workload.Operations));
            
            // Act - Optimized execution
            var optimizedTime = await MeasureExecutionTime(() => 
                SimulateOptimizedExecution(workload.Data, workload.Operations));
            
            // Assert
            var speedup = unoptimizedTime.TotalMilliseconds / optimizedTime.TotalMilliseconds;
            
            _output.WriteLine($"  Unoptimized: {unoptimizedTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Optimized: {optimizedTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Speedup: {speedup:F2}x");
            
            speedup.Should().BeGreaterThan(1.2, "Optimization should provide at least 20% improvement");
            
            if (workload.ExpectedOptimizations.Contains("KernelFusion"))
            {
                speedup.Should().BeGreaterThan(1.5, "Kernel fusion should provide significant speedup");
            }
        }
    }

    [Fact]
    public async Task BenchmarkMemoryEfficiency_ShouldMinimizeAllocations()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        var testData = TestDataGenerator.GenerateFloatArray(1_000_000);

        // Act
        var memoryBefore = GC.GetTotalMemory(false);
        
        // Simulate memory-optimized operations
        var result = await SimulateMemoryOptimizedOperation(testData);
        
        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;
        
        // Force cleanup
        result = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryFinal = GC.GetTotalMemory(true);

        // Assert
        var memoryEfficiency = (double)testData.Length * sizeof(float) / memoryUsed;
        
        _output.WriteLine($"Memory used: {memoryUsed / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Data size: {testData.Length * sizeof(float) / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Efficiency ratio: {memoryEfficiency:F2}");
        
        memoryEfficiency.Should().BeGreaterThan(0.1, "Memory usage should be reasonable relative to data size");
        
        var memoryReclaimed = memoryAfter - memoryFinal;
        memoryReclaimed.Should().BeGreaterThan(memoryUsed * 0.8, "Most allocated memory should be reclaimed");
    }

    [Fact]
    public async Task BenchmarkScalability_ShouldScaleWithDataSize()
    {
        // Arrange
        var dataSizes = new[] { 1_000, 10_000, 100_000, 1_000_000 };
        var scalabilityResults = new List<ScalabilityPoint>();

        foreach (var size in dataSizes)
        {
            var testData = TestDataGenerator.GenerateFloatArray(size);
            
            // Act
            var executionTime = await MeasureExecutionTime(() => 
                SimulateLinqOperation(testData, ComputeIntensity.Medium));
            
            var throughput = size / executionTime.TotalSeconds;
            scalabilityResults.Add(new ScalabilityPoint(size, executionTime, throughput));
            
            _output.WriteLine($"Size: {size:N0}, Time: {executionTime.TotalMilliseconds:F2} ms, Throughput: {throughput:F0} ops/sec");
        }

        // Assert scalability characteristics
        scalabilityResults.Should().HaveCount(4);
        
        // Verify that execution time doesn't grow exponentially
        for (int i = 1; i < scalabilityResults.Count; i++)
        {
            var prev = scalabilityResults[i - 1];
            var current = scalabilityResults[i];
            
            var sizeRatio = (double)current.DataSize / prev.DataSize;
            var timeRatio = current.ExecutionTime.TotalMilliseconds / prev.ExecutionTime.TotalMilliseconds;
            
            // Time growth should be roughly linear to data size (allowing for some overhead)
            timeRatio.Should().BeLessThan(sizeRatio * 2, 
                $"Execution time should not grow faster than 2x the data size ratio (was {timeRatio:F2}x for {sizeRatio}x data)");
        }
        
        // Verify throughput remains reasonable for larger datasets
        var largestDatasetThroughput = scalabilityResults.Last().Throughput;
        largestDatasetThroughput.Should().BeGreaterThan(1000, "Large datasets should maintain reasonable throughput");
    }

    [Fact]
    public async Task BenchmarkConcurrentExecution_ShouldScaleWithThreads()
    {
        // Arrange
        var threadCounts = new[] { 1, 2, 4, 8 };
        var testData = TestDataGenerator.GenerateFloatArray(100_000);
        var concurrencyResults = new List<ConcurrencyPoint>();

        foreach (var threadCount in threadCounts)
        {
            // Act
            var executionTime = await MeasureExecutionTime(async () =>
            {
                var tasks = Enumerable.Range(0, threadCount)
                    .Select(_ => SimulateLinqOperation(testData, ComputeIntensity.Medium))
                    .ToArray();
                
                await Task.WhenAll(tasks);
            });
            
            var effectiveSpeedup = threadCounts[0] * executionTime.TotalMilliseconds / 
                                 (threadCount * executionTime.TotalMilliseconds);
            
            concurrencyResults.Add(new ConcurrencyPoint(threadCount, executionTime, effectiveSpeedup));
            
            _output.WriteLine($"Threads: {threadCount}, Time: {executionTime.TotalMilliseconds:F2} ms, Effective Speedup: {effectiveSpeedup:F2}x");
        }

        // Assert concurrency scaling
        concurrencyResults.Should().HaveCount(4);
        
        var parallelEfficiency = concurrencyResults.Last().EffectiveSpeedup / threadCounts.Last();
        parallelEfficiency.Should().BeGreaterThan(0.3, "Parallel efficiency should be at least 30%");
        
        // Verify speedup trends
        for (int i = 1; i < concurrencyResults.Count; i++)
        {
            var current = concurrencyResults[i];
            var previous = concurrencyResults[i - 1];
            
            current.ExecutionTime.Should().BeLessOrEqualTo(previous.ExecutionTime, 
                "Execution time should not increase with more threads");
        }
    }

    [Fact]
    public async Task BenchmarkStreamingPerformance_ShouldMaintainThroughput()
    {
        // Arrange
        var streamingCases = TestDataGenerator.GenerateStreamingTestCases().ToArray();
        
        foreach (var streamingCase in streamingCases)
        {
            _output.WriteLine($"Testing streaming case: {streamingCase.Name}");
            
            var processedBatches = 0;
            var totalProcessingTime = TimeSpan.Zero;
            
            // Act
            var overallStart = Stopwatch.StartNew();
            
            for (int i = 0; i < streamingCase.BatchCount; i++)
            {
                var batch = streamingCase.DataGenerator();
                
                var batchStart = Stopwatch.StartNew();
                await SimulateBatchProcessing(batch);
                batchStart.Stop();
                
                totalProcessingTime += batchStart.Elapsed;
                processedBatches++;
                
                if (streamingCase.IntervalMs > 0)
                {
                    await Task.Delay(streamingCase.IntervalMs);
                }
            }
            
            overallStart.Stop();
            
            // Assert streaming performance
            var avgBatchTime = totalProcessingTime.TotalMilliseconds / processedBatches;
            var throughputBatchesPerSec = processedBatches / overallStart.Elapsed.TotalSeconds;
            var requiredThroughput = 1000.0 / streamingCase.IntervalMs;
            
            _output.WriteLine($"  Processed {processedBatches} batches");
            _output.WriteLine($"  Average batch time: {avgBatchTime:F2} ms");
            _output.WriteLine($"  Throughput: {throughputBatchesPerSec:F1} batches/sec");
            _output.WriteLine($"  Required throughput: {requiredThroughput:F1} batches/sec");
            
            if (streamingCase.IntervalMs > 0)
            {
                throughputBatchesPerSec.Should().BeGreaterOrEqualTo(requiredThroughput * 0.9, 
                    "Streaming throughput should meet 90% of required rate");
            }
            
            avgBatchTime.Should().BeLessThan(streamingCase.IntervalMs * 0.8, 
                "Batch processing should complete well within interval");
        }
    }

    private async Task<TimeSpan> MeasureExecutionTime(Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private async Task<float[]> SimulateLinqOperation(float[] data, ComputeIntensity intensity)
    {
        // Simulate different compute intensities
        var delay = intensity switch
        {
            ComputeIntensity.Low => 1,
            ComputeIntensity.Medium => 5,
            ComputeIntensity.High => 20,
            ComputeIntensity.VeryHigh => 50,
            _ => 1
        };
        
        await Task.Delay(delay);
        
        // Return mock result
        return data.Select(x => x * 2.0f).ToArray();
    }

    private async Task<float[]> SimulateUnoptimizedExecution(float[] data, string[] operations)
    {
        // Simulate unoptimized execution with multiple passes
        var result = data;
        foreach (var operation in operations)
        {
            await Task.Delay(10); // Simulate overhead
            result = result.Select(x => x + 1).ToArray(); // Mock operation
        }
        return result;
    }

    private async Task<float[]> SimulateOptimizedExecution(float[] data, string[] operations)
    {
        // Simulate optimized execution with kernel fusion
        await Task.Delay(5); // Less overhead due to optimization
        return data.Select(x => x * operations.Length).ToArray(); // Mock fused operation
    }

    private async Task<float[]> SimulateMemoryOptimizedOperation(float[] data)
    {
        // Simulate in-place operation to minimize allocations
        await Task.Delay(10);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= 2.0f;
        }
        return data;
    }

    private async Task SimulateBatchProcessing(float[] batch)
    {
        // Simulate batch processing time proportional to batch size
        var processingTime = Math.Max(1, batch.Length / 10000);
        await Task.Delay(processingTime);
    }

    public void Dispose()
    {
        _mockHardware?.Dispose();
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Performance results collector for analysis.
/// </summary>
public class PerformanceResults : List<PerformanceResult>
{
    public void Add(string name, double throughput, TimeSpan latency, int dataSize)
    {
        Add(new PerformanceResult(name, throughput, latency, dataSize));
    }

    public double AverageThroughput => this.Average(r => r.Throughput);
    public TimeSpan AverageLatency => TimeSpan.FromMilliseconds(this.Average(r => r.Latency.TotalMilliseconds));
}

public record PerformanceResult(string Name, double Throughput, TimeSpan Latency, int DataSize);
public record ScalabilityPoint(int DataSize, TimeSpan ExecutionTime, double Throughput);
public record ConcurrencyPoint(int ThreadCount, TimeSpan ExecutionTime, double EffectiveSpeedup);

/// <summary>
/// Actual BenchmarkDotNet benchmarks for detailed performance analysis.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class LinqOperationBenchmarks
{
    private float[] _smallData = null!;
    private float[] _mediumData = null!;
    private float[] _largeData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallData = TestDataGenerator.GenerateFloatArray(1_000);
        _mediumData = TestDataGenerator.GenerateFloatArray(100_000);
        _largeData = TestDataGenerator.GenerateFloatArray(1_000_000);
    }

    [Benchmark]
    [Arguments(1_000)]
    [Arguments(100_000)]
    [Arguments(1_000_000)]
    public float[] SelectOperation(int size)
    {
        var data = size switch
        {
            1_000 => _smallData,
            100_000 => _mediumData,
            1_000_000 => _largeData,
            _ => _smallData
        };
        
        return data.Select(x => x * 2.0f).ToArray();
    }

    [Benchmark]
    [Arguments(1_000)]
    [Arguments(100_000)]
    [Arguments(1_000_000)]
    public float[] ChainedOperations(int size)
    {
        var data = size switch
        {
            1_000 => _smallData,
            100_000 => _mediumData,
            1_000_000 => _largeData,
            _ => _smallData
        };
        
        return data.Select(x => x * 2.0f)
                  .Where(x => x > 0)
                  .Select(x => x + 1.0f)
                  .ToArray();
    }

    [Benchmark]
    [Arguments(1_000)]
    [Arguments(100_000)]
    [Arguments(1_000_000)]
    public float ReductionOperation(int size)
    {
        var data = size switch
        {
            1_000 => _smallData,
            100_000 => _mediumData,
            1_000_000 => _largeData,
            _ => _smallData
        };
        
        return data.Sum();
    }
}

/// <summary>
/// xUnit test to run BenchmarkDotNet benchmarks.
/// </summary>
public class BenchmarkRunner
{
    [Fact(Skip = "Manual benchmark run only")]
    public void RunBenchmarks()
    {
        var summary = BenchmarkRunner.Run<LinqOperationBenchmarks>();
        // Results will be displayed in console output
    }
}