// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Execution.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    /// <summary>
    /// LoggerMessage delegates for CudaStreamManager
    /// </summary>
    public sealed partial class CudaStreamManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 24061, Level = LogLevel.Debug,
            Message = "Created RTX-optimized stream group '{GroupName}' with {StreamCount} streams")]
        private static partial void LogCreatedStreamGroup(ILogger logger, string groupName, int streamCount);

        [LoggerMessage(EventId = 24062, Level = LogLevel.Debug,
            Message = "Created CUDA stream {StreamId} (handle={Handle}) with priority={Priority}, flags={Flags}")]
        private static partial void LogCreatedStream(ILogger logger, string streamId, long handle, CudaStreamPriority priority, CudaStreamFlags flags);

        [LoggerMessage(EventId = 24063, Level = LogLevel.Debug,
            Message = "Created batch of {Count} streams")]
        private static partial void LogCreatedStreamBatch(ILogger logger, int count);

        [LoggerMessage(EventId = 24064, Level = LogLevel.Debug,
            Message = "Completed execution graph with {TotalNodes} nodes in {LevelCount} levels")]
        private static partial void LogCompletedExecutionGraph(ILogger logger, int totalNodes, int levelCount);

        [LoggerMessage(EventId = 24065, Level = LogLevel.Debug,
            Message = "RTX 2000 optimization: {ActiveStreams} active streams, {HighPriorityStreams} high-priority active")]
        private static partial void LogRtxOptimization(ILogger logger, int activeStreams, int highPriorityStreams);

        [LoggerMessage(EventId = 24066, Level = LogLevel.Warning,
            Message = "Failed to get stream priority range, using defaults: {ErrorMessage}")]
        private static partial void LogFailedToGetPriorityRange(ILogger logger, string errorMessage);

        [LoggerMessage(EventId = 24067, Level = LogLevel.Warning,
            Message = "Failed to create RTX-optimized stream {Index}: {ErrorMessage}")]
        private static partial void LogFailedToCreateOptimizedStream(ILogger logger, int index, string errorMessage);

        [LoggerMessage(EventId = 24068, Level = LogLevel.Debug,
            Message = "Initialized {StreamCount} RTX-optimized streams with priority range [{LeastPriority}, {GreatestPriority}]")]
        private static partial void LogInitializedOptimizedStreams(ILogger logger, int streamCount, int leastPriority, int greatestPriority);

        [LoggerMessage(EventId = 24069, Level = LogLevel.Debug,
            Message = "Cleaned up {Count} idle streams")]
        private static partial void LogCleanedUpIdleStreams(ILogger logger, int count);

        [LoggerMessage(EventId = 24070, Level = LogLevel.Warning,
            Message = "Failed to destroy CUDA stream {Stream}: {ErrorMessage}")]
        private static partial void LogFailedToDestroyStream(ILogger logger, long stream, string errorMessage);

        [LoggerMessage(EventId = 24071, Level = LogLevel.Error,
            Message = "Error in stream callback execution")]
        private static partial void LogStreamCallbackError(ILogger logger);

        [LoggerMessage(EventId = 24072, Level = LogLevel.Information,
            Message = "CUDA Stream Manager disposed")]
        private static partial void LogStreamManagerDisposed(ILogger logger);

        [LoggerMessage(EventId = 24073, Level = LogLevel.Trace,
            Message = "Cleaned up idle stream {StreamId} (idle for {IdleTime})")]
        private static partial void LogCleanedUpIdleStream(ILogger logger, StreamId streamId, TimeSpan idleTime);

        [LoggerMessage(EventId = 24074, Level = LogLevel.Warning,
            Message = "Some executions did not complete within timeout during disposal")]
        private static partial void LogDisposalTimeoutWarning(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 24075, Level = LogLevel.Warning,
            Message = "Failed to clean up execution events")]
        private static partial void LogFailedToCleanupEvents(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 6300, Level = LogLevel.Information,
            Message = "CUDA Stream Manager initialized for RTX optimization: {OptimalStreams} optimal streams, priority range [{Least}, {Greatest}], max concurrent: {MaxStreams}")]
        private partial void LogStreamManagerInitialized(int optimalStreams, int least, int greatest, int maxStreams);

        [LoggerMessage(EventId = 6301, Level = LogLevel.Trace,
            Message = "Synchronized stream {WaitingStream} to wait for stream {SignalStream} via event {Event}")]
        private partial void LogStreamSynchronizedViaEvent(StreamId waitingStream, StreamId signalStream, IntPtr @event);

        [LoggerMessage(EventId = 6302, Level = LogLevel.Trace,
            Message = "Completed execution graph node {NodeId} on stream {StreamId}")]
        private partial void LogCompletedGraphNode(string nodeId, StreamId streamId);

        [LoggerMessage(EventId = 6303, Level = LogLevel.Warning,
            Message = "Exception while destroying stream {Stream}")]
        private partial void LogExceptionDestroyingStream(Exception ex, long stream);

        [LoggerMessage(EventId = 6304, Level = LogLevel.Warning,
            Message = "Error during stream manager maintenance")]
        private partial void LogErrorDuringMaintenance(Exception ex);

        #endregion
    }
}
