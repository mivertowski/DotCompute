// <copyright file="PluginRuntimeMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Runtime metrics for a loaded plugin.
/// Tracks performance and resource usage of an active plugin.
/// </summary>
public class PluginRuntimeMetrics
{
    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// Current memory consumption of the plugin.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage.
    /// Processor utilization by the plugin (0-100).
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of active requests.
    /// Currently processing request count.
    /// </summary>
    public int ActiveRequests { get; set; }

    /// <summary>
    /// Gets or sets the total number of processed requests.
    /// Cumulative count of all handled requests.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// Total number of errors encountered.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// Mean processing time for requests.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets custom metrics specific to the plugin.
    /// Plugin-defined performance indicators.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = [];
}