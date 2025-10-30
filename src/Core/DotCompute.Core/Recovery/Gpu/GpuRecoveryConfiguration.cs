// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Configuration settings that control GPU recovery behavior and policies.
/// Defines timeouts, retry limits, and feature toggles for the recovery system.
/// </summary>
/// <remarks>
/// This configuration class allows fine-tuning of the recovery system's behavior
/// to match the requirements of different applications and environments.
/// </remarks>
public class GpuRecoveryConfiguration
{
    /// <summary>
    /// Gets or sets the interval between periodic health checks of GPU devices.
    /// </summary>
    /// <value>The time interval between health checks. Default is 1 minute.</value>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the default timeout for kernel execution before considering it hung.
    /// </summary>
    /// <value>The maximum time to wait for kernel completion. Default is 5 minutes.</value>
    public TimeSpan DefaultKernelTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the delay between retry attempts during recovery operations.
    /// </summary>
    /// <value>The time to wait before retrying a failed operation. Default is 100 milliseconds.</value>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum number of consecutive failures before marking a device as unhealthy.
    /// </summary>
    /// <value>The threshold for consecutive failures. Default is 3.</value>
    public int MaxConsecutiveFailures { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for a single operation.
    /// </summary>
    /// <value>The maximum retry count per operation. Default is 3.</value>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether automatic recovery is enabled.
    /// </summary>
    /// <value><c>true</c> if automatic recovery should be performed; otherwise, <c>false</c>. Default is <c>true</c>.</value>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether kernel timeout detection is enabled.
    /// </summary>
    /// <value><c>true</c> if kernel timeouts should be detected and handled; otherwise, <c>false</c>. Default is <c>true</c>.</value>
    public bool EnableKernelTimeoutDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether memory pressure detection is enabled.
    /// </summary>
    /// <value><c>true</c> if memory pressure should be monitored; otherwise, <c>false</c>. Default is <c>true</c>.</value>
    public bool EnableMemoryPressureDetection { get; set; } = true;

    /// <summary>
    /// Gets a default configuration instance with standard settings.
    /// </summary>
    /// <value>A new instance with default configuration values.</value>
    public static GpuRecoveryConfiguration Default => new();

    /// <summary>
    /// Returns a string representation of the key configuration settings.
    /// </summary>
    /// <returns>A formatted string containing the main configuration values.</returns>
    public override string ToString()
        => $"HealthCheck={HealthCheckInterval}, KernelTimeout={DefaultKernelTimeout}, MaxFailures={MaxConsecutiveFailures}";
}
