// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Throughput performance metrics for pipeline execution.
/// Measures data processing rates and operational throughput.
/// </summary>
public sealed class ThroughputMetrics
{
    /// <summary>
    /// Gets or sets operations per second.
    /// Rate of operation completion.
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets bytes processed per second.
    /// Data throughput rate.
    /// </summary>
    public double BytesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the peak throughput.
    /// Maximum observed throughput rate.
    /// </summary>
    public double PeakThroughput { get; set; }

    /// <summary>
    /// Gets or sets memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets compute performance in GFLOPS.
    /// </summary>
    public double ComputePerformance { get; set; }

    /// <summary>
    /// Gets or sets throughput by operation type.
    /// Breakdown of throughput rates by operation category.
    /// </summary>
    public Dictionary<string, double> ThroughputByOperation { get; } = [];
}