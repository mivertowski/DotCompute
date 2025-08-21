// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Provides comprehensive information about the current system memory pressure,
/// including usage statistics and pressure level classification.
/// </summary>
/// <remarks>
/// This class captures a snapshot of system memory state at a specific point in time,
/// including both managed and unmanaged memory usage. The pressure level and ratio
/// can be used to make informed decisions about memory recovery strategies.
/// </remarks>
public class MemoryPressureInfo
{
    /// <summary>
    /// Gets or sets the current memory pressure level classification.
    /// </summary>
    /// <value>
    /// A <see cref="MemoryPressureLevel"/> indicating the severity of memory pressure.
    /// </value>
    /// <remarks>
    /// This level is typically determined by comparing the pressure ratio
    /// against predefined thresholds to categorize the memory state.
    /// </remarks>
    public MemoryPressureLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the memory pressure ratio as a value between 0.0 and 1.0.
    /// </summary>
    /// <value>
    /// A double representing the fraction of total memory currently in use.
    /// </value>
    /// <remarks>
    /// This ratio is calculated as UsedMemory / TotalMemory. A value of 0.85
    /// indicates that 85% of available memory is currently being used.
    /// Values closer to 1.0 indicate higher memory pressure.
    /// </remarks>
    public double PressureRatio { get; set; }

    /// <summary>
    /// Gets or sets the total amount of system memory, in bytes.
    /// </summary>
    /// <value>
    /// The total memory available to the system.
    /// </value>
    /// <remarks>
    /// This represents the combined available and used memory.
    /// The calculation may vary based on the platform and available
    /// system information.
    /// </remarks>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Gets or sets the amount of available (free) memory, in bytes.
    /// </summary>
    /// <value>
    /// The memory currently available for allocation.
    /// </value>
    /// <remarks>
    /// This value represents memory that can be immediately allocated
    /// without triggering garbage collection or other recovery mechanisms.
    /// </remarks>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Gets or sets the amount of memory currently in use, in bytes.
    /// </summary>
    /// <value>
    /// The memory currently allocated and in use by the system.
    /// </value>
    /// <remarks>
    /// This includes both managed and unmanaged memory allocations.
    /// The value is typically calculated as TotalMemory - AvailableMemory.
    /// </remarks>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this memory pressure information was captured.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the snapshot was taken.
    /// </value>
    /// <remarks>
    /// This timestamp allows consumers to determine the age of the memory
    /// pressure information and decide whether to request updated data.
    /// </remarks>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the garbage collection pressure indicator.
    /// </summary>
    /// <value>
    /// A double representing the relative frequency of generation 2 garbage collections.
    /// </value>
    /// <remarks>
    /// This metric is calculated as the ratio of Gen2 collections to Gen0 collections.
    /// Higher values indicate that the garbage collector is working harder to
    /// reclaim memory, suggesting increased memory pressure in the managed heap.
    /// </remarks>
    public double GCPressure { get; set; }

    /// <summary>
    /// Returns a string representation of the memory pressure information.
    /// </summary>
    /// <returns>
    /// A formatted string containing the pressure level, pressure ratio,
    /// and available memory in megabytes.
    /// </returns>
    /// <example>
    /// "Level=High, Pressure=87.5%, Available=512MB"
    /// </example>
    public override string ToString()
        => $"Level={Level}, Pressure={PressureRatio:P1}, Available={AvailableMemory / 1024 / 1024}MB";
}