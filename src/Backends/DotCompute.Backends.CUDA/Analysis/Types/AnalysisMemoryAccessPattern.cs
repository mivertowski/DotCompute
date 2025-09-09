// <copyright file="AnalysisMemoryAccessPattern.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Enumeration of memory access patterns.
/// </summary>
public enum MemoryAccessPattern
{
    /// <summary>
    /// Sequential access pattern.
    /// </summary>
    Sequential,

    /// <summary>
    /// Strided access pattern.
    /// </summary>
    Strided,

    /// <summary>
    /// Random access pattern.
    /// </summary>
    Random,

    /// <summary>
    /// Broadcast access pattern.
    /// </summary>
    Broadcast,

    /// <summary>
    /// Scattered access pattern.
    /// </summary>
    Scattered,

    /// <summary>
    /// Unknown access pattern.
    /// </summary>
    Unknown
}