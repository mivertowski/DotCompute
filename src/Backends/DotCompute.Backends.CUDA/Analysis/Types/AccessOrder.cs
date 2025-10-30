// <copyright file="AccessOrder.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Represents memory access order enumeration.
/// </summary>
public enum AccessOrder
{
    /// <summary>
    /// Row-major access pattern.
    /// </summary>
    RowMajor,

    /// <summary>
    /// Column-major access pattern.
    /// </summary>
    ColumnMajor,

    /// <summary>
    /// Tiled access pattern.
    /// </summary>
    Tiled
}
