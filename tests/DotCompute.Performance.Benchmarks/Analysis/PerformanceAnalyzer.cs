// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Performance.Benchmarks.Analysis;

/// <summary>
/// Performance analysis and profiling tools
/// </summary>
public class PerformanceAnalyzer
{
    private readonly ILogger<PerformanceAnalyzer> _logger;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly Process _currentProcess;
    
    public PerformanceAnalyzer(ILogger<PerformanceAnalyzer> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Performance counters not available: {Error}", ex.Message);
        }
    }

    public async Task<PerformanceProfile> StartProfiling(string profileName, TimeSpan duration)
    {
        _logger.LogInformation("Starting performance profiling for {ProfileName} over {Duration}", profileName, duration);
        
        var profile = new PerformanceProfile
        {
            Name = profileName,
            StartTime = DateTime.UtcNow,
            Duration = duration
        };
        
        var cancellationTokenSource = new CancellationTokenSource(duration);
        var profilingTasks = new List<Task>();
        
        // Start various profiling tasks
        profilingTasks.Add(Task.Run(() => ProfileMemoryUsage(profile, cancellationTokenSource.Token)));
        profilingTasks.Add(Task.Run(() => ProfileCpuUsage(profile, cancellationTokenSource.Token)));
        profilingTasks.Add(Task.Run(() => ProfileGarbageCollection(profile, cancellationTokenSource.Token)));
        profilingTasks.Add(Task.Run(() => ProfileThreadPoolUsage(profile, cancellationTokenSource.Token)));
        profilingTasks.Add(Task.Run(() => ProfileExceptionHandling(profile, cancellationTokenSource.Token)));
        
        try
        {
            await Task.WhenAll(profilingTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when duration expires
        }
        
        profile.EndTime = DateTime.UtcNow;
        await GenerateProfilingReport(profile);
        
        return profile;
    }

    private async Task ProfileMemoryUsage(PerformanceProfile profile, CancellationToken cancellationToken)
    {
        var memoryMetrics = new List<MemoryMetric>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var metric = new MemoryMetric
                {
                    Timestamp = DateTime.UtcNow,
                    WorkingSetBytes = _currentProcess.WorkingSet64,
                    PrivateMemoryBytes = _currentProcess.PrivateMemorySize64,
                    GcTotalMemoryBytes = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    TotalAllocatedBytes = GC.GetTotalAllocatedBytes(),
                    AvailableMemoryBytes = _memoryCounter?.NextValue() * 1024 * 1024 ?? 0
                };
                
                memoryMetrics.Add(metric);
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error profiling memory usage: {Error}", ex.Message);
            }
        }
        
        profile.MemoryMetrics = memoryMetrics;
    }

    private async Task ProfileCpuUsage(PerformanceProfile profile, CancellationToken cancellationToken)
    {
        var cpuMetrics = new List<CpuMetric>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var metric = new CpuMetric
                {
                    Timestamp = DateTime.UtcNow,
                    TotalProcessorTime = _currentProcess.TotalProcessorTime,
                    UserProcessorTime = _currentProcess.UserProcessorTime,
                    PrivilegedProcessorTime = _currentProcess.PrivilegedProcessorTime,
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    SystemCpuUsage = _cpuCounter?.NextValue() ?? 0
                };
                
                cpuMetrics.Add(metric);
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error profiling CPU usage: {Error}", ex.Message);
            }
        }
        
        profile.CpuMetrics = cpuMetrics;
    }

    private async Task ProfileGarbageCollection(PerformanceProfile profile, CancellationToken cancellationToken)
    {
        var gcMetrics = new List<GarbageCollectionMetric>();
        var lastGen0 = GC.CollectionCount(0);
        var lastGen1 = GC.CollectionCount(1);
        var lastGen2 = GC.CollectionCount(2);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var currentGen0 = GC.CollectionCount(0);
                var currentGen1 = GC.CollectionCount(1);
                var currentGen2 = GC.CollectionCount(2);
                
                if (currentGen0 > lastGen0 || currentGen1 > lastGen1 || currentGen2 > lastGen2)
                {
                    var metric = new GarbageCollectionMetric
                    {
                        Timestamp = DateTime.UtcNow,
                        Gen0Collections = currentGen0,
                        Gen1Collections = currentGen1,
                        Gen2Collections = currentGen2,
                        Gen0CollectionsSinceLast = currentGen0 - lastGen0,
                        Gen1CollectionsSinceLast = currentGen1 - lastGen1,
                        Gen2CollectionsSinceLast = currentGen2 - lastGen2,
                        TotalMemoryBeforeGC = GC.GetTotalMemory(false),
                        TotalMemoryAfterGC = GC.GetTotalMemory(true)
                    };
                    
                    gcMetrics.Add(metric);
                    
                    lastGen0 = currentGen0;
                    lastGen1 = currentGen1;
                    lastGen2 = currentGen2;
                }
                
                await Task.Delay(100, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error profiling garbage collection: {Error}", ex.Message);
            }
        }
        
        profile.GarbageCollectionMetrics = gcMetrics;
    }

    private async Task ProfileThreadPoolUsage(PerformanceProfile profile, CancellationToken cancellationToken)
    {
        var threadPoolMetrics = new List<ThreadPoolMetric>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
                ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
                ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
                
                var metric = new ThreadPoolMetric
                {
                    Timestamp = DateTime.UtcNow,
                    MaxWorkerThreads = maxWorkerThreads,
                    MaxCompletionPortThreads = maxCompletionPortThreads,
                    AvailableWorkerThreads = availableWorkerThreads,
                    AvailableCompletionPortThreads = availableCompletionPortThreads,
                    MinWorkerThreads = minWorkerThreads,
                    MinCompletionPortThreads = minCompletionPortThreads,
                    ActiveWorkerThreads = maxWorkerThreads - availableWorkerThreads,
                    ActiveCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads,
                    PendingWorkItemCount = ThreadPool.PendingWorkItemCount,
                    CompletedWorkItemCount = ThreadPool.CompletedWorkItemCount
                };
                
                threadPoolMetrics.Add(metric);
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error profiling thread pool usage: {Error}", ex.Message);
            }
        }
        
        profile.ThreadPoolMetrics = threadPoolMetrics;
    }

    private async Task ProfileExceptionHandling(PerformanceProfile profile, CancellationToken cancellationToken)
    {
        var exceptionMetrics = new List<ExceptionMetric>();
        var lastExceptionCount = 0L;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // This would need to be implemented with custom exception tracking
                // For now, we'll track AppDomain exceptions
                var currentExceptionCount = 0L; // Placeholder
                
                if (currentExceptionCount > lastExceptionCount)
                {
                    var metric = new ExceptionMetric
                    {
                        Timestamp = DateTime.UtcNow,
                        TotalExceptions = currentExceptionCount,
                        ExceptionsSinceLast = currentExceptionCount - lastExceptionCount
                    };
                    
                    exceptionMetrics.Add(metric);
                    lastExceptionCount = currentExceptionCount;
                }
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error profiling exception handling: {Error}", ex.Message);
            }
        }
        
        profile.ExceptionMetrics = exceptionMetrics;
    }

    public async Task<BottleneckAnalysis> AnalyzeBottlenecks(PerformanceProfile profile)
    {
        var analysis = new BottleneckAnalysis
        {
            ProfileName = profile.Name,
            AnalysisTime = DateTime.UtcNow
        };
        
        // Analyze memory bottlenecks
        if (profile.MemoryMetrics?.Any() == true)
        {
            var memoryGrowthRate = CalculateMemoryGrowthRate(profile.MemoryMetrics);
            var peakMemoryUsage = profile.MemoryMetrics.Max(m => m.WorkingSetBytes);
            var gcPressure = profile.MemoryMetrics.Last().Gen2Collections - profile.MemoryMetrics.First().Gen2Collections;
            
            analysis.MemoryBottlenecks = new MemoryBottleneckAnalysis
            {
                MemoryGrowthRateMBPerSecond = memoryGrowthRate,
                PeakMemoryUsageMB = peakMemoryUsage / (1024.0 * 1024.0),
                GcPressure = gcPressure,
                IsMemoryBottleneck = memoryGrowthRate > 10 || gcPressure > 100,
                Recommendations = GenerateMemoryRecommendations(memoryGrowthRate, gcPressure)
            };
        }
        
        // Analyze CPU bottlenecks
        if (profile.CpuMetrics?.Any() == true)
        {
            var avgCpuUsage = profile.CpuMetrics.Average(m => m.SystemCpuUsage);
            var maxThreads = profile.CpuMetrics.Max(m => m.ThreadCount);
            var avgThreads = profile.CpuMetrics.Average(m => m.ThreadCount);
            
            analysis.CpuBottlenecks = new CpuBottleneckAnalysis
            {
                AverageCpuUsage = avgCpuUsage,
                MaxThreadCount = maxThreads,
                AverageThreadCount = avgThreads,
                IsCpuBottleneck = avgCpuUsage > 80,
                Recommendations = GenerateCpuRecommendations(avgCpuUsage, maxThreads)
            };
        }
        
        // Analyze thread pool bottlenecks
        if (profile.ThreadPoolMetrics?.Any() == true)
        {
            var maxActiveWorkers = profile.ThreadPoolMetrics.Max(m => m.ActiveWorkerThreads);
            var avgActiveWorkers = profile.ThreadPoolMetrics.Average(m => m.ActiveWorkerThreads);
            var maxPendingItems = profile.ThreadPoolMetrics.Max(m => m.PendingWorkItemCount);
            
            analysis.ThreadPoolBottlenecks = new ThreadPoolBottleneckAnalysis
            {
                MaxActiveWorkerThreads = maxActiveWorkers,
                AverageActiveWorkerThreads = avgActiveWorkers,
                MaxPendingWorkItems = maxPendingItems,
                IsThreadPoolBottleneck = maxPendingItems > 100 || maxActiveWorkers > Environment.ProcessorCount * 2,
                Recommendations = GenerateThreadPoolRecommendations(maxActiveWorkers, maxPendingItems)
            };
        }
        
        await SaveBottleneckAnalysis(analysis);
        return analysis;
    }

    private double CalculateMemoryGrowthRate(List<MemoryMetric> metrics)
    {
        if (metrics.Count < 2) return 0;
        
        var firstMetric = metrics.First();
        var lastMetric = metrics.Last();
        var timeDiff = (lastMetric.Timestamp - firstMetric.Timestamp).TotalSeconds;
        var memoryDiff = (lastMetric.WorkingSetBytes - firstMetric.WorkingSetBytes) / (1024.0 * 1024.0);
        
        return memoryDiff / timeDiff;
    }

    private List<string> GenerateMemoryRecommendations(double growthRate, long gcPressure)
    {
        var recommendations = new List<string>();
        
        if (growthRate > 10)
        {
            recommendations.Add("High memory growth rate detected. Consider using object pooling.");
            recommendations.Add("Review large object allocations and consider streaming approaches.");
        }
        
        if (gcPressure > 100)
        {
            recommendations.Add("High GC pressure detected. Consider reducing object allocations.");
            recommendations.Add("Use structs instead of classes for small data types.");
            recommendations.Add("Implement IDisposable for resources and use using statements.");
        }
        
        return recommendations;
    }

    private List<string> GenerateCpuRecommendations(double avgCpuUsage, int maxThreads)
    {
        var recommendations = new List<string>();
        
        if (avgCpuUsage > 80)
        {
            recommendations.Add("High CPU usage detected. Consider optimizing algorithms.");
            recommendations.Add("Review hot paths and consider caching or memoization.");
        }
        
        if (maxThreads > Environment.ProcessorCount * 4)
        {
            recommendations.Add("High thread count detected. Consider using async/await patterns.");
            recommendations.Add("Review thread pool usage and consider Task-based patterns.");
        }
        
        return recommendations;
    }

    private List<string> GenerateThreadPoolRecommendations(int maxActiveWorkers, long maxPendingItems)
    {
        var recommendations = new List<string>();
        
        if (maxPendingItems > 100)
        {
            recommendations.Add("High pending work items detected. Consider increasing thread pool size.");
            recommendations.Add("Review work item granularity and consider batching.");
        }
        
        if (maxActiveWorkers > Environment.ProcessorCount * 2)
        {
            recommendations.Add("High active worker threads detected. Consider CPU-bound vs IO-bound work separation.");
            recommendations.Add("Use ConfigureAwait(false) for library code to avoid deadlocks.");
        }
        
        return recommendations;
    }

    private async Task GenerateProfilingReport(PerformanceProfile profile)
    {
        var report = new
        {
            Profile = profile.Name,
            Duration = profile.Duration,
            StartTime = profile.StartTime,
            EndTime = profile.EndTime,
            MemoryStatistics = profile.MemoryMetrics?.Any() == true ? new
            {
                PeakWorkingSetMB = profile.MemoryMetrics.Max(m => m.WorkingSetBytes) / (1024.0 * 1024.0),
                AverageWorkingSetMB = profile.MemoryMetrics.Average(m => m.WorkingSetBytes) / (1024.0 * 1024.0),
                TotalGen0Collections = profile.MemoryMetrics.Last().Gen0Collections - profile.MemoryMetrics.First().Gen0Collections,
                TotalGen1Collections = profile.MemoryMetrics.Last().Gen1Collections - profile.MemoryMetrics.First().Gen1Collections,
                TotalGen2Collections = profile.MemoryMetrics.Last().Gen2Collections - profile.MemoryMetrics.First().Gen2Collections,
                MemoryGrowthRateMBPerSecond = CalculateMemoryGrowthRate(profile.MemoryMetrics)
            } : null,
            CpuStatistics = profile.CpuMetrics?.Any() == true ? new
            {
                AverageCpuUsage = profile.CpuMetrics.Average(m => m.SystemCpuUsage),
                MaxCpuUsage = profile.CpuMetrics.Max(m => m.SystemCpuUsage),
                AverageThreadCount = profile.CpuMetrics.Average(m => m.ThreadCount),
                MaxThreadCount = profile.CpuMetrics.Max(m => m.ThreadCount)
            } : null,
            ThreadPoolStatistics = profile.ThreadPoolMetrics?.Any() == true ? new
            {
                MaxActiveWorkerThreads = profile.ThreadPoolMetrics.Max(m => m.ActiveWorkerThreads),
                AverageActiveWorkerThreads = profile.ThreadPoolMetrics.Average(m => m.ActiveWorkerThreads),
                MaxPendingWorkItems = profile.ThreadPoolMetrics.Max(m => m.PendingWorkItemCount),
                TotalCompletedWorkItems = profile.ThreadPoolMetrics.Last().CompletedWorkItemCount
            } : null
        };
        
        _logger.LogInformation("Performance Profiling Report: {@Report}", report);
        
        // Save detailed report to file
        var reportPath = Path.Combine("Analysis", $"performance-profile-{profile.Name}-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Detailed performance profile saved to: {ReportPath}", reportPath);
    }

    private async Task SaveBottleneckAnalysis(BottleneckAnalysis analysis)
    {
        var reportPath = Path.Combine("Analysis", $"bottleneck-analysis-{analysis.ProfileName}-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(analysis, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Bottleneck analysis saved to: {ReportPath}", reportPath);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _currentProcess?.Dispose();
    }
}

public class PerformanceProfile
{
    public string Name { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<MemoryMetric>? MemoryMetrics { get; set; }
    public List<CpuMetric>? CpuMetrics { get; set; }
    public List<GarbageCollectionMetric>? GarbageCollectionMetrics { get; set; }
    public List<ThreadPoolMetric>? ThreadPoolMetrics { get; set; }
    public List<ExceptionMetric>? ExceptionMetrics { get; set; }
}

public class MemoryMetric
{
    public DateTime Timestamp { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PrivateMemoryBytes { get; set; }
    public long GcTotalMemoryBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
}

public class CpuMetric
{
    public DateTime Timestamp { get; set; }
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }
    public TimeSpan PrivilegedProcessorTime { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public float SystemCpuUsage { get; set; }
}

public class GarbageCollectionMetric
{
    public DateTime Timestamp { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int Gen0CollectionsSinceLast { get; set; }
    public int Gen1CollectionsSinceLast { get; set; }
    public int Gen2CollectionsSinceLast { get; set; }
    public long TotalMemoryBeforeGC { get; set; }
    public long TotalMemoryAfterGC { get; set; }
}

public class ThreadPoolMetric
{
    public DateTime Timestamp { get; set; }
    public int MaxWorkerThreads { get; set; }
    public int MaxCompletionPortThreads { get; set; }
    public int AvailableWorkerThreads { get; set; }
    public int AvailableCompletionPortThreads { get; set; }
    public int MinWorkerThreads { get; set; }
    public int MinCompletionPortThreads { get; set; }
    public int ActiveWorkerThreads { get; set; }
    public int ActiveCompletionPortThreads { get; set; }
    public long PendingWorkItemCount { get; set; }
    public long CompletedWorkItemCount { get; set; }
}

public class ExceptionMetric
{
    public DateTime Timestamp { get; set; }
    public long TotalExceptions { get; set; }
    public long ExceptionsSinceLast { get; set; }
}

public class BottleneckAnalysis
{
    public string ProfileName { get; set; } = "";
    public DateTime AnalysisTime { get; set; }
    public MemoryBottleneckAnalysis? MemoryBottlenecks { get; set; }
    public CpuBottleneckAnalysis? CpuBottlenecks { get; set; }
    public ThreadPoolBottleneckAnalysis? ThreadPoolBottlenecks { get; set; }
}

public class MemoryBottleneckAnalysis
{
    public double MemoryGrowthRateMBPerSecond { get; set; }
    public double PeakMemoryUsageMB { get; set; }
    public long GcPressure { get; set; }
    public bool IsMemoryBottleneck { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class CpuBottleneckAnalysis
{
    public double AverageCpuUsage { get; set; }
    public int MaxThreadCount { get; set; }
    public double AverageThreadCount { get; set; }
    public bool IsCpuBottleneck { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class ThreadPoolBottleneckAnalysis
{
    public int MaxActiveWorkerThreads { get; set; }
    public double AverageActiveWorkerThreads { get; set; }
    public long MaxPendingWorkItems { get; set; }
    public bool IsThreadPoolBottleneck { get; set; }
    public List<string> Recommendations { get; set; } = new();
}