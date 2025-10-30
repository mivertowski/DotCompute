// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// LoggerMessage delegates for CudaMemoryPoolManager.
    /// </summary>
    public sealed partial class CudaMemoryPoolManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 23101,
            Level = LogLevel.Information,
            Message = "Memory pool manager initialized with {PoolCount} size classes")]
        private partial void LogPoolManagerInitialized(int poolCount);

        [LoggerMessage(
            EventId = 23102,
            Level = LogLevel.Warning,
            Message = "Pool for size {PoolSize} not found, allocating directly")]
        private partial void LogPoolNotFound(int poolSize);

        [LoggerMessage(
            EventId = 23103,
            Level = LogLevel.Trace,
            Message = "Allocated {Size} bytes from pool (hit rate: {HitRate:P2})")]
        private partial void LogPoolHit(int size, double hitRate);

        [LoggerMessage(
            EventId = 23104,
            Level = LogLevel.Trace,
            Message = "Returned {Size} bytes to pool")]
        private partial void LogReturnedToPool(int size);

        [LoggerMessage(
            EventId = 23105,
            Level = LogLevel.Warning,
            Message = "Failed to zero memory: {Result}")]
        private partial void LogZeroMemoryFailed(CudaError result);

        [LoggerMessage(
            EventId = 23106,
            Level = LogLevel.Trace,
            Message = "Freed {Size} byte memory block")]
        private partial void LogMemoryBlockFreed(long size);

        [LoggerMessage(
            EventId = 23107,
            Level = LogLevel.Warning,
            Message = "Failed to free memory block: {Result}")]
        private partial void LogFreeMemoryBlockFailed(CudaError result);

        [LoggerMessage(
            EventId = 23108,
            Level = LogLevel.Debug,
            Message = "Pool maintenance freed {Blocks} blocks ({Bytes} bytes)")]
        private partial void LogMaintenanceCompleted(int blocks, long bytes);

        [LoggerMessage(
            EventId = 23109,
            Level = LogLevel.Error,
            Message = "Error during pool maintenance")]
        private partial void LogMaintenanceError(Exception ex);

        [LoggerMessage(
            EventId = 23110,
            Level = LogLevel.Information,
            Message = "Cleared all memory pools")]
        private partial void LogPoolsCleared();

        [LoggerMessage(
            EventId = 23111,
            Level = LogLevel.Information,
            Message = "Disposed memory pool manager. Stats: {Allocations} allocations, {HitRate:P2} hit rate, {BytesAllocated:N0} bytes allocated")]
        private partial void LogPoolManagerDisposed(long allocations, double hitRate, long bytesAllocated);

        [LoggerMessage(
            EventId = 23112,
            Level = LogLevel.Debug,
            Message = "Allocated new {PoolSize} byte block for pool")]
        private partial void LogNewBlockAllocated(int poolSize);

        #endregion
    }
}
