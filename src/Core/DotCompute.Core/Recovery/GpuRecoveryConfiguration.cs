// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Configuration for GPU recovery behavior
/// </summary>
public class GpuRecoveryConfiguration
{
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan DefaultKernelTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public int MaxConsecutiveFailures { get; set; } = 3;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableAutoRecovery { get; set; } = true;
    public bool EnableKernelTimeoutDetection { get; set; } = true;
    public bool EnableMemoryPressureDetection { get; set; } = true;

    public static GpuRecoveryConfiguration Default => new();

    public override string ToString()
        => $"HealthCheck={HealthCheckInterval}, KernelTimeout={DefaultKernelTimeout}, MaxFailures={MaxConsecutiveFailures}";
}