// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Options;

/// <summary>
/// Configuration options for distributed tracing functionality.
/// Provides settings for trace export limits, retention, and sampling.
/// </summary>
public sealed class DistributedTracingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of traces to export in a single batch.
    /// Default value is 1000 traces.
    /// </summary>
    /// <value>The maximum number of traces per export batch.</value>
    public int MaxTracesPerExport { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the trace retention period in hours.
    /// Traces older than this period may be purged from storage.
    /// Default value is 24 hours.
    /// </summary>
    /// <value>The trace retention period in hours.</value>
    public int TraceRetentionHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets a value indicating whether sampling is enabled.
    /// When enabled, only a percentage of traces are captured based on the sampling rate.
    /// Default value is true.
    /// </summary>
    /// <value>true if sampling is enabled; otherwise, false.</value>
    public bool EnableSampling { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling rate as a decimal value between 0.0 and 1.0.
    /// A value of 0.1 means 10% of traces will be sampled.
    /// Default value is 0.1 (10%).
    /// </summary>
    /// <value>The sampling rate as a decimal between 0.0 and 1.0.</value>
    public double SamplingRate { get; set; } = 0.1; // 10% sampling
}
