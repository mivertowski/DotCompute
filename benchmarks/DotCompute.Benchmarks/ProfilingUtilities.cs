using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotCompute.Benchmarks;

public static class ProfilingUtilities
{
    /// <summary>
    /// Measures the execution time of an action with high precision.
    /// </summary>
    public static TimeSpan MeasureTime(Action action)
    {
        // Warm up
        action();
        
        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        
        return sw.Elapsed;
    }

    /// <summary>
    /// Measures the execution time of an async action.
    /// </summary>
    public static async Task<TimeSpan> MeasureTimeAsync(Func<Task> action)
    {
        // Warm up
        await action();
        
        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        
        return sw.Elapsed;
    }

    /// <summary>
    /// Measures memory allocation of an action.
    /// </summary>
    public static MemoryProfile MeasureMemory(Action action)
    {
        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var startMemory = GC.GetTotalMemory(false);
        var startGen0 = GC.CollectionCount(0);
        var startGen1 = GC.CollectionCount(1);
        var startGen2 = GC.CollectionCount(2);
        
        action();
        
        var endMemory = GC.GetTotalMemory(false);
        var endGen0 = GC.CollectionCount(0);
        var endGen1 = GC.CollectionCount(1);
        var endGen2 = GC.CollectionCount(2);
        
        return new MemoryProfile
        {
            BytesAllocated = endMemory - startMemory,
            Gen0Collections = endGen0 - startGen0,
            Gen1Collections = endGen1 - startGen1,
            Gen2Collections = endGen2 - startGen2
        };
    }

    /// <summary>
    /// Runs an action multiple times and returns statistics.
    /// </summary>
    public static PerformanceStatistics RunMultiple(Action action, int iterations = 100)
    {
        var times = new List<double>(iterations);
        
        // Warm up
        for (int i = 0; i < 5; i++)
        {
            action();
        }
        
        // Actual measurements
        for (int i = 0; i < iterations; i++)
        {
            var time = MeasureTime(action);
            times.Add(time.TotalMilliseconds);
        }
        
        times.Sort();
        
        return new PerformanceStatistics
        {
            Min = times[0],
            Max = times[^1],
            Mean = times.Average(),
            Median = times[times.Count / 2],
            StdDev = CalculateStdDev(times),
            P95 = times[(int)(times.Count * 0.95)],
            P99 = times[(int)(times.Count * 0.99)]
        };
    }

    private static double CalculateStdDev(List<double> values)
    {
        var mean = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    /// <summary>
    /// Gets current process memory information.
    /// </summary>
    public static ProcessMemoryInfo GetProcessMemoryInfo()
    {
        using var process = Process.GetCurrentProcess();
        return new ProcessMemoryInfo
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            VirtualMemory = process.VirtualMemorySize64,
            PagedMemory = process.PagedMemorySize64,
            NonPagedMemory = process.NonpagedSystemMemorySize64,
            GCHeapSize = GC.GetTotalMemory(false),
            Gen0Size = GC.GetGeneration(new object()),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount
        };
    }

    /// <summary>
    /// Forces memory cleanup.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ForceCleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForFullGCComplete();
    }
}

public struct MemoryProfile
{
    public long BytesAllocated { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    
    public override string ToString()
    {
        return $"Allocated: {BytesAllocated:N0} bytes, GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
    }
}

public struct PerformanceStatistics
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double StdDev { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    
    public override string ToString()
    {
        return $"Min={Min:F2}ms, Max={Max:F2}ms, Mean={Mean:F2}ms, Median={Median:F2}ms, StdDev={StdDev:F2}ms, P95={P95:F2}ms, P99={P99:F2}ms";
    }
}

public struct ProcessMemoryInfo
{
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long VirtualMemory { get; set; }
    public long PagedMemory { get; set; }
    public long NonPagedMemory { get; set; }
    public long GCHeapSize { get; set; }
    public int Gen0Size { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    
    public override string ToString()
    {
        return $@"
Working Set: {WorkingSet / (1024 * 1024):N0} MB
Private Memory: {PrivateMemory / (1024 * 1024):N0} MB
Virtual Memory: {VirtualMemory / (1024 * 1024):N0} MB
GC Heap: {GCHeapSize / (1024 * 1024):N0} MB
Threads: {ThreadCount}, Handles: {HandleCount}";
    }
}