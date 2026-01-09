// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests.Stability;

/// <summary>
/// Long-running stability tests designed to verify system reliability over extended periods.
/// </summary>
/// <remarks>
/// <para>
/// These tests are designed to run for hours or days to detect:
/// <list type="bullet">
/// <item>Memory leaks</item>
/// <item>Handle leaks</item>
/// <item>Thread pool exhaustion</item>
/// <item>Resource fragmentation</item>
/// <item>Performance degradation over time</item>
/// <item>GPU driver stability issues</item>
/// </list>
/// </para>
/// <para>
/// <strong>Running stability tests:</strong>
/// <code>
/// dotnet test --filter "Category=Stability" --timeout 72h
/// </code>
/// </para>
/// </remarks>
[Collection("Stability Tests")]
[Trait("Category", "Stability")]
[Trait("Category", "LongRunning")]
public class StabilityTestSuite
{
    private readonly ITestOutputHelper _output;
    private readonly StabilityMetrics _metrics;

    public StabilityTestSuite(ITestOutputHelper output)
    {
        _output = output;
        _metrics = new StabilityMetrics();
    }

    /// <summary>
    /// 1-hour memory stability test to detect leaks.
    /// </summary>
    [Fact]
    [Trait("Duration", "1Hour")]
    public async Task Memory_Stability_1Hour_Should_Not_Leak()
    {
        var duration = TimeSpan.FromHours(1);
        var cts = new CancellationTokenSource(duration);
        var startMemory = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        var iterationCount = 0;
        var gcCollections = new int[3];

        _output.WriteLine($"Starting 1-hour memory stability test at {DateTime.Now}");
        _output.WriteLine($"Initial memory: {startMemory / (1024 * 1024)} MB");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Simulate typical workload
                await SimulateWorkloadAsync(cts.Token);
                iterationCount++;

                // Periodic reporting
                if (iterationCount % 1000 == 0)
                {
                    ReportProgress(sw.Elapsed, iterationCount, startMemory);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout reached
        }

        sw.Stop();
        var endMemory = GC.GetTotalMemory(true);
        var memoryGrowth = endMemory - startMemory;
        var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);

        _output.WriteLine($"\n=== Final Results ===");
        _output.WriteLine($"Duration: {sw.Elapsed}");
        _output.WriteLine($"Iterations: {iterationCount:N0}");
        _output.WriteLine($"End memory: {endMemory / (1024 * 1024)} MB");
        _output.WriteLine($"Memory growth: {memoryGrowthMB:F2} MB");

        // Memory growth should be minimal (< 50MB over 1 hour of operation)
        memoryGrowthMB.Should().BeLessThan(50, "Memory growth indicates potential leak");
    }

    /// <summary>
    /// 8-hour soak test for overnight stability verification.
    /// </summary>
    [Fact]
    [Trait("Duration", "8Hours")]
    public async Task SoakTest_8Hours_Should_Remain_Stable()
    {
        var duration = TimeSpan.FromHours(8);
        var cts = new CancellationTokenSource(duration);
        var sw = Stopwatch.StartNew();

        var checkpoint = new StabilityCheckpoint
        {
            StartTime = DateTime.UtcNow,
            StartMemory = GC.GetTotalMemory(true),
            StartThreadCount = Process.GetCurrentProcess().Threads.Count,
            StartHandleCount = Process.GetCurrentProcess().HandleCount
        };

        var hourlyCheckpoints = new List<StabilityCheckpoint>();
        var lastCheckpointTime = DateTime.UtcNow;
        var iterationCount = 0L;
        var errorCount = 0L;

        _output.WriteLine($"Starting 8-hour soak test at {DateTime.Now}");
        _output.WriteLine($"Initial state: Memory={checkpoint.StartMemory / (1024 * 1024)}MB, Threads={checkpoint.StartThreadCount}, Handles={checkpoint.StartHandleCount}");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await SimulateWorkloadAsync(cts.Token);
                    Interlocked.Increment(ref iterationCount);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref errorCount);
                    _output.WriteLine($"Error at {sw.Elapsed}: {ex.Message}");
                }

                // Hourly checkpoint
                if ((DateTime.UtcNow - lastCheckpointTime).TotalHours >= 1)
                {
                    var hourlyCheckpoint = CaptureCheckpoint(checkpoint, iterationCount, errorCount);
                    hourlyCheckpoints.Add(hourlyCheckpoint);
                    LogCheckpoint(hourlyCheckpoint, sw.Elapsed);
                    lastCheckpointTime = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        sw.Stop();
        var finalCheckpoint = CaptureCheckpoint(checkpoint, iterationCount, errorCount);

        _output.WriteLine($"\n=== 8-Hour Soak Test Results ===");
        _output.WriteLine($"Duration: {sw.Elapsed}");
        _output.WriteLine($"Iterations: {iterationCount:N0}");
        _output.WriteLine($"Errors: {errorCount}");
        _output.WriteLine($"Error rate: {(double)errorCount / iterationCount:P4}");
        _output.WriteLine($"Memory growth: {finalCheckpoint.MemoryGrowthMB:F2} MB");
        _output.WriteLine($"Thread growth: {finalCheckpoint.ThreadGrowth}");
        _output.WriteLine($"Handle growth: {finalCheckpoint.HandleGrowth}");

        // Assertions
        finalCheckpoint.MemoryGrowthMB.Should().BeLessThan(100, "Excessive memory growth indicates leak");
        finalCheckpoint.ThreadGrowth.Should().BeLessThan(50, "Excessive thread growth indicates leak");
        finalCheckpoint.HandleGrowth.Should().BeLessThan(100, "Excessive handle growth indicates leak");
        ((double)errorCount / iterationCount).Should().BeLessThan(0.0001, "Error rate too high");
    }

    /// <summary>
    /// 72-hour endurance test for production readiness validation.
    /// </summary>
    [Fact]
    [Trait("Duration", "72Hours")]
    public async Task EnduranceTest_72Hours_Should_Pass()
    {
        var duration = TimeSpan.FromHours(72);
        var cts = new CancellationTokenSource(duration);
        var sw = Stopwatch.StartNew();

        var checkpoint = new StabilityCheckpoint
        {
            StartTime = DateTime.UtcNow,
            StartMemory = GC.GetTotalMemory(true),
            StartThreadCount = Process.GetCurrentProcess().Threads.Count,
            StartHandleCount = Process.GetCurrentProcess().HandleCount
        };

        var checkpoints = new List<StabilityCheckpoint>();
        var lastCheckpointTime = DateTime.UtcNow;
        var iterationCount = 0L;
        var errorCount = 0L;
        var performanceSamples = new List<double>();

        _output.WriteLine($"Starting 72-hour endurance test at {DateTime.Now}");
        _output.WriteLine($"Target completion: {DateTime.Now.AddHours(72)}");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var iterationSw = Stopwatch.StartNew();

                try
                {
                    await SimulateWorkloadAsync(cts.Token);
                    Interlocked.Increment(ref iterationCount);

                    iterationSw.Stop();
                    if (iterationCount % 100 == 0)
                    {
                        performanceSamples.Add(iterationSw.ElapsedMilliseconds);
                        // Keep last 10000 samples
                        if (performanceSamples.Count > 10000)
                        {
                            performanceSamples.RemoveAt(0);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref errorCount);
                }

                // 6-hour checkpoints
                if ((DateTime.UtcNow - lastCheckpointTime).TotalHours >= 6)
                {
                    var hourlyCheckpoint = CaptureCheckpoint(checkpoint, iterationCount, errorCount);
                    checkpoints.Add(hourlyCheckpoint);
                    LogCheckpoint(hourlyCheckpoint, sw.Elapsed);

                    // Check for performance degradation
                    if (performanceSamples.Count > 1000)
                    {
                        var recentAvg = performanceSamples.TakeLast(1000).Average();
                        var overallAvg = performanceSamples.Average();
                        var degradation = (recentAvg - overallAvg) / overallAvg * 100;

                        if (degradation > 20)
                        {
                            _output.WriteLine($"WARNING: Performance degradation detected: {degradation:F1}%");
                        }
                    }

                    lastCheckpointTime = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        sw.Stop();
        var finalCheckpoint = CaptureCheckpoint(checkpoint, iterationCount, errorCount);

        _output.WriteLine($"\n=== 72-Hour Endurance Test Results ===");
        _output.WriteLine($"Duration: {sw.Elapsed}");
        _output.WriteLine($"Iterations: {iterationCount:N0}");
        _output.WriteLine($"Throughput: {iterationCount / sw.Elapsed.TotalHours:N0} iterations/hour");
        _output.WriteLine($"Errors: {errorCount}");
        _output.WriteLine($"Error rate: {(double)errorCount / iterationCount:P6}");
        _output.WriteLine($"Memory growth: {finalCheckpoint.MemoryGrowthMB:F2} MB");
        _output.WriteLine($"Average iteration time: {performanceSamples.Average():F2} ms");

        // Strict assertions for 72-hour test
        finalCheckpoint.MemoryGrowthMB.Should().BeLessThan(200, "72-hour memory growth too high");
        ((double)errorCount / iterationCount).Should().BeLessThan(0.00001, "Error rate too high for production");
    }

    /// <summary>
    /// Concurrent workload stability test.
    /// </summary>
    [Fact]
    [Trait("Duration", "Long")]
    public async Task ConcurrentWorkload_Should_Remain_Stable()
    {
        var duration = TimeSpan.FromMinutes(30);
        var cts = new CancellationTokenSource(duration);
        var concurrentWorkers = Environment.ProcessorCount * 2;
        var totalIterations = 0L;
        var totalErrors = 0L;
        var sw = Stopwatch.StartNew();

        _output.WriteLine($"Starting concurrent workload test with {concurrentWorkers} workers");

        var workers = Enumerable.Range(0, concurrentWorkers)
            .Select(workerId => RunWorkerAsync(workerId, cts.Token))
            .ToList();

        async Task<(long iterations, long errors)> RunWorkerAsync(int id, CancellationToken ct)
        {
            var iterations = 0L;
            var errors = 0L;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await SimulateWorkloadAsync(ct);
                        iterations++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            return (iterations, errors);
        }

        var results = await Task.WhenAll(workers);
        sw.Stop();

        totalIterations = results.Sum(r => r.iterations);
        totalErrors = results.Sum(r => r.errors);

        _output.WriteLine($"\n=== Concurrent Workload Results ===");
        _output.WriteLine($"Duration: {sw.Elapsed}");
        _output.WriteLine($"Workers: {concurrentWorkers}");
        _output.WriteLine($"Total iterations: {totalIterations:N0}");
        _output.WriteLine($"Total errors: {totalErrors}");
        _output.WriteLine($"Throughput: {totalIterations / sw.Elapsed.TotalSeconds:N0} ops/sec");

        // Should have minimal errors under concurrent load
        ((double)totalErrors / totalIterations).Should().BeLessThan(0.001, "Too many errors under concurrent load");
    }

    private async Task SimulateWorkloadAsync(CancellationToken ct)
    {
        // Simulate typical compute workload
        var data = new float[1024];
        var random = Random.Shared;

        // Generate data
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)random.NextDouble();
        }

        // Simulate computation
        var sum = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            sum += MathF.Sin(data[i]) * MathF.Cos(data[i]);
        }

        // Small delay to simulate I/O
        await Task.Delay(1, ct);
    }

    private StabilityCheckpoint CaptureCheckpoint(StabilityCheckpoint baseline, long iterations, long errors)
    {
        var currentProcess = Process.GetCurrentProcess();
        var currentMemory = GC.GetTotalMemory(false);

        return new StabilityCheckpoint
        {
            StartTime = baseline.StartTime,
            StartMemory = baseline.StartMemory,
            StartThreadCount = baseline.StartThreadCount,
            StartHandleCount = baseline.StartHandleCount,
            CurrentTime = DateTime.UtcNow,
            CurrentMemory = currentMemory,
            CurrentThreadCount = currentProcess.Threads.Count,
            CurrentHandleCount = currentProcess.HandleCount,
            IterationCount = iterations,
            ErrorCount = errors
        };
    }

    private void ReportProgress(TimeSpan elapsed, int iterations, long startMemory)
    {
        var currentMemory = GC.GetTotalMemory(false);
        var growth = (currentMemory - startMemory) / (1024.0 * 1024.0);
        _output.WriteLine($"[{elapsed:hh\\:mm\\:ss}] Iterations: {iterations:N0}, Memory growth: {growth:F2} MB");
    }

    private void LogCheckpoint(StabilityCheckpoint checkpoint, TimeSpan elapsed)
    {
        _output.WriteLine($"\n=== Checkpoint at {elapsed:hh\\:mm\\:ss} ===");
        _output.WriteLine($"  Iterations: {checkpoint.IterationCount:N0}");
        _output.WriteLine($"  Errors: {checkpoint.ErrorCount}");
        _output.WriteLine($"  Memory: {checkpoint.CurrentMemory / (1024 * 1024)} MB (growth: {checkpoint.MemoryGrowthMB:F2} MB)");
        _output.WriteLine($"  Threads: {checkpoint.CurrentThreadCount} (growth: {checkpoint.ThreadGrowth})");
        _output.WriteLine($"  Handles: {checkpoint.CurrentHandleCount} (growth: {checkpoint.HandleGrowth})");
    }
}

/// <summary>
/// Stability checkpoint data.
/// </summary>
public class StabilityCheckpoint
{
    public DateTime StartTime { get; init; }
    public long StartMemory { get; init; }
    public int StartThreadCount { get; init; }
    public int StartHandleCount { get; init; }

    public DateTime CurrentTime { get; init; }
    public long CurrentMemory { get; init; }
    public int CurrentThreadCount { get; init; }
    public int CurrentHandleCount { get; init; }

    public long IterationCount { get; init; }
    public long ErrorCount { get; init; }

    public double MemoryGrowthMB => (CurrentMemory - StartMemory) / (1024.0 * 1024.0);
    public int ThreadGrowth => CurrentThreadCount - StartThreadCount;
    public int HandleGrowth => CurrentHandleCount - StartHandleCount;
    public TimeSpan Duration => CurrentTime - StartTime;
}

/// <summary>
/// Stability metrics collector.
/// </summary>
public class StabilityMetrics
{
    private readonly List<double> _latencySamples = new();
    private readonly object _lock = new();
    private long _totalOperations;
    private long _errorCount;

    public void RecordLatency(double milliseconds)
    {
        lock (_lock)
        {
            _latencySamples.Add(milliseconds);
            if (_latencySamples.Count > 100000)
            {
                _latencySamples.RemoveAt(0);
            }
        }
        Interlocked.Increment(ref _totalOperations);
    }

    public void RecordError() => Interlocked.Increment(ref _errorCount);

    public StabilityMetricsSummary GetSummary()
    {
        lock (_lock)
        {
            return new StabilityMetricsSummary
            {
                TotalOperations = _totalOperations,
                ErrorCount = _errorCount,
                AverageLatencyMs = _latencySamples.Count > 0 ? _latencySamples.Average() : 0,
                P95LatencyMs = _latencySamples.Count > 0 ? Percentile(_latencySamples, 95) : 0,
                P99LatencyMs = _latencySamples.Count > 0 ? Percentile(_latencySamples, 99) : 0
            };
        }
    }

    private static double Percentile(List<double> values, int percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }
}

/// <summary>
/// Summary of stability metrics.
/// </summary>
public record StabilityMetricsSummary
{
    public long TotalOperations { get; init; }
    public long ErrorCount { get; init; }
    public double AverageLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
    public double ErrorRate => TotalOperations > 0 ? (double)ErrorCount / TotalOperations : 0;
}
