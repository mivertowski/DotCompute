// <copyright file="MemoryPressureInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Information about current memory pressure.
/// Provides detailed metrics about system memory status.
/// </summary>
public class MemoryPressureInfo
{
    /// <summary>
    /// Gets or sets the memory pressure level.
    /// Current severity of memory constraints.
    /// </summary>
    public MemoryPressureLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the pressure ratio.
    /// Percentage of memory in use (0.0 to 1.0).
    /// </summary>
    public double PressureRatio { get; set; }

    /// <summary>
    /// Gets or sets the total memory in bytes.
    /// Total system memory available.
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Gets or sets the available memory in bytes.
    /// Memory currently free for allocation.
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Gets or sets the used memory in bytes.
    /// Memory currently in use.
    /// </summary>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// When this pressure information was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the GC pressure.
    /// Ratio indicating garbage collection frequency.
    /// </summary>
    public double GCPressure { get; set; }

    /// <summary>
    /// Returns a string representation of the memory pressure.
    /// </summary>
    /// <returns>Summary of current memory pressure.</returns>
    public override string ToString()
        => $"Level={Level}, Pressure={PressureRatio:P1}, Available={AvailableMemory / 1024 / 1024}MB";
}