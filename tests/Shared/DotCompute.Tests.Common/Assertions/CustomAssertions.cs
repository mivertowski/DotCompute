using AssertionException = Xunit.Sdk.XunitException;

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
        if (Math.Abs(actual - expected) > tolerance)
        {
            throw new AssertionException($"Expected {actual} to be approximately {expected} ± {tolerance}{(string.IsNullOrEmpty(because) ? "" : " because " + because)}, but the difference was {Math.Abs(actual - expected)}.");
        }
    }


    /// <summary>
    /// Asserts that two double-precision values are approximately equal within a specified tolerance.
    /// </summary>
    public static void ShouldBeApproximately(this double actual, double expected, double tolerance = 1e-12, string because = "")
    {
        if (Math.Abs(actual - expected) > tolerance)
        {
            throw new AssertionException($"Expected {actual} to be approximately {expected} ± {tolerance}{(string.IsNullOrEmpty(because) ? "" : " because " + because)}, but the difference was {Math.Abs(actual - expected)}.");
        }
    }


    /// <summary>
    /// Asserts that a floating-point value is within a relative tolerance of the expected value.
    /// </summary>
    public static void ShouldBeApproximatelyRelative(this float actual, float expected, float relativeTolerance = 1e-6f, string because = "")
    {
        var tolerance = Math.Abs(expected) * relativeTolerance;
        if (Math.Abs(actual - expected) > tolerance)
        {
            throw new AssertionException($"Expected {actual} to be within {relativeTolerance * 100}% of {expected}{(string.IsNullOrEmpty(because) ? "" : " because " + because)}, but the difference was {Math.Abs(actual - expected) / Math.Abs(expected) * 100:F2}%.");
        }
    }

    #endregion

    #region Array Assertions


    /// <summary>
    /// Asserts that two float arrays are approximately equal element-wise.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this float[] actual, float[] expected, float tolerance = 1e-6f, string because = "")
    {
        _ = actual.Should().NotBeNull();
        _ = expected.Should().NotBeNull();
        _ = actual.Should().HaveCount(expected.Length);


        for (var i = 0; i < actual.Length; i++)
        {
            if (Math.Abs(actual[i] - expected[i]) > tolerance)
            {
                throw new AssertionException($"Arrays differ at index {i}: expected {expected[i]} but was {actual[i]} (tolerance: {tolerance}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
            }
        }
    }


    /// <summary>
    /// Asserts that two double arrays are approximately equal element-wise.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this double[] actual, double[] expected, double tolerance = 1e-12, string because = "")
    {
        _ = actual.Should().NotBeNull();
        _ = expected.Should().NotBeNull();
        _ = actual.Should().HaveCount(expected.Length);


        for (var i = 0; i < actual.Length; i++)
        {
            if (Math.Abs(actual[i] - expected[i]) > tolerance)
            {
                throw new AssertionException($"Arrays differ at index {i}: expected {expected[i]} but was {actual[i]} (tolerance: {tolerance}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
            }
        }
    }


    /// <summary>
    /// Asserts that an array contains only finite values (no NaN or Infinity).
    /// </summary>
    public static void ShouldContainOnlyFiniteValues(this float[] array, string because = "")
    {
        _ = array.Should().NotBeNull();


        for (var i = 0; i < array.Length; i++)
        {
            if (!float.IsFinite(array[i]))
            {
                throw new InvalidOperationException($"Array contains non-finite values: {array[i]} at index {i}{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
            }
        }
    }


    /// <summary>
    /// Asserts that an array contains only finite values (no NaN or Infinity).
    /// </summary>
    public static void ShouldContainOnlyFiniteValues(this double[] array, string because = "")
    {
        _ = array.Should().NotBeNull();


        for (var i = 0; i < array.Length; i++)
        {
            if (!double.IsFinite(array[i]))
            {
                throw new InvalidOperationException($"Array contains non-finite values: {array[i]} at index {i}{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
            }
        }
    }

    #endregion

    #region Matrix Assertions


    /// <summary>
    /// Asserts that two matrices are approximately equal.
    /// </summary>
    public static void ShouldBeApproximatelyEqualTo(this float[] actual, float[] expected, int rows, int cols, float tolerance = 1e-6f, string because = "")
    {
        _ = actual.Should().NotBeNull();
        _ = expected.Should().NotBeNull();


        var expectedLength = rows * cols;
        _ = actual.Should().HaveCount(expectedLength);
        _ = expected.Should().HaveCount(expectedLength);


        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var index = row * cols + col;
                if (Math.Abs(actual[index] - expected[index]) > tolerance)
                {
                    throw new AssertionException($"Matrices differ at [{row},{col}]: expected {expected[index]} but was {actual[index]} (tolerance: {tolerance}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
                }
            }
        }
    }

    #endregion

    #region Performance Assertions


    /// <summary>
    /// Asserts that a measured time is within an expected limit.
    /// </summary>
    public static void ShouldBeWithinTimeLimit(this double actualTimeMs, double maxTimeMs, string because = "")
    {
        if (actualTimeMs > maxTimeMs)
        {
            throw new AssertionException($"Operation took {actualTimeMs:F2}ms but was expected to complete within {maxTimeMs:F2}ms{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }


        if (actualTimeMs < 0)
        {
            throw new AssertionException($"Operation time cannot be negative ({actualTimeMs:F2}ms)");
        }
    }


    /// <summary>
    /// Asserts that memory usage is within expected bounds.
    /// </summary>
    public static void ShouldUseMemoryWithinBounds(this long actualBytes, long maxBytes, string because = "")
    {
        if (actualBytes > maxBytes)
        {
            throw new AssertionException($"Memory usage was {actualBytes / (1024.0 * 1024.0):F2}MB but was expected to be at most {maxBytes / (1024.0 * 1024.0):F2}MB{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }


        if (actualBytes < 0)
        {
            throw new AssertionException($"Memory usage cannot be negative ({actualBytes} bytes)");
        }
    }

    #endregion

    #region Memory Buffer Assertions


    /// <summary>
    /// Asserts that a memory buffer is properly aligned.
    /// </summary>
    public static void ShouldBeAligned(this IntPtr pointer, int alignment, string because = "")
    {
        var address = pointer.ToInt64();
        if (address % alignment != 0)
        {
            throw new AssertionException($"Pointer 0x{address:X} is not aligned to {alignment} bytes{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }


    /// <summary>
    /// Asserts that a buffer size meets minimum requirements.
    /// </summary>
    public static void ShouldHaveMinimumSize(this long actualSize, long minimumSize, string because = "")
    {
        if (actualSize < minimumSize)
        {
            throw new AssertionException($"Buffer size {actualSize} bytes is less than minimum required {minimumSize} bytes{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }

    #endregion

    #region Numerical Range Assertions


    /// <summary>
    /// Asserts that all values in an array are within specified bounds.
    /// </summary>
    public static void ShouldBeWithinBounds(this float[] array, float min, float max, string because = "")
    {
        _ = array.Should().NotBeNull();


        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] < min || array[i] > max)
            {
                throw new AssertionException($"Value {array[i]} at index {i} is outside bounds [{min}, {max}]{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
            }
        }
    }


    /// <summary>
    /// Asserts that a value is normalized (between 0 and 1).
    /// </summary>
    public static void ShouldBeNormalized(this float value, string because = "")
    {
        if (value is < 0.0f or > 1.0f)
        {
            throw new AssertionException($"Value {value} is not normalized (should be in [0, 1]){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }


    /// <summary>
    /// Asserts that a value is normalized (between 0 and 1).
    /// </summary>
    public static void ShouldBeNormalized(this double value, string because = "")
    {
        if (value is < 0.0 or > 1.0)
        {
            throw new AssertionException($"Value {value} is not normalized (should be in [0, 1]){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }

    #endregion

    #region Statistical Assertions


    /// <summary>
    /// Asserts that the mean of an array is approximately equal to an expected value.
    /// </summary>
    public static void ShouldHaveMeanApproximately(this float[] array, float expectedMean, float tolerance = 1e-6f, string because = "")
    {
        _ = array.Should().NotBeNull();
        _ = array.Should().NotBeEmpty();


        var actualMean = array.Average();
        if (Math.Abs(actualMean - expectedMean) > tolerance)
        {
            throw new AssertionException($"Array mean {actualMean} differs from expected {expectedMean} by {Math.Abs(actualMean - expectedMean)} (tolerance: {tolerance}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }


    /// <summary>
    /// Asserts that the standard deviation of an array is approximately equal to an expected value.
    /// </summary>
    public static void ShouldHaveStandardDeviationApproximately(this float[] array, float expectedStdDev, float tolerance = 1e-6f, string because = "")
    {
        _ = array.Should().NotBeNull();
        _ = array.Should().NotBeEmpty();


        var mean = array.Average();
        var variance = array.Select(x => Math.Pow(x - mean, 2)).Average();
        var actualStdDev = (float)Math.Sqrt(variance);


        if (Math.Abs(actualStdDev - expectedStdDev) > tolerance)
        {
            throw new AssertionException($"Array standard deviation {actualStdDev} differs from expected {expectedStdDev} by {Math.Abs(actualStdDev - expectedStdDev)} (tolerance: {tolerance}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }

    #endregion

    #region Sparse Data Assertions


    /// <summary>
    /// Asserts that an array has a specific sparsity ratio (percentage of zero values).
    /// </summary>
    public static void ShouldHaveSparsityRatio(this float[] array, float expectedRatio, float tolerance = 0.01f, string because = "")
    {
        _ = array.Should().NotBeNull();
        _ = array.Should().NotBeEmpty();


        var zeroCount = array.Count(x => Math.Abs(x) < float.Epsilon);
        var actualRatio = (float)zeroCount / array.Length;


        if (Math.Abs(actualRatio - expectedRatio) > tolerance)
        {
            throw new AssertionException($"Array sparsity ratio {actualRatio:P2} differs from expected {expectedRatio:P2} (tolerance: {tolerance:P2}){(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }

    #endregion

    #region GPU-Specific Assertions


    /// <summary>
    /// Asserts that a kernel execution time is within expected bounds for GPU operations.
    /// </summary>
    public static void ShouldHaveReasonableGpuPerformance(this double kernelTimeMs, int elementCount, double minThroughputGBps = 10.0, string because = "")
    {
        if (kernelTimeMs <= 0)
        {
            throw new AssertionException($"Kernel time must be positive (was {kernelTimeMs}ms)");
        }

        // Calculate achieved throughput (assuming float32 read + write)

        var bytesProcessed = elementCount * sizeof(float) * 2; // Read + Write
        var throughputGBps = (bytesProcessed / 1e9) / (kernelTimeMs / 1000.0);


        if (throughputGBps < minThroughputGBps)
        {
            throw new AssertionException($"GPU throughput {throughputGBps:F2} GB/s is below minimum expected {minThroughputGBps:F2} GB/s{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }


    /// <summary>
    /// Asserts that memory is coalesced-friendly for GPU access.
    /// </summary>
    public static void ShouldBeCoalescedFriendly(this int stride, int warpSize = 32, string because = "")
    {
        if (stride % warpSize != 0)
        {
            throw new AssertionException($"Stride {stride} is not aligned to warp size {warpSize}, which may cause uncoalesced memory access{(string.IsNullOrEmpty(because) ? "" : " because " + because)}");
        }
    }


    #endregion
}
