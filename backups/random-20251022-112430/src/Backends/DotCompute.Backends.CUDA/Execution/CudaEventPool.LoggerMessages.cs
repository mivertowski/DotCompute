// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    internal sealed partial class CudaEventPool
    {
        [LoggerMessage(EventId = 28500, Level = LogLevel.Trace,
            Message = "Acquired timing event {Event} (acquired {Count} times)")]
        private static partial void LogTimingEventAcquired(ILogger logger, IntPtr @event, long count);

        [LoggerMessage(EventId = 28501, Level = LogLevel.Trace,
            Message = "Acquired sync event {Event} (acquired {Count} times)")]
        private static partial void LogSyncEventAcquired(ILogger logger, IntPtr @event, long count);

        [LoggerMessage(EventId = 28502, Level = LogLevel.Trace,
            Message = "Returned {Type} event {Event} to pool")]
        private static partial void LogEventReturned(ILogger logger, CudaEventType type, IntPtr @event);

        [LoggerMessage(EventId = 28503, Level = LogLevel.Trace,
            Message = "Destroyed excess {Type} event {Event}")]
        private static partial void LogEventDestroyed(ILogger logger, CudaEventType type, IntPtr @event);

        [LoggerMessage(EventId = 28504, Level = LogLevel.Trace,
            Message = "Created new timing event {Event} for pool")]
        private static partial void LogTimingEventCreated(ILogger logger, IntPtr @event);

        [LoggerMessage(EventId = 28505, Level = LogLevel.Trace,
            Message = "Created new sync event {Event} for pool")]
        private static partial void LogSyncEventCreated(ILogger logger, IntPtr @event);

        [LoggerMessage(EventId = 28506, Level = LogLevel.Trace,
            Message = "Added {Count} events to maintain minimum size for {Queue} pool")]
        private static partial void LogEventsAdded(ILogger logger, int count, string queue);

        [LoggerMessage(EventId = 28507, Level = LogLevel.Trace,
            Message = "Pool imbalance detected: timing={TimingCount}, sync={SyncCount}")]
        private static partial void LogPoolImbalance(ILogger logger, int timingCount, int syncCount);

        [LoggerMessage(EventId = 28508, Level = LogLevel.Trace,
            Message = "Event pool maintenance completed")]
        private static partial void LogMaintenanceCompleted(ILogger logger);

        [LoggerMessage(EventId = 28509, Level = LogLevel.Warning,
            Message = "Error during event pool maintenance")]
        private static partial void LogMaintenanceError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 28510, Level = LogLevel.Warning,
            Message = "Exception destroying pooled event {Event}")]
        private static partial void LogEventDestructionError(ILogger logger, Exception ex, IntPtr @event);
    }
}
