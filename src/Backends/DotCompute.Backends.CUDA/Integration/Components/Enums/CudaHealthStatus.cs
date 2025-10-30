// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Enums;
/// <summary>
/// An cuda health status enumeration.
/// </summary>

/// <summary>
/// CUDA health status indicators.
/// </summary>
public enum CudaHealthStatus
{
    /// <summary>
    /// CUDA device is operating normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// CUDA device has warnings but is still operational.
    /// </summary>
    Warning,

    /// <summary>
    /// CUDA device has critical issues affecting operation.
    /// </summary>
    Critical
}
