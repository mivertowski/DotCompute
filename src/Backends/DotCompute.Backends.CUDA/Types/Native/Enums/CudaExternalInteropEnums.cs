// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda external memory handle type enumeration.
    /// </summary>
    /// <summary>
    /// CUDA external memory handle type enumeration
    /// </summary>
    public enum CudaExternalMemoryHandleType : uint
    {
        /// <summary>
        /// Opaque file descriptor handle (Linux/Unix).
        /// </summary>
        OpaqueFd = 1,

        /// <summary>
        /// Opaque Windows handle.
        /// </summary>
        OpaqueWin32 = 2,

        /// <summary>
        /// Opaque Windows Kernel Mode handle.
        /// </summary>
        OpaqueWin32Kmt = 3,

        /// <summary>
        /// Direct3D 12 heap resource handle.
        /// </summary>
        D3D12Heap = 4,

        /// <summary>
        /// Direct3D 12 resource handle.
        /// </summary>
        D3D12Resource = 5,

        /// <summary>
        /// Direct3D 11 resource handle.
        /// </summary>
        D3D11Resource = 6,

        /// <summary>
        /// Direct3D 11 resource Kernel Mode handle.
        /// </summary>
        D3D11ResourceKmt = 7,

        /// <summary>
        /// NVIDIA NvSciBuf interop handle.
        /// </summary>
        NvSciBuf = 8
    }
    /// <summary>
    /// An cuda external semaphore handle type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA external semaphore handle type enumeration
    /// </summary>
    public enum CudaExternalSemaphoreHandleType : uint
    {
        /// <summary>
        /// Opaque file descriptor semaphore handle (Linux/Unix).
        /// </summary>
        OpaqueFd = 1,

        /// <summary>
        /// Opaque Windows semaphore handle.
        /// </summary>
        OpaqueWin32 = 2,

        /// <summary>
        /// Opaque Windows Kernel Mode semaphore handle.
        /// </summary>
        OpaqueWin32Kmt = 3,

        /// <summary>
        /// Direct3D 12 fence handle.
        /// </summary>
        D3D12Fence = 4,

        /// <summary>
        /// Direct3D 11 fence handle.
        /// </summary>
        D3D11Fence = 5,

        /// <summary>
        /// NVIDIA NvSciSync interop handle.
        /// </summary>
        NvSciSync = 6,

        /// <summary>
        /// Windows keyed mutex handle.
        /// </summary>
        KeyedMutex = 7,

        /// <summary>
        /// Windows keyed mutex Kernel Mode handle.
        /// </summary>
        KeyedMutexKmt = 8,

        /// <summary>
        /// Timeline semaphore file descriptor (Linux/Unix).
        /// </summary>
        TimelineSemaphoreFd = 9,

        /// <summary>
        /// Timeline semaphore Windows handle.
        /// </summary>
        TimelineSemaphoreWin32 = 10
    }
    /// <summary>
    /// An cuda external semaphore signal flags enumeration.
    /// </summary>

    /// <summary>
    /// CUDA external semaphore signal flags enumeration
    /// </summary>
    public enum CudaExternalSemaphoreSignalFlags : uint
    {
        /// <summary>
        /// No special signal flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Skip NvSciBuf memory synchronization on signal.
        /// </summary>
        SignalSkipNvSciBufMemSync = 1,

        /// <summary>
        /// Skip NvSciSync fence on signal.
        /// </summary>
        SignalSkipNvSciSyncFence = 2
    }
    /// <summary>
    /// An cuda external semaphore wait flags enumeration.
    /// </summary>

    /// <summary>
    /// CUDA external semaphore wait flags enumeration
    /// </summary>
    public enum CudaExternalSemaphoreWaitFlags : uint
    {
        /// <summary>
        /// No special wait flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Skip NvSciBuf memory synchronization on wait.
        /// </summary>
        WaitSkipNvSciBufMemSync = 1,

        /// <summary>
        /// Skip NvSciSync fence on wait.
        /// </summary>
        WaitSkipNvSciSyncFence = 2
    }
}
