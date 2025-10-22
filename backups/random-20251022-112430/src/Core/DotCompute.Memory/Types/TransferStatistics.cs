// <copyright file="TransferStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Memory.Types;

/// <summary>
/// Comprehensive transfer statistics for monitoring memory transfer operations.
/// </summary>
/// <remarks>
/// This class provides detailed statistics about memory transfer operations,
/// including throughput metrics, transfer counts, and system resource utilization.
/// </remarks>
public class TransferStatistics
{
    /// <summary>
    /// Gets or sets the total bytes transferred.
    /// </summary>
    /// <value>The cumulative number of bytes transferred across all operations.</value>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the total transfer count.
    /// </summary>
    /// <value>The total number of transfer operations completed.</value>
    public long TotalTransferCount { get; set; }

    /// <summary>
    /// Gets or sets the average size of the transfer.
    /// </summary>
    /// <value>The mean size of transfer operations in bytes.</value>
    public long AverageTransferSize { get; set; }

    /// <summary>
    /// Gets or sets the current memory pressure.
    /// </summary>
    /// <value>A value between 0 and 1 indicating the current memory pressure (1 = maximum pressure).</value>
    public double CurrentMemoryPressure { get; set; }

    /// <summary>
    /// Gets or sets the active transfers.
    /// </summary>
    /// <value>The number of transfer operations currently in progress.</value>
    public int ActiveTransfers { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage during transfers.
    /// </summary>
    /// <value>The maximum memory usage observed during transfer operations in bytes.</value>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak transfer rate achieved.
    /// </summary>
    /// <value>The maximum transfer rate observed in bytes per second.</value>
    public double PeakTransferRate { get; set; }

    /// <summary>
    /// Gets or sets the average transfer rate.
    /// </summary>
    /// <value>The mean transfer rate across all operations in bytes per second.</value>
    public double AverageTransferRate { get; set; }

    /// <summary>
    /// Gets or sets the number of failed transfers.
    /// </summary>
    /// <value>The count of transfer operations that encountered errors.</value>
    public long FailedTransferCount { get; set; }

    /// <summary>
    /// Gets or sets the total retry count across all transfers.
    /// </summary>
    /// <value>The cumulative number of retry attempts made.</value>
    public long TotalRetryCount { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    /// <value>The percentage of successful transfers (0-100).</value>
    public double SuccessRate => TotalTransferCount > 0
        ? ((TotalTransferCount - FailedTransferCount) * 100.0) / TotalTransferCount
        : 0;

    /// <summary>
    /// Gets the failure rate as a percentage.
    /// </summary>
    /// <value>The percentage of failed transfers (0-100).</value>
    public double FailureRate => TotalTransferCount > 0
        ? (FailedTransferCount * 100.0) / TotalTransferCount
        : 0;

    /// <summary>
    /// Resets all statistics to their initial values.
    /// </summary>
    public void Reset()
    {
        TotalBytesTransferred = 0;
        TotalTransferCount = 0;
        AverageTransferSize = 0;
        CurrentMemoryPressure = 0;
        ActiveTransfers = 0;
        PeakMemoryUsage = 0;
        PeakTransferRate = 0;
        AverageTransferRate = 0;
        FailedTransferCount = 0;
        TotalRetryCount = 0;
    }

    /// <summary>
    /// Gets a formatted summary of the transfer statistics.
    /// </summary>
    /// <returns>A human-readable summary of the transfer statistics.</returns>
    public override string ToString()
    {
        var totalGB = TotalBytesTransferred / (1024.0 * 1024.0 * 1024.0);
        var avgMB = AverageTransferSize / (1024.0 * 1024.0);
        var peakMBps = PeakTransferRate / (1024.0 * 1024.0);
        var avgMBps = AverageTransferRate / (1024.0 * 1024.0);

        return $"Transfer Statistics: {TotalTransferCount} transfers, " +
               $"{totalGB:F2} GB total, " +
               $"Avg size: {avgMB:F2} MB, " +
               $"Success rate: {SuccessRate:F1}%, " +
               $"Peak rate: {peakMBps:F2} MB/s, " +
               $"Avg rate: {avgMBps:F2} MB/s, " +
               $"Active: {ActiveTransfers}, " +
               $"Memory pressure: {CurrentMemoryPressure:F2}";
    }
}