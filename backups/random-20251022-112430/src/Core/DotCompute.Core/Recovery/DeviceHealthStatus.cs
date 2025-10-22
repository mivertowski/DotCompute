// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Health status information for a device
/// </summary>
public class DeviceHealthStatus
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool IsHealthy { get; set; }
    /// <summary>
    /// Gets or sets the error rate.
    /// </summary>
    /// <value>The error rate.</value>
    public double ErrorRate { get; set; }
    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    /// <value>The last error.</value>
    public Exception? LastError { get; set; }
    /// <summary>
    /// Gets or sets the consecutive failures.
    /// </summary>
    /// <value>The consecutive failures.</value>
    public int ConsecutiveFailures { get; set; }
    /// <summary>
    /// Gets or sets the total recovery attempts.
    /// </summary>
    /// <value>The total recovery attempts.</value>
    public int TotalRecoveryAttempts { get; set; }
    /// <summary>
    /// Gets or sets the successful recoveries.
    /// </summary>
    /// <value>The successful recoveries.</value>
    public int SuccessfulRecoveries { get; set; }
    /// <summary>
    /// Gets or sets the last health check.
    /// </summary>
    /// <value>The last health check.</value>
    public DateTimeOffset LastHealthCheck { get; set; }
}