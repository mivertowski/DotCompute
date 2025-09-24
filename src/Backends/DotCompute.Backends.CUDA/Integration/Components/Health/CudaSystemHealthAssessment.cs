// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// System health assessment result.
/// </summary>
public sealed class CudaSystemHealthAssessment
{
    public DateTimeOffset Timestamp { get; init; }
    public CudaHealthStatus OverallHealth { get; init; }
    public bool DeviceAccessible { get; init; }
    public CudaHealthStatus MemoryHealth { get; init; }
    public CudaHealthStatus ErrorRateHealth { get; init; }
    public CudaHealthStatus ContextHealth { get; init; }
    public Dictionary<string, HealthCheckResult> HealthChecks { get; init; } = [];
}