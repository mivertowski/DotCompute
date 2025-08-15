using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Tests.Shared.TestInfrastructure;

/// <summary>
/// Base class for performance benchmark tests
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class PerformanceBenchmarkBase : CoverageTestBase
{
    protected PerformanceBenchmark Benchmark { get; }

    protected PerformanceBenchmarkBase(ITestOutputHelper output) : base(output)
    {
        Benchmark = new PerformanceBenchmark(Logger);
    }

    /// <summary>
    /// Assert that operation completes within expected time
    /// </summary>
    protected async Task AssertPerformance<T>(
        Func<Task<T>> operation,
        TimeSpan expectedMaxTime,
        string operationName,
        int iterations = 1)
    {
        var results = await Benchmark.MeasureAsync(operation, iterations, operationName);

        Logger.LogInformation(
            "Performance test '{Operation}': Avg {AvgTime}ms, Min {MinTime}ms, Max {MaxTime}ms ({Iterations} iterations)",
            operationName, results.AverageTime.TotalMilliseconds, results.MinTime.TotalMilliseconds,
            results.MaxTime.TotalMilliseconds, iterations);

        if (results.AverageTime > expectedMaxTime)
        {
            throw new InvalidOperationException(
                $"Performance test '{operationName}' failed. " +
                $"Average time: {results.AverageTime.TotalMilliseconds:F2}ms, " +
                $"Expected max: {expectedMaxTime.TotalMilliseconds:F2}ms");
        }
    }

    /// <summary>
    /// Benchmark memory allocation performance
    /// </summary>
    protected async Task<MemoryBenchmarkResult> BenchmarkMemoryAllocation(
        Func<int, Task<IDisposable>> allocator,
        int[] sizes,
        int iterationsPerSize = 10)
    {
        var results = new List<(int Size, TimeSpan AllocTime, long MemoryBefore, long MemoryAfter)>();

        foreach (var size in sizes)
        {
            var allocTimes = new List<TimeSpan>();
            var memoryBefore = GC.GetTotalMemory(true);

            for (var i = 0; i < iterationsPerSize; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                using var allocated = await allocator(size);
                stopwatch.Stop();
                allocTimes.Add(stopwatch.Elapsed);
            }

            var memoryAfter = GC.GetTotalMemory(true);
            var avgAllocTime = TimeSpan.FromTicks(allocTimes.Sum(t => t.Ticks) / allocTimes.Count);

            results.Add((size, avgAllocTime, memoryBefore, memoryAfter));

            Logger.LogDebug("Memory allocation benchmark - Size: {Size} bytes, Avg time: {Time}ms",
                size, avgAllocTime.TotalMilliseconds);
        }

        return new MemoryBenchmarkResult(results);
    }

    /// <summary>
    /// Compare performance between two operations
    /// </summary>
    protected async Task<PerformanceComparisonResult> ComparePerformance<T>(
        Func<Task<T>> operation1,
        Func<Task<T>> operation2,
        string operation1Name,
        string operation2Name,
        int iterations = 10)
    {
        var result1 = await Benchmark.MeasureAsync(operation1, iterations, operation1Name);
        var result2 = await Benchmark.MeasureAsync(operation2, iterations, operation2Name);

        var comparison = new PerformanceComparisonResult(
            result1, result2, operation1Name, operation2Name);

        Logger.LogInformation(
            "Performance comparison: {Op1} avg {Time1}ms vs {Op2} avg {Time2}ms (speedup: {Speedup:F2}x)",
            operation1Name, result1.AverageTime.TotalMilliseconds,
            operation2Name, result2.AverageTime.TotalMilliseconds,
            comparison.SpeedupRatio);

        return comparison;
    }
}

/// <summary>
/// Performance benchmark utilities
/// </summary>
[ExcludeFromCodeCoverage]
public class PerformanceBenchmark
{
    private readonly ILogger _logger;

    public PerformanceBenchmark(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Measure operation performance
    /// </summary>
    public async Task<BenchmarkResult> MeasureAsync<T>(
        Func<Task<T>> operation,
        int iterations,
        string operationName)
    {
        var times = new List<TimeSpan>();
        var results = new List<T>();

        // Warm up
        try
        {
            await operation();
        }
        catch
        {
            // Ignore warmup failures
        }

        // Measure iterations
        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await operation();
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Benchmark iteration {Iteration} failed for {Operation}", i, operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed);
            }
        }

        return new BenchmarkResult(operationName, times, results.Cast<object>().ToList());
    }

    /// <summary>
    /// Measure synchronous operation performance
    /// </summary>
    public BenchmarkResult Measure<T>(
        Func<T> operation,
        int iterations,
        string operationName)
    {
        var times = new List<TimeSpan>();
        var results = new List<T>();

        // Warm up
        try
        {
            operation();
        }
        catch
        {
            // Ignore warmup failures
        }

        // Measure iterations
        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = operation();
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Benchmark iteration {Iteration} failed for {Operation}", i, operationName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed);
            }
        }

        return new BenchmarkResult(operationName, times, results.Cast<object>().ToList());
    }
}

/// <summary>
/// Benchmark result data
/// </summary>
[ExcludeFromCodeCoverage]
public record BenchmarkResult(
    string OperationName,
    IReadOnlyList<TimeSpan> Times,
    IReadOnlyList<object> Results)
{
    public TimeSpan AverageTime => TimeSpan.FromTicks(Times.Sum(t => t.Ticks) / Times.Count);
    public TimeSpan MinTime => Times.Min();
    public TimeSpan MaxTime => Times.Max();
    public TimeSpan MedianTime => Times.OrderBy(t => t).Skip(Times.Count / 2).First();
    public double StandardDeviation
    {
        get
        {
            var avg = AverageTime.TotalMilliseconds;
            var variance = Times.Select(t => Math.Pow(t.TotalMilliseconds - avg, 2)).Average();
            return Math.Sqrt(variance);
        }
    }
}

/// <summary>
/// Memory benchmark result
/// </summary>
[ExcludeFromCodeCoverage]
public record MemoryBenchmarkResult(
    IReadOnlyList<(int Size, TimeSpan AllocTime, long MemoryBefore, long MemoryAfter)> Results)
{
    public double AverageAllocTimeMs => Results.Average(r => r.AllocTime.TotalMilliseconds);
    public long TotalMemoryAllocated => Results.Sum(r => r.MemoryAfter - r.MemoryBefore);
    public double AllocationEfficiency => Results.Average(r => r.Size / r.AllocTime.TotalMilliseconds);
}

/// <summary>
/// Performance comparison result
/// </summary>
[ExcludeFromCodeCoverage]
public record PerformanceComparisonResult(
    BenchmarkResult Result1,
    BenchmarkResult Result2,
    string Operation1Name,
    string Operation2Name)
{
    public double SpeedupRatio => Result2.AverageTime.TotalMilliseconds / Result1.AverageTime.TotalMilliseconds;
    public bool IsFirstFaster => SpeedupRatio > 1.0;
    public TimeSpan TimeDifference => Result1.AverageTime - Result2.AverageTime;
    public double PercentageImprovement => (SpeedupRatio - 1.0) * 100.0;
}
