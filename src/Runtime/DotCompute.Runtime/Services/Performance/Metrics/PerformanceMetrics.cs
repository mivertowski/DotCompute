// <copyright file="PerformanceMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Services.Performance.Types;

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Comprehensive aggregated performance metrics collection.
/// Contains aggregated performance data for a specific time period.
/// </summary>
public class AggregatedPerformanceMetrics
{
    /// <summary>
    /// Gets the time period for these metrics.
    /// Defines the temporal bounds of the collected data.
    /// </summary>
    public required TimeRange Period { get; init; }

    /// <summary>
    /// Gets operation-specific metrics.
    /// Performance data broken down by individual operations.
    /// </summary>
    public Dictionary<string, OperationMetrics> Operations { get; init; } = [];

    /// <summary>
    /// Gets system-wide metrics.
    /// Overall system performance indicators.
    /// </summary>
    public SystemMetrics System { get; init; } = new();

    /// <summary>
    /// Gets memory metrics.
    /// Memory usage and allocation statistics.
    /// </summary>
    public MemoryMetrics Memory { get; init; } = new();

    /// <summary>
    /// Gets throughput metrics.
    /// Data processing rate measurements.
    /// </summary>
    public ThroughputMetrics Throughput { get; init; } = new();
}
