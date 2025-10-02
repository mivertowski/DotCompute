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
        SetReadMostly = 1,
        UnsetReadMostly = 2,
        SetPreferredLocation = 3,
        UnsetPreferredLocation = 4,
        SetAccessedBy = 5,
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
        Global = 0,
        ThreadLocal = 1,
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
        None = 0x00,
        AutoFreeOnLaunch = 0x01,
        Upload = 0x02,
        DeviceLaunch = 0x04,
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
        Kernel = 0x00,
        Memcpy = 0x01,
        Memset = 0x02,
        Host = 0x03,
        Graph = 0x04,
        Empty = 0x05,
        WaitEvent = 0x06,
        EventRecord = 0x07,
        ExtSemasignal = 0x08,
        ExtSemawait = 0x09,
        MemAlloc = 0x0a,
        MemFree = 0x0b,
        BatchMemOp = 0x0c,
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
        Default = 0,
        Programmatic = 1
    }
}