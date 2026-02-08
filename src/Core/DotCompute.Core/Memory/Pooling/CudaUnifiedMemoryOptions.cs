// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.Pooling;

/// <summary>
/// CUDA memory attach flags for unified memory allocation.
/// </summary>
[Flags]
public enum CudaMemAttachFlag
{
    /// <summary>
    /// No specific attachment.
    /// </summary>
    None = 0,

    /// <summary>
    /// Memory is accessible from any stream on any device.
    /// </summary>
    Global = 0x01,

    /// <summary>
    /// Memory is accessible only from streams on the allocating device.
    /// </summary>
    Host = 0x02,

    /// <summary>
    /// Memory is initially attached to a single stream.
    /// </summary>
    SingleStream = 0x04
}

/// <summary>
/// Configuration options for CUDA unified memory management.
/// </summary>
public sealed class CudaUnifiedMemoryOptions
{
    /// <summary>
    /// Gets or sets whether to use managed memory (cudaMallocManaged).
    /// </summary>
    public bool UseManagedMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable memory prefetching hints.
    /// </summary>
    public bool EnablePrefetching { get; set; } = true;

    /// <summary>
    /// Gets or sets the default attach policy for managed memory.
    /// </summary>
    public CudaMemAttachFlag DefaultAttachPolicy { get; set; } = CudaMemAttachFlag.Global;

    /// <summary>
    /// Gets or sets whether to enable read-only optimization hints.
    /// </summary>
    public bool EnableReadOnlyOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the prefetch threshold in bytes.
    /// Allocations below this size won't trigger prefetching.
    /// </summary>
    public long PrefetchThreshold { get; set; } = 4096; // 4 KB

    /// <summary>
    /// Gets or sets whether to prefetch to host on allocation.
    /// </summary>
    public bool PrefetchToHostOnAllocation { get; set; }

    /// <summary>
    /// Gets or sets whether to prefetch to device before kernel execution.
    /// </summary>
    public bool PrefetchToDeviceBeforeKernel { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory advice flags to set on allocations.
    /// </summary>
    public CudaMemAdvise MemoryAdvise { get; set; } = CudaMemAdvise.None;

    /// <summary>
    /// Gets or sets the target device ID for prefetching (-1 for current).
    /// </summary>
    public int PrefetchDeviceId { get; set; } = -1;
}

/// <summary>
/// CUDA memory advise flags for memory optimization hints.
/// </summary>
[Flags]
public enum CudaMemAdvise
{
    /// <summary>
    /// No memory advice.
    /// </summary>
    None = 0,

    /// <summary>
    /// Memory will be mostly read and rarely written by the specified device.
    /// </summary>
    SetReadMostly = 1,

    /// <summary>
    /// Unset the read-mostly hint.
    /// </summary>
    UnsetReadMostly = 2,

    /// <summary>
    /// Set preferred location to the specified device.
    /// </summary>
    SetPreferredLocation = 3,

    /// <summary>
    /// Unset the preferred location hint.
    /// </summary>
    UnsetPreferredLocation = 4,

    /// <summary>
    /// Data will be accessed by the specified device.
    /// </summary>
    SetAccessedBy = 5,

    /// <summary>
    /// Unset the accessed-by hint.
    /// </summary>
    UnsetAccessedBy = 6
}
