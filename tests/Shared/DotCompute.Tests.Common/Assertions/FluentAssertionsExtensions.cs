// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Tests.Common.Assertions;

/// <summary>
/// Extension methods for FluentAssertions to support domain-specific assertions
/// for DotCompute testing scenarios including numerical accuracy, performance,
/// memory operations, and computational result validation.
/// </summary>
public static class FluentAssertionsExtensions
{
    /// <summary>
    /// Verifies that two float arrays are approximately equal within specified tolerance.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this IEnumerable<float> subject, IEnumerable<float> expected, 
        float tolerance, string because = "", params object[] becauseArgs)
    {
        subject.Should().NotBeNull();
        expected.Should().NotBeNull();
        
        var subjectArray = subject.ToArray();
        var expectedArray = expected.ToArray();
        
        subjectArray.Should().HaveCount(expectedArray.Length, because, becauseArgs);
        
        for (var i = 0; i < subjectArray.Length; i++)
        {
            subjectArray[i].Should().BeApproximately(expectedArray[i], tolerance, 
                $"element at index {i} should be approximately equal {because}", becauseArgs);
        }
    }

    /// <summary>
    /// Verifies that two double arrays are approximately equal within specified tolerance.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this IEnumerable<double> subject, IEnumerable<double> expected, 
        double tolerance, string because = "", params object[] becauseArgs)
    {
        subject.Should().NotBeNull();
        expected.Should().NotBeNull();
        
        var subjectArray = subject.ToArray();
        var expectedArray = expected.ToArray();
        
        subjectArray.Should().HaveCount(expectedArray.Length, because, becauseArgs);
        
        for (var i = 0; i < subjectArray.Length; i++)
        {
            subjectArray[i].Should().BeApproximately(expectedArray[i], tolerance, 
                $"element at index {i} should be approximately equal {because}", becauseArgs);
        }
    }
    
    /// <summary>
    /// Verifies that a collection contains only finite values (not NaN or Infinity)
    /// </summary>
    public static void ShouldContainOnlyFiniteValues<T>(this IEnumerable<T> subject, string because = "", params object[] becauseArgs)
        where T : IComparable<T>
    {
        subject.Should().NotBeNull();
        
        if (typeof(T) == typeof(float))
        {
            var floats = subject.Cast<float>();
            floats.Should().Match(values => values.All(v => float.IsFinite(v)), because, becauseArgs);
        }
        else if (typeof(T) == typeof(double))
        {
            var doubles = subject.Cast<double>();
            doubles.Should().Match(values => values.All(v => double.IsFinite(v)), because, becauseArgs);
        }
        // For other types, assume they are finite by nature
    }


    /// <summary>
    /// Verifies that a single value is finite (not NaN or Infinity)
    /// </summary>
    public static void ShouldBeFinite(this float subject, string because = "", params object[] becauseArgs) => subject.Should().Match(v => float.IsFinite(v), because, becauseArgs);


    /// <summary>
    /// Verifies that a single value is finite (not NaN or Infinity)
    /// </summary>
    public static void ShouldBeFinite(this double subject, string because = "", params object[] becauseArgs) => subject.Should().Match(v => double.IsFinite(v), because, becauseArgs);


    /// <summary>
    /// Verifies that execution time is within acceptable limits
    /// </summary>
    public static void ShouldBeWithinTimeLimit(this double executionTimeMs, double maxTimeMs, string because = "", params object[] becauseArgs)
    {
        executionTimeMs.Should().BeLessThanOrEqualTo(maxTimeMs, because, becauseArgs);
        executionTimeMs.Should().BeGreaterThan(0, "because execution should take some measurable time");
    }


    /// <summary>
    /// Verifies that a value is greater than a threshold
    /// </summary>
    public static void ShouldBeGreaterThan<T>(this T subject, T threshold, string because = "", params object[] becauseArgs)
        where T : IComparable<T> => subject.Should().BeGreaterThan(threshold, because, becauseArgs);


    /// <summary>
    /// Verifies that a value is less than a threshold
    /// </summary>
    public static void ShouldBeLessThan<T>(this T subject, T threshold, string because = "", params object[] becauseArgs)
        where T : IComparable<T> => subject.Should().BeLessThan(threshold, because, becauseArgs);


    /// <summary>
    /// Verifies that all elements in a collection satisfy a condition
    /// </summary>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> subject, Func<T, bool> predicate, string because = "", params object[] becauseArgs)
    {
        subject.Should().NotBeNull();
        subject.Should().Match(items => items.All(predicate), because, becauseArgs);
    }

    /// <summary>
    /// Verifies that a memory buffer contains valid data patterns
    /// </summary>
    public static void ShouldContainValidData<T>(this IEnumerable<T> subject, string because = "", params object[] becauseArgs)
        where T : unmanaged
    {
        subject.Should().NotBeNull(because, becauseArgs);
        subject.Should().NotBeEmpty(because, becauseArgs);
        
        if (typeof(T) == typeof(float))
        {
            var floats = subject.Cast<float>();
            floats.Should().Match(buffer => buffer.All(value => !float.IsInfinity(value) && !float.IsNaN(value)), because, becauseArgs);
        }
        else if (typeof(T) == typeof(double))
        {
            var doubles = subject.Cast<double>();
            doubles.Should().Match(buffer => buffer.All(value => !double.IsInfinity(value) && !double.IsNaN(value)), because, becauseArgs);
        }
    }

    /// <summary>
    /// Verifies that performance metrics meet expected thresholds
    /// </summary>
    public static void ShouldMeetPerformanceThreshold(this double actualThroughput, double expectedMinThroughput, 
        string unit = "ops/sec", string because = "", params object[] becauseArgs)
    {
        actualThroughput.Should().BeGreaterThanOrEqualTo(expectedMinThroughput, 
            $"because performance should achieve at least {expectedMinThroughput:F2} {unit} {because}", becauseArgs);
        actualThroughput.Should().BeGreaterThan(0, "because throughput should be positive");
    }

    /// <summary>
    /// Verifies that computation results are numerically stable
    /// </summary>
    public static void ShouldBeNumericallyStable<T>(this IEnumerable<T> subject, string because = "", params object[] becauseArgs)
        where T : IComparable<T>
    {
        subject.Should().NotBeNull();
        subject.ShouldContainOnlyFiniteValues(because, becauseArgs);
        
        var array = subject.ToArray();
        array.Should().NotBeEmpty("because stable computation should produce results");
        
        // Additional stability checks could be added here
        // e.g., checking for reasonable value ranges, no sudden spikes, etc.
    }

    /// <summary>
    /// Verifies that memory usage is within acceptable bounds
    /// </summary>
    public static void ShouldBeWithinMemoryBounds(this long actualMemoryBytes, long maxMemoryBytes, 
        string because = "", params object[] becauseArgs)
    {
        actualMemoryBytes.Should().BeLessThanOrEqualTo(maxMemoryBytes, because, becauseArgs);
        actualMemoryBytes.Should().BeGreaterThanOrEqualTo(0, "because memory usage should be non-negative");
    }

    /// <summary>
    /// Verifies that error recovery was successful
    /// </summary>
    public static void ShouldRecoverGracefully<TException>(this Action action, string because = "", params object[] becauseArgs)
        where TException : Exception
    {
        // This would be used to test that operations can recover from specific exceptions
        var recovered = false;
        try
        {
            action();
            recovered = true;
        }
        catch (TException)
        {
            // Expected exception occurred, now check if system can recover
            try
            {
                action(); // Try again
                recovered = true;
            }
            catch
            {
                // Recovery failed - recovered remains false
            }
        }
        
        recovered.Should().BeTrue($"because the system should recover gracefully from {typeof(TException).Name} {because}", becauseArgs);
    }
    
    /// <summary>
    /// Asserts that an action does not exceed a specified memory increase.
    /// </summary>
    public static void ShouldNotExceedMemoryIncrease(this Action action, long maxMemoryIncrease, string because = "", params object[] becauseArgs)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Measure initial memory
        var initialMemory = GC.GetTotalMemory(true);
        
        // Execute the action
        action();
        
        // Force garbage collection after execution
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Measure final memory
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Assert memory increase is within bounds
        memoryIncrease.Should().BeLessThanOrEqualTo(maxMemoryIncrease, because, becauseArgs);
    }
}