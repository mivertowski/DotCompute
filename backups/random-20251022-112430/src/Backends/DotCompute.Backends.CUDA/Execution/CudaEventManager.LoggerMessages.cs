// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    /// <summary>
    /// Logger message definitions for CudaEventManager using source-generated logging.
    /// Event ID range: 28200-28299
    /// </summary>
    public sealed partial class CudaEventManager
    {
        // Initialization (28200-28209)

        [LoggerMessage(EventId = 28200, Level = LogLevel.Information,
            Message = "CUDA Event Manager initialized: max concurrent events={MaxEvents}, timing pool={TimingPool}, sync pool={SyncPool}")]
        private static partial void LogEventManagerInitialized(ILogger logger, int maxEvents, int timingPool, int syncPool);

        // Event Creation (28210-28219)

        [LoggerMessage(EventId = 28210, Level = LogLevel.Trace,
            Message = "Created timing event {EventId} (handle={Handle})")]
        private static partial void LogTimingEventCreated(ILogger logger, EventId eventId, IntPtr handle);

        [LoggerMessage(EventId = 28211, Level = LogLevel.Trace,
            Message = "Created sync event {EventId} (handle={Handle})")]
        private static partial void LogSyncEventCreated(ILogger logger, EventId eventId, IntPtr handle);

        [LoggerMessage(EventId = 28212, Level = LogLevel.Debug,
            Message = "Created timing pair: start={StartEventId}, end={EndEventId}")]
        private static partial void LogTimingPairCreated(ILogger logger, EventId startEventId, EventId endEventId);

        [LoggerMessage(EventId = 28213, Level = LogLevel.Debug,
            Message = "Created batch of {Count} {EventType} events")]
        private static partial void LogEventBatchCreated(ILogger logger, int count, string eventType);

        // Event Recording (28220-28229)

        [LoggerMessage(EventId = 28220, Level = LogLevel.Trace,
            Message = "Recorded event {EventId} on stream {Stream}")]
        private static partial void LogEventRecorded(ILogger logger, EventId eventId, IntPtr stream);

        // Event Timing (28230-28239)

        [LoggerMessage(EventId = 28230, Level = LogLevel.Trace,
            Message = "Elapsed time between events {StartEvent} and {EndEvent}: {TimeMs}ms")]
        private static partial void LogElapsedTime(ILogger logger, EventId startEvent, EventId endEvent, float timeMs);

        [LoggerMessage(EventId = 28231, Level = LogLevel.Debug,
            Message = "Measured operation '{OperationName}': GPU={GpuTimeMs}ms, CPU={CpuTimeMs}ms, Overhead={OverheadMs}ms")]
        private static partial void LogOperationMeasured(ILogger logger, string operationName, float gpuTimeMs, float cpuTimeMs, float overheadMs);

        // Profiling (28240-28249)

        [LoggerMessage(EventId = 28240, Level = LogLevel.Debug,
            Message = "Starting profiling session {SessionId}: {WarmupIterations} warmup + {Iterations} iterations")]
        private static partial void LogProfilingSessionStarted(ILogger logger, string sessionId, int warmupIterations, int iterations);

        [LoggerMessage(EventId = 28241, Level = LogLevel.Debug,
            Message = "Profiling progress {SessionId}: {Progress:F1}% ({CurrentIteration}/{TotalIterations})")]
        private static partial void LogProfilingProgress(ILogger logger, string sessionId, double progress, int currentIteration, int totalIterations);

        [LoggerMessage(EventId = 28242, Level = LogLevel.Information,
            Message = "Completed profiling session {SessionId}: avg={AvgGpu:F3}ms, min={MinGpu:F3}ms, max={MaxGpu:F3}ms, p95={P95:F3}ms, throughput={Throughput:F1}ops/s")]
        private static partial void LogProfilingSessionCompleted(ILogger logger, string sessionId, double avgGpu, double minGpu, double maxGpu, double p95, double throughput);

        // Event Callbacks (28250-28259)

        [LoggerMessage(EventId = 28250, Level = LogLevel.Error,
            Message = "Error in event callback execution")]
        private static partial void LogEventCallbackError(ILogger logger);

        // Maintenance (28260-28269)

        [LoggerMessage(EventId = 28260, Level = LogLevel.Warning,
            Message = "Error during event manager maintenance")]
        private static partial void LogMaintenanceError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 28261, Level = LogLevel.Trace,
            Message = "Cleaned up {Count} completed events")]
        private static partial void LogEventsCleanedUp(ILogger logger, int count);

        // Performance Warnings (28270-28279)

        [LoggerMessage(EventId = 28270, Level = LogLevel.Warning,
            Message = "High event usage: {ActiveEvents}/{MaxEvents} active events")]
        private static partial void LogHighEventUsage(ILogger logger, int activeEvents, int maxEvents);

        [LoggerMessage(EventId = 28271, Level = LogLevel.Information,
            Message = "Many active timing sessions: {ActiveSessions}")]
        private static partial void LogManyTimingSessions(ILogger logger, int activeSessions);

        // Disposal (28280-28289)

        [LoggerMessage(EventId = 28280, Level = LogLevel.Warning,
            Message = "Error returning event {EventId} to pool during disposal")]
        private static partial void LogErrorReturningEventOnDisposal(ILogger logger, EventId eventId, Exception ex);

        [LoggerMessage(EventId = 28281, Level = LogLevel.Information,
            Message = "CUDA Event Manager disposed: created {TotalEvents} events")]
        private static partial void LogEventManagerDisposed(ILogger logger, long totalEvents);
    }
}
