// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Helpers;

/// <summary>
/// Unified test helpers combining CUDA, Metal, and general test functionality.
/// Provides hardware detection, test data generation, performance measurement, and test utilities.
/// </summary>
public static class UnifiedTestHelpers
{
    #region Hardware Detection

    /// <summary>
    /// Gets comprehensive system hardware information.
    /// </summary>
    /// <returns>Hardware information structure.</returns>
    public static SystemHardwareInfo GetSystemHardwareInfo()
    {
        return new SystemHardwareInfo
        {
            Platform = $"{RuntimeInformation.OSDescription} - {RuntimeInformation.OSArchitecture}",
            HasCuda = ConsolidatedTestBase.IsCudaAvailable(),
            HasOpenCL = ConsolidatedTestBase.IsOpenClAvailable(),
            HasMetal = ConsolidatedTestBase.IsMetalAvailable(),
            IsAppleSilicon = ConsolidatedTestBase.IsAppleSilicon(),
            SupportsSIMD = ConsolidatedTestBase.IsSIMDSupported(),
            ProcessorCount = Environment.ProcessorCount,
            TotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };
    }

    /// <summary>
    /// Checks if the system meets minimum hardware requirements.
    /// </summary>
    /// <param name="requireGpu">Whether GPU is required.</param>
    /// <param name="requireCuda">Whether CUDA is required.</param>
    /// <param name="requireMetal">Whether Metal is required.</param>
    /// <param name="minMemoryGB">Minimum memory requirement in GB.</param>
    /// <returns>True if requirements are met, false otherwise.</returns>
    public static bool MeetsHardwareRequirements(
        bool requireGpu = false,
        bool requireCuda = false,
        bool requireMetal = false,
        int minMemoryGB = 0)
    {
        var info = GetSystemHardwareInfo();

        if (requireGpu && !info.HasAnyGpu)
            return false;

        if (requireCuda && !info.HasCuda)
            return false;

        if (requireMetal && !info.HasMetal)
            return false;

        if (minMemoryGB > 0 && info.TotalMemoryMB < minMemoryGB * 1024)
            return false;

        return true;
    }

    #endregion

    #region Test Data Generation

    /// <summary>
    /// Unified test data generator with various patterns and data types.
    /// </summary>
    public static class TestDataGenerator
    {
        /// <summary>
        /// Creates a linear sequence of float values.
        /// </summary>
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
        /// Creates a sinusoidal wave pattern.
        /// </summary>
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
        /// Creates random data with specified distribution.
        /// </summary>
        public static float[] CreateRandomData(int count, int seed = 42, float min = -1.0f, float max = 1.0f)
        {
            var random = new Random(seed);
            var data = new float[count];
            var range = max - min;

            for (var i = 0; i < count; i++)
            {
                data[i] = min + (float)random.NextDouble() * range;
            }
            return data;
        }

        /// <summary>
        /// Creates constant data array.
        /// </summary>
        public static float[] CreateConstantData(int count, float value)
        {
            var data = new float[count];
            Array.Fill(data, value);
            return data;
        }

        /// <summary>
        /// Creates integer sequence.
        /// </summary>
        public static int[] CreateIntegerSequence(int count, int start = 0, int step = 1)
        {
            var data = new int[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = start + i * step;
            }
            return data;
        }

        /// <summary>
        /// Creates random integer data.
        /// </summary>
        public static int[] CreateRandomInts(int count, int minValue = 0, int maxValue = 1000, int seed = 42)
        {
            var random = new Random(seed);
            var data = new int[count];

            for (var i = 0; i < count; i++)
            {
                data[i] = random.Next(minValue, maxValue);
            }
            return data;
        }

        /// <summary>
        /// Creates 2D matrix data for matrix operations.
        /// </summary>
        public static float[] CreateMatrix(int rows, int cols, bool identity = false, int seed = 42)
        {
            var data = new float[rows * cols];
            if (identity && rows == cols)
            {
                for (var i = 0; i < rows; i++)
                {
                    data[i * cols + i] = 1.0f;
                }
            }
            else
            {
                var random = new Random(seed);
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                }
            }
            return data;
        }

        /// <summary>
        /// Creates complex number data (interleaved real/imaginary).
        /// </summary>
        public static float[] CreateComplexData(int count, int seed = 42)
        {
            var random = new Random(seed);
            var data = new float[count * 2]; // Interleaved real/imaginary

            for (var i = 0; i < count; i++)
            {
                data[i * 2] = (float)(random.NextDouble() * 2.0 - 1.0);     // Real
                data[i * 2 + 1] = (float)(random.NextDouble() * 2.0 - 1.0); // Imaginary
            }
            return data;
        }

        /// <summary>
        /// Creates sparse data with specified density.
        /// </summary>
        public static float[] CreateSparseData(int count, float density = 0.1f, int seed = 42)
        {
            var random = new Random(seed);
            var data = new float[count];

            for (var i = 0; i < count; i++)
            {
                if (random.NextDouble() < density)
                {
                    data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                }
                // else remains 0
            }
            return data;
        }
    }

    #endregion

    #region Performance and Validation Utilities

    /// <summary>
    /// Performance measurement utility for various operations.
    /// </summary>
    public static class PerformanceHelpers
    {
        /// <summary>
        /// Measures operation performance with detailed metrics.
        /// </summary>
        public static PerformanceResult MeasureOperation(
            Action operation,
            string operationName,
            int iterations = 1,
            long dataSize = 0)
        {
            // Warm-up run
            if (iterations > 1)
            {
                operation();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var initialMemory = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
            {
                operation();
            }

            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);

            return new PerformanceResult
            {
                OperationName = operationName,
                TotalTime = stopwatch.Elapsed,
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations),
                Iterations = iterations,
                MemoryDelta = finalMemory - initialMemory,
                DataSize = dataSize,
                ThroughputGBps = dataSize > 0 ? CalculateThroughput(dataSize, stopwatch.Elapsed.TotalSeconds) : 0
            };
        }

        /// <summary>
        /// Measures async operation performance.
        /// </summary>
        public static async Task<PerformanceResult> MeasureOperationAsync(
            Func<Task> operation,
            string operationName,
            int iterations = 1,
            long dataSize = 0)
        {
            // Warm-up run
            if (iterations > 1)
            {
                await operation();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var initialMemory = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
            {
                await operation();
            }

            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);

            return new PerformanceResult
            {
                OperationName = operationName,
                TotalTime = stopwatch.Elapsed,
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations),
                Iterations = iterations,
                MemoryDelta = finalMemory - initialMemory,
                DataSize = dataSize,
                ThroughputGBps = dataSize > 0 ? CalculateThroughput(dataSize, stopwatch.Elapsed.TotalSeconds) : 0
            };
        }

        private static double CalculateThroughput(long bytes, double seconds) => seconds > 0 ? (bytes / (1024.0 * 1024.0 * 1024.0)) / seconds : 0;
    }

    /// <summary>
    /// Validation utilities for test results.
    /// </summary>
    public static class ValidationHelpers
    {
        /// <summary>
        /// Verifies that two float arrays match within tolerance.
        /// </summary>
        public static void VerifyFloatArraysMatch(
            float[] expected,
            float[] actual,
            float tolerance = 0.0001f,
            int maxElementsToCheck = 1000,
            string? context = null)
        {
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException(
                    $"Array length mismatch: expected {expected.Length}, actual {actual.Length}");
            }

            var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
            var errorCount = 0;
            const int maxErrorsToReport = 10;

            for (var i = 0; i < elementsToCheck; i++)
            {
                var diff = Math.Abs(expected[i] - actual[i]);
                if (diff > tolerance)
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Mismatch at index {i}: expected {expected[i]}, actual {actual[i]}, diff {diff}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                var message = $"Found {errorCount} mismatches out of {elementsToCheck} elements checked (tolerance: {tolerance})";
                if (context != null)
                {
                    message = $"{context} - {message}";
                }
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Verifies that two integer arrays match exactly.
        /// </summary>
        public static void VerifyIntArraysMatch(
            int[] expected,
            int[] actual,
            int maxElementsToCheck = 1000,
            string? context = null)
        {
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException(
                    $"Array length mismatch: expected {expected.Length}, actual {actual.Length}");
            }

            var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
            var errorCount = 0;
            const int maxErrorsToReport = 10;

            for (var i = 0; i < elementsToCheck; i++)
            {
                if (expected[i] != actual[i])
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Mismatch at index {i}: expected {expected[i]}, actual {actual[i]}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
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

        /// <summary>
        /// Verifies that a value is within expected range.
        /// </summary>
        public static void VerifyValueInRange<T>(T value, T min, T max, string? context = null)

            where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                var message = $"Value {value} is outside expected range [{min}, {max}]";
                if (context != null)
                {
                    message = $"{context} - {message}";
                }
                throw new InvalidOperationException(message);
            }
        }
    }

    #endregion

    #region Kernel Testing Utilities

    /// <summary>
    /// Utilities for testing kernel operations across different backends.
    /// </summary>
    public static class KernelTestHelpers
    {
        /// <summary>
        /// Creates a test kernel definition.
        /// </summary>
        public static KernelDefinition CreateTestKernelDefinition(
            string name,
            string code,
            string language = "CUDA",
            string entryPoint = "")
        {
            return new KernelDefinition
            {
                Name = name,
                Code = code,
                EntryPoint = string.IsNullOrEmpty(entryPoint) ? name : entryPoint
            };
        }

        /// <summary>
        /// Creates kernel arguments with proper dimensions.
        /// </summary>
        public static KernelArguments CreateKernelArguments(
            object[] arguments,
            (int x, int y, int z)? gridDim = null,
            (int x, int y, int z)? blockDim = null)
        {
            var kernelArgs = new KernelArguments();

            foreach (var arg in arguments)
            {
                kernelArgs.Add(arg);
            }

            if (gridDim.HasValue)
            {
                // Set grid dimensions (implementation depends on kernel framework)
            }

            if (blockDim.HasValue)
            {
                // Set block dimensions (implementation depends on kernel framework)
            }

            return kernelArgs;
        }

        /// <summary>
        /// Creates optimal launch configuration based on problem size.
        /// </summary>
        public static (int gridSize, int blockSize) CalculateOptimalLaunchConfig(
            int problemSize,
            int preferredBlockSize = 256,
            int maxBlockSize = 1024)
        {
            var blockSize = Math.Min(preferredBlockSize, maxBlockSize);
            var gridSize = (problemSize + blockSize - 1) / blockSize;

            return (gridSize, blockSize);
        }
    }

    #endregion

    #region Logging Utilities

    /// <summary>
    /// Test logger implementation for xUnit output.
    /// </summary>
    public class TestLogger(ITestOutputHelper output, string categoryName = "Test") : ILogger
    {
        private readonly ITestOutputHelper _output = output;
        private readonly string _categoryName = categoryName;
        /// <summary>
        /// Gets begin scope.
        /// </summary>
        /// <typeparam name="TState">The TState type parameter.</typeparam>
        /// <param name="state">The state.</param>
        /// <returns>The result of the operation.</returns>

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        /// <summary>
        /// Determines whether enabled.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns>true if the condition is met; otherwise, false.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <summary>
        /// Performs log.
        /// </summary>
        /// <typeparam name="TState">The TState type parameter.</typeparam>
        /// <param name="logLevel">The log level.</param>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="state">The state.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="formatter">The formatter.</param>

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }

    /// <summary>
    /// Factory for creating test loggers.
    /// </summary>
    public sealed class TestLoggerFactory(ITestOutputHelper output) : ILoggerFactory
    {
        private readonly ITestOutputHelper _output = output;
        private bool _disposed;
        /// <summary>
        /// Creates a new logger.
        /// </summary>
        /// <param name="categoryName">The category name.</param>
        /// <returns>The created logger.</returns>

        public ILogger CreateLogger(string categoryName) => new TestLogger(_output, categoryName);
        /// <summary>
        /// Performs add provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        public void AddProvider(ILoggerProvider provider) { }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if any
                    // TestLoggerFactory doesn't have any managed resources to dispose
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Compares two performance results for test assertions.
    /// </summary>
    /// <param name="expected">Expected performance result</param>
    /// <param name="actual">Actual performance result</param>
    /// <param name="tolerancePercent">Tolerance percentage for comparison (default 10%)</param>
    public static void ComparePerformanceResults(
        SharedTestUtilities.Performance.PerformanceResult expected,
        SharedTestUtilities.Performance.PerformanceResult actual,
        double tolerancePercent = 10.0)
    {
        // Allow for some variation in performance measurements
        var tolerance = expected.Duration.TotalMilliseconds * (tolerancePercent / 100.0);
        var actualMs = actual.Duration.TotalMilliseconds;
        var expectedMs = expected.Duration.TotalMilliseconds;

        // Performance can be better than expected, so only check if it's too slow
        if (actualMs > expectedMs + tolerance)
        {
            throw new InvalidOperationException(
                $"Performance test failed: actual duration {actualMs:F2}ms exceeded expected {expectedMs:F2}ms " +
                $"by more than {tolerancePercent}% tolerance ({tolerance:F2}ms)");
        }
    }

    /// <summary>
    /// Verifies that two float arrays match within tolerance (delegated to ValidationHelpers).
    /// </summary>
    public static void VerifyFloatArraysMatch(
        float[] expected,
        float[] actual,
        float tolerance = 0.0001f,
        int maxElementsToCheck = 1000,
        string? context = null) => ValidationHelpers.VerifyFloatArraysMatch(expected, actual, tolerance, maxElementsToCheck, context);

    #endregion
}

/// <summary>
/// System hardware information structure.
/// </summary>
public class SystemHardwareInfo
{
    /// <summary>
    /// Gets or sets the platform.
    /// </summary>
    /// <value>The platform.</value>
    public string Platform { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether cuda.
    /// </summary>
    /// <value>The has cuda.</value>
    public bool HasCuda { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether open c l.
    /// </summary>
    /// <value>The has open c l.</value>
    public bool HasOpenCL { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether metal.
    /// </summary>
    /// <value>The has metal.</value>
    public bool HasMetal { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether apple silicon.
    /// </summary>
    /// <value>The is apple silicon.</value>
    public bool IsAppleSilicon { get; set; }
    /// <summary>
    /// Gets or sets the supports s i m d.
    /// </summary>
    /// <value>The supports s i m d.</value>
    public bool SupportsSIMD { get; set; }
    /// <summary>
    /// Gets or sets the processor count.
    /// </summary>
    /// <value>The processor count.</value>
    public int ProcessorCount { get; set; }
    /// <summary>
    /// Gets or sets the total memory m b.
    /// </summary>
    /// <value>The total memory m b.</value>
    public long TotalMemoryMB { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether any gpu.
    /// </summary>
    /// <value>The has any gpu.</value>

    public bool HasAnyGpu => HasCuda || HasOpenCL || HasMetal;
}

/// <summary>
/// Performance measurement result.
/// </summary>
public class PerformanceResult
{
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    /// <value>The operation name.</value>
    public string OperationName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the total time.
    /// </summary>
    /// <value>The total time.</value>
    public TimeSpan TotalTime { get; set; }
    /// <summary>
    /// Gets or sets the average time.
    /// </summary>
    /// <value>The average time.</value>
    public TimeSpan AverageTime { get; set; }
    /// <summary>
    /// Gets or sets the iterations.
    /// </summary>
    /// <value>The iterations.</value>
    public int Iterations { get; set; }
    /// <summary>
    /// Gets or sets the memory delta.
    /// </summary>
    /// <value>The memory delta.</value>
    public long MemoryDelta { get; set; }
    /// <summary>
    /// Gets or sets the data size.
    /// </summary>
    /// <value>The data size.</value>
    public long DataSize { get; set; }
    /// <summary>
    /// Gets or sets the throughput g bps.
    /// </summary>
    /// <value>The throughput g bps.</value>
    public double ThroughputGBps { get; set; }
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()
    {
        var result = $"{OperationName}: {AverageTime.TotalMilliseconds:F2}ms avg";
        if (ThroughputGBps > 0)
        {
            result += $", {ThroughputGBps:F2} GB/s";
        }
        if (MemoryDelta != 0)
        {
            result += $", {MemoryDelta / 1024:F1}KB memory delta";
        }
        return result;
    }
}