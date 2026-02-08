// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Profiling.Types;

/// <summary>
/// Types of memory transfer operations for profiling classification.
/// </summary>
public enum MemoryTransferType
{
    /// <summary>Host to device memory transfer.</summary>
    HostToDevice,

    /// <summary>Device to host memory transfer.</summary>
    DeviceToHost,

    /// <summary>Device to device memory transfer (peer-to-peer).</summary>
    DeviceToDevice,

    /// <summary>Unknown or unclassified transfer type.</summary>
    Unknown
}
