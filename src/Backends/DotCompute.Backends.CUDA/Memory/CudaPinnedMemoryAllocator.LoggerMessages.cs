// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// LoggerMessage delegates for CudaPinnedMemoryAllocator.
    /// </summary>
    public sealed partial class CudaPinnedMemoryAllocator
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 23301,
            Level = LogLevel.Information,
            Message = "Initialized pinned memory allocator with {MaxPinnedMemory} bytes limit")]
        private partial void LogPinnedAllocatorInitialized(long maxPinnedMemory);

        [LoggerMessage(
            EventId = 23302,
            Level = LogLevel.Debug,
            Message = "Allocated {Size:N0} bytes of pinned memory at {HostPtr:X}, device ptr: {DevicePtr:X}")]
        private partial void LogPinnedMemoryAllocated(long size, IntPtr hostPtr, IntPtr devicePtr);

        [LoggerMessage(
            EventId = 23303,
            Level = LogLevel.Debug,
            Message = "Freed {Size:N0} bytes of pinned memory at 0x{HostPtr:X}")]
        private partial void LogPinnedMemoryFreed(long size, IntPtr hostPtr);

        [LoggerMessage(
            EventId = 23304,
            Level = LogLevel.Warning,
            Message = "Failed to free pinned memory at 0x{HostPtr:X}: {Result}")]
        private partial void LogPinnedMemoryFreeFailed(IntPtr hostPtr, CudaError result);

        [LoggerMessage(
            EventId = 23305,
            Level = LogLevel.Debug,
            Message = "Registered {SizeInBytes:N0} bytes of host memory at 0x{HostPtr:X}")]
        private partial void LogHostMemoryRegistered(long sizeInBytes, IntPtr hostPtr);

        [LoggerMessage(
            EventId = 23306,
            Level = LogLevel.Warning,
            Message = "Failed to unregister host memory")]
        private partial void LogHostMemoryUnregisterFailed();

        [LoggerMessage(
            EventId = 23307,
            Level = LogLevel.Information,
            Message = "Disposed pinned memory allocator")]
        private partial void LogPinnedAllocatorDisposed();

        #endregion
    }
}
