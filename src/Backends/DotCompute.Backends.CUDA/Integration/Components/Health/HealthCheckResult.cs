// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// Health check result.
/// </summary>
public sealed class HealthCheckResult
{
    public bool Success { get; init; }
    public CudaHealthStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}