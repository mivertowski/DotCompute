// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA memory alignment requirements
    /// </summary>
    public static class CudaMemoryAlignment
    {
        /// <summary>
        /// The min alignment.
        /// </summary>
        public const int MinAlignment = 256; // 256 bytes minimum for coalesced access
        /// <summary>
        /// The optimal alignment.
        /// </summary>
        public const int OptimalAlignment = 512; // 512 bytes optimal for RTX 2000 series
        /// <summary>
        /// The texture alignment.
        /// </summary>
        public const int TextureAlignment = 512; // Texture memory alignment
        /// <summary>
        /// The surface alignment.
        /// </summary>
        public const int SurfaceAlignment = 512; // Surface memory alignment
        /// <summary>
        /// Gets align up.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="alignment">The alignment.</param>
        /// <returns>The result of the operation.</returns>

        public static long AlignUp(long size, int alignment = OptimalAlignment) => ((size + alignment - 1) / alignment) * alignment;
        /// <summary>
        /// Determines whether aligned.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="alignment">The alignment.</param>
        /// <returns>true if the condition is met; otherwise, false.</returns>

        public static bool IsAligned(long size, int alignment = OptimalAlignment) => size % alignment == 0;
    }
}