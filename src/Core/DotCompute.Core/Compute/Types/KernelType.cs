// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Compute.Types
{
    /// <summary>
    /// Enumeration of kernel types for optimization selection.
    /// Used to determine the most appropriate execution strategy for different computational patterns.
    /// </summary>
    internal enum KernelType
    {
        /// <summary>
        /// Generic kernel with no specific optimization pattern.
        /// </summary>
        Generic,

        /// <summary>
        /// Vector addition operation (element-wise addition of two arrays).
        /// </summary>
        VectorAdd,

        /// <summary>
        /// Vector multiplication operation (element-wise multiplication of two arrays).
        /// </summary>
        VectorMultiply,

        /// <summary>
        /// Vector scaling operation (multiplication of array by scalar).
        /// </summary>
        VectorScale,

        /// <summary>
        /// Matrix multiplication operation (dense matrix-matrix multiply).
        /// </summary>
        MatrixMultiply,

        /// <summary>
        /// Reduction operation (sum, min, max, etc. across array elements).
        /// </summary>
        Reduction,

        /// <summary>
        /// Memory-intensive operation with complex memory access patterns.
        /// </summary>
        MemoryIntensive,

        /// <summary>
        /// Compute-intensive operation with mathematical functions.
        /// </summary>
        ComputeIntensive
    }
}
