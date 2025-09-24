// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Performance metric tracking for specific operations.
/// </summary>
internal sealed class PerformanceMetric
{
    public long Count { get; set; }
    public double TotalDuration { get; set; }
    public double MinDuration { get; set; } = double.MaxValue;
    public double MaxDuration { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public double AverageDuration => Count > 0 ? TotalDuration / Count : 0.0;
}