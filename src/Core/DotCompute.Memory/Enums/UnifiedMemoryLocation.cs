// <copyright file="UnifiedMemoryLocation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Memory.Enums;

/// <summary>
/// Specifies the preferred memory location for unified memory allocations.
/// </summary>
public enum UnifiedMemoryLocation
{
    /// <summary>
    /// Let the system decide the best location.
    /// </summary>
    Auto,

    /// <summary>
    /// Prefer host (CPU) memory.
    /// </summary>
    Host,

    /// <summary>
    /// Prefer device (GPU) memory.
    /// </summary>
    Device,

    /// <summary>
    /// Use shared memory if available.
    /// </summary>
    Shared,

    /// <summary>
    /// Use managed memory with automatic migration.
    /// </summary>
    Managed
}
