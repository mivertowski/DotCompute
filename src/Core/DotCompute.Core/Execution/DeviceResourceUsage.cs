// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents resource usage metrics for a specific device during execution.
/// </summary>
public class DeviceResourceUsage
{
    public required string DeviceId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public long InitialMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public double AverageUtilization { get; set; }
}
