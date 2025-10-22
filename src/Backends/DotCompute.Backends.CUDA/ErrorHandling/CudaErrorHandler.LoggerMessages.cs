// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// LoggerMessage delegates for CudaErrorHandler.
/// Event IDs: 5450-5499
/// </summary>
public sealed partial class CudaErrorHandler
{
    [LoggerMessage(
        EventId = 5450,
        Level = LogLevel.Information,
        Message = "Circuit breaker reset, GPU operations resuming")]
    private partial void LogCircuitBreakerReset();

    [LoggerMessage(
        EventId = 5451,
        Level = LogLevel.Information,
        Message = "Circuit breaker half-open, testing GPU availability")]
    private partial void LogCircuitBreakerHalfOpen();

    [LoggerMessage(
        EventId = 5452,
        Level = LogLevel.Error,
        Message = "Unexpected error in {OperationName}")]
    private partial void LogUnexpectedError(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 5453,
        Level = LogLevel.Error,
        Message = "CUDA error in {OperationName}: {ErrorCode}")]
    private partial void LogCudaError(Exception ex, string operationName, CudaError errorCode);

    [LoggerMessage(
        EventId = 5454,
        Level = LogLevel.Warning,
        Message = "Attempting memory error recovery for {OperationName}")]
    private partial void LogMemoryRecoveryAttempt(string operationName);

    [LoggerMessage(
        EventId = 5455,
        Level = LogLevel.Error,
        Message = "Memory error recovery failed for {OperationName}")]
    private partial void LogMemoryRecoveryFailed(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 5456,
        Level = LogLevel.Warning,
        Message = "Attempting device error recovery for {OperationName}")]
    private partial void LogDeviceRecoveryAttempt(string operationName);

    [LoggerMessage(
        EventId = 5457,
        Level = LogLevel.Error,
        Message = "Device error recovery failed for {OperationName}")]
    private partial void LogDeviceRecoveryFailed(Exception ex, string operationName);

    [LoggerMessage(
        EventId = 5458,
        Level = LogLevel.Information,
        Message = "Falling back to CPU for {OperationName}")]
    private partial void LogCpuFallback(string operationName);

    [LoggerMessage(
        EventId = 5459,
        Level = LogLevel.Warning,
        Message = "Resetting CUDA device...")]
    private partial void LogDeviceReset();

    [LoggerMessage(
        EventId = 5460,
        Level = LogLevel.Information,
        Message = "Device reset successful")]
    private partial void LogDeviceResetSuccess();

    [LoggerMessage(
        EventId = 5461,
        Level = LogLevel.Error,
        Message = "Device reset exception")]
    private partial void LogDeviceResetException(Exception ex);
}
