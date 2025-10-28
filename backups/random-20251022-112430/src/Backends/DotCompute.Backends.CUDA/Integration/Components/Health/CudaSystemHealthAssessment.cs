// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// System health assessment result.
/// </summary>
public sealed class CudaSystemHealthAssessment
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the overall health.
    /// </summary>
    /// <value>The overall health.</value>
    public CudaHealthStatus OverallHealth { get; init; }
    /// <summary>
    /// Gets or sets the device accessible.
    /// </summary>
    /// <value>The device accessible.</value>
    public bool DeviceAccessible { get; init; }
    /// <summary>
    /// Gets or sets the memory health.
    /// </summary>
    /// <value>The memory health.</value>
    public CudaHealthStatus MemoryHealth { get; init; }
    /// <summary>
    /// Gets or sets the error rate health.
    /// </summary>
    /// <value>The error rate health.</value>
    public CudaHealthStatus ErrorRateHealth { get; init; }
    /// <summary>
    /// Gets or sets the context health.
    /// </summary>
    /// <value>The context health.</value>
    public CudaHealthStatus ContextHealth { get; init; }
    /// <summary>
    /// Gets or sets the health checks.
    /// </summary>
    /// <value>The health checks.</value>
    public Dictionary<string, HealthCheckResult> HealthChecks { get; init; } = [];
}