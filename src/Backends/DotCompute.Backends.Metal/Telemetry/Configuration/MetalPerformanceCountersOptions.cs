// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal performance counters
/// </summary>
public sealed class MetalPerformanceCountersOptions
{
    /// <summary>
    /// Gets or sets whether to enable continuous sampling
    /// </summary>
    public bool EnableContinuousSampling { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling interval
    /// </summary>
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the slow allocation threshold in milliseconds
    /// </summary>
    public double SlowAllocationThresholdMs { get; set; } = 50.0;

    /// <summary>
    /// Gets or sets the slow kernel threshold in milliseconds
    /// </summary>
    public double SlowKernelThresholdMs { get; set; } = 100.0;
}