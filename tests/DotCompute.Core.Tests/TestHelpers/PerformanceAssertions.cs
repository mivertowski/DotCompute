using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;

namespace DotCompute.Core.Tests.TestHelpers;

/// <summary>
/// Provides performance-related assertion helpers.
/// </summary>
public static class PerformanceAssertions
{
    /// <summary>
    /// Asserts that an action completes within the specified time limit.
    /// </summary>
    public static async Task ShouldCompleteWithinAsync(Func<Task> action, TimeSpan timeLimit, string because = "")
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();

        Execute.Assertion
            .ForCondition(stopwatch.Elapsed <= timeLimit)
            .BecauseOf(because)
            .FailWith($"Expected task to complete within {timeLimit.TotalMilliseconds}ms, but it took {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Asserts that an action completes within the specified time limit.
    /// </summary>
    public static void ShouldCompleteWithin(Action action, TimeSpan timeLimit, string because = "")
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();

        Execute.Assertion
            .ForCondition(stopwatch.Elapsed <= timeLimit)
            .BecauseOf(because)
            .FailWith($"Expected action to complete within {timeLimit.TotalMilliseconds}ms, but it took {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Measures and asserts that an operation shows expected speedup.
    /// </summary>
    public static async Task ShouldShowSpeedupAsync(
        Func<Task> baselineAction, 
        Func<Task> optimizedAction, 
        double minimumSpeedup,
        int iterations = 10)
    {
        // Warmup
        await baselineAction();
        await optimizedAction();

        // Measure baseline
        var baselineTime = await MeasureAverageTimeAsync(baselineAction, iterations);
        
        // Measure optimized
        var optimizedTime = await MeasureAverageTimeAsync(optimizedAction, iterations);
        
        var speedup = baselineTime.TotalMilliseconds / optimizedTime.TotalMilliseconds;

        Execute.Assertion
            .ForCondition(speedup >= minimumSpeedup)
            .FailWith($"Expected speedup of at least {minimumSpeedup}x, but got {speedup:F2}x " +
                     $"(baseline: {baselineTime.TotalMilliseconds:F2}ms, optimized: {optimizedTime.TotalMilliseconds:F2}ms)");
    }

    /// <summary>
    /// Measures the average execution time of an action over multiple iterations.
    /// </summary>
    public static async Task<TimeSpan> MeasureAverageTimeAsync(Func<Task> action, int iterations)
    {
        var totalTime = TimeSpan.Zero;
        
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            totalTime += stopwatch.Elapsed;
        }

        return TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / iterations);
    }

    /// <summary>
    /// Asserts that memory usage stays within bounds during an operation.
    /// </summary>
    public static async Task ShouldNotExceedMemoryAsync(Func<Task> action, long maxBytesIncrease)
    {
        // Force garbage collection to get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var startMemory = GC.GetTotalMemory(false);
        
        await action();
        
        var endMemory = GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        Execute.Assertion
            .ForCondition(memoryIncrease <= maxBytesIncrease)
            .FailWith($"Expected memory increase to be at most {maxBytesIncrease:N0} bytes, " +
                     $"but was {memoryIncrease:N0} bytes");
    }

    /// <summary>
    /// Asserts that an operation scales linearly with input size.
    /// </summary>
    public static async Task ShouldScaleLinearlyAsync(
        Func<int, Task> action,
        int[] sizes,
        double tolerance = 0.2)
    {
        if (sizes.Length < 3)
        {
            throw new ArgumentException("Need at least 3 sizes to test linear scaling");
        }

        var times = new double[sizes.Length];
        
        for (int i = 0; i < sizes.Length; i++)
        {
            // Warmup
            await action(sizes[i]);
            
            // Measure
            var time = await MeasureAverageTimeAsync(() => action(sizes[i]), 5);
            times[i] = time.TotalMilliseconds;
        }

        // Check that time increases roughly linearly with size
        for (int i = 1; i < sizes.Length - 1; i++)
        {
            var expectedRatio = (double)sizes[i + 1] / sizes[i];
            var actualRatio = times[i + 1] / times[i];
            var deviation = Math.Abs(actualRatio - expectedRatio) / expectedRatio;

            Execute.Assertion
                .ForCondition(deviation <= tolerance)
                .FailWith($"Expected linear scaling between sizes {sizes[i]} and {sizes[i + 1]}, " +
                         $"but time ratio was {actualRatio:F2} instead of expected {expectedRatio:F2} " +
                         $"(deviation: {deviation:P0})");
        }
    }
}