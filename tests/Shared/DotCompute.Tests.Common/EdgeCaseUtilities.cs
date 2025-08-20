// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotCompute.Tests.Common;


/// <summary>
/// Utility methods for edge case testing across the DotCompute test suite.
/// Provides common patterns for boundary testing, concurrency validation, and resource management.
/// </summary>
public static class EdgeCaseUtilities
{
    private static readonly char[] Separators = [' ', '\t'];

    #region Boundary Value Generators

    /// <summary>
    /// Gets a set of boundary values for integer testing.
    /// </summary>
    public static IEnumerable<int> IntegerBoundaryValues
    {
        get
        {
            yield return 0;
            yield return 1;
            yield return -1;
            yield return int.MinValue;
            yield return int.MaxValue;
            yield return int.MinValue + 1;
            yield return int.MaxValue - 1;

            // Powers of 2
            for (var i = 0; i < 31; i++)
            {
                var value = 1 << i;
                yield return value;
                yield return value - 1;
                yield return value + 1;
                yield return -value;
            }
        }
    }

    /// <summary>
    /// Gets a set of boundary values for long testing.
    /// </summary>
    public static IEnumerable<long> LongBoundaryValues
    {
        get
        {
            yield return 0L;
            yield return 1L;
            yield return -1L;
            yield return long.MinValue;
            yield return long.MaxValue;
            yield return long.MinValue + 1;
            yield return long.MaxValue - 1;

            // Powers of 2
            for (var i = 0; i < 63; i++)
            {
                var value = 1L << i;
                yield return value;
                yield return value - 1;
                yield return value + 1;
                yield return -value;
            }
        }
    }

    /// <summary>
    /// Gets a set of boundary values for floating-point testing.
    /// </summary>
    public static IEnumerable<float> FloatBoundaryValues
    {
        get
        {
            yield return 0.0f;
            yield return -0.0f;
            yield return 1.0f;
            yield return -1.0f;
            yield return float.MinValue;
            yield return float.MaxValue;
            yield return float.Epsilon;
            yield return -float.Epsilon;
            yield return float.PositiveInfinity;
            yield return float.NegativeInfinity;
            yield return float.NaN;

            // Very small and large values
            yield return 1e-10f;
            yield return -1e-10f;
            yield return 1e10f;
            yield return -1e10f;
            yield return 1e38f;
            yield return -1e38f;
        }
    }

    /// <summary>
    /// Gets problematic string values for testing.
    /// </summary>
    public static IEnumerable<string> ProblematicStrings
    {
        get
        {
            yield return "";
            yield return " ";
            yield return "\t";
            yield return "\r";
            yield return "\n";
            yield return "\r\n";
            yield return "\0";
            yield return "null";
            yield return "NULL";
            yield return "undefined";
            yield return new string('A', 10000); // Very long string
            yield return "🔥💻🚀"; // Emojis
            yield return "Тест"; // Cyrillic
            yield return "テスト"; // Japanese
            yield return "SELECT * FROM users"; // SQL injection attempt
            yield return "<script>alert('test')</script>"; // XSS attempt
            yield return "../../etc/passwd"; // Path traversal attempt
            yield return "\uffff\ufffe"; // Unicode edge cases
        }
    }

    #endregion

    #region Memory Test Patterns

    /// <summary>
    /// Creates test data with specific patterns for memory corruption detection.
    /// </summary>
    public static byte[] CreateMemoryTestPattern(int size, TestPattern pattern = TestPattern.Sequential)
    {
        var data = new byte[size];

        switch (pattern)
        {
            case TestPattern.Sequential:
                for (var i = 0; i < size; i++)
                {
                    data[i] = (byte)(i % 256);
                }
                break;

            case TestPattern.AllZeros:
                // Already initialized to zeros
                break;

            case TestPattern.AllOnes:
                Array.Fill(data, (byte)0xFF);
                break;

            case TestPattern.Alternating:
                for (var i = 0; i < size; i++)
                {
                    data[i] = (byte)(i % 2 == 0 ? 0xAA : 0x55);
                }
                break;

            case TestPattern.Random:
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(data);
                }
                break;

            case TestPattern.Checksum:
                for (var i = 0; i < size; i++)
                {
                    data[i] = (byte)(i ^ i >> 8 ^ i >> 16 ^ i >> 24);
                }
                break;
        }

        return data;
    }

    /// <summary>
    /// Verifies that test data maintains its expected pattern.
    /// </summary>
    public static bool VerifyMemoryTestPattern(ReadOnlySpan<byte> data, TestPattern pattern)
    {
        switch (pattern)
        {
            case TestPattern.Sequential:
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i] != (byte)(i % 256))
                    {
                        return false;
                    }
                }
                break;

            case TestPattern.AllZeros:
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i] != 0)
                    {
                        return false;
                    }
                }
                break;

            case TestPattern.AllOnes:
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i] != 0xFF)
                    {
                        return false;
                    }
                }
                break;

            case TestPattern.Alternating:
                for (var i = 0; i < data.Length; i++)
                {
                    var expected = (byte)(i % 2 == 0 ? 0xAA : 0x55);
                    if (data[i] != expected)
                    {
                        return false;
                    }
                }
                break;

            case TestPattern.Checksum:
                for (var i = 0; i < data.Length; i++)
                {
                    var expected = (byte)(i ^ i >> 8 ^ i >> 16 ^ i >> 24);
                    if (data[i] != expected)
                    {
                        return false;
                    }
                }
                break;

            case TestPattern.Random:
                // Can't verify random pattern
                return true;
        }

        return true;
    }

    #endregion

    #region Concurrency Test Helpers

    /// <summary>
    /// Executes a function concurrently from multiple threads and collects exceptions.
    /// </summary>
    public static async Task<ConcurrencyTestResult> RunConcurrencyTest(
        Func<int, Task> action,
        int threadCount = 10,
        int operationsPerThread = 100,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromMinutes(2);
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = new ConcurrentBag<int>();
        var startTime = DateTime.UtcNow;

        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < operationsPerThread; i++)
                    {
                        await action(threadId * operationsPerThread + i);
                        completedOperations.Add(threadId * operationsPerThread + i);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        var timeoutTask = Task.Delay(actualTimeout);
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
        var endTime = DateTime.UtcNow;

        return new ConcurrencyTestResult
        {
            TimedOut = completedTask == timeoutTask,
            Exceptions = [.. exceptions],
            CompletedOperations = completedOperations.Count,
            ExpectedOperations = threadCount * operationsPerThread,
            Duration = endTime - startTime
        };
    }

    /// <summary>
    /// Executes a function concurrently with different data patterns to detect race conditions.
    /// </summary>
    public static async Task<ConcurrencyTestResult> RunRaceConditionTest<T>(
        Func<T, int, Task> action,
        T[] testData,
        int threadCount = 10,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromMinutes(2);
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = new ConcurrentBag<int>();
        var startTime = DateTime.UtcNow;

        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < testData.Length; i++)
                    {
                        var dataIndex = (threadId * testData.Length + i) % testData.Length;
                        await action(testData[dataIndex], threadId);
                        completedOperations.Add(threadId * testData.Length + i);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        var timeoutTask = Task.Delay(actualTimeout);
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
        var endTime = DateTime.UtcNow;

        return new ConcurrencyTestResult
        {
            TimedOut = completedTask == timeoutTask,
            Exceptions = [.. exceptions],
            CompletedOperations = completedOperations.Count,
            ExpectedOperations = threadCount * testData.Length,
            Duration = endTime - startTime
        };
    }

    #endregion

    #region Resource Management Helpers

    /// <summary>
    /// Measures memory usage before and after an operation.
    /// </summary>
    public static async Task<MemoryUsageResult> MeasureMemoryUsage(Func<Task> operation)
    {
        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);
        var beforeGenCounts = new[]
        {
        GC.CollectionCount(0),
        GC.CollectionCount(1),
        GC.CollectionCount(2)
    };

        var startTime = DateTime.UtcNow;

        await operation();

        var endTime = DateTime.UtcNow;

        // Force GC after measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterMemory = GC.GetTotalMemory(false);
        var afterGenCounts = new[]
        {
        GC.CollectionCount(0),
        GC.CollectionCount(1),
        GC.CollectionCount(2)
    };

        return new MemoryUsageResult
        {
            MemoryBefore = beforeMemory,
            MemoryAfter = afterMemory,
            MemoryDelta = afterMemory - beforeMemory,
            Duration = endTime - startTime,
            GcGen0Collections = afterGenCounts[0] - beforeGenCounts[0],
            GcGen1Collections = afterGenCounts[1] - beforeGenCounts[1],
            GcGen2Collections = afterGenCounts[2] - beforeGenCounts[2]
        };
    }

    /// <summary>
    /// Creates temporary files for testing and ensures cleanup.
    /// </summary>
    public static TempFileCollection CreateTempFiles(int count, long sizeBytes = 1024)
    {
        var files = new List<string>();
        using var rng = RandomNumberGenerator.Create();

        for (var i = 0; i < count; i++)
        {
            var fileName = Path.GetTempFileName();
            files.Add(fileName);

            // Write random data
            var data = new byte[sizeBytes];
            rng.GetBytes(data);
            File.WriteAllBytes(fileName, data);
        }

        return new TempFileCollection(files);
    }

    #endregion

    #region System Resource Detection

    /// <summary>
    /// Gets system resource information for test scaling.
    /// </summary>
    public static SystemResourceInfo GetSystemResources()
    {
        var availableMemory = GetAvailableMemory();
        var processorCount = Environment.ProcessorCount;
        var architecture = RuntimeInformation.ProcessArchitecture;
        var osDescription = RuntimeInformation.OSDescription;

        return new SystemResourceInfo
        {
            ProcessorCount = processorCount,
            AvailableMemoryBytes = availableMemory,
            Architecture = architecture,
            OperatingSystem = osDescription,
            Is64Bit = Environment.Is64BitProcess,
            HasSimdSupport = System.Numerics.Vector.IsHardwareAccelerated
        };
    }

    private static long GetAvailableMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use performance counters or WMI on Windows
                return 8L * 1024 * 1024 * 1024; // Default to 8GB
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Parse /proc/meminfo on Linux
                var memInfo = File.ReadAllText("/proc/meminfo");
                var lines = memInfo.Split('\n');
                var memTotal = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:", StringComparison.Ordinal));
                if (memTotal != null)
                {
                    var parts = memTotal.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        return kb * 1024; // Convert KB to bytes
                    }
                }
            }
        }
        catch
        {
            // Ignore errors and use default
        }

        return 4L * 1024 * 1024 * 1024; // Default to 4GB
    }

    #endregion
}

/// <summary>
/// Test patterns for memory operations.
/// </summary>
public enum TestPattern
{
    Sequential,
    AllZeros,
    AllOnes,
    Alternating,
    Random,
    Checksum
}

/// <summary>
/// Result of a concurrency test.
/// </summary>
public record ConcurrencyTestResult
{
    public bool TimedOut { get; init; }
    public Exception[] Exceptions { get; init; } = Array.Empty<Exception>();
    public int CompletedOperations { get; init; }
    public int ExpectedOperations { get; init; }
    public TimeSpan Duration { get; init; }

    public bool IsSuccessful => !TimedOut && Exceptions.Length == 0 &&
                                CompletedOperations == ExpectedOperations;
}

/// <summary>
/// Result of memory usage measurement.
/// </summary>
public record MemoryUsageResult
{
    public long MemoryBefore { get; init; }
    public long MemoryAfter { get; init; }
    public long MemoryDelta { get; init; }
    public TimeSpan Duration { get; init; }
    public int GcGen0Collections { get; init; }
    public int GcGen1Collections { get; init; }
    public int GcGen2Collections { get; init; }

    public bool HasSignificantLeak => MemoryDelta > 10 * 1024 * 1024; // > 10MB
}

/// <summary>
/// System resource information.
/// </summary>
public record SystemResourceInfo
{
    public int ProcessorCount { get; init; }
    public long AvailableMemoryBytes { get; init; }
    public Architecture Architecture { get; init; }
    public string OperatingSystem { get; init; } = "";
    public bool Is64Bit { get; init; }
    public bool HasSimdSupport { get; init; }
}

/// <summary>
/// Manages temporary files and ensures cleanup.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class TempFileCollection : IDisposable
#pragma warning restore CA1711
{
    private readonly List<string> _files;
    private bool _disposed;

    internal TempFileCollection(List<string> files)
    {
        _files = files;
    }

    public IReadOnlyList<string> Files => _files;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var file in _files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
