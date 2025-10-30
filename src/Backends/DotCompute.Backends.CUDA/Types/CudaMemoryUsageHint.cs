// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Types
{
    /// <summary>
    /// Memory usage hints for optimization.
    /// </summary>
    public enum CudaMemoryUsageHint
    {
        /// <summary>
        /// Data is mostly read.
        /// </summary>
        ReadMostly,

        /// <summary>
        /// Data is mostly written.
        /// </summary>
        WriteMostly,

        /// <summary>
        /// Equal read/write access.
        /// </summary>
        ReadWrite,

        /// <summary>
        /// Short-lived temporary data.
        /// </summary>
        Temporary,

        /// <summary>
        /// Long-lived persistent data.
        /// </summary>
        Persistent,

        /// <summary>
        /// Data shared between devices.
        /// </summary>
        Shared
    }
}
