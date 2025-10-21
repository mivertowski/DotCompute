// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation
{
    /// <summary>
    /// LoggerMessage delegates for CudaKernelLauncher
    /// </summary>
    public sealed partial class CudaKernelLauncher
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 24001, Level = LogLevel.Information,
            Message = "Preparing kernel argument {Index}: Type={TypeName}, Value={Value}, FullName={FullName}")]
        private static partial void LogPreparingKernelArgument(ILogger logger, int index, string typeName, object value, string? fullName);

        [LoggerMessage(EventId = 24002, Level = LogLevel.Debug,
            Message = "Kernel argument {Index} prepared: Pointer=0x{Pointer}")]
        private static partial void LogKernelArgumentPrepared(ILogger logger, int index, long pointer);

        [LoggerMessage(EventId = 24003, Level = LogLevel.Debug,
            Message = "Launching CUDA kernel with config: Grid({GridX},{GridY},{GridZ}), Block({BlockX},{BlockY},{BlockZ}), SharedMem={SharedMemoryBytes}, ArgCount={ArgCount}")]
        private static partial void LogLaunchingKernel(ILogger logger, uint gridX, uint gridY, uint gridZ, uint blockX, uint blockY, uint blockZ, uint sharedMemoryBytes, int argCount);

        [LoggerMessage(EventId = 24004, Level = LogLevel.Debug,
            Message = "Total threads: {TotalThreads}, Function ptr: 0x{FunctionPtr}, Stream: 0x{StreamPtr}")]
        private static partial void LogKernelLaunchDetails(ILogger logger, ulong totalThreads, long functionPtr, long streamPtr);

        [LoggerMessage(EventId = 24005, Level = LogLevel.Debug,
            Message = "Arg[{Index}]: Ptr=0x{ArgPtr} -> Value=0x{Value}")]
        private static partial void LogArgumentPointerValue(ILogger logger, int index, long argPtr, long value);

        [LoggerMessage(EventId = 24006, Level = LogLevel.Debug,
            Message = "Launching cooperative kernel for grid-wide synchronization")]
        private static partial void LogLaunchingCooperativeKernel(ILogger logger);

        [LoggerMessage(EventId = 24007, Level = LogLevel.Debug,
            Message = "Calculated optimal block size: {BlockSize} threads")]
        private static partial void LogOptimalBlockSize(ILogger logger, int blockSize);

        [LoggerMessage(EventId = 24008, Level = LogLevel.Warning,
            Message = "Block size {BlockSize} exceeds device limit {DeviceLimit}")]
        private static partial void LogBlockSizeExceedsLimit(ILogger logger, uint blockSize, int deviceLimit);

        [LoggerMessage(EventId = 24009, Level = LogLevel.Warning,
            Message = "Block dimensions ({BlockX},{BlockY},{BlockZ}) exceed device limits ({LimitX},{LimitY},{LimitZ})")]
        private static partial void LogBlockDimensionsExceedLimits(ILogger logger, uint blockX, uint blockY, uint blockZ, int limitX, int limitY, int limitZ);

        [LoggerMessage(EventId = 24010, Level = LogLevel.Warning,
            Message = "Grid dimensions ({GridX},{GridY},{GridZ}) exceed device limits ({LimitX},{LimitY},{LimitZ})")]
        private static partial void LogGridDimensionsExceedLimits(ILogger logger, uint gridX, uint gridY, uint gridZ, int limitX, int limitY, int limitZ);

        [LoggerMessage(EventId = 24011, Level = LogLevel.Warning,
            Message = "Shared memory {SharedMemory} bytes exceeds device limit {DeviceLimit} bytes")]
        private static partial void LogSharedMemoryExceedsLimit(ILogger logger, uint sharedMemory, uint deviceLimit);

        [LoggerMessage(EventId = 24012, Level = LogLevel.Information,
            Message = "RTX 2000 Ada: Low occupancy detected. Consider reducing block size for better performance")]
        private static partial void LogLowOccupancyWarning(ILogger logger);

        [LoggerMessage(EventId = 24013, Level = LogLevel.Information,
            Message = "SimpleCudaUnifiedMemoryBuffer (first check): DevicePtr=0x{DevicePtr}, Storage=0x{Storage}")]
        private static partial void LogSimpleCudaUnifiedMemoryBufferFirstCheck(ILogger logger, long devicePtr, long storage);

        [LoggerMessage(EventId = 24014, Level = LogLevel.Debug,
            Message = "CudaMemoryBuffer: DevicePtr=0x{DevicePtr}, Storage=0x{Storage}")]
        private static partial void LogCudaMemoryBuffer(ILogger logger, long devicePtr, long storage);

        [LoggerMessage(EventId = 24015, Level = LogLevel.Debug,
            Message = "CudaMemoryBuffer (field): DevicePtr=0x{DevicePtr}, Storage=0x{Storage}")]
        private static partial void LogCudaMemoryBufferField(ILogger logger, long devicePtr, long storage);

        [LoggerMessage(EventId = 24016, Level = LogLevel.Debug,
            Message = "SimpleCudaUnifiedMemoryBuffer: DevicePtr=0x{DevicePtr}, Storage=0x{Storage}")]
        private static partial void LogSimpleCudaUnifiedMemoryBuffer(ILogger logger, long devicePtr, long storage);

        [LoggerMessage(EventId = 24017, Level = LogLevel.Debug,
            Message = "IUnifiedMemoryBuffer: DevicePtr=0x{DevicePtr}, Storage=0x{Storage}")]
        private static partial void LogUnifiedMemoryBuffer(ILogger logger, long devicePtr, long storage);

        [LoggerMessage(EventId = 24018, Level = LogLevel.Information,
            Message = "Scalar argument: Type={TypeName}, Value={Value}, Size={Size}, Ptr=0x{Ptr}")]
        private static partial void LogScalarArgument(ILogger logger, string typeName, object value, int size, long ptr);

        #endregion
    }
}
