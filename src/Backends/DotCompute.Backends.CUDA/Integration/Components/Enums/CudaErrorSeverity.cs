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
    /// <summary>
    /// No error detected.
    /// </summary>
    None,

    /// <summary>
    /// Low severity error - informational only.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity error - may impact performance.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity error - significant impact on functionality.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity error - system failure or data loss possible.
    /// </summary>
    Critical,

    /// <summary>
    /// Unknown severity level.
    /// </summary>
    Unknown
}