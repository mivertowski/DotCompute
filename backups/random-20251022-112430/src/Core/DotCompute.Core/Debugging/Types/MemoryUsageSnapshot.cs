// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Memory usage snapshot at a specific point in time.
/// </summary>
public class MemoryUsageSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the total memory used.
    /// </summary>
    /// <value>The total memory used.</value>
    public long TotalMemoryUsed { get; set; }
}