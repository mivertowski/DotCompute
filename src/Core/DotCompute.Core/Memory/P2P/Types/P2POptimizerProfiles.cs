// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// P2P optimization profile for device pairs.
/// </summary>
/// <remarks>
/// <para>
/// Contains learned optimization parameters for specific GPU pair transfers.
/// Profiles are adaptive and continuously updated based on actual performance.
/// </para>
/// <para>
/// Internal type used by <see cref="P2POptimizer"/> for optimization state management.
/// </para>
/// </remarks>
internal sealed class P2POptimizationProfile
{
    /// <summary>
    /// Gets or sets the source device identifier.
    /// </summary>
    public required string SourceDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the target device identifier.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Gets or sets the P2P capability for this device pair.
    /// </summary>
    public required P2PConnectionCapability P2PCapability { get; init; }

    /// <summary>
    /// Gets or sets the optimal chunk size in bytes.
    /// </summary>
    /// <remarks>
    /// Dynamically adjusted based on observed performance. Typical range: 1MB-64MB.
    /// </remarks>
    public int OptimalChunkSize { get; set; }

    /// <summary>
    /// Gets or sets the optimal pipeline depth for pipelined transfers.
    /// </summary>
    /// <remarks>
    /// Number of concurrent transfers. Typical range: 1-8.
    /// </remarks>
    public int OptimalPipelineDepth { get; set; }

    /// <summary>
    /// Gets or sets the preferred transfer strategy for this device pair.
    /// </summary>
    public P2PTransferStrategy PreferredStrategy { get; set; }

    /// <summary>
    /// Gets or sets the bandwidth utilization (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Tracks how efficiently the available bandwidth is being used.
    /// </remarks>
    public double BandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets the efficiency score (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Ratio of actual to estimated performance. Calculated using exponential moving average.
    /// </remarks>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Gets or sets the overall optimization score (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Composite score based on bandwidth, efficiency, and connection quality.
    /// </remarks>
    public double OptimizationScore { get; set; }

    /// <summary>
    /// Gets or sets when this profile was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// Transfer history for optimization learning.
/// </summary>
/// <remarks>
/// <para>
/// Maintains a sliding window of recent transfers for machine learning-based optimization.
/// History is capped at a configurable limit (default 1000 transfers).
/// </para>
/// </remarks>
internal sealed class P2PTransferHistory
{
    /// <summary>
    /// Gets or sets the device pair key (format: "sourceId_targetId").
    /// </summary>
    public required string DevicePairKey { get; init; }

    /// <summary>
    /// Gets or sets the list of transfer records.
    /// </summary>
    /// <remarks>
    /// Maintained as a sliding window. Oldest records are removed when limit is reached.
    /// </remarks>
    public required List<P2PTransferRecord> TransferRecords { get; init; }

    /// <summary>
    /// Gets or sets the total number of transfers performed.
    /// </summary>
    public long TotalTransfers { get; set; }

    /// <summary>
    /// Gets or sets the total bytes transferred across all operations.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the average throughput in GB/s.
    /// </summary>
    /// <remarks>
    /// Calculated from successful transfers only.
    /// </remarks>
    public double AverageThroughput { get; set; }
}

/// <summary>
/// Individual transfer record for history tracking.
/// </summary>
/// <remarks>
/// Immutable record of a single P2P transfer operation for optimization analysis.
/// </remarks>
internal sealed class P2PTransferRecord
{
    /// <summary>
    /// Gets or sets the transfer size in bytes.
    /// </summary>
    public required long TransferSize { get; init; }

    /// <summary>
    /// Gets or sets the strategy used for this transfer.
    /// </summary>
    public required P2PTransferStrategy Strategy { get; init; }

    /// <summary>
    /// Gets or sets the chunk size used in bytes.
    /// </summary>
    public required int ChunkSize { get; init; }

    /// <summary>
    /// Gets or sets the estimated transfer time in milliseconds.
    /// </summary>
    public required double EstimatedTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the actual transfer time in milliseconds.
    /// </summary>
    public required double ActualTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the achieved throughput in GB/s.
    /// </summary>
    public required double ThroughputGBps { get; init; }

    /// <summary>
    /// Gets or sets whether the transfer completed successfully.
    /// </summary>
    public required bool WasSuccessful { get; init; }

    /// <summary>
    /// Gets or sets when this transfer occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
