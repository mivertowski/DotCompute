// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text.Json.Serialization;

namespace DotCompute.Backends.Metal.Execution.Graph.Statistics;

/// <summary>
/// Contains comprehensive statistics and performance metrics for Metal compute graphs.
/// </summary>
public class MetalGraphStatistics
{
    /// <summary>
    /// Gets or sets the name of the graph these statistics represent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the graph was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when statistics were last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    #region Graph Structure Statistics

    /// <summary>
    /// Gets or sets the total number of nodes in the graph.
    /// </summary>
    public int NodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of kernel nodes in the graph.
    /// </summary>
    public int KernelNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of memory operation nodes in the graph.
    /// </summary>
    public int MemoryOperationNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of barrier nodes in the graph.
    /// </summary>
    public int BarrierNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the critical path length through the graph.
    /// </summary>
    public int CriticalPathLength { get; set; }

    /// <summary>
    /// Gets or sets the number of execution levels in the graph.
    /// </summary>
    public int ExecutionLevels { get; set; }

    /// <summary>
    /// Gets or sets the maximum parallelism opportunities in the graph.
    /// </summary>
    public int ParallelismOpportunities { get; set; }

    #endregion

    #region Execution Statistics

    /// <summary>
    /// Gets or sets the total number of times the graph has been executed.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last execution.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful execution.
    /// </summary>
    public DateTime? LastSuccessfulExecutionAt { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    [JsonIgnore]
    public double SuccessRate
        => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;

    #endregion

    #region Performance Metrics

    /// <summary>
    /// Gets or sets the total GPU execution time across all executions in milliseconds.
    /// </summary>
    public double TotalGpuExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average GPU execution time per execution in milliseconds.
    /// </summary>
    public double AverageGpuExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum GPU execution time recorded in milliseconds.
    /// </summary>
    public double MinGpuExecutionTimeMs { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the maximum GPU execution time recorded in milliseconds.
    /// </summary>
    public double MaxGpuExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the execution time of the last run in milliseconds.
    /// </summary>
    public double LastExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the total CPU overhead time in milliseconds.
    /// </summary>
    public double TotalCpuOverheadTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average CPU overhead time per execution in milliseconds.
    /// </summary>
    public double AverageCpuOverheadTimeMs { get; set; }

    #endregion

    #region Memory Statistics

    /// <summary>
    /// Gets or sets the total memory transferred across all executions in bytes.
    /// </summary>
    public long TotalMemoryTransferred { get; set; }

    /// <summary>
    /// Gets or sets the average memory transferred per execution in bytes.
    /// </summary>
    public long AverageMemoryTransferred { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage recorded in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory footprint of the graph in bytes.
    /// </summary>
    public long EstimatedMemoryFootprint { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory allocations performed.
    /// </summary>
    public long TotalMemoryAllocations { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth utilization in GB/s.
    /// </summary>
    public double AverageMemoryBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the peak memory bandwidth utilization in GB/s.
    /// </summary>
    public double PeakMemoryBandwidthGBps { get; set; }

    #endregion

    #region Command Buffer Statistics

    /// <summary>
    /// Gets or sets the total number of command buffers used across all executions.
    /// </summary>
    public long TotalCommandBuffers { get; set; }

    /// <summary>
    /// Gets or sets the average number of command buffers per execution.
    /// </summary>
    public double AverageCommandBuffersPerExecution { get; set; }

    /// <summary>
    /// Gets or sets the total number of compute command encoders used.
    /// </summary>
    public long TotalComputeEncoders { get; set; }

    /// <summary>
    /// Gets or sets the total number of blit command encoders used.
    /// </summary>
    public long TotalBlitEncoders { get; set; }

    /// <summary>
    /// Gets or sets the average command buffer utilization percentage.
    /// </summary>
    public double AverageCommandBufferUtilization { get; set; }

    #endregion

    #region Optimization Statistics

    /// <summary>
    /// Gets or sets the number of optimization passes applied to the graph.
    /// </summary>
    public int OptimizationPasses { get; set; }

    /// <summary>
    /// Gets or sets the number of kernel fusion optimizations applied.
    /// </summary>
    public int KernelFusionsApplied { get; set; }

    /// <summary>
    /// Gets or sets the number of memory coalescing optimizations applied.
    /// </summary>
    public int MemoryCoalescingsApplied { get; set; }

    /// <summary>
    /// Gets or sets the number of command buffer batching optimizations applied.
    /// </summary>
    public int CommandBufferBatchingsApplied { get; set; }

    /// <summary>
    /// Gets or sets the performance improvement factor from optimizations.
    /// </summary>
    public double OptimizationSpeedup { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the memory reduction factor from optimizations.
    /// </summary>
    public double MemoryReductionFactor { get; set; } = 1.0;

    #endregion

    #region Error and Reliability Statistics

    /// <summary>
    /// Gets or sets the total number of node execution failures.
    /// </summary>
    public long NodeExecutionFailures { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory allocation failures.
    /// </summary>
    public long MemoryAllocationFailures { get; set; }

    /// <summary>
    /// Gets or sets the total number of command buffer creation failures.
    /// </summary>
    public long CommandBufferCreationFailures { get; set; }

    /// <summary>
    /// Gets or sets the total number of timeout occurrences.
    /// </summary>
    public long TimeoutOccurrences { get; set; }

    /// <summary>
    /// Gets or sets the most common error message.
    /// </summary>
    public string? MostCommonError { get; set; }

    /// <summary>
    /// Gets or sets error frequency statistics.
    /// </summary>
    public Dictionary<string, int> ErrorFrequency { get; } = [];

    #endregion

    #region Derived Metrics

    /// <summary>
    /// Gets the average performance in operations per second.
    /// </summary>
    [JsonIgnore]
    public double AveragePerformanceOpsPerSec
        => AverageGpuExecutionTimeMs > 0 ? NodeCount / (AverageGpuExecutionTimeMs / 1000.0) : 0;

    /// <summary>
    /// Gets the throughput in executions per second.
    /// </summary>
    [JsonIgnore]
    public double ThroughputExecutionsPerSec
        => AverageGpuExecutionTimeMs > 0 ? 1000.0 / AverageGpuExecutionTimeMs : 0;

    /// <summary>
    /// Gets the efficiency ratio (GPU time / total time).
    /// </summary>
    [JsonIgnore]
    public double EfficiencyRatio
        => AverageCpuOverheadTimeMs + AverageGpuExecutionTimeMs > 0
            ? AverageGpuExecutionTimeMs / (AverageGpuExecutionTimeMs + AverageCpuOverheadTimeMs)
            : 0;

    /// <summary>
    /// Gets the parallelization effectiveness.
    /// </summary>
    [JsonIgnore]
    public double ParallelizationEffectiveness
        => CriticalPathLength > 0 ? (double)NodeCount / CriticalPathLength : 1.0;

    /// <summary>
    /// Gets the resource utilization score (0-100).
    /// </summary>
    [JsonIgnore]
    public double ResourceUtilizationScore
        => Math.Min(100, (AverageCommandBufferUtilization * 0.4 + EfficiencyRatio * 100 * 0.6));

    #endregion

    #region Methods

    /// <summary>
    /// Updates the statistics with execution results.
    /// </summary>
    /// <param name="executionTimeMs">The GPU execution time in milliseconds.</param>
    /// <param name="cpuOverheadMs">The CPU overhead time in milliseconds.</param>
    /// <param name="memoryTransferred">The amount of memory transferred in bytes.</param>
    /// <param name="commandBuffersUsed">The number of command buffers used.</param>
    /// <param name="success">Whether the execution was successful.</param>
    public void UpdateExecutionStatistics(
        double executionTimeMs,
        double cpuOverheadMs,
        long memoryTransferred,
        int commandBuffersUsed,
        bool success)
    {
        TotalExecutions++;
        LastExecutedAt = DateTime.UtcNow;
        LastExecutionTimeMs = executionTimeMs;

        if (success)
        {
            SuccessfulExecutions++;
            LastSuccessfulExecutionAt = DateTime.UtcNow;

            // Update performance metrics
            TotalGpuExecutionTimeMs += executionTimeMs;
            AverageGpuExecutionTimeMs = TotalGpuExecutionTimeMs / SuccessfulExecutions;


            MinGpuExecutionTimeMs = Math.Min(MinGpuExecutionTimeMs, executionTimeMs);
            MaxGpuExecutionTimeMs = Math.Max(MaxGpuExecutionTimeMs, executionTimeMs);

            // Update CPU overhead
            TotalCpuOverheadTimeMs += cpuOverheadMs;
            AverageCpuOverheadTimeMs = TotalCpuOverheadTimeMs / SuccessfulExecutions;

            // Update memory statistics
            TotalMemoryTransferred += memoryTransferred;
            AverageMemoryTransferred = TotalMemoryTransferred / SuccessfulExecutions;

            // Update command buffer statistics
            TotalCommandBuffers += commandBuffersUsed;
            AverageCommandBuffersPerExecution = (double)TotalCommandBuffers / SuccessfulExecutions;

            // Calculate memory bandwidth
            if (executionTimeMs > 0)
            {
                var bandwidthGBps = (memoryTransferred / (1024.0 * 1024.0 * 1024.0)) / (executionTimeMs / 1000.0);
                AverageMemoryBandwidthGBps = (AverageMemoryBandwidthGBps * (SuccessfulExecutions - 1) + bandwidthGBps) / SuccessfulExecutions;
                PeakMemoryBandwidthGBps = Math.Max(PeakMemoryBandwidthGBps, bandwidthGBps);
            }
        }
        else
        {
            FailedExecutions++;
        }

        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates optimization statistics.
    /// </summary>
    /// <param name="fusionsApplied">Number of kernel fusions applied.</param>
    /// <param name="coalescingsApplied">Number of memory coalescings applied.</param>
    /// <param name="batchingsApplied">Number of command buffer batchings applied.</param>
    /// <param name="speedupFactor">Performance speedup factor achieved.</param>
    public void UpdateOptimizationStatistics(
        int fusionsApplied,
        int coalescingsApplied,
        int batchingsApplied,
        double speedupFactor = 1.0)
    {
        OptimizationPasses++;
        KernelFusionsApplied += fusionsApplied;
        MemoryCoalescingsApplied += coalescingsApplied;
        CommandBufferBatchingsApplied += batchingsApplied;
        OptimizationSpeedup = Math.Max(OptimizationSpeedup, speedupFactor);


        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Records an execution error.
    /// </summary>
    /// <param name="errorType">The type of error that occurred.</param>
    /// <param name="errorMessage">The error message.</param>
    public void RecordError(string errorType, string errorMessage)
    {
        switch (errorType.ToLowerInvariant())
        {
            case "node_execution":
                NodeExecutionFailures++;
                break;
            case "memory_allocation":
                MemoryAllocationFailures++;
                break;
            case "command_buffer_creation":
                CommandBufferCreationFailures++;
                break;
            case "timeout":
                TimeoutOccurrences++;
                break;
        }

        // Update error frequency
        if (!string.IsNullOrEmpty(errorMessage))
        {
            ErrorFrequency[errorMessage] = ErrorFrequency.GetValueOrDefault(errorMessage, 0) + 1;

            // Update most common error

            if (MostCommonError == null || ErrorFrequency[errorMessage] > ErrorFrequency.GetValueOrDefault(MostCommonError, 0))
            {
                MostCommonError = errorMessage;
            }
        }

        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets all statistics to their initial state.
    /// </summary>
    public void Reset()
    {
        TotalExecutions = 0;
        SuccessfulExecutions = 0;
        FailedExecutions = 0;
        LastExecutedAt = null;
        LastSuccessfulExecutionAt = null;


        TotalGpuExecutionTimeMs = 0;
        AverageGpuExecutionTimeMs = 0;
        MinGpuExecutionTimeMs = double.MaxValue;
        MaxGpuExecutionTimeMs = 0;
        LastExecutionTimeMs = 0;
        TotalCpuOverheadTimeMs = 0;
        AverageCpuOverheadTimeMs = 0;


        TotalMemoryTransferred = 0;
        AverageMemoryTransferred = 0;
        PeakMemoryUsage = 0;
        TotalMemoryAllocations = 0;
        AverageMemoryBandwidthGBps = 0;
        PeakMemoryBandwidthGBps = 0;


        TotalCommandBuffers = 0;
        AverageCommandBuffersPerExecution = 0;
        TotalComputeEncoders = 0;
        TotalBlitEncoders = 0;
        AverageCommandBufferUtilization = 0;


        OptimizationPasses = 0;
        KernelFusionsApplied = 0;
        MemoryCoalescingsApplied = 0;
        CommandBufferBatchingsApplied = 0;
        OptimizationSpeedup = 1.0;
        MemoryReductionFactor = 1.0;


        NodeExecutionFailures = 0;
        MemoryAllocationFailures = 0;
        CommandBufferCreationFailures = 0;
        TimeoutOccurrences = 0;
        MostCommonError = null;
        ErrorFrequency.Clear();


        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a summary report of the statistics.
    /// </summary>
    /// <returns>A formatted string containing the statistics summary.</returns>
    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();


        _ = report.AppendLine(CultureInfo.InvariantCulture, $"Metal Graph Statistics Report for '{Name}'");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = report.AppendLine(new string('=', 60));

        // Graph structure

        _ = report.AppendLine("\nGraph Structure:");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total Nodes: {NodeCount:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Kernel Nodes: {KernelNodeCount:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Memory Operation Nodes: {MemoryOperationNodeCount:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Critical Path Length: {CriticalPathLength:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Parallelism Opportunities: {ParallelismOpportunities:N0}");

        // Execution statistics

        _ = report.AppendLine("\nExecution Statistics:");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total Executions: {TotalExecutions:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Success Rate: {SuccessRate:F1}%");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Last Executed: {LastExecutedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "Never"}");

        // Performance metrics

        _ = report.AppendLine("\nPerformance Metrics:");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Average GPU Time: {AverageGpuExecutionTimeMs:F2} ms");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Min/Max GPU Time: {MinGpuExecutionTimeMs:F2} / {MaxGpuExecutionTimeMs:F2} ms");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Throughput: {ThroughputExecutionsPerSec:F2} executions/sec");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Efficiency Ratio: {EfficiencyRatio:F2}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Resource Utilization: {ResourceUtilizationScore:F1}%");

        // Memory statistics

        _ = report.AppendLine("\nMemory Statistics:");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total Memory Transferred: {TotalMemoryTransferred:N0} bytes");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Average Memory Bandwidth: {AverageMemoryBandwidthGBps:F2} GB/s");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Peak Memory Bandwidth: {PeakMemoryBandwidthGBps:F2} GB/s");

        // Optimization results

        if (OptimizationPasses > 0)
        {
            _ = report.AppendLine("\nOptimization Results:");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Optimization Passes: {OptimizationPasses:N0}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Kernel Fusions Applied: {KernelFusionsApplied:N0}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Performance Speedup: {OptimizationSpeedup:F2}x");
        }

        // Error summary

        if (FailedExecutions > 0)
        {
            _ = report.AppendLine("\nError Summary:");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Failed Executions: {FailedExecutions:N0}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Node Execution Failures: {NodeExecutionFailures:N0}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Memory Allocation Failures: {MemoryAllocationFailures:N0}");
            if (!string.IsNullOrEmpty(MostCommonError))
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Most Common Error: {MostCommonError}");
            }
        }


        return report.ToString();
    }

    #endregion
}