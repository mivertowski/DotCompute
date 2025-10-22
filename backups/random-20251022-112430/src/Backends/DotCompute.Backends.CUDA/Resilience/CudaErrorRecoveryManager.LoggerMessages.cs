// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Resilience
{
    /// <summary>
    /// LoggerMessage delegates for CudaErrorRecoveryManager.
    /// </summary>
    public sealed partial class CudaErrorRecoveryManager
    {
        // Event IDs 5850-5899

        [LoggerMessage(
            EventId = 5850,
            Level = LogLevel.Information,
            Message = "CUDA error recovery manager initialized with {MaxRetries} retries and circuit breaker")]
        private partial void LogInitialized(int maxRetries);

        [LoggerMessage(
            EventId = 5851,
            Level = LogLevel.Warning,
            Message = "CUDA operation failed with {Error}, retrying attempt {RetryCount}/{MaxRetries} after {DelayMs}ms")]
        private partial void LogRetryAttempt(CudaError error, int retryCount, int maxRetries, double delayMs);

        [LoggerMessage(
            EventId = 5852,
            Level = LogLevel.Error,
            Message = "Circuit breaker opened due to excessive failures. Breaking for {DurationSeconds} seconds")]
        private partial void LogCircuitBreakerOpened(double durationSeconds);

        [LoggerMessage(
            EventId = 5853,
            Level = LogLevel.Information,
            Message = "Circuit breaker reset, resuming operations")]
        private partial void LogCircuitBreakerReset();

        [LoggerMessage(
            EventId = 5854,
            Level = LogLevel.Information,
            Message = "Circuit breaker half-open, testing with next operation")]
        private partial void LogCircuitBreakerHalfOpen();

        [LoggerMessage(
            EventId = 5855,
            Level = LogLevel.Error,
            Message = "Permanent failure for operation '{OperationName}' after all retry attempts")]
        private partial void LogPermanentFailure(Exception? ex, string operationName);

        [LoggerMessage(
            EventId = 5856,
            Level = LogLevel.Warning,
            Message = "Operation '{OperationName}' took {DurationSeconds} seconds with recovery")]
        private partial void LogSlowOperation(string operationName, double durationSeconds);

        [LoggerMessage(
            EventId = 5857,
            Level = LogLevel.Warning,
            Message = "Attempting CUDA context recovery")]
        private partial void LogContextRecoveryAttempt();

        [LoggerMessage(
            EventId = 5858,
            Level = LogLevel.Information,
            Message = "Created pre-recovery snapshot {SnapshotId}")]
        private partial void LogSnapshotCreated(Guid snapshotId);

        [LoggerMessage(
            EventId = 5859,
            Level = LogLevel.Information,
            Message = "Progressive recovery successful: {Message}")]
        private partial void LogProgressiveRecoverySuccess(string message);

        [LoggerMessage(
            EventId = 5860,
            Level = LogLevel.Warning,
            Message = "Progressive recovery failed, attempting full device reset")]
        private partial void LogProgressiveRecoveryFailed();

        [LoggerMessage(
            EventId = 5861,
            Level = LogLevel.Warning,
            Message = "Device synchronization failed during recovery: {Error}")]
        private partial void LogSyncFailedDuringRecovery(CudaError error);

        [LoggerMessage(
            EventId = 5862,
            Level = LogLevel.Information,
            Message = "CUDA device reset successful")]
        private partial void LogDeviceResetSuccess();

        [LoggerMessage(
            EventId = 5863,
            Level = LogLevel.Information,
            Message = "Context state restored: {Message}. Restored {RestoredStreams} streams, {RestoredKernels} kernels")]
        private partial void LogStateRestored(string message, int restoredStreams, int restoredKernels);

        [LoggerMessage(
            EventId = 5864,
            Level = LogLevel.Warning,
            Message = "Context state restoration partial: {Message}")]
        private partial void LogPartialStateRestoration(string message);

        [LoggerMessage(
            EventId = 5865,
            Level = LogLevel.Information,
            Message = "CUDA context recovery completed successfully")]
        private partial void LogContextRecoveryComplete();

        [LoggerMessage(
            EventId = 5866,
            Level = LogLevel.Error,
            Message = "CUDA device reset failed: {Error}")]
        private partial void LogDeviceResetFailed(CudaError error);

        [LoggerMessage(
            EventId = 5867,
            Level = LogLevel.Information,
            Message = "Error recovery statistics reset")]
        private partial void LogStatisticsReset();

        [LoggerMessage(
            EventId = 5868,
            Level = LogLevel.Information,
            Message = "Disposed error recovery manager. Total: {Total}, Recovered: {Recovered}, Failed: {Failed}, Recovery Count: {RecoveryCount}")]
        private partial void LogDisposed(long total, long recovered, long failed, int recoveryCount);
    }
}
