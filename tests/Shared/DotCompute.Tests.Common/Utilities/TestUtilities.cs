// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Extensions;
using DotCompute.Memory;
using DotCompute.SharedTestUtilities.Performance;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Tests.Common.Utilities;

/// <summary>
/// Comprehensive test utilities providing common functionality for DotCompute testing.
/// Includes performance measurement, memory validation, concurrency testing,
/// and mock object creation utilities.
/// </summary>
public static class TestUtilities
{
    /// <summary>
    /// Dictionary to store log entries for mock loggers.
    /// </summary>
    private static readonly ConditionalWeakTable<object, List<LogEntry>> _mockLoggerEntries = new();
    private static readonly ConcurrentDictionary<string, object> _testCache = new();

    #region Performance Testing Utilities

    /// <summary>
    /// Measures execution time and validates performance against target metrics.
    /// </summary>
    /// <param name="action">Action to measure</param>
    /// <param name="targetTimeMs">Target execution time in milliseconds</param>
    /// <param name="iterations">Number of iterations to run</param>
    /// <param name="warmupIterations">Number of warmup iterations</param>
    /// <returns>Performance measurement results</returns>
    public static async Task<PerformanceStats> MeasurePerformanceAsync(
        Func<Task> action,
        double? targetTimeMs = null,
        int iterations = 10,
        int warmupIterations = 3)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Warmup
        for (var i = 0; i < warmupIterations; i++)
        {
            await action();
        }

        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var measurements = new List<double>();
        var stopwatch = new Stopwatch();
        var totalStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            await action();
            stopwatch.Stop();
            measurements.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        totalStopwatch.Stop();

        var result = new PerformanceStats
        {
            Iterations = iterations,
            MeanTimeMs = measurements.Average(),
            MedianTimeMs = CalculateMedian(measurements),
            MinTimeMs = measurements.Min(),
            MaxTimeMs = measurements.Max(),
            StandardDeviationMs = CalculateStandardDeviation(measurements),
            TotalTimeMs = totalStopwatch.Elapsed.TotalMilliseconds,
            TargetTimeMs = targetTimeMs,
            MeetsTarget = targetTimeMs.HasValue ? measurements.Average() <= targetTimeMs.Value : true,
            Percentile95Ms = CalculatePercentile(measurements, 0.95),
            Percentile99Ms = CalculatePercentile(measurements, 0.99)
        };

        return result;
    }

    /// <summary>
    /// Measures synchronous execution performance.
    /// </summary>
    public static PerformanceStats MeasurePerformance(
        Action action,
        double? targetTimeMs = null,
        int iterations = 10,
        int warmupIterations = 3)
    {
        return MeasurePerformanceAsync(() =>
        {
            action();
            return Task.CompletedTask;
        }, targetTimeMs, iterations, warmupIterations).GetAwaiter().GetResult();
    }

    #endregion

    #region Memory Testing Utilities

    /// <summary>
    /// Monitors memory usage during test execution and validates against limits.
    /// </summary>
    public static async Task<MemoryMeasurement> MeasureMemoryUsageAsync(
        Func<Task> action,
        long? maxMemoryBytes = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var initialMemory = GC.GetTotalMemory(true);
        var peakMemory = initialMemory;
        var measurements = new List<long>();

        using var timer = new Timer(_ =>
        {
            var currentMemory = GC.GetTotalMemory(false);
            measurements.Add(currentMemory);
            if (currentMemory > peakMemory)
                peakMemory = currentMemory;
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));

        try
        {
            await action();
        }
        finally
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        var finalMemory = GC.GetTotalMemory(true);

        return new MemoryMeasurement
        {
            InitialMemoryBytes = initialMemory,
            FinalMemoryBytes = finalMemory,
            PeakMemoryBytes = peakMemory,
            AllocatedBytes = Math.Max(0, finalMemory - initialMemory),
            MaxMemoryBytes = maxMemoryBytes,
            MeetsTarget = maxMemoryBytes.HasValue ? peakMemory <= maxMemoryBytes.Value : true,
            AverageMemoryBytes = measurements.Count > 0 ? (long)measurements.Average() : initialMemory,
            Measurements = measurements.ToArray()
        };
    }

    /// <summary>
    /// Validates that no memory leaks occur during test execution.
    /// </summary>
    public static async Task<bool> ValidateNoMemoryLeaksAsync(
        Func<Task> action,
        int iterations = 100,
        double tolerancePercentage = 5.0)
    {
        ArgumentNullException.ThrowIfNull(action);

        var initialMemory = GC.GetTotalMemory(true);

        // Run iterations
        for (var i = 0; i < iterations; i++)
        {
            await action();
            if (i % 10 == 0) // Periodic cleanup
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Final cleanup and measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = ((double)(finalMemory - initialMemory) / initialMemory) * 100;

        return memoryIncrease <= tolerancePercentage;
    }

    #endregion

    #region Concurrency Testing Utilities

    /// <summary>
    /// Tests thread safety by executing an action concurrently from multiple threads.
    /// </summary>
    public static async Task<ConcurrencyTestResult> TestConcurrencyAsync<T>(
        Func<int, Task<T>> action,
        int threadCount = 10,
        int operationsPerThread = 100,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var actualTimeout = timeout ?? TimeSpan.FromMinutes(5);
        var results = new ConcurrentBag<T>();
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = 0;

        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            try
            {
                for (var i = 0; i < operationsPerThread; i++)
                {
                    var result = await action(threadId * operationsPerThread + i);
                    results.Add(result);
                    Interlocked.Increment(ref completedOperations);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks).WaitAsync(actualTimeout);
        stopwatch.Stop();

        return new ConcurrencyTestResult
        {
            ThreadCount = threadCount,
            OperationsPerThread = operationsPerThread,
            TotalOperations = threadCount * operationsPerThread,
            CompletedOperations = completedOperations,
            SuccessfulOperations = results.Count,
            Exceptions = exceptions.ToArray(),
            ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            OperationsPerSecond = completedOperations / stopwatch.Elapsed.TotalSeconds,
            Success = exceptions.IsEmpty && completedOperations == threadCount * operationsPerThread,
            Results = results.Cast<object>().ToArray()
        };
    }

    /// <summary>
    /// Tests for race conditions by repeatedly executing concurrent operations.
    /// </summary>
    public static async Task<bool> DetectRaceConditionsAsync(
        Func<Task> action,
        int iterations = 1000,
        int concurrentTasks = 10)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            try
            {
                var tasks = Enumerable.Range(0, concurrentTasks)
                    .Select(_ => Task.Run(action));

                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                return true; // Race condition detected
            }
        }

        return false; // No race conditions detected
    }

    #endregion

    #region Mock Object Utilities

    /// <summary>
    /// Creates a mock accelerator with specified characteristics.
    /// </summary>
    public static Mock<IAccelerator> CreateMockAccelerator(
        string name,
        string description = "Test Accelerator",
        bool isAvailable = true,
        long totalMemory = 1024L * 1024L * 1024L, // 1GB
        double availableMemoryRatio = 0.8)
    {
        var mock = new Mock<IAccelerator>();

        var acceleratorInfo = new AcceleratorInfo
        {
            Id = "test-" + name.ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal),
            Name = name,
            DeviceType = description,
            Vendor = "Test Vendor",
            DriverVersion = "1.0",
            TotalMemory = totalMemory,
            AvailableMemory = (long)(totalMemory * availableMemoryRatio)
        };

        mock.Setup(a => a.Info).Returns(acceleratorInfo);
        mock.Setup(a => a.DeviceType).Returns(description);

        // The IAccelerator interface doesn't have GetMemoryInfoAsync - memory info is in Info property

        return mock;
    }

    /// <summary>
    /// Creates a mock logger that captures log entries for verification.
    /// </summary>
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mock = new Mock<ILogger<T>>();
        var logEntries = new List<LogEntry>();

        mock.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, eventId, state, exception, formatter) =>
            {
                logEntries.Add(new LogEntry
                {
                    Level = level,
                    EventId = eventId,
                    Message = state?.ToString() ?? string.Empty,
                    Exception = exception,
                    Timestamp = DateTime.UtcNow
                });
            });

        // Return a mock that includes a way to access log entries
        // Note: The logEntries list can be accessed through the GetLogEntries helper method
        _mockLoggerEntries.Add(mock, logEntries);
        return mock;
    }

    /// <summary>
    /// Gets the log entries captured by a mock logger created with CreateMockLogger.
    /// </summary>
    /// <typeparam name="T">The logger type.</typeparam>
    /// <param name="mockLogger">The mock logger to get entries from.</param>
    /// <returns>The list of captured log entries.</returns>
    public static List<LogEntry> GetLogEntries<T>(Mock<ILogger<T>> mockLogger)
    {
        return _mockLoggerEntries.TryGetValue(mockLogger, out var entries) ? entries : new List<LogEntry>();
    }

    /// <summary>
    /// Creates a mock memory buffer for testing.
    /// </summary>
    public static Mock<IUnifiedMemoryBuffer<T>> CreateMockBuffer<T>(int size) where T : unmanaged
    {
        var mock = new Mock<IUnifiedMemoryBuffer<T>>();
        var data = new T[size];

        mock.Setup(b => b.Length).Returns(size);
        mock.Setup(b => b.SizeInBytes).Returns(size * Marshal.SizeOf<T>());
        mock.Setup(b => b.IsDisposed).Returns(false);
        // Note: Cannot directly mock Span<T> returns as they are ref structs
        // Use AsMemory() instead which returns Memory<T> and can be mocked
        mock.Setup(b => b.AsMemory()).Returns(data.AsMemory());
        mock.Setup(b => b.AsReadOnlyMemory()).Returns(data.AsMemory());

        return mock;
    }

    #endregion

    #region Test Data Generation

    /// <summary>
    /// Generates deterministic test data for consistent testing.
    /// </summary>
    public static T[] GenerateTestData<T>(int size, Func<int, T> generator, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(generator);

        var data = new T[size];

        for (var i = 0; i < size; i++)
        {
            data[i] = generator(i);
        }

        return data;
    }

    /// <summary>
    /// Generates float test data with specified characteristics.
    /// </summary>
    public static float[] GenerateFloatTestData(
        int size,
        float min = -100f,
        float max = 100f,
        int seed = 42)
    {
        var random = new Random(seed);
        var data = new float[size];
        var range = max - min;

        for (var i = 0; i < size; i++)
        {
            data[i] = min + (float)random.NextDouble() * range;
        }

        return data;
    }

    /// <summary>
    /// Generates integer test data with specified characteristics.
    /// </summary>
    public static int[] GenerateIntTestData(
        int size,
        int min = -1000,
        int max = 1000,
        int seed = 42)
    {
        var random = new Random(seed);
        var data = new int[size];

        for (var i = 0; i < size; i++)
        {
            data[i] = random.Next(min, max + 1);
        }

        return data;
    }

    #endregion

    #region Cache and State Management

    /// <summary>
    /// Caches test objects for reuse across test methods.
    /// </summary>
    public static T GetOrCreateCached<T>(string key, Func<T> factory) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);

        return (T)_testCache.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// Clears the test cache.
    /// </summary>
    public static void ClearCache()
    {
        _testCache.Clear();
    }

    #endregion

    #region Statistical Utilities

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;

        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            return sorted[count / 2];
        }
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        var mean = values.Average();
        var variance = values.Select(x => Math.Pow(x - mean, 2)).Average();
        return Math.Sqrt(variance);
    }

    private static double CalculatePercentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = percentile * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    #endregion
}


/// <summary>
/// Memory usage measurement results
/// </summary>
public class MemoryMeasurement
{
    public long InitialMemoryBytes { get; set; }
    public long FinalMemoryBytes { get; set; }
    public long PeakMemoryBytes { get; set; }
    public long AllocatedBytes { get; set; }
    public long? MaxMemoryBytes { get; set; }
    public bool MeetsTarget { get; set; }
    public long AverageMemoryBytes { get; set; }
    public long[] Measurements { get; set; } = Array.Empty<long>();
}

/// <summary>
/// Concurrency test results
/// </summary>
public class ConcurrencyTestResult
{
    public int ThreadCount { get; set; }
    public int OperationsPerThread { get; set; }
    public int TotalOperations { get; set; }
    public int CompletedOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public Exception[] Exceptions { get; set; } = Array.Empty<Exception>();
    public double ExecutionTimeMs { get; set; }
    public double OperationsPerSecond { get; set; }
    public bool Success { get; set; }
    public object[] Results { get; set; } = Array.Empty<object>();
}

/// <summary>
/// Log entry for testing
/// </summary>
public class LogEntry
{
    public LogLevel Level { get; set; }
    public EventId EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance measurement statistics for testing
/// </summary>
public class PerformanceStats
{
    public int Iterations { get; set; }
    public double MeanTimeMs { get; set; }
    public double MedianTimeMs { get; set; }
    public double MinTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
    public double StandardDeviationMs { get; set; }
    public double TotalTimeMs { get; set; }
    public double? TargetTimeMs { get; set; }
    public bool MeetsTarget { get; set; }
    public double OperationsPerSecond { get; set; }
    public double Percentile95Ms { get; set; }
    public double Percentile99Ms { get; set; }
}

/// <summary>
/// Extended mock logger interface for testing
/// </summary>
public interface IMockLogger<T> : ILogger<T>
{
    public List<LogEntry> LogEntries { get; }
}

