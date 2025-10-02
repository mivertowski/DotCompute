// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Memory usage analysis result.
/// </summary>
public class MemoryUsageAnalysis
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the average memory usage.
    /// </summary>
    /// <value>The average memory usage.</value>
    public double AverageMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the peak memory usage.
    /// </summary>
    /// <value>The peak memory usage.</value>
    public long PeakMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the min memory usage.
    /// </summary>
    /// <value>The min memory usage.</value>
    public long MinMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the total memory allocated.
    /// </summary>
    /// <value>The total memory allocated.</value>
    public long TotalMemoryAllocated { get; set; }
    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    /// <value>The analysis time.</value>
    public DateTime AnalysisTime { get; set; }
}