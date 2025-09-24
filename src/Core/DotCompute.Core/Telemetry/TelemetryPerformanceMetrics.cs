// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Performance metrics for telemetry operations.
/// </summary>
public sealed class TelemetryPerformanceMetrics
{
    public long TotalOperations { get; init; }
    public long TotalErrors { get; init; }
    public long OverheadTicks { get; init; }
    public double OverheadPercentage { get; init; }
    public int EventQueueSize { get; init; }
    public int ActiveMetrics { get; init; }
    public string BackendType { get; init; } = string.Empty;
}