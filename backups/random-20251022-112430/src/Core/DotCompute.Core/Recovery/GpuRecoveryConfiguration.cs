// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Configuration for GPU recovery behavior
/// </summary>
public class GpuRecoveryConfiguration
{
    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    /// <value>The health check interval.</value>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>
    /// Gets or sets the default kernel timeout.
    /// </summary>
    /// <value>The default kernel timeout.</value>
    public TimeSpan DefaultKernelTimeout { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    /// <value>The retry delay.</value>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the max consecutive failures.
    /// </summary>
    /// <value>The max consecutive failures.</value>
    public int MaxConsecutiveFailures { get; set; } = 3;
    /// <summary>
    /// Gets or sets the max retry attempts.
    /// </summary>
    /// <value>The max retry attempts.</value>
    public int MaxRetryAttempts { get; set; } = 3;
    /// <summary>
    /// Gets or sets the enable auto recovery.
    /// </summary>
    /// <value>The enable auto recovery.</value>
    public bool EnableAutoRecovery { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable kernel timeout detection.
    /// </summary>
    /// <value>The enable kernel timeout detection.</value>
    public bool EnableKernelTimeoutDetection { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable memory pressure detection.
    /// </summary>
    /// <value>The enable memory pressure detection.</value>
    public bool EnableMemoryPressureDetection { get; set; } = true;
    /// <summary>
    /// Gets or sets the default.
    /// </summary>
    /// <value>The default.</value>

    public static GpuRecoveryConfiguration Default => new();
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()
        => $"HealthCheck={HealthCheckInterval}, KernelTimeout={DefaultKernelTimeout}, MaxFailures={MaxConsecutiveFailures}";
}