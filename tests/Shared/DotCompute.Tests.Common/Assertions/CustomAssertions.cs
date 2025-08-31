using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace DotCompute.Tests.Common.Assertions;

/// <summary>
/// Provides specialized assertions for numerical computations and array operations.
/// Extends FluentAssertions with domain-specific assertion methods.
/// </summary>
public static class CustomAssertions
{
    #region Floating Point Assertions
    
    /// <summary>
    /// Asserts that two floating-point values are approximately equal within a specified tolerance.
    /// </summary>
    public static void ShouldBeApproximately(this float actual, float expected, float tolerance = 1e-6f, string because = "")
    {
        Execute.Assertion
            .ForCondition(Math.Abs(actual - expected) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected {0} to be approximately {1} ± {2}{reason}, but the difference was {3}.",
                actual, expected, tolerance, Math.Abs(actual - expected));
    }
    
    /// <summary>
    /// Asserts that two double-precision values are approximately equal within a specified tolerance.
    /// </summary>
    public static void ShouldBeApproximately(this double actual, double expected, double tolerance = 1e-12, string because = "")
    {
        Execute.Assertion
            .ForCondition(Math.Abs(actual - expected) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected {0} to be approximately {1} ± {2}{reason}, but the difference was {3}.",
                actual, expected, tolerance, Math.Abs(actual - expected));
    }
    
    /// <summary>
    /// Asserts that a floating-point value is within a relative tolerance of the expected value.
    /// </summary>
    public static void ShouldBeApproximatelyRelative(this float actual, float expected, float relativeTolerance = 1e-6f, string because = "")
    {
        var tolerance = Math.Abs(expected) * relativeTolerance;
        Execute.Assertion
            .ForCondition(Math.Abs(actual - expected) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected {0} to be approximately {1} within {2}% relative tolerance{reason}, but the difference was {3}.",
                actual, expected, relativeTolerance * 100, Math.Abs(actual - expected));
    }
    
    /// <summary>
    /// Asserts that a double-precision value is within a relative tolerance of the expected value.
    /// </summary>
    public static void ShouldBeApproximatelyRelative(this double actual, double expected, double relativeTolerance = 1e-12, string because = "")
    {
        var tolerance = Math.Abs(expected) * relativeTolerance;
        Execute.Assertion
            .ForCondition(Math.Abs(actual - expected) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected {0} to be approximately {1} within {2}% relative tolerance{reason}, but the difference was {3}.",
                actual, expected, relativeTolerance * 100, Math.Abs(actual - expected));
    }
    
    #endregion
    
    #region Array Assertions
    
    /// <summary>
    /// Asserts that two arrays are element-wise approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this float[] actual, float[] expected, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        actual.Should().HaveSameCount(expected, because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(Math.Abs(actual[i] - expected[i]) <= tolerance)
                .BecauseOf(because)
                .FailWith("Expected array element at index {0} to be approximately {1} ± {2}{reason}, but found {3} (difference: {4}).",
                    i, expected[i], tolerance, actual[i], Math.Abs(actual[i] - expected[i]));
        }
    }
    
    /// <summary>
    /// Asserts that two arrays are element-wise approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this double[] actual, double[] expected, double tolerance = 1e-12, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        actual.Should().HaveSameCount(expected, because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(Math.Abs(actual[i] - expected[i]) <= tolerance)
                .BecauseOf(because)
                .FailWith("Expected array element at index {0} to be approximately {1} ± {2}{reason}, but found {3} (difference: {4}).",
                    i, expected[i], tolerance, actual[i], Math.Abs(actual[i] - expected[i]));
        }
    }
    
    /// <summary>
    /// Asserts that an array contains only finite values (no NaN or infinity).
    /// </summary>
    public static void ShouldContainOnlyFiniteValues(this float[] actual, string because = "")
    {
        actual.Should().NotBeNull(because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(float.IsFinite(actual[i]))
                .BecauseOf(because)
                .FailWith("Expected array to contain only finite values{reason}, but found {0} at index {1}.",
                    actual[i], i);
        }
    }
    
    /// <summary>
    /// Asserts that an array contains only finite values (no NaN or infinity).
    /// </summary>
    public static void ShouldContainOnlyFiniteValues(this double[] actual, string because = "")
    {
        actual.Should().NotBeNull(because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(double.IsFinite(actual[i]))
                .BecauseOf(because)
                .FailWith("Expected array to contain only finite values{reason}, but found {0} at index {1}.",
                    actual[i], i);
        }
    }
    
    /// <summary>
    /// Asserts that an array is sorted in ascending order.
    /// </summary>
    public static void ShouldBeSortedAscending<T>(this T[] actual, string because = "") where T : IComparable<T>
    {
        actual.Should().NotBeNull(because);
        
        for (int i = 1; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(actual[i - 1].CompareTo(actual[i]) <= 0)
                .BecauseOf(because)
                .FailWith("Expected array to be sorted in ascending order{reason}, but found {0} > {1} at indices {2} and {3}.",
                    actual[i - 1], actual[i], i - 1, i);
        }
    }
    
    /// <summary>
    /// Asserts that an array is sorted in descending order.
    /// </summary>
    public static void ShouldBeSortedDescending<T>(this T[] actual, string because = "") where T : IComparable<T>
    {
        actual.Should().NotBeNull(because);
        
        for (int i = 1; i < actual.Length; i++)
        {
            Execute.Assertion
                .ForCondition(actual[i - 1].CompareTo(actual[i]) >= 0)
                .BecauseOf(because)
                .FailWith("Expected array to be sorted in descending order{reason}, but found {0} < {1} at indices {2} and {3}.",
                    actual[i - 1], actual[i], i - 1, i);
        }
    }
    
    #endregion
    
    #region Matrix Assertions
    
    /// <summary>
    /// Asserts that two matrices are element-wise approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this float[,] actual, float[,] expected, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        
        var actualRows = actual.GetLength(0);
        var actualCols = actual.GetLength(1);
        var expectedRows = expected.GetLength(0);
        var expectedCols = expected.GetLength(1);
        
        Execute.Assertion
            .ForCondition(actualRows == expectedRows && actualCols == expectedCols)
            .BecauseOf(because)
            .FailWith("Expected matrix dimensions to be [{0}, {1}]{reason}, but found [{2}, {3}].",
                expectedRows, expectedCols, actualRows, actualCols);
        
        for (int i = 0; i < actualRows; i++)
        {
            for (int j = 0; j < actualCols; j++)
            {
                Execute.Assertion
                    .ForCondition(Math.Abs(actual[i, j] - expected[i, j]) <= tolerance)
                    .BecauseOf(because)
                    .FailWith("Expected matrix element at [{0}, {1}] to be approximately {2} ± {3}{reason}, but found {4} (difference: {5}).",
                        i, j, expected[i, j], tolerance, actual[i, j], Math.Abs(actual[i, j] - expected[i, j]));
            }
        }
    }
    
    /// <summary>
    /// Asserts that a matrix is symmetric.
    /// </summary>
    public static void ShouldBeSymmetric(this float[,] actual, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        
        var rows = actual.GetLength(0);
        var cols = actual.GetLength(1);
        
        Execute.Assertion
            .ForCondition(rows == cols)
            .BecauseOf(because)
            .FailWith("Expected matrix to be square (symmetric matrices must be square){reason}, but found dimensions [{0}, {1}].",
                rows, cols);
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                Execute.Assertion
                    .ForCondition(Math.Abs(actual[i, j] - actual[j, i]) <= tolerance)
                    .BecauseOf(because)
                    .FailWith("Expected matrix to be symmetric{reason}, but element [{0}, {1}] = {2} != [{3}, {4}] = {5}.",
                        i, j, actual[i, j], j, i, actual[j, i]);
            }
        }
    }
    
    /// <summary>
    /// Asserts that a matrix is an identity matrix.
    /// </summary>
    public static void ShouldBeIdentityMatrix(this float[,] actual, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        
        var rows = actual.GetLength(0);
        var cols = actual.GetLength(1);
        
        Execute.Assertion
            .ForCondition(rows == cols)
            .BecauseOf(because)
            .FailWith("Expected matrix to be square (identity matrices must be square){reason}, but found dimensions [{0}, {1}].",
                rows, cols);
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                var expectedValue = i == j ? 1.0f : 0.0f;
                Execute.Assertion
                    .ForCondition(Math.Abs(actual[i, j] - expectedValue) <= tolerance)
                    .BecauseOf(because)
                    .FailWith("Expected matrix to be identity{reason}, but element [{0}, {1}] = {2} (expected {3}).",
                        i, j, actual[i, j], expectedValue);
            }
        }
    }
    
    #endregion
    
    #region Vector Assertions
    
    /// <summary>
    /// Asserts that two Vector2 arrays are approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this Vector2[] actual, Vector2[] expected, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        actual.Should().HaveSameCount(expected, because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            var distance = Vector2.Distance(actual[i], expected[i]);
            Execute.Assertion
                .ForCondition(distance <= tolerance)
                .BecauseOf(because)
                .FailWith("Expected Vector2 at index {0} to be approximately {1} ± {2}{reason}, but found {3} (distance: {4}).",
                    i, expected[i], tolerance, actual[i], distance);
        }
    }
    
    /// <summary>
    /// Asserts that two Vector3 arrays are approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this Vector3[] actual, Vector3[] expected, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        actual.Should().HaveSameCount(expected, because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            var distance = Vector3.Distance(actual[i], expected[i]);
            Execute.Assertion
                .ForCondition(distance <= tolerance)
                .BecauseOf(because)
                .FailWith("Expected Vector3 at index {0} to be approximately {1} ± {2}{reason}, but found {3} (distance: {4}).",
                    i, expected[i], tolerance, actual[i], distance);
        }
    }
    
    /// <summary>
    /// Asserts that two Vector4 arrays are approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this Vector4[] actual, Vector4[] expected, float tolerance = 1e-6f, string because = "")
    {
        actual.Should().NotBeNull(because);
        expected.Should().NotBeNull(because);
        actual.Should().HaveSameCount(expected, because);
        
        for (int i = 0; i < actual.Length; i++)
        {
            var distance = Vector4.Distance(actual[i], expected[i]);
            Execute.Assertion
                .ForCondition(distance <= tolerance)
                .BecauseOf(because)
                .FailWith("Expected Vector4 at index {0} to be approximately {1} ± {2}{reason}, but found {3} (distance: {4}).",
                    i, expected[i], tolerance, actual[i], distance);
        }
    }
    
    /// <summary>
    /// Asserts that a vector has approximately unit length.
    /// </summary>
    public static void ShouldBeNormalized(this Vector2 actual, float tolerance = 1e-6f, string because = "")
    {
        var length = actual.Length();
        Execute.Assertion
            .ForCondition(Math.Abs(length - 1.0f) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected Vector2 to be normalized (length ≈ 1){reason}, but length was {0}.",
                length);
    }
    
    /// <summary>
    /// Asserts that a vector has approximately unit length.
    /// </summary>
    public static void ShouldBeNormalized(this Vector3 actual, float tolerance = 1e-6f, string because = "")
    {
        var length = actual.Length();
        Execute.Assertion
            .ForCondition(Math.Abs(length - 1.0f) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected Vector3 to be normalized (length ≈ 1){reason}, but length was {0}.",
                length);
    }
    
    /// <summary>
    /// Asserts that a vector has approximately unit length.
    /// </summary>
    public static void ShouldBeNormalized(this Vector4 actual, float tolerance = 1e-6f, string because = "")
    {
        var length = actual.Length();
        Execute.Assertion
            .ForCondition(Math.Abs(length - 1.0f) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected Vector4 to be normalized (length ≈ 1){reason}, but length was {0}.",
                length);
    }
    
    #endregion
    
    #region Performance Assertions
    
    /// <summary>
    /// Asserts that an operation completes within a specified time limit.
    /// </summary>
    public static void ShouldCompleteWithin(this Action action, TimeSpan timeLimit, string because = "")
    {
        action.Should().NotBeNull(because);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        
        Execute.Assertion
            .ForCondition(stopwatch.Elapsed <= timeLimit)
            .BecauseOf(because)
            .FailWith("Expected operation to complete within {0}{reason}, but it took {1}.",
                timeLimit, stopwatch.Elapsed);
    }
    
    /// <summary>
    /// Asserts that an async operation completes within a specified time limit.
    /// </summary>
    public static async System.Threading.Tasks.Task ShouldCompleteWithin(this System.Threading.Tasks.Task task, TimeSpan timeLimit, string because = "")
    {
        task.Should().NotBeNull(because);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await task;
        stopwatch.Stop();
        
        Execute.Assertion
            .ForCondition(stopwatch.Elapsed <= timeLimit)
            .BecauseOf(because)
            .FailWith("Expected operation to complete within {0}{reason}, but it took {1}.",
                timeLimit, stopwatch.Elapsed);
    }
    
    #endregion
    
    #region Memory Assertions
    
    /// <summary>
    /// Asserts that the memory usage increase is within acceptable limits.
    /// </summary>
    public static void ShouldNotExceedMemoryIncrease(this Action action, long maxMemoryIncrease, string because = "")
    {
        action.Should().NotBeNull(because);
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        action();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        Execute.Assertion
            .ForCondition(memoryIncrease <= maxMemoryIncrease)
            .BecauseOf(because)
            .FailWith("Expected memory increase to be at most {0} bytes{reason}, but it was {1} bytes.",
                maxMemoryIncrease, memoryIncrease);
    }
    
    #endregion
    
    #region Statistical Assertions
    
    /// <summary>
    /// Asserts that the mean of a dataset is approximately equal to an expected value.
    /// </summary>
    public static void ShouldHaveMean(this IEnumerable<double> values, double expectedMean, double tolerance = 1e-12, string because = "")
    {
        var valueList = values.ToList();
        valueList.Should().NotBeEmpty(because);
        
        var actualMean = valueList.Average();
        Execute.Assertion
            .ForCondition(Math.Abs(actualMean - expectedMean) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected mean to be approximately {0} ± {1}{reason}, but found {2}.",
                expectedMean, tolerance, actualMean);
    }
    
    /// <summary>
    /// Asserts that the standard deviation of a dataset is approximately equal to an expected value.
    /// </summary>
    public static void ShouldHaveStandardDeviation(this IEnumerable<double> values, double expectedStdDev, double tolerance = 1e-12, string because = "")
    {
        var valueList = values.ToList();
        valueList.Should().NotBeEmpty(because);
        
        var mean = valueList.Average();
        var variance = valueList.Average(x => Math.Pow(x - mean, 2));
        var actualStdDev = Math.Sqrt(variance);
        
        Execute.Assertion
            .ForCondition(Math.Abs(actualStdDev - expectedStdDev) <= tolerance)
            .BecauseOf(because)
            .FailWith("Expected standard deviation to be approximately {0} ± {1}{reason}, but found {2}.",
                expectedStdDev, tolerance, actualStdDev);
    }
    
    #endregion
}