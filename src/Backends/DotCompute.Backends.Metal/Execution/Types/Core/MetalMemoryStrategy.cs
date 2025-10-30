// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Defines memory allocation strategies for Metal resources
/// </summary>
public enum MetalMemoryStrategy
{
    /// <summary>
    /// Default Metal memory allocation
    /// </summary>
    Default,

    /// <summary>
    /// Optimized for Apple Silicon unified memory
    /// </summary>
    UnifiedMemory,

    /// <summary>
    /// Optimized for discrete GPU memory
    /// </summary>
    DiscreteGpu,

    /// <summary>
    /// Use shared memory when possible
    /// </summary>
    SharedMemory,

    /// <summary>
    /// Private GPU memory only
    /// </summary>
    PrivateMemory,

    /// <summary>
    /// Managed memory with automatic synchronization
    /// </summary>
    ManagedMemory,

    /// <summary>
    /// Balanced memory allocation strategy
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive memory allocation strategy
    /// </summary>
    Aggressive
}
