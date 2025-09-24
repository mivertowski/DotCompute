// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory.Types;

/// <summary>
/// Performance metrics for unified buffer operations.
/// </summary>
public record BufferPerformanceMetrics
{
    public long TransferCount { get; init; }
    public TimeSpan AverageTransferTime { get; init; }
    public DateTimeOffset LastAccessTime { get; init; }
    public long SizeInBytes { get; init; }
    public string AllocationSource { get; init; } = "Unknown";
    public double TransfersPerSecond => TransferCount > 0 && AverageTransferTime > TimeSpan.Zero
        ? 1.0 / AverageTransferTime.TotalSeconds : 0;
}