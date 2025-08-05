// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DotCompute.Abstractions;

namespace DotCompute.SharedTestUtilities;

/// <summary>
/// Monitors memory usage during test execution.
/// </summary>
public sealed class MemoryMonitor : IDisposable
{
    private readonly string _testName;
    private readonly Process _currentProcess;
    private readonly List<MemorySnapshot> _snapshots;
    private readonly long _startTime;
    private bool _disposed;

    public MemoryMonitor(string testName)
    {
        _testName = testName;
        _currentProcess = Process.GetCurrentProcess();
        _snapshots = [];
        _startTime = Stopwatch.GetTimestamp();

        TakeSnapshot("Start");
    }

    public void TakeSnapshot(string label)
    {
        if (_disposed)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        var elapsedMs = (timestamp - _startTime) * 1000 / Stopwatch.Frequency;

        _currentProcess.Refresh();
        var snapshot = new MemorySnapshot
        {
            Label = label,
            ElapsedMs = elapsedMs,
            WorkingSetMB = _currentProcess.WorkingSet64 / (1024 * 1024),
            PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
            GCGen0Collections = GC.CollectionCount(0),
            GCGen1Collections = GC.CollectionCount(1),
            GCGen2Collections = GC.CollectionCount(2),
            TotalAllocatedBytes = GC.GetTotalAllocatedBytes(false)
        };

        _snapshots.Add(snapshot);
    }

    public MemoryReport GenerateReport()
    {
        TakeSnapshot("End");

        return new MemoryReport
        {
            TestName = _testName,
            Snapshots = [.. _snapshots],
            PeakWorkingSetMB = GetPeakWorkingSet(),
            TotalGCCollections = GetTotalGCCollections(),
            MemoryLeakDetected = DetectMemoryLeak()
        };
    }

    private long GetPeakWorkingSet()
    {
        long peak = 0;
        foreach (var snapshot in _snapshots)
        {
            if (snapshot.WorkingSetMB > peak)
            {
                peak = snapshot.WorkingSetMB;
            }
        }
        return peak;
    }

    private int GetTotalGCCollections()
    {
        if (_snapshots.Count < 2)
        {
            return 0;
        }

        var start = _snapshots[0];
        var end = _snapshots[^1];

        return (end.GCGen0Collections - start.GCGen0Collections) +
               (end.GCGen1Collections - start.GCGen1Collections) +
               (end.GCGen2Collections - start.GCGen2Collections);
    }

    private bool DetectMemoryLeak()
    {
        if (_snapshots.Count < 2)
        {
            return false;
        }

        var start = _snapshots[0];
        var end = _snapshots[^1];

        // Consider it a leak if memory increased by more than 10MB and no GC occurred
        var memoryIncrease = end.WorkingSetMB - start.WorkingSetMB;
        var gcOccurred = end.GCGen0Collections > start.GCGen0Collections;

        return memoryIncrease > 10 && !gcOccurred;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _currentProcess?.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Represents a memory snapshot at a point in time.
/// </summary>
public class MemorySnapshot
{
    public string Label { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public int GCGen0Collections { get; set; }
    public int GCGen1Collections { get; set; }
    public int GCGen2Collections { get; set; }
    public long TotalAllocatedBytes { get; set; }
}

/// <summary>
/// Represents a complete memory report for a test.
/// </summary>
public class MemoryReport
{
    public string TestName { get; set; } = string.Empty;
    public MemorySnapshot[] Snapshots { get; set; } = [];
    public long PeakWorkingSetMB { get; set; }
    public int TotalGCCollections { get; set; }
    public bool MemoryLeakDetected { get; set; }
}

/// <summary>
/// Performance benchmark for memory operations.
/// </summary>
public class MemoryPerformanceBenchmark
{
    private readonly List<BenchmarkResult> _results;

    public MemoryPerformanceBenchmark()
    {
        _results = [];
    }

    public async Task<BenchmarkResult> BenchmarkAllocation(IMemoryManager memoryManager, long bufferSize, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        var buffers = new List<IMemoryBuffer>();

        try
        {
            for (var i = 0; i < iterations; i++)
            {
                var buffer = await memoryManager.AllocateAsync(bufferSize);
                buffers.Add(buffer);
            }

            stopwatch.Stop();

            var result = new BenchmarkResult
            {
                OperationType = "Allocation",
                BufferSize = bufferSize,
                Iterations = iterations,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                OperationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0),
                ThroughputMBps = (bufferSize * iterations) / (1024.0 * 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0)
            };

            _results.Add(result);
            return result;
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    public async Task<BenchmarkResult> BenchmarkCopyToHost(IMemoryManager memoryManager, long bufferSize, int iterations)
    {
        var buffer = await memoryManager.AllocateAsync(bufferSize);
        var hostData = new byte[bufferSize];

        try
        {
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
            {
                await buffer.CopyToHostAsync<byte>(hostData);
            }

            stopwatch.Stop();

            var result = new BenchmarkResult
            {
                OperationType = "CopyToHost",
                BufferSize = bufferSize,
                Iterations = iterations,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                OperationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0),
                ThroughputMBps = (bufferSize * iterations) / (1024.0 * 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0)
            };

            _results.Add(result);
            return result;
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    public async Task<BenchmarkResult> BenchmarkCopyFromHost(IMemoryManager memoryManager, long bufferSize, int iterations)
    {
        var buffer = await memoryManager.AllocateAsync(bufferSize);
        var hostData = new byte[bufferSize];

        try
        {
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
            {
                await buffer.CopyFromHostAsync<byte>(hostData);
            }

            stopwatch.Stop();

            var result = new BenchmarkResult
            {
                OperationType = "CopyFromHost",
                BufferSize = bufferSize,
                Iterations = iterations,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                OperationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0),
                ThroughputMBps = (bufferSize * iterations) / (1024.0 * 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0)
            };

            _results.Add(result);
            return result;
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    public BenchmarkSummary GenerateSummary()
    {
        return new BenchmarkSummary
        {
            Results = [.. _results],
            TotalOperations = _results.Count,
            AverageOperationsPerSecond = _results.Count > 0 ? _results.Average(r => r.OperationsPerSecond) : 0,
            AverageThroughputMBps = _results.Count > 0 ? _results.Average(r => r.ThroughputMBps) : 0
        };
    }
}

/// <summary>
/// Represents a single benchmark result.
/// </summary>
public class BenchmarkResult
{
    public string OperationType { get; set; } = string.Empty;
    public long BufferSize { get; set; }
    public int Iterations { get; set; }
    public long TotalTimeMs { get; set; }
    public double OperationsPerSecond { get; set; }
    public double ThroughputMBps { get; set; }
}

/// <summary>
/// Summary of all benchmark results.
/// </summary>
public class BenchmarkSummary
{
    public BenchmarkResult[] Results { get; set; } = [];
    public int TotalOperations { get; set; }
    public double AverageOperationsPerSecond { get; set; }
    public double AverageThroughputMBps { get; set; }
}

/// <summary>
/// Automated test reporting system.
/// </summary>
public static class TestReporter
{
    private static readonly string ReportDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DotCompute", "TestReports");
    
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [RequiresUnreferencedCode("This method uses System.Text.Json serialization which may require dynamic code generation")]
    [RequiresDynamicCode("This method uses System.Text.Json serialization which may require dynamic code generation")]
    public static async Task SaveMemoryReport(MemoryReport report)
    {
        Directory.CreateDirectory(ReportDirectory);

        var fileName = $"memory_report_{report.TestName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(ReportDirectory, fileName);

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    [RequiresUnreferencedCode("This method uses System.Text.Json serialization which may require dynamic code generation")]
    [RequiresDynamicCode("This method uses System.Text.Json serialization which may require dynamic code generation")]
    public static async Task SaveBenchmarkReport(BenchmarkSummary summary, string testName)
    {
        Directory.CreateDirectory(ReportDirectory);

        var fileName = $"benchmark_report_{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(ReportDirectory, fileName);

        var json = JsonSerializer.Serialize(summary, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    [RequiresDynamicCode("This method uses System.Text.Json deserialization which may require dynamic code generation")]
    [RequiresUnreferencedCode("This method uses System.Text.Json deserialization which may require dynamic code generation")]
    public static async Task<MemoryReport[]> LoadHistoricalReports(string testName)
    {
        if (!Directory.Exists(ReportDirectory))
        {
            return [];
        }

        var reports = new List<MemoryReport>();
        var files = Directory.GetFiles(ReportDirectory, $"memory_report_{testName}_*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var report = JsonSerializer.Deserialize<MemoryReport>(json);
                if (report != null)
                {
                    reports.Add(report);
                }
            }
            catch
            {
                // Ignore corrupted files
            }
        }

        return [.. reports];
    }

    public static void CleanupOldReports(int maxDays = 30)
    {
        if (!Directory.Exists(ReportDirectory))
        {
            return;
        }

        var cutoffDate = DateTime.Now.AddDays(-maxDays);
        var files = Directory.GetFiles(ReportDirectory, "*.json");

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    fileInfo.Delete();
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }
}

/// <summary>
/// Performance regression detection utilities.
/// </summary>
public static class RegressionDetector
{
    public static bool DetectPerformanceRegression(BenchmarkResult current, BenchmarkResult baseline, double tolerancePercent = 10.0)
    {
        if (current.OperationType != baseline.OperationType)
        {
            return false;
        }

        var performanceChange = (current.OperationsPerSecond - baseline.OperationsPerSecond) / baseline.OperationsPerSecond * 100;
        return performanceChange < -tolerancePercent;
    }

    public static bool DetectMemoryRegression(MemoryReport current, MemoryReport baseline, double tolerancePercent = 20.0)
    {
        if (current.Snapshots.Length == 0 || baseline.Snapshots.Length == 0)
        {
            return false;
        }

        var currentPeak = current.PeakWorkingSetMB;
        var baselinePeak = baseline.PeakWorkingSetMB;

        var memoryIncrease = (currentPeak - baselinePeak) / (double)baselinePeak * 100;
        return memoryIncrease > tolerancePercent;
    }
}