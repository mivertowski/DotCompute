// <copyright file="IPerformanceProfiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Services.Performance.Metrics;
using DotCompute.Runtime.Services.Performance.Results;
using DotCompute.Runtime.Services.Performance.Types;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for profiling performance across the DotCompute runtime.
/// Provides comprehensive performance monitoring and analysis capabilities.
/// </summary>
public interface IPerformanceProfiler
{
    /// <summary>
    /// Starts profiling for a specific operation.
    /// Creates a new profiling session to track performance metrics.
    /// </summary>
    /// <param name="operationName">The name of the operation to profile.</param>
    /// <param name="metadata">Additional metadata about the operation.</param>
    /// <returns>A profiling session token for tracking the operation.</returns>
    public IProfilingSession StartProfiling(string operationName, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets performance metrics for a specific time period.
    /// Retrieves aggregated performance data within the specified range.
    /// </summary>
    /// <param name="startTime">The start time of the period.</param>
    /// <param name="endTime">The end time of the period.</param>
    /// <returns>Performance metrics for the specified period.</returns>
    public Task<AggregatedPerformanceMetrics> GetMetricsAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// Gets real-time performance data.
    /// Provides current performance metrics and system status.
    /// </summary>
    /// <returns>Current real-time performance data.</returns>
    public Task<RealTimePerformanceData> GetRealTimeDataAsync();

    /// <summary>
    /// Exports performance data to a file.
    /// Saves collected performance metrics in the specified format.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="format">The export format for the data.</param>
    /// <returns>A task representing the export operation.</returns>
    public Task ExportDataAsync(string filePath, PerformanceExportFormat format);

    /// <summary>
    /// Gets performance summary for all operations.
    /// Provides an overview of system performance across all tracked operations.
    /// </summary>
    /// <returns>Comprehensive performance summary.</returns>
    public Task<PerformanceSummary> GetSummaryAsync();
}