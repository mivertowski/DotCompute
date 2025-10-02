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
        OpaqueFd = 1,
        OpaqueWin32 = 2,
        OpaqueWin32Kmt = 3,
        D3D12Heap = 4,
        D3D12Resource = 5,
        D3D11Resource = 6,
        D3D11ResourceKmt = 7,
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
        OpaqueFd = 1,
        OpaqueWin32 = 2,
        OpaqueWin32Kmt = 3,
        D3D12Fence = 4,
        D3D11Fence = 5,
        NvSciSync = 6,
        KeyedMutex = 7,
        KeyedMutexKmt = 8,
        TimelineSemaphoreFd = 9,
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
        None = 0,
        SignalSkipNvSciBufMemSync = 1,
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
        None = 0,
        WaitSkipNvSciBufMemSync = 1,
        WaitSkipNvSciSyncFence = 2
    }
}