// <copyright file="AnalysisMemoryAccessPattern.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// This file is kept for backward compatibility and will be removed in future versions.
// Use DotCompute.Abstractions.Types.MemoryAccessPattern instead.

using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Legacy alias for MemoryAccessPattern. Use DotCompute.Abstractions.Types.MemoryAccessPattern instead.
/// </summary>
[System.Obsolete("Use DotCompute.Abstractions.Types.MemoryAccessPattern instead. This alias will be removed in a future version.")]
public enum MemoryAccessPattern
{
    /// <summary>
    /// Sequential access pattern.
    /// </summary>
    Sequential = Abstractions.Types.MemoryAccessPattern.Sequential,

    /// <summary>
    /// Strided access pattern.
    /// </summary>
    Strided = Abstractions.Types.MemoryAccessPattern.Strided,

    /// <summary>
    /// Random access pattern.
    /// </summary>
    Random = Abstractions.Types.MemoryAccessPattern.Random,

    /// <summary>
    /// Broadcast access pattern.
    /// </summary>
    Broadcast = Abstractions.Types.MemoryAccessPattern.Broadcast,

    /// <summary>
    /// Scattered access pattern.
    /// </summary>
    Scattered = Abstractions.Types.MemoryAccessPattern.Scatter,

    /// <summary>
    /// Unknown access pattern.
    /// </summary>
    Unknown = Abstractions.Types.MemoryAccessPattern.Unknown
}