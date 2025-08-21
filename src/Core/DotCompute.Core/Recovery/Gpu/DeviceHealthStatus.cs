// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Represents the current health status and metrics for a specific GPU device.
/// Provides a snapshot of device performance and recovery statistics.
/// </summary>
/// <remarks>
/// This class serves as a data transfer object containing comprehensive
/// health information for a single GPU device, typically used in health
/// reports and monitoring dashboards.
/// </remarks>
public class DeviceHealthStatus
{
    /// <summary>
    /// Gets or sets the unique identifier of the GPU device.
    /// </summary>
    /// <value>A string identifier for the device, typically the device index or name.</value>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the device is currently healthy.
    /// </summary>
    /// <value><c>true</c> if the device is operating normally; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// A device is considered healthy based on its error rate, consecutive failures,
    /// and recent performance metrics.
    /// </remarks>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the current error rate for the device.
    /// </summary>
    /// <value>The number of errors per unit time (typically per hour).</value>
    /// <remarks>
    /// The error rate provides insight into the frequency of failures
    /// and helps identify problematic devices or patterns.
    /// </remarks>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the most recent error that occurred on this device.
    /// </summary>
    /// <value>The last exception that occurred, or <c>null</c> if no errors have been recorded.</value>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the current count of consecutive failures without successful recovery.
    /// </summary>
    /// <value>The number of consecutive failures.</value>
    /// <remarks>
    /// High consecutive failure counts indicate persistent issues with the device
    /// that may require more aggressive recovery strategies.
    /// </remarks>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets the total number of recovery attempts made for this device.
    /// </summary>
    /// <value>The cumulative count of all recovery attempts.</value>
    public int TotalRecoveryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful recovery operations for this device.
    /// </summary>
    /// <value>The count of recovery attempts that succeeded.</value>
    /// <remarks>
    /// This metric, combined with TotalRecoveryAttempts, provides the success rate
    /// for recovery operations on this specific device.
    /// </remarks>
    public int SuccessfulRecoveries { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last health check performed on this device.
    /// </summary>
    /// <value>The UTC timestamp when the device was last checked for health status.</value>
    public DateTimeOffset LastHealthCheck { get; set; }
}