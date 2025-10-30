// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common;

/// <summary>
/// Consolidated test utilities extracting common patterns from duplicate test classes.
/// Provides standardized test data generation, performance measurement, and validation utilities.
/// </summary>
public static class ConsolidatedTestUtilities
{
    /// <summary>
    /// Standard test data sizes for comprehensive testing.
    /// </summary>
    public static readonly int[] StandardTestSizes =

    [
        4,          // Single element (4 bytes for float)
        1024,       // Small buffer (1KB)
        4096,       // Medium buffer (4KB) 
        65536,      // Large buffer (64KB)
        1048576     // Very large buffer (1MB)
    ];

    /// <summary>
    /// Standard alignment values for alignment testing.
    /// </summary>
    public static readonly int[] StandardAlignments = [16, 32, 64, 128, 256];

    #region Test Data Generation

    /// <summary>
    /// Generates test data for the specified type and size.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="count">Number of elements.</param>
    /// <param name="seed">Random seed for reproducible results.</param>
    /// <returns>Array of test data.</returns>
    public static T[] GenerateTestData<T>(int count, int seed = 42) where T : unmanaged
    {
        var random = new Random(seed);
        var data = new T[count];

        if (typeof(T) == typeof(float))
        {
            var floatData = data as float[];
            for (var i = 0; i < count; i++)
            {
                floatData![i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
        }
        else if (typeof(T) == typeof(double))
        {
            var doubleData = data as double[];
            for (var i = 0; i < count; i++)
            {
                doubleData![i] = random.NextDouble() * 2.0 - 1.0;
            }
        }
        else if (typeof(T) == typeof(int))
        {
            var intData = data as int[];
            for (var i = 0; i < count; i++)
            {
                intData![i] = random.Next(-1000, 1000);
            }
        }
        else if (typeof(T) == typeof(byte))
        {
            var byteData = data as byte[];
            random.NextBytes(byteData!);
        }
        else
        {
            // For other types, fill with pattern based on index
            for (var i = 0; i < count; i++)
            {
                var bytes = new byte[Unsafe.SizeOf<T>()];
                random.NextBytes(bytes);
                data[i] = Unsafe.ReadUnaligned<T>(ref bytes[0]);
            }
        }

        return data;
    }

    /// <summary>
    /// Creates linear sequential data for testing.
    /// </summary>
    /// <param name="count">Number of elements.</param>
    /// <param name="start">Starting value.</param>
    /// <param name="step">Step between values.</param>
    /// <returns>Sequential data array.</returns>
    public static float[] CreateLinearSequence(int count, float start = 0.0f, float step = 1.0f)
    {
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = start + i * step;
        }
        return data;
    }

    /// <summary>
    /// Creates sinusoidal test data for pattern testing.
    /// </summary>
    /// <param name="count">Number of elements.</param>
    /// <param name="frequency">Frequency of the sine wave.</param>
    /// <param name="amplitude">Amplitude of the sine wave.</param>
    /// <returns>Sinusoidal data array.</returns>
    public static float[] CreateSinusoidalData(int count, double frequency = 0.01, float amplitude = 1.0f)
    {
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = amplitude * (float)Math.Sin(i * frequency);
        }
        return data;
    }

    /// <summary>
    /// Creates constant value array for filling tests.
    /// </summary>
    /// <param name="count">Number of elements.</param>
    /// <param name="value">Constant value to fill with.</param>
    /// <returns>Constant data array.</returns>
    public static float[] CreateConstantData(int count, float value)
    {
        var data = new float[count];
        Array.Fill(data, value);
        return data;
    }

    #endregion

    #region Array Validation

    /// <summary>
    /// Validates that two arrays are equal within the specified tolerance.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="expected">Expected values.</param>
    /// <param name="actual">Actual values.</param>
    /// <param name="tolerance">Tolerance for floating-point comparison.</param>
    /// <param name="maxElementsToCheck">Maximum number of elements to check.</param>
    /// <param name="context">Context for error messages.</param>
    public static void ValidateArraysEqual<T>(
        T[] expected,
        T[] actual,
        double tolerance = 0.0001,
        int maxElementsToCheck = 1000,
        string? context = null)
        where T : unmanaged, IEquatable<T>
    {
        _ = expected.Length.Should().Be(actual.Length,

            $"Array lengths should match{(context != null ? $" - {context}" : "")}");

        var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
        var errorCount = 0;
        const int maxErrorsToReport = 10;

        for (var i = 0; i < elementsToCheck; i++)
        {
            if (typeof(T) == typeof(float))
            {
                var expectedFloat = Unsafe.As<T, float>(ref expected[i]);
                var actualFloat = Unsafe.As<T, float>(ref actual[i]);
                var diff = Math.Abs(expectedFloat - actualFloat);


                if (diff > tolerance)
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Float mismatch at index {i}: expected {expectedFloat}, actual {actualFloat}, diff {diff}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
                }
            }
            else if (typeof(T) == typeof(double))
            {
                var expectedDouble = Unsafe.As<T, double>(ref expected[i]);
                var actualDouble = Unsafe.As<T, double>(ref actual[i]);
                var diff = Math.Abs(expectedDouble - actualDouble);


                if (diff > tolerance)
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Double mismatch at index {i}: expected {expectedDouble}, actual {actualDouble}, diff {diff}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
                }
            }
            else
            {
                if (!expected[i].Equals(actual[i]))
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Value mismatch at index {i}: expected {expected[i]}, actual {actual[i]}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
                }
            }
        }

        if (errorCount > 0)
        {
            var message = $"Found {errorCount} mismatches out of {elementsToCheck} elements checked";
            if (context != null)
            {
                message = $"{context} - {message}";
            }
            throw new InvalidOperationException(message);
        }
    }

    #endregion

    #region Performance Measurement

    /// <summary>
    /// Measures operation performance with memory tracking.
    /// </summary>
    /// <param name="operation">The operation to measure.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="output">Test output helper for logging.</param>
    /// <returns>Performance measurement results.</returns>
    public static (TimeSpan duration, long memoryAllocated) MeasureOperation(
        Action operation,
        string operationName,
        ITestOutputHelper output)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        operation();

        stopwatch.Stop();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryAllocated = finalMemory - initialMemory;

        output.WriteLine($"{operationName}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms, " +
                        $"Memory: {memoryAllocated:N0} bytes");

        return (stopwatch.Elapsed, memoryAllocated);
    }

    /// <summary>
    /// Measures throughput for data processing operations.
    /// </summary>
    /// <param name="operation">The operation to measure.</param>
    /// <param name="dataSize">Size of data processed in bytes.</param>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="output">Test output helper.</param>
    /// <returns>Performance results including throughput.</returns>
    public static (TimeSpan duration, double throughputGBps) MeasureThroughput(
        Action operation,
        long dataSize,
        string operationName,
        ITestOutputHelper output)
    {
        var stopwatch = Stopwatch.StartNew();
        operation();
        stopwatch.Stop();

        var throughputGBps = dataSize / (stopwatch.Elapsed.TotalSeconds * 1024.0 * 1024.0 * 1024.0);


        output.WriteLine($"{operationName}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms, " +
                        $"Throughput: {throughputGBps:F2} GB/s");

        return (stopwatch.Elapsed, throughputGBps);
    }

    #endregion

    #region Test Constants

    /// <summary>
    /// Standard test categories for consistent categorization.
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// The memory allocation.
        /// </summary>
        public const string MemoryAllocation = "MemoryAllocation";
        /// <summary>
        /// The buffer types.
        /// </summary>
        public const string BufferTypes = "BufferTypes";
        /// <summary>
        /// The performance.
        /// </summary>

        public const string Performance = "Performance";
        /// <summary>
        /// The hardware.
        /// </summary>
        public const string Hardware = "Hardware";
        /// <summary>
        /// The unit.
        /// </summary>
        public const string Unit = "Unit";
        /// <summary>
        /// The integration.
        /// </summary>
        public const string Integration = "Integration";
        /// <summary>
        /// The gpu.
        /// </summary>
        public const string Gpu = "Gpu";
        /// <summary>
        /// The cuda.
        /// </summary>
        public const string Cuda = "Cuda";
        /// <summary>
        /// The open c l.
        /// </summary>
        public const string OpenCL = "OpenCL";
    }

    /// <summary>
    /// Standard test timeouts in milliseconds.
    /// </summary>
    public static class TestTimeouts
    {
        /// <summary>
        /// The short.
        /// </summary>
        public const int Short = 5000;      // 5 seconds
        /// <summary>
        /// The medium.
        /// </summary>
        public const int Medium = 30000;    // 30 seconds
        /// <summary>
        /// The long.
        /// </summary>
        public const int Long = 120000;     // 2 minutes
        /// <summary>
        /// The very long.
        /// </summary>
        public const int VeryLong = 300000; // 5 minutes
    }

    /// <summary>
    /// Standard tolerances for floating-point comparisons.
    /// </summary>
    public static class Tolerances
    {
        /// <summary>
        /// The float.
        /// </summary>
        public const float Float = 0.0001f;
        /// <summary>
        /// The double.
        /// </summary>
        public const double Double = 0.000000001;
        /// <summary>
        /// The single precision.
        /// </summary>
        public const float SinglePrecision = 1e-6f;
        /// <summary>
        /// The double precision.
        /// </summary>
        public const double DoublePrecision = 1e-12;
    }

    #endregion
}
