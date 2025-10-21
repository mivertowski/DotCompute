// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// LoggerMessage delegates for CudaMemoryPrefetcher.
    /// </summary>
    public sealed partial class CudaMemoryPrefetcher
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 23201,
            Level = LogLevel.Warning,
            Message = "Failed to create prefetch stream, disabling prefetch")]
        private partial void LogPrefetchStreamCreationFailed();

        [LoggerMessage(
            EventId = 23202,
            Level = LogLevel.Information,
            Message = "Memory prefetching enabled for device")]
        private partial void LogPrefetchEnabled();

        [LoggerMessage(
            EventId = 23203,
            Level = LogLevel.Information,
            Message = "Memory prefetching not supported on device")]
        private partial void LogPrefetchNotSupported();

        [LoggerMessage(
            EventId = 23204,
            Level = LogLevel.Warning,
            Message = "Error checking prefetch support")]
        private partial void LogPrefetchSupportCheckError(Exception ex);

        [LoggerMessage(
            EventId = 23205,
            Level = LogLevel.Trace,
            Message = "Prefetch not supported, skipping")]
        private partial void LogPrefetchSkipped();

        [LoggerMessage(
            EventId = 23206,
            Level = LogLevel.Trace,
            Message = "Prefetched {Size:N0} bytes to device {DeviceId}")]
        private partial void LogPrefetchedToDevice(long size, int deviceId);

        [LoggerMessage(
            EventId = 23207,
            Level = LogLevel.Trace,
            Message = "Memory at {Ptr:X} is not managed memory")]
        private partial void LogNotManagedMemory(IntPtr ptr);

        [LoggerMessage(
            EventId = 23208,
            Level = LogLevel.Warning,
            Message = "Failed to prefetch memory to device")]
        private partial void LogPrefetchToDeviceFailed();

        [LoggerMessage(
            EventId = 23209,
            Level = LogLevel.Trace,
            Message = "Prefetched {Size:N0} bytes to host")]
        private partial void LogPrefetchedToHost(long size);

        [LoggerMessage(
            EventId = 23210,
            Level = LogLevel.Warning,
            Message = "Failed to prefetch memory to host")]
        private partial void LogPrefetchToHostFailed();

        [LoggerMessage(
            EventId = 23211,
            Level = LogLevel.Trace,
            Message = "Memory advice not supported, skipping")]
        private partial void LogMemoryAdviceSkipped();

        [LoggerMessage(
            EventId = 23212,
            Level = LogLevel.Trace,
            Message = "Set memory advice {Advice} for {Size:N0} bytes at {Ptr:X}")]
        private partial void LogMemoryAdviceSet(CudaMemoryAdvise advice, long size, IntPtr ptr);

        [LoggerMessage(
            EventId = 23213,
            Level = LogLevel.Warning,
            Message = "Failed to set memory advice")]
        private partial void LogMemoryAdviceSetFailed();

        [LoggerMessage(
            EventId = 23214,
            Level = LogLevel.Debug,
            Message = "Batch prefetch completed: {SuccessCount}/{TotalCount} successful")]
        private partial void LogBatchPrefetchCompleted(int successCount, int totalCount);

        [LoggerMessage(
            EventId = 23215,
            Level = LogLevel.Warning,
            Message = "Failed to synchronize prefetch stream")]
        private partial void LogPrefetchSyncFailed();

        [LoggerMessage(
            EventId = 23216,
            Level = LogLevel.Warning,
            Message = "Failed to destroy prefetch stream")]
        private partial void LogPrefetchStreamDestroyFailed();

        [LoggerMessage(
            EventId = 23217,
            Level = LogLevel.Information,
            Message = "Disposed memory prefetcher. Total prefetched: {TotalBytes} bytes in {Count} operations")]
        private partial void LogPrefetcherDisposed(long totalBytes, long count);

        #endregion
    }
}
