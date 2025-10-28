// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// LoggerMessage delegates for CudaErrorHandler.
/// Event IDs: 6100-6149
/// </summary>
public sealed partial class CudaErrorHandler
{
    [LoggerMessage(
        EventId = 6100,
        Level = LogLevel.Information,
        Message = "Circuit breaker reset, GPU operations resuming")]
    private partial void LogCircuitBreakerReset();

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Information,
        Message = "Circuit breaker half-open, testing GPU availability")]
    private partial void LogCircuitBreakerHalfOpen();

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Error,
        Message = "Unexpected error in {OperationName}")]
    private partial void LogUnexpectedError(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Error,
        Message = "CUDA error in {OperationName}: {ErrorCode}")]
    private partial void LogCudaError(Exception ex, string operationName, CudaError errorCode);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Warning,
        Message = "Attempting memory error recovery for {OperationName}")]
    private partial void LogMemoryRecoveryAttempt(string operationName);

    [LoggerMessage(
        EventId = 6105,
        Level = LogLevel.Error,
        Message = "Memory error recovery failed for {OperationName}")]
    private partial void LogMemoryRecoveryFailed(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6106,
        Level = LogLevel.Warning,
        Message = "Attempting device error recovery for {OperationName}")]
    private partial void LogDeviceRecoveryAttempt(string operationName);

    [LoggerMessage(
        EventId = 6107,
        Level = LogLevel.Error,
        Message = "Device error recovery failed for {OperationName}")]
    private partial void LogDeviceRecoveryFailed(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6108,
        Level = LogLevel.Information,
        Message = "Falling back to CPU for {OperationName}")]
    private partial void LogCpuFallback(string operationName);

    [LoggerMessage(
        EventId = 6109,
        Level = LogLevel.Warning,
        Message = "Resetting CUDA device...")]
    private partial void LogDeviceReset();

    [LoggerMessage(
        EventId = 6110,
        Level = LogLevel.Information,
        Message = "Device reset successful")]
    private partial void LogDeviceResetSuccess();

    [LoggerMessage(
        EventId = 6111,
        Level = LogLevel.Error,
        Message = "Device reset exception")]
    private partial void LogDeviceResetException(Exception ex);

    [LoggerMessage(
        EventId = 6112,
        Level = LogLevel.Warning,
        Message = "Retry {RetryCount}/{MaxRetries} after {Delay}ms for error: {Error}")]
    private partial void LogRetryAttempt(int retryCount, int maxRetries, int delay, CudaError error);

    [LoggerMessage(
        EventId = 6113,
        Level = LogLevel.Warning,
        Message = "Memory allocation retry {RetryCount}, attempting cleanup...")]
    private partial void LogMemoryAllocationRetry(int retryCount);

    [LoggerMessage(
        EventId = 6114,
        Level = LogLevel.Error,
        Message = "Circuit breaker opened for {Duration}s due to repeated failures")]
    private partial void LogCircuitBreakerOpened(double duration);

    [LoggerMessage(
        EventId = 6115,
        Level = LogLevel.Information,
        Message = "Memory cleanup completed. Free: {Free:N0} MB, Total: {Total:N0} MB")]
    private partial void LogMemoryCleanupCompleted(long free, long total);

    [LoggerMessage(
        EventId = 6116,
        Level = LogLevel.Warning,
        Message = "Memory cleanup failed")]
    private partial void LogMemoryCleanupFailed(Exception ex);

    [LoggerMessage(
        EventId = 6117,
        Level = LogLevel.Error,
        Message = "Device reset failed: {Error}")]
    private partial void LogDeviceResetFailed(CudaError error);
}
