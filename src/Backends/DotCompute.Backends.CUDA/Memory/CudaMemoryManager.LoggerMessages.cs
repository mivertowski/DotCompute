// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// LoggerMessage delegates for CudaMemoryManager.
    /// </summary>
    public sealed partial class CudaMemoryManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 23001,
            Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes at 0x{DevicePtr:X}")]
        private partial void LogMemoryAllocated(long sizeInBytes, IntPtr devicePtr);

        [LoggerMessage(
            EventId = 23003,
            Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes unified memory at {UnifiedPtr} for type {TypeName}")]
        private partial void LogUnifiedMemoryAllocated(long sizeInBytes, IntPtr unifiedPtr, string typeName);

        [LoggerMessage(
            EventId = 23004,
            Level = LogLevel.Debug,
            Message = "Freed {Size} bytes at 0x{DevicePtr:X}")]
        private partial void LogMemoryFreed(long size, IntPtr devicePtr);

        [LoggerMessage(
            EventId = 23006,
            Level = LogLevel.Debug,
            Message = "Initialized CUDA memory info - Total: {TotalMemory} bytes, Max allocation: {MaxAllocationSize} bytes")]
        private partial void LogMemoryInfoInitialized(long totalMemory, long maxAllocationSize);

        [LoggerMessage(
            EventId = 23007,
            Level = LogLevel.Warning,
            Message = "Failed to initialize CUDA memory info, using fallback values")]
        private partial void LogMemoryInfoInitializationFailed();

        [LoggerMessage(
            EventId = 23008,
            Level = LogLevel.Warning,
            Message = "Exception while initializing CUDA memory info")]
        private partial void LogMemoryInfoInitializationException(Exception ex);

        [LoggerMessage(
            EventId = 23009,
            Level = LogLevel.Information,
            Message = "CUDA memory manager reset completed")]
        private partial void LogMemoryManagerReset();

        [LoggerMessage(
            EventId = 23011,
            Level = LogLevel.Debug,
            Message = "CUDA memory optimization completed")]
        private partial void LogOptimizationCompleted();

        [LoggerMessage(
            EventId = 23012,
            Level = LogLevel.Warning,
            Message = "Error during CUDA memory optimization")]
        private partial void LogOptimizationError(Exception ex);

        [LoggerMessage(
            EventId = 23013,
            Level = LogLevel.Information,
            Message = "CUDA memory manager cleared")]
        private partial void LogMemoryManagerCleared();

        [LoggerMessage(
            EventId = 23014,
            Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes unified memory at {UnifiedPtr}")]
        private partial void LogUnifiedMemoryAllocatedRaw(long sizeInBytes, IntPtr unifiedPtr);

        [LoggerMessage(
            EventId = 23016,
            Level = LogLevel.Warning,
            Message = "Cannot free non-CUDA buffer of type {BufferTypeName}")]
        private partial void LogCannotFreeNonCudaBuffer(string bufferTypeName);

        [LoggerMessage(
            EventId = 23017,
            Level = LogLevel.Warning,
            Message = "Error retrieving CUDA memory statistics")]
        private partial void LogStatisticsError(Exception ex);

        #endregion
    }
}
