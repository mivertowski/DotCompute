// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Execution.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    internal sealed partial class CudaStreamPool
    {
        [LoggerMessage(EventId = 28300, Level = LogLevel.Trace,
            Message = "Acquired stream {Stream} from {Priority} priority pool (acquired {Count} times)")]
        private static partial void LogStreamAcquired(ILogger logger, IntPtr stream, CudaStreamPriority priority, long count);

        [LoggerMessage(EventId = 28301, Level = LogLevel.Trace,
            Message = "Returned stream {Stream} to {Priority} priority pool")]
        private static partial void LogStreamReturned(ILogger logger, IntPtr stream, CudaStreamPriority priority);

        [LoggerMessage(EventId = 28302, Level = LogLevel.Trace,
            Message = "Destroyed excess stream {Stream} from {Priority} priority pool")]
        private static partial void LogStreamDestroyed(ILogger logger, IntPtr stream, CudaStreamPriority priority);

        [LoggerMessage(EventId = 28303, Level = LogLevel.Trace,
            Message = "Using fallback stream from different priority pool for {Priority} request")]
        private static partial void LogStreamFallback(ILogger logger, CudaStreamPriority priority);

        [LoggerMessage(EventId = 28304, Level = LogLevel.Trace,
            Message = "Created new stream {Stream} for {Priority} priority pool")]
        private static partial void LogStreamCreated(ILogger logger, IntPtr stream, CudaStreamPriority priority);

        [LoggerMessage(EventId = 28305, Level = LogLevel.Trace,
            Message = "Promoted stream to high priority pool during rebalancing")]
        private static partial void LogStreamPromoted(ILogger logger);

        [LoggerMessage(EventId = 28306, Level = LogLevel.Trace,
            Message = "Added {Count} streams to maintain minimum size for {Queue} priority pool")]
        private static partial void LogStreamsAdded(ILogger logger, int count, string queue);

        [LoggerMessage(EventId = 28307, Level = LogLevel.Trace,
            Message = "Stream pool maintenance completed")]
        private static partial void LogMaintenanceCompleted(ILogger logger);

        [LoggerMessage(EventId = 28308, Level = LogLevel.Warning,
            Message = "Error during stream pool maintenance")]
        private static partial void LogMaintenanceError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 28309, Level = LogLevel.Warning,
            Message = "Exception destroying pooled stream {Stream}")]
        private static partial void LogStreamDestructionError(ILogger logger, Exception ex, IntPtr stream);
    }
}
