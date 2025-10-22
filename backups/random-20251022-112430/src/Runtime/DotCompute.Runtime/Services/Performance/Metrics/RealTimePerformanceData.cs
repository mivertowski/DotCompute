// <copyright file="RealTimePerformanceData.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Real-time performance data snapshot.
/// Provides current system performance metrics for monitoring and analysis.
/// </summary>
public class RealTimePerformanceData
{
    /// <summary>
    /// Gets the timestamp of this data snapshot.
    /// Indicates when these metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets current CPU usage percentage.
    /// Overall CPU utilization across all cores.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets current memory usage in bytes.
    /// Total memory consumption of the process.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets current GPU usage percentage (if applicable).
    /// GPU utilization for systems with GPU acceleration.
    /// </summary>
    public double GpuUsagePercent { get; init; }

    /// <summary>
    /// Gets current operations per second.
    /// Rate of operation completion.
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets per-accelerator usage data.
    /// Individual accelerator device performance metrics.
    /// </summary>
    public Dictionary<string, AcceleratorUsageData> AcceleratorUsage { get; init; } = [];

    /// <summary>
    /// Gets active operation count.
    /// Number of operations currently in progress.
    /// </summary>
    public int ActiveOperationCount { get; init; }
}