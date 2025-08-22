// <copyright file="RecoveryStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Contains comprehensive statistics about recovery operations.
/// </summary>
public class RecoveryStatistics
{
    /// <summary>
    /// Gets or sets the total number of recovery attempts.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful recoveries.
    /// </summary>
    public int SuccessfulRecoveries { get; set; }

    /// <summary>
    /// Gets or sets the number of failed recoveries.
    /// </summary>
    public int FailedRecoveries { get; set; }

    /// <summary>
    /// Gets or sets the number of partial recoveries.
    /// </summary>
    public int PartialRecoveries { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 
        ? (SuccessfulRecoveries * 100.0) / TotalAttempts 
        : 0;

    /// <summary>
    /// Gets or sets the average recovery time.
    /// </summary>
    public TimeSpan AverageRecoveryTime { get; set; }

    /// <summary>
    /// Gets or sets the fastest recovery time.
    /// </summary>
    public TimeSpan FastestRecoveryTime { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Gets or sets the slowest recovery time.
    /// </summary>
    public TimeSpan SlowestRecoveryTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the last recovery timestamp.
    /// </summary>
    public DateTimeOffset? LastRecoveryTime { get; set; }

    /// <summary>
    /// Gets or sets the recovery type statistics.
    /// </summary>
    public Dictionary<string, int> RecoveryTypeCount { get; set; } = new();

    /// <summary>
    /// Gets or sets the error type statistics.
    /// </summary>
    public Dictionary<string, int> ErrorTypeCount { get; set; } = new();

    /// <summary>
    /// Gets or sets the total downtime.
    /// </summary>
    public TimeSpan TotalDowntime { get; set; }

    /// <summary>
    /// Gets or sets the total uptime after recovery.
    /// </summary>
    public TimeSpan TotalUptime { get; set; }

    /// <summary>
    /// Gets the availability percentage.
    /// </summary>
    public double AvailabilityPercentage
    {
        get
        {
            var totalTime = TotalUptime + TotalDowntime;
            return totalTime > TimeSpan.Zero 
                ? (TotalUptime.TotalSeconds * 100.0) / totalTime.TotalSeconds 
                : 100.0;
        }
    }

    /// <summary>
    /// Gets or sets the mean time between failures (MTBF).
    /// </summary>
    public TimeSpan MeanTimeBetweenFailures { get; set; }

    /// <summary>
    /// Gets or sets the mean time to recovery (MTTR).
    /// </summary>
    public TimeSpan MeanTimeToRecovery { get; set; }

    /// <summary>
    /// Updates the statistics with a new recovery result.
    /// </summary>
    /// <param name="success">Whether the recovery was successful.</param>
    /// <param name="duration">The duration of the recovery.</param>
    /// <param name="recoveryType">The type of recovery performed.</param>
    /// <param name="errorType">The type of error if recovery failed.</param>
    public void UpdateStatistics(bool success, TimeSpan duration, string recoveryType, string? errorType = null)
    {
        TotalAttempts++;
        
        if (success)
        {
            SuccessfulRecoveries++;
        }
        else
        {
            FailedRecoveries++;
            if (!string.IsNullOrEmpty(errorType))
            {
                if (!ErrorTypeCount.ContainsKey(errorType))
                {
                    ErrorTypeCount[errorType] = 0;
                }
                ErrorTypeCount[errorType]++;
            }
        }

        if (!RecoveryTypeCount.ContainsKey(recoveryType))
        {
            RecoveryTypeCount[recoveryType] = 0;
        }
        RecoveryTypeCount[recoveryType]++;

        // Update timing statistics
        if (duration < FastestRecoveryTime)
        {
            FastestRecoveryTime = duration;
        }
        
        if (duration > SlowestRecoveryTime)
        {
            SlowestRecoveryTime = duration;
        }

        // Update average (simple running average)
        var totalSeconds = AverageRecoveryTime.TotalSeconds * (TotalAttempts - 1) + duration.TotalSeconds;
        AverageRecoveryTime = TimeSpan.FromSeconds(totalSeconds / TotalAttempts);

        LastRecoveryTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void Reset()
    {
        TotalAttempts = 0;
        SuccessfulRecoveries = 0;
        FailedRecoveries = 0;
        PartialRecoveries = 0;
        AverageRecoveryTime = TimeSpan.Zero;
        FastestRecoveryTime = TimeSpan.MaxValue;
        SlowestRecoveryTime = TimeSpan.Zero;
        LastRecoveryTime = null;
        RecoveryTypeCount.Clear();
        ErrorTypeCount.Clear();
        TotalDowntime = TimeSpan.Zero;
        TotalUptime = TimeSpan.Zero;
        MeanTimeBetweenFailures = TimeSpan.Zero;
        MeanTimeToRecovery = TimeSpan.Zero;
    }
}