// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Defines the memory access order patterns for 2D data structures.
    /// </summary>
    public enum AccessOrder
    {
        /// <summary>
        /// Row-major order where consecutive elements in a row are stored contiguously.
        /// Optimal for accessing data row by row.
        /// </summary>
        RowMajor,

        /// <summary>
        /// Column-major order where consecutive elements in a column are stored contiguously.
        /// Optimal for accessing data column by column.
        /// </summary>
        ColumnMajor,

        /// <summary>
        /// Tiled access pattern where data is accessed in tile blocks.
        /// Can optimize cache locality for 2D operations.
        /// </summary>
        Tiled
    }
}