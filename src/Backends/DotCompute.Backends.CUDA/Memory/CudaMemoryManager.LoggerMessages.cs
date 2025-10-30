// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    public sealed partial class CudaMemoryManager
    {
        [LoggerMessage(EventId = 23100, Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes of device memory at 0x{DevicePtr:X}")]
        private static partial void LogMemoryAllocated(ILogger logger, long sizeInBytes, IntPtr devicePtr);

        [LoggerMessage(EventId = 23101, Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes of unified memory at 0x{UnifiedPtr:X} for type {TypeName}")]
        private static partial void LogUnifiedMemoryAllocated(ILogger logger, long sizeInBytes, IntPtr unifiedPtr, string typeName);

        [LoggerMessage(EventId = 23102, Level = LogLevel.Debug,
            Message = "Freed {SizeInBytes} bytes of device memory at 0x{DevicePtr:X}")]
        private static partial void LogMemoryFreed(ILogger logger, long sizeInBytes, IntPtr devicePtr);

        [LoggerMessage(EventId = 23103, Level = LogLevel.Information,
            Message = "Initialized memory info: Total={TotalMemory} bytes, MaxAllocation={MaxAllocationSize} bytes")]
        private static partial void LogMemoryInfoInitialized(ILogger logger, long totalMemory, long maxAllocationSize);

        [LoggerMessage(EventId = 23104, Level = LogLevel.Warning,
            Message = "Failed to initialize memory info, using defaults")]
        private static partial void LogMemoryInfoInitializationFailed(ILogger logger);

        [LoggerMessage(EventId = 23105, Level = LogLevel.Error,
            Message = "Exception during memory info initialization")]
        private static partial void LogMemoryInfoInitializationException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 23106, Level = LogLevel.Information,
            Message = "Memory manager reset completed")]
        private static partial void LogMemoryManagerReset(ILogger logger);

        [LoggerMessage(EventId = 23107, Level = LogLevel.Debug,
            Message = "Memory optimization completed")]
        private static partial void LogOptimizationCompleted(ILogger logger);

        [LoggerMessage(EventId = 23108, Level = LogLevel.Error,
            Message = "Error during memory optimization")]
        private static partial void LogOptimizationError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 23109, Level = LogLevel.Information,
            Message = "Memory manager cleared")]
        private static partial void LogMemoryManagerCleared(ILogger logger);

        [LoggerMessage(EventId = 23110, Level = LogLevel.Debug,
            Message = "Allocated {SizeInBytes} bytes of unified memory at 0x{UnifiedPtr:X}")]
        private static partial void LogUnifiedMemoryAllocatedRaw(ILogger logger, long sizeInBytes, IntPtr unifiedPtr);

        [LoggerMessage(EventId = 23111, Level = LogLevel.Warning,
            Message = "Cannot free non-CUDA buffer of type {BufferType}")]
        private static partial void LogCannotFreeNonCudaBuffer(ILogger logger, string bufferType);

        [LoggerMessage(EventId = 23112, Level = LogLevel.Error,
            Message = "Error getting memory statistics")]
        private static partial void LogStatisticsError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 23113, Level = LogLevel.Warning,
            Message = "Failed to free CUDA memory at 0x{DevicePtr:X}: {Result}")]
        private static partial void LogMemoryFreeError(ILogger logger, IntPtr devicePtr, CudaError result);

        [LoggerMessage(EventId = 23114, Level = LogLevel.Warning,
            Message = "CUDA synchronization during optimization failed: {Result}")]
        private static partial void LogOptimizationSyncError(ILogger logger, CudaError result);
    }
}
