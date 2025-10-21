// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda memory advise enumeration.
    /// </summary>
    /// <summary>
    /// CUDA memory advise flags for unified memory
    /// </summary>
    public enum CudaMemoryAdvise
    {
        /// <summary>
        /// Advise that the memory is mostly read-only for optimization.
        /// </summary>
        SetReadMostly = 1,

        /// <summary>
        /// Remove the read-mostly hint from memory.
        /// </summary>
        UnsetReadMostly = 2,

        /// <summary>
        /// Set preferred memory location for the data.
        /// </summary>
        SetPreferredLocation = 3,

        /// <summary>
        /// Remove the preferred location setting.
        /// </summary>
        UnsetPreferredLocation = 4,

        /// <summary>
        /// Indicate which processor will access the memory.
        /// </summary>
        SetAccessedBy = 5,

        /// <summary>
        /// Remove the accessed-by hint for a processor.
        /// </summary>
        UnsetAccessedBy = 6
    }
    /// <summary>
    /// An cuda stream capture mode enumeration.
    /// </summary>

    /// <summary>
    /// CUDA stream capture modes
    /// </summary>
    public enum CudaStreamCaptureMode
    {
        /// <summary>
        /// Global capture mode that synchronizes all streams.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Thread-local capture mode.
        /// </summary>
        ThreadLocal = 1,

        /// <summary>
        /// Relaxed capture mode with fewer synchronization requirements.
        /// </summary>
        Relaxed = 2
    }
    /// <summary>
    /// An cuda graph instantiate flags enumeration.
    /// </summary>

    /// <summary>
    /// CUDA graph instantiate flags
    /// </summary>
    [Flags]
    public enum CudaGraphInstantiateFlags : uint
    {
        /// <summary>
        /// No special flags.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Automatically free memory on launch completion.
        /// </summary>
        AutoFreeOnLaunch = 0x01,

        /// <summary>
        /// Upload graph to device memory during instantiation.
        /// </summary>
        Upload = 0x02,

        /// <summary>
        /// Enable device-side kernel launch.
        /// </summary>
        DeviceLaunch = 0x04,

        /// <summary>
        /// Use per-node priority settings.
        /// </summary>
        UseNodePriority = 0x08
    }
    /// <summary>
    /// An cuda graph node type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA graph node type
    /// </summary>
    public enum CudaGraphNodeType
    {
        /// <summary>
        /// Kernel execution node.
        /// </summary>
        Kernel = 0x00,

        /// <summary>
        /// Memory copy node.
        /// </summary>
        Memcpy = 0x01,

        /// <summary>
        /// Memory set node.
        /// </summary>
        Memset = 0x02,

        /// <summary>
        /// Host function callback node.
        /// </summary>
        Host = 0x03,

        /// <summary>
        /// Child graph node.
        /// </summary>
        Graph = 0x04,

        /// <summary>
        /// Empty node for synchronization.
        /// </summary>
        Empty = 0x05,

        /// <summary>
        /// Wait for event node.
        /// </summary>
        WaitEvent = 0x06,

        /// <summary>
        /// Record event node.
        /// </summary>
        EventRecord = 0x07,

        /// <summary>
        /// External semaphore signal node.
        /// </summary>
        ExtSemasignal = 0x08,

        /// <summary>
        /// External semaphore wait node.
        /// </summary>
        ExtSemawait = 0x09,

        /// <summary>
        /// Memory allocation node.
        /// </summary>
        MemAlloc = 0x0a,

        /// <summary>
        /// Memory free node.
        /// </summary>
        MemFree = 0x0b,

        /// <summary>
        /// Batch memory operation node.
        /// </summary>
        BatchMemOp = 0x0c,

        /// <summary>
        /// Conditional execution node.
        /// </summary>
        Conditional = 0x0d
    }
    /// <summary>
    /// An cuda graph dependency type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA graph dependency type
    /// </summary>
    public enum CudaGraphDependencyType
    {
        /// <summary>
        /// Default dependency type based on graph structure.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Programmatic dependency explicitly defined.
        /// </summary>
        Programmatic = 1
    }
}
