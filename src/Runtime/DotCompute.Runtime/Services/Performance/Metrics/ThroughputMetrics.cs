// <copyright file="ThroughputMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Throughput performance metrics.
/// Measures data processing rates and operational throughput.
/// </summary>
public class ThroughputMetrics
{
    /// <summary>
    /// Gets operations per second.
    /// Rate of operation completion.
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets bytes processed per second.
    /// Data throughput rate.
    /// </summary>
    public double BytesPerSecond { get; init; }

    /// <summary>
    /// Gets the peak throughput.
    /// Maximum observed throughput rate.
    /// </summary>
    public double PeakThroughput { get; init; }

    /// <summary>
    /// Gets throughput by operation type.
    /// Breakdown of throughput rates by operation category.
    /// </summary>
    public Dictionary<string, double> ThroughputByOperation { get; init; } = [];
}