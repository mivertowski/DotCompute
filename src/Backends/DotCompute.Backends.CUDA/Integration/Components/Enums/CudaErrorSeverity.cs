// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Enums;
/// <summary>
/// An cuda error severity enumeration.
/// </summary>

/// <summary>
/// CUDA error severity levels.
/// </summary>
public enum CudaErrorSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical,
    Unknown
}