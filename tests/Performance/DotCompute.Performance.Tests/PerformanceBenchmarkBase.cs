using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Xunit;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Base class for all performance benchmarks providing common functionality
/// for timing measurements, throughput calculations, and statistical analysis.
/// </summary>
[Config(typeof(PerformanceBenchmarkConfig))]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Trait("Category", "Performance")]
public abstract class PerformanceBenchmarkBase
{
    protected readonly List<double> _measurements = [];
    protected readonly Stopwatch _stopwatch = new();
    
    /// <summary>
    /// Number of warmup iterations before actual measurements
    /// </summary>
    [Params(10)]
    public int WarmupIterations { get; set; } = 10;
    
    /// <summary>
    /// Number of measurement iterations for statistical analysis
    /// </summary>
    [Params(100)]
    public int MeasurementIterations { get; set; } = 100;
    
    /// <summary>
    /// Data size for throughput calculations (in bytes)
    /// </summary>
    [Params(1024, 4096, 16384, 65536, 262144, 1048576)]
    public int DataSize { get; set; }
    
    /// <summary>
    /// Performance metrics container
    /// </summary>
    protected class PerformanceMetrics
    {
        public double MinTime { get; set; }
        public double MaxTime { get; set; }
        public double AverageTime { get; set; }
        public double MedianTime { get; set; }
        public double StandardDeviation { get; set; }
        public double Throughput { get; set; } // MB/s or operations/s
        public double P95Time { get; set; }
        public double P99Time { get; set; }
        public long TotalAllocations { get; set; }
        public long PeakMemoryUsage { get; set; }
        
        public override string ToString()
        {
            return $"Avg: {AverageTime:F3}ms, Median: {MedianTime:F3}ms, " +
                   $"P95: {P95Time:F3}ms, P99: {P99Time:F3}ms, " +
                   $"Throughput: {Throughput:F2} MB/s, " +
                   $"StdDev: {StandardDeviation:F3}ms";
        }
    }
    
    /// <summary>
    /// Custom benchmark configuration for performance tests
    /// </summary>
    private class PerformanceBenchmarkConfig : ManualConfig
    {
        public PerformanceBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(10)
                .WithIterationCount(100)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
        }
    }
    
    /// <summary>
    /// Performs warmup operations to stabilize JIT compilation and memory allocation
    /// </summary>
    protected virtual async Task WarmupAsync()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteOperationAsync();
        }
        
        // Clear any measurements from warmup
        _measurements.Clear();
        
        // Force garbage collection before measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    
    /// <summary>
    /// Abstract method that derived classes must implement for the operation being benchmarked
    /// </summary>
    protected abstract Task ExecuteOperationAsync();
    
    /// <summary>
    /// Measures execution time of a single operation
    /// </summary>
    /// <returns>Execution time in milliseconds</returns>
    protected async Task<double> MeasureOperationAsync()
    {
        _stopwatch.Restart();
        await ExecuteOperationAsync();
        _stopwatch.Stop();
        
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    /// <summary>
    /// Runs multiple iterations and collects performance metrics
    /// </summary>
    /// <returns>Comprehensive performance metrics</returns>
    protected async Task<PerformanceMetrics> RunBenchmarkAsync()
    {
        await WarmupAsync();
        
        var memoryBefore = GC.GetTotalMemory(true);
        
        // Collect measurements
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureOperationAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        var allocations = memoryAfter - memoryBefore;
        
        return CalculateMetrics(allocations);
    }
    
    /// <summary>
    /// Calculates comprehensive performance metrics from collected measurements
    /// </summary>
    private PerformanceMetrics CalculateMetrics(long allocations)
    {
        _measurements.Sort();
        
        var count = _measurements.Count;
        var sum = 0.0;
        foreach (var measurement in _measurements)
        {
            sum += measurement;
        }
        
        var average = sum / count;
        var median = count % 2 == 0 
            ? (_measurements[count / 2 - 1] + _measurements[count / 2]) / 2.0
            : _measurements[count / 2];
        
        // Calculate standard deviation
        var variance = 0.0;
        foreach (var measurement in _measurements)
        {
            variance += Math.Pow(measurement - average, 2);
        }
        var stdDev = Math.Sqrt(variance / count);
        
        // Calculate percentiles
        var p95Index = (int)Math.Ceiling(0.95 * count) - 1;
        var p99Index = (int)Math.Ceiling(0.99 * count) - 1;
        
        // Calculate throughput (MB/s)
        var throughput = DataSize > 0 
            ? (DataSize / (1024.0 * 1024.0)) / (average / 1000.0)
            : 1000.0 / average; // operations per second
        
        return new PerformanceMetrics
        {
            MinTime = _measurements[0],
            MaxTime = _measurements[count - 1],
            AverageTime = average,
            MedianTime = median,
            StandardDeviation = stdDev,
            P95Time = _measurements[p95Index],
            P99Time = _measurements[p99Index],
            Throughput = throughput,
            TotalAllocations = allocations,
            PeakMemoryUsage = GC.GetTotalMemory(false)
        };
    }
    
    /// <summary>
    /// Asserts that performance metrics meet expected criteria
    /// </summary>
    protected static void AssertPerformanceExpectations(PerformanceMetrics metrics, 
        double maxAverageTimeMs = double.MaxValue,
        double minThroughputMBps = 0.0,
        double maxStandardDeviationMs = double.MaxValue)
    {
        Assert.True(metrics.AverageTime <= maxAverageTimeMs, 
            $"Average time {metrics.AverageTime:F3}ms exceeds maximum {maxAverageTimeMs:F3}ms");
        
        Assert.True(metrics.Throughput >= minThroughputMBps,
            $"Throughput {metrics.Throughput:F2} MB/s is below minimum {minThroughputMBps:F2} MB/s");
        
        Assert.True(metrics.StandardDeviation <= maxStandardDeviationMs,
            $"Standard deviation {metrics.StandardDeviation:F3}ms exceeds maximum {maxStandardDeviationMs:F3}ms");
    }
    
    /// <summary>
    /// Compares performance between two benchmark runs
    /// </summary>
    protected static void ComparePerformance(PerformanceMetrics baseline, PerformanceMetrics current, 
        double maxRegressionPercent = 10.0)
    {
        var regressionPercent = ((current.AverageTime - baseline.AverageTime) / baseline.AverageTime) * 100.0;
        
        Assert.True(regressionPercent <= maxRegressionPercent,
            $"Performance regression of {regressionPercent:F1}% exceeds maximum allowed {maxRegressionPercent:F1}%");
    }
    
    /// <summary>
    /// Logs performance metrics for analysis
    /// </summary>
    protected void LogMetrics(PerformanceMetrics metrics, string operationName)
    {
        Console.WriteLine($"Performance Metrics for {operationName}:");
        Console.WriteLine($"  Data Size: {DataSize} bytes");
        Console.WriteLine($"  {metrics}");
        Console.WriteLine($"  Memory: {metrics.TotalAllocations} bytes allocated, " +
                         $"{metrics.PeakMemoryUsage} bytes peak usage");
    }
    
    /// <summary>
    /// Cleanup method called after benchmarks
    /// </summary>
    [GlobalCleanup]
    public virtual void Cleanup()
    {
        _measurements.Clear();
        GC.Collect();
    }
}