// <copyright file="ConcurrentTransferResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Memory.Types;

/// <summary>
/// Represents the result of concurrent memory transfer operations.
/// </summary>
/// <remarks>
/// This class provides comprehensive metrics and statistics for multiple concurrent transfer operations,
/// including individual transfer results, aggregate performance metrics, and error tracking.
/// </remarks>
public class ConcurrentTransferResult
{
    /// <summary>
    /// Gets or sets a value indicating whether all transfers succeeded.
    /// </summary>
    /// <value>True if all transfers completed successfully; otherwise, false.</value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the start time of the concurrent transfer operation.
    /// </summary>
    /// <value>The UTC timestamp when the concurrent transfers began.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration of all concurrent transfers.
    /// </summary>
    /// <value>The time taken to complete all transfers.</value>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the number of transfers executed.
    /// </summary>
    /// <value>The total number of transfer operations.</value>
    public int TransferCount { get; set; }

    /// <summary>
    /// Gets or sets the number of successful transfers.
    /// </summary>
    /// <value>The count of transfers that completed without errors.</value>
    public int SuccessfulTransfers { get; set; }

    /// <summary>
    /// Gets or sets the number of failed transfers.
    /// </summary>
    /// <value>The count of transfers that encountered errors.</value>
    public int FailedTransfers { get; set; }

    /// <summary>
    /// Gets or sets the total bytes transferred across all operations.
    /// </summary>
    /// <value>The aggregate size of all data transferred in bytes.</value>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the aggregate throughput in megabytes per second.
    /// </summary>
    /// <value>The combined throughput of all concurrent transfers in MB/s.</value>
    public double AggregateThroughputMBps { get; set; }

    /// <summary>
    /// Gets or sets the average throughput per transfer in megabytes per second.
    /// </summary>
    /// <value>The average throughput across all transfers in MB/s.</value>
    public double AverageThroughputMBps { get; set; }

    /// <summary>
    /// Gets or sets the peak throughput achieved in megabytes per second.
    /// </summary>
    /// <value>The maximum throughput observed during the concurrent transfers in MB/s.</value>
    public double PeakThroughputMBps { get; set; }

    /// <summary>
    /// Gets or sets the minimum throughput observed in megabytes per second.
    /// </summary>
    /// <value>The minimum throughput observed during the concurrent transfers in MB/s.</value>
    public double MinThroughputMBps { get; set; }

    /// <summary>
    /// Gets or sets the list of individual transfer results.
    /// </summary>
    /// <value>A list containing the result of each individual transfer operation.</value>
    public List<AdvancedTransferResult> IndividualResults { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of errors encountered during transfers.
    /// </summary>
    /// <value>A list of error messages from failed transfers.</value>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum concurrency level achieved.
    /// </summary>
    /// <value>The maximum number of transfers running simultaneously.</value>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// Gets or sets the average concurrency level.
    /// </summary>
    /// <value>The average number of transfers running simultaneously.</value>
    public double AverageConcurrency { get; set; }

    /// <summary>
    /// Gets or sets the memory pressure during the transfers.
    /// </summary>
    /// <value>A value between 0 and 1 indicating peak memory pressure.</value>
    public double PeakMemoryPressure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether memory mapping was used.
    /// </summary>
    /// <value>True if any transfer used memory mapping; otherwise, false.</value>
    public bool UsedMemoryMapping { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether streaming was used.
    /// </summary>
    /// <value>True if any transfer used streaming; otherwise, false.</value>
    public bool UsedStreaming { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compression was used.
    /// </summary>
    /// <value>True if any transfer used compression; otherwise, false.</value>
    public bool UsedCompression { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the concurrent transfers.
    /// </summary>
    /// <value>A dictionary containing additional transfer metadata.</value>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the concurrency benefit ratio.
    /// </summary>
    /// <value>A value indicating the benefit gained from running transfers concurrently.</value>
    public double ConcurrencyBenefit { get; set; }

    /// <summary>
    /// Gets or sets the total bytes transferred (alias for TotalBytesTransferred).
    /// </summary>
    /// <value>The aggregate size of all data transferred in bytes.</value>
    public long TotalBytes
    {
        get => TotalBytesTransferred;
        set => TotalBytesTransferred = value;
    }

    /// <summary>
    /// Gets or sets the list of individual transfer results (alias for IndividualResults).
    /// </summary>
    /// <value>A read-only list containing the result of each individual transfer operation.</value>
    public IReadOnlyList<AdvancedTransferResult> Results
    {
        get => IndividualResults;
        set => IndividualResults = value?.ToList() ?? [];
    }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    /// <value>The percentage of successful transfers (0-100).</value>
    public double SuccessRate => TransferCount > 0 ? (SuccessfulTransfers * 100.0) / TransferCount : 0;

    /// <summary>
    /// Gets the efficiency ratio of the concurrent transfers.
    /// </summary>
    /// <value>A value indicating how efficiently the concurrent transfers utilized available resources.</value>
    public double EfficiencyRatio => AverageConcurrency > 0 ? AggregateThroughputMBps / (AverageThroughputMBps * AverageConcurrency) : 0;

    /// <summary>
    /// Gets a formatted summary of the concurrent transfer results.
    /// </summary>
    /// <returns>A human-readable summary of the concurrent transfer operations.</returns>
    public override string ToString()
    {
        var totalGB = TotalBytesTransferred / (1024.0 * 1024.0 * 1024.0);


        return $"Concurrent Transfer Result: {TransferCount} transfers, " +
               $"{totalGB:F2} GB total in {Duration.TotalSeconds:F2}s " +
               $"(Aggregate: {AggregateThroughputMBps:F2} MB/s, Average: {AverageThroughputMBps:F2} MB/s) - " +
               $"Success Rate: {SuccessRate:F1}%, " +
               $"Max Concurrency: {MaxConcurrency}, " +
               $"Efficiency: {EfficiencyRatio:F2}";
    }
}