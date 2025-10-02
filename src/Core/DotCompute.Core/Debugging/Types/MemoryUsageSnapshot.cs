// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Memory usage snapshot at a specific point in time.
/// </summary>
public class MemoryUsageSnapshot
{
    public DateTime Timestamp { get; set; }
    public long TotalMemoryUsed { get; set; }
}