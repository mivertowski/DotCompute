// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// Health check result.
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; init; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public CudaHealthStatus Status { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string Message { get; init; } = string.Empty;
}
