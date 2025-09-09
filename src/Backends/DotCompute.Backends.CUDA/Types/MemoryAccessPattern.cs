// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Memory access pattern for optimization.
    /// </summary>
    public enum MemoryAccessPattern
    {
        Sequential,
        Strided,
        Random,
        Coalesced,
        Broadcast
    }
}