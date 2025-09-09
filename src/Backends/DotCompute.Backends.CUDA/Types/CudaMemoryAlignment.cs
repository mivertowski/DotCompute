// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA memory alignment requirements
    /// </summary>
    public static class CudaMemoryAlignment
    {
        public const int MinAlignment = 256; // 256 bytes minimum for coalesced access
        public const int OptimalAlignment = 512; // 512 bytes optimal for RTX 2000 series
        public const int TextureAlignment = 512; // Texture memory alignment
        public const int SurfaceAlignment = 512; // Surface memory alignment

        public static long AlignUp(long size, int alignment = OptimalAlignment) => ((size + alignment - 1) / alignment) * alignment;

        public static bool IsAligned(long size, int alignment = OptimalAlignment) => size % alignment == 0;
    }
}