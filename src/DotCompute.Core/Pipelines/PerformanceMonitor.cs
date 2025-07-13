// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Provides real-time performance monitoring for pipeline execution.
/// </summary>
internal static class PerformanceMonitor
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();
    private static DateTime _lastCpuTime = DateTime.UtcNow;
    private static TimeSpan _lastTotalProcessorTime = CurrentProcess.TotalProcessorTime;
    private static long _lastMemoryWorkingSet = CurrentProcess.WorkingSet64;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the current CPU utilization (0.0 to 1.0).
    /// </summary>
    public static double GetCpuUtilization()
    {
        lock (_lock)
        {
            try
            {
                CurrentProcess.Refresh();
                
                var currentTime = DateTime.UtcNow;
                var currentTotalProcessorTime = CurrentProcess.TotalProcessorTime;
                
                var timeDiff = currentTime.Subtract(_lastCpuTime).TotalMilliseconds;
                var cpuTimeDiff = currentTotalProcessorTime.Subtract(_lastTotalProcessorTime).TotalMilliseconds;
                
                if (timeDiff > 0)
                {
                    // Calculate CPU usage as a percentage of available CPU time
                    var cpuUsage = cpuTimeDiff / (timeDiff * Environment.ProcessorCount);
                    
                    // Update last values
                    _lastCpuTime = currentTime;
                    _lastTotalProcessorTime = currentTotalProcessorTime;
                    
                    // Clamp between 0 and 1
                    return Math.Max(0.0, Math.Min(1.0, cpuUsage));
                }
                
                return 0.0;
            }
            catch
            {
                // Fallback to a reasonable estimate based on thread activity
                return EstimateCpuUtilizationFallback();
            }
        }
    }

    /// <summary>
    /// Gets the current memory bandwidth utilization (0.0 to 1.0).
    /// </summary>
    public static double GetMemoryBandwidthUtilization()
    {
        lock (_lock)
        {
            try
            {
                CurrentProcess.Refresh();
                
                var currentMemoryWorkingSet = CurrentProcess.WorkingSet64;
                var memoryDelta = Math.Abs(currentMemoryWorkingSet - _lastMemoryWorkingSet);
                
                // Estimate bandwidth utilization based on memory access patterns
                // Calculate actual memory bandwidth usage from working set changes
                var estimatedBandwidthGB = memoryDelta / (1024.0 * 1024.0 * 1024.0);
                
                // Get theoretical memory bandwidth based on system characteristics
                var theoreticalBandwidthGB = GetTheoreticalMemoryBandwidth();
                
                var utilization = estimatedBandwidthGB / theoreticalBandwidthGB;
                
                _lastMemoryWorkingSet = currentMemoryWorkingSet;
                
                // Also factor in GC pressure as it indicates memory activity
                var gcPressure = GetGCPressure();
                utilization = Math.Max(utilization, gcPressure);
                
                // Clamp between 0 and 1
                return Math.Max(0.0, Math.Min(1.0, utilization));
            }
            catch
            {
                // Fallback to GC-based estimation
                return GetGCPressure();
            }
        }
    }

    /// <summary>
    /// Gets compute utilization for the current execution context.
    /// </summary>
    public static double GetComputeUtilization(TimeSpan executionTime, long workItems)
    {
        // Get CPU utilization as base
        var cpuUtilization = GetCpuUtilization();
        
        // Factor in work efficiency
        if (workItems > 0 && executionTime.TotalMilliseconds > 0)
        {
            // Calculate items per millisecond
            var throughput = workItems / executionTime.TotalMilliseconds;
            
            // Estimate theoretical throughput based on processor count
            var theoreticalThroughput = Environment.ProcessorCount * 1000.0; // Simplified model
            
            var efficiency = Math.Min(1.0, throughput / theoreticalThroughput);
            
            // Combine CPU utilization with work efficiency
            return (cpuUtilization + efficiency) / 2.0;
        }
        
        return cpuUtilization;
    }

    /// <summary>
    /// Gets memory statistics for the current process.
    /// </summary>
    public static (long workingSet, long privateMemory, long virtualMemory) GetMemoryStats()
    {
        CurrentProcess.Refresh();
        return (
            CurrentProcess.WorkingSet64,
            CurrentProcess.PrivateMemorySize64,
            CurrentProcess.VirtualMemorySize64
        );
    }

    /// <summary>
    /// Gets thread pool statistics.
    /// </summary>
    public static (int workerThreads, int completionPortThreads, int availableWorkerThreads, int availableCompletionPortThreads) GetThreadPoolStats()
    {
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
        
        var activeWorkerThreads = maxWorkerThreads - availableWorkerThreads;
        var activeCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads;
        
        return (activeWorkerThreads, activeCompletionPortThreads, availableWorkerThreads, availableCompletionPortThreads);
    }

    private static double EstimateCpuUtilizationFallback()
    {
        // Fallback estimation based on thread pool activity
        var (activeWorkers, _, availableWorkers, _) = GetThreadPoolStats();
        var totalWorkers = activeWorkers + availableWorkers;
        
        if (totalWorkers > 0)
        {
            // Estimate based on active thread ratio
            var threadUtilization = (double)activeWorkers / totalWorkers;
            
            // Factor in processor count
            var normalizedUtilization = threadUtilization * (activeWorkers / (double)Environment.ProcessorCount);
            
            return Math.Max(0.0, Math.Min(1.0, normalizedUtilization));
        }
        
        return 0.0;
    }

    private static double GetGCPressure()
    {
        // Get GC collection counts
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);
        
        // Higher generation collections indicate more memory pressure
        var pressure = (gen0Collections * 0.1 + gen1Collections * 0.3 + gen2Collections * 0.6) / 1000.0;
        
        // Also factor in current memory pressure
        var totalMemory = GC.GetTotalMemory(false);
        var maxMemory = GC.GetTotalMemory(true);
        var memoryPressure = maxMemory > 0 ? (double)totalMemory / maxMemory : 0.0;
        
        return Math.Max(0.0, Math.Min(1.0, (pressure + memoryPressure) / 2.0));
    }

    private static double GetTheoreticalMemoryBandwidth()
    {
        // Simplified estimation of theoretical memory bandwidth
        // Real implementation would query system-specific information
        
        if (IsWindows || IsLinux)
        {
            // Modern DDR4/DDR5 systems typically have 25-50 GB/s bandwidth
            // This is a conservative estimate
            return 25.0;
        }
        else if (IsMacOS)
        {
            // Apple Silicon has higher bandwidth
            return 50.0;
        }
        
        // Conservative default
        return 20.0;
    }

    /// <summary>
    /// Gets performance counters for a specific execution.
    /// </summary>
    public static class ExecutionMetrics
    {
        private static readonly ThreadLocal<ExecutionContext> _context = new(() => new ExecutionContext());

        public static void StartExecution()
        {
            var context = _context.Value!;
            context.StartTime = Stopwatch.GetTimestamp();
            context.StartCpuTotal = CurrentProcess.TotalProcessorTime;
            context.StartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        }

        public static (double cpuTime, long allocatedBytes, double elapsedMs) EndExecution()
        {
            var context = _context.Value!;
            var endTime = Stopwatch.GetTimestamp();
            CurrentProcess.Refresh();
            var endCpuTotal = CurrentProcess.TotalProcessorTime;
            var endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

            var elapsedMs = (endTime - context.StartTime) * 1000.0 / Stopwatch.Frequency;
            var cpuTime = (endCpuTotal - context.StartCpuTotal).TotalMilliseconds;
            var allocatedBytes = endAllocatedBytes - context.StartAllocatedBytes;

            return (cpuTime, allocatedBytes, elapsedMs);
        }

        private class ExecutionContext
        {
            public long StartTime;
            public TimeSpan StartCpuTotal;
            public long StartAllocatedBytes;
        }
    }
}