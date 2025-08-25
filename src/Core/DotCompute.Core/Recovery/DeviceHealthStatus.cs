// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Health status information for a device
/// </summary>
public class DeviceHealthStatus
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public double ErrorRate { get; set; }
    public Exception? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalRecoveryAttempts { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public DateTimeOffset LastHealthCheck { get; set; }
}