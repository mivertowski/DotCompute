// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is kept for backward compatibility and will be removed in future versions.
// Use DotCompute.Abstractions.Types.MemoryAccessPattern instead.

using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Legacy alias for MemoryAccessPattern. Use DotCompute.Abstractions.Types.MemoryAccessPattern instead.
    /// </summary>
    [System.Obsolete("Use DotCompute.Abstractions.Types.MemoryAccessPattern instead. This alias will be removed in a future version.")]
    public enum MemoryAccessPattern
    {
        Sequential = Abstractions.Types.MemoryAccessPattern.Sequential,
        Strided = Abstractions.Types.MemoryAccessPattern.Strided,
        Random = Abstractions.Types.MemoryAccessPattern.Random,
        Coalesced = Abstractions.Types.MemoryAccessPattern.Coalesced,
        Broadcast = Abstractions.Types.MemoryAccessPattern.Broadcast
    }
}