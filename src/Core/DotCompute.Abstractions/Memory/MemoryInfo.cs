// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Represents memory information for an accelerator device.
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// Gets or sets the total memory available on the device in bytes.
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Gets or sets the available memory on the device in bytes.
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Gets or sets the used memory on the device in bytes.
    /// </summary>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Gets the memory usage percentage (0-100).
    /// </summary>
    public double UsagePercentage => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;
}