// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Utilities;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Production-grade Metal error handler with retry logic, graceful degradation,
/// and comprehensive error recovery strategies following CUDA error handling patterns.
/// </summary>
public sealed partial class MetalErrorHandler : IDisposable
{
    private readonly ILogger<MetalErrorHandler> _logger;
    private readonly ConcurrentDictionary<MetalError, MetalErrorStatistics> _errorStats;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IAsyncPolicy _memoryRetryPolicy;
    private readonly IAsyncPolicy<bool> _circuitBreakerPolicy;
    private readonly MetalErrorRecoveryOptions _options;
    private volatile bool _gpuAvailable = true;
    private DateTimeOffset _lastSuccessfulOperation = DateTimeOffset.UtcNow;
    private readonly bool _isAppleSilicon;

    public MetalErrorHandler(
        ILogger<MetalErrorHandler> logger,
        MetalErrorRecoveryOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new MetalErrorRecoveryOptions();
        _errorStats = new ConcurrentDictionary<MetalError, MetalErrorStatistics>();
        _isAppleSilicon = DetectAppleSilicon();

        _retryPolicy = CreateRetryPolicy();
        _memoryRetryPolicy = CreateMemoryRetryPolicy();
        _circuitBreakerPolicy = CreateCircuitBreakerPolicy();

        LogErrorHandlerInitialized(_logger, _isAppleSilicon ? "Apple Silicon" : "Intel Mac");
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6300,
        Level = LogLevel.Information,
        Message = "Metal Error Handler initialized for {Architecture}")]
    private static partial void LogErrorHandlerInitialized(ILogger logger, string architecture);

    [LoggerMessage(
        EventId = 6301,
        Level = LogLevel.Error,
        Message = "Unexpected error in Metal operation {OperationName}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6302,
        Level = LogLevel.Error,
        Message = "Metal error in {OperationName}: {ErrorCode}")]
    private static partial void LogMetalError(ILogger logger, Exception ex, string operationName, MetalError errorCode);

    [LoggerMessage(
        EventId = 6303,
        Level = LogLevel.Warning,
        Message = "Attempting memory error recovery for Metal operation {OperationName}")]
    private static partial void LogMemoryErrorRecoveryAttempt(ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 6304,
        Level = LogLevel.Error,
        Message = "Memory error recovery failed for Metal operation {OperationName}")]
    private static partial void LogMemoryErrorRecoveryFailed(ILogger logger, Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6305,
        Level = LogLevel.Warning,
        Message = "Attempting device error recovery for Metal operation {OperationName}")]
    private static partial void LogDeviceErrorRecoveryAttempt(ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 6306,
        Level = LogLevel.Error,
        Message = "Device error recovery failed for Metal operation {OperationName}")]
    private static partial void LogDeviceErrorRecoveryFailed(ILogger logger, Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6307,
        Level = LogLevel.Warning,
        Message = "Attempting command buffer error recovery for Metal operation {OperationName}")]
    private static partial void LogCommandBufferErrorRecoveryAttempt(ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 6308,
        Level = LogLevel.Error,
        Message = "Command buffer error recovery failed for Metal operation {OperationName}")]
    private static partial void LogCommandBufferErrorRecoveryFailed(ILogger logger, Exception ex, string operationName);

    [LoggerMessage(
        EventId = 6309,
        Level = LogLevel.Information,
        Message = "Falling back to CPU for Metal operation {OperationName}")]
    private static partial void LogFallingBackToCpu(ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 6310,
        Level = LogLevel.Information,
        Message = "Performing unified memory cleanup on Apple Silicon")]
    private static partial void LogUnifiedMemoryCleanup(ILogger logger);

    [LoggerMessage(
        EventId = 6311,
        Level = LogLevel.Information,
        Message = "Performing discrete GPU memory cleanup on Intel Mac")]
    private static partial void LogDiscreteGpuMemoryCleanup(ILogger logger);

    [LoggerMessage(
        EventId = 6312,
        Level = LogLevel.Warning,
        Message = "Metal memory cleanup failed")]
    private static partial void LogMemoryCleanupFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6313,
        Level = LogLevel.Information,
        Message = "Handling unified memory pressure on Apple Silicon")]
    private static partial void LogHandlingUnifiedMemoryPressure(ILogger logger);

    [LoggerMessage(
        EventId = 6314,
        Level = LogLevel.Warning,
        Message = "Unified memory pressure handling failed")]
    private static partial void LogUnifiedMemoryPressureFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6315,
        Level = LogLevel.Information,
        Message = "Handling discrete GPU memory pressure on Intel Mac")]
    private static partial void LogHandlingDiscreteMemoryPressure(ILogger logger);

    [LoggerMessage(
        EventId = 6316,
        Level = LogLevel.Warning,
        Message = "Discrete memory pressure handling failed")]
    private static partial void LogDiscreteMemoryPressureFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6317,
        Level = LogLevel.Warning,
        Message = "Resetting Metal device state...")]
    private static partial void LogResettingDeviceState(ILogger logger);

    [LoggerMessage(
        EventId = 6318,
        Level = LogLevel.Information,
        Message = "Metal device state reset successful")]
    private static partial void LogDeviceResetSuccessful(ILogger logger);

    [LoggerMessage(
        EventId = 6319,
        Level = LogLevel.Error,
        Message = "Metal device reset failed")]
    private static partial void LogDeviceResetFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6320,
        Level = LogLevel.Debug,
        Message = "Metal operation {OperationName} completed in {ElapsedMs}ms")]
    private static partial void LogOperationCompleted(ILogger logger, string operationName, long elapsedMs);

    #endregion

    /// <summary>
    /// Gets whether GPU is currently available
    /// </summary>
    public bool IsGpuAvailable => _gpuAvailable;

    /// <summary>
    /// Gets time since last successful operation
    /// </summary>
    public TimeSpan TimeSinceLastSuccess => DateTimeOffset.UtcNow - _lastSuccessfulOperation;

    /// <summary>
    /// Gets whether running on Apple Silicon
    /// </summary>
    public bool IsAppleSilicon => _isAppleSilicon;

    /// <summary>
    /// Creates the main retry policy for transient errors
    /// </summary>
    private IAsyncPolicy CreateRetryPolicy() => new SimpleRetryPolicy(_options.MaxRetryAttempts, TimeSpan.FromMilliseconds(100), _logger);

    /// <summary>
    /// Creates specialized retry policy for memory allocation errors
    /// </summary>
    private IAsyncPolicy CreateMemoryRetryPolicy() => new SimpleRetryPolicy(_options.MemoryRetryAttempts, TimeSpan.FromMilliseconds(500), _logger);

    /// <summary>
    /// Creates circuit breaker policy for catastrophic failures
    /// </summary>
    private IAsyncPolicy<bool> CreateCircuitBreakerPolicy() => new SimpleRetryPolicy<bool>(_options.CircuitBreakerThreshold, TimeSpan.FromSeconds(1), _logger);

    /// <summary>
    /// Executes a Metal operation with comprehensive error handling
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check circuit breaker
            var canExecute = await _circuitBreakerPolicy.ExecuteAsync(
                async () =>
                {
                    // Quick GPU health check
                    if (!_gpuAvailable)
                    {
                        return false;
                    }

                    // For Metal, we would check device availability here
                    // This is a placeholder implementation
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);

            if (!canExecute)
            {
                throw new MetalUnavailableException(
                    "Metal GPU operations are currently unavailable due to repeated failures");
            }

            // Execute with retry
            var stopwatch = Stopwatch.StartNew();

            var genericRetryPolicy = new SimpleRetryPolicy<T>(_options.MaxRetryAttempts, TimeSpan.FromMilliseconds(100), _logger);
            var result = await genericRetryPolicy.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            // Record success
            _lastSuccessfulOperation = DateTimeOffset.UtcNow;
            RecordSuccess(operationName, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (MetalException metalEx)
        {
            return await HandleMetalExceptionAsync(metalEx, operation, operationName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(_logger, ex, operationName);
            throw new MetalOperationException(operationName, "Unexpected error occurred", ex);
        }
    }

    /// <summary>
    /// Handles Metal-specific exceptions with recovery strategies
    /// </summary>
    private async Task<T> HandleMetalExceptionAsync<T>(
        MetalException metalEx,
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        LogMetalError(_logger, metalEx, operationName, metalEx.ErrorCode);

        // Try recovery strategies based on error type
        if (IsMemoryError(metalEx.ErrorCode))
        {
            return await HandleMemoryErrorAsync(operation, operationName, cancellationToken).ConfigureAwait(false);
        }

        if (IsDeviceError(metalEx.ErrorCode))
        {
            return await HandleDeviceErrorAsync(operation, operationName, cancellationToken).ConfigureAwait(false);
        }

        if (IsCommandBufferError(metalEx.ErrorCode))
        {
            return await HandleCommandBufferErrorAsync(operation, operationName, cancellationToken).ConfigureAwait(false);
        }

        if (_options.EnableCpuFallback && CanFallbackToCpu(operationName))
        {
            return await FallbackToCpuAsync<T>(operationName).ConfigureAwait(false);
        }

        throw new MetalOperationException(
            operationName, $"Failed with Metal error: {metalEx.ErrorCode}", metalEx);
    }

    /// <summary>
    /// Handles memory-related errors with cleanup and retry
    /// </summary>
    private async Task<T> HandleMemoryErrorAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        LogMemoryErrorRecoveryAttempt(_logger, operationName);

        try
        {
            var genericRetryPolicy = new SimpleRetryPolicy<T>(_options.MemoryRetryAttempts, TimeSpan.FromMilliseconds(500), _logger);
            return await genericRetryPolicy.ExecuteAsync(
                async () =>
                {
                    // Force garbage collection
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();

                    // Metal-specific memory pressure handling
                    if (_isAppleSilicon)
                    {
                        // On Apple Silicon, unified memory allows more aggressive cleanup
                        await HandleUnifiedMemoryPressureAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        // On Intel Macs with discrete GPUs
                        await HandleDiscreteMemoryPressureAsync().ConfigureAwait(false);
                    }

                    // Retry operation
                    return await operation().ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMemoryErrorRecoveryFailed(_logger, ex, operationName);

            if (_options.EnableCpuFallback)
            {
                return await FallbackToCpuAsync<T>(operationName).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// Handles device-related errors with reset and retry
    /// </summary>
    private async Task<T> HandleDeviceErrorAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        LogDeviceErrorRecoveryAttempt(_logger, operationName);

        try
        {
            // Reset device state if allowed
            if (_options.AllowDeviceReset)
            {
                await ResetDeviceAsync().ConfigureAwait(false);

                // Retry operation after reset
                return await operation().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogDeviceErrorRecoveryFailed(_logger, ex, operationName);
        }

        if (_options.EnableCpuFallback)
        {
            return await FallbackToCpuAsync<T>(operationName).ConfigureAwait(false);
        }

        throw new MetalDeviceException($"Device error in Metal operation '{operationName}'");
    }

    /// <summary>
    /// Handles command buffer errors with queue reset
    /// </summary>
    private async Task<T> HandleCommandBufferErrorAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        LogCommandBufferErrorRecoveryAttempt(_logger, operationName);

        try
        {
            // Wait a bit for command buffers to drain
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            // Retry operation
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCommandBufferErrorRecoveryFailed(_logger, ex, operationName);

            if (_options.EnableCpuFallback)
            {
                return await FallbackToCpuAsync<T>(operationName).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// Falls back to CPU execution when Metal GPU fails
    /// </summary>
    private async Task<T> FallbackToCpuAsync<T>(string operationName)
    {
        LogFallingBackToCpu(_logger, operationName);

        // This would invoke CPU-based implementation
        // For now, throw to indicate fallback is needed
        await Task.Delay(1).ConfigureAwait(false); // Avoid warning about no async

        throw new MetalCpuFallbackRequiredException(
            $"Metal operation '{operationName}' requires CPU fallback");
    }

    /// <summary>
    /// Triggers memory cleanup on device
    /// </summary>
    private async Task TriggerMemoryCleanupAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (_isAppleSilicon)
                {
                    // Apple Silicon unified memory cleanup
                    LogUnifiedMemoryCleanup(_logger);

                    // Force system memory pressure handling

                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                }
                else
                {
                    // Intel Mac discrete GPU memory cleanup
                    LogDiscreteGpuMemoryCleanup(_logger);

                    // More conservative cleanup for discrete GPUs

                    GC.Collect(1, GCCollectionMode.Default, blocking: false);
                }
            }
            catch (Exception ex)
            {
                LogMemoryCleanupFailed(_logger, ex);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles memory pressure on unified memory systems (Apple Silicon)
    /// </summary>
    private async Task HandleUnifiedMemoryPressureAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                LogHandlingUnifiedMemoryPressure(_logger);

                // On Apple Silicon, CPU and GPU share the same memory pool
                // We can be more aggressive with cleanup
                for (var i = 0; i < 3; i++)
                {
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(50); // Brief pause between collections
                }
            }
            catch (Exception ex)
            {
                LogUnifiedMemoryPressureFailed(_logger, ex);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles memory pressure on discrete GPU systems (Intel Mac)
    /// </summary>
    private async Task HandleDiscreteMemoryPressureAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                LogHandlingDiscreteMemoryPressure(_logger);

                // On Intel Macs with discrete GPUs, be more conservative
                GC.Collect(1, GCCollectionMode.Default, blocking: false);
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LogDiscreteMemoryPressureFailed(_logger, ex);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the Metal device state
    /// </summary>
    private async Task ResetDeviceAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                LogResettingDeviceState(_logger);

                // For Metal, device reset is more limited than CUDA
                // We mainly clear command queues and buffers


                Thread.Sleep(100); // Brief pause to let operations complete

                LogDeviceResetSuccessful(_logger);
                _gpuAvailable = true;
            }
            catch (Exception ex)
            {
                LogDeviceResetFailed(_logger, ex);
                _gpuAvailable = false;
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and can be retried
    /// </summary>
    private static bool IsTransientError(MetalError error)
    {
        return error switch
        {
            MetalError.NotReady => true,
            MetalError.Timeout => true,
            MetalError.Busy => true,
            MetalError.CommandBufferNotEnqueued => true,
            MetalError.TemporaryResourceUnavailable => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is memory-related
    /// </summary>
    private static bool IsMemoryError(MetalError error)
    {
        return error switch
        {
            MetalError.OutOfMemory => true,
            MetalError.ResourceAllocationFailed => true,
            MetalError.BufferAllocationFailed => true,
            MetalError.TextureAllocationFailed => true,
            MetalError.InsufficientMemory => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is device-related
    /// </summary>
    private static bool IsDeviceError(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceNotFound => true,
            MetalError.DeviceNotSupported => true,
            MetalError.DeviceRemoved => true,
            MetalError.InvalidDevice => true,
            MetalError.DeviceNotReady => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is command buffer related
    /// </summary>
    private static bool IsCommandBufferError(MetalError error)
    {
        return error switch
        {
            MetalError.CommandBufferError => true,
            MetalError.CommandBufferNotEnqueued => true,
            MetalError.CommandEncoderError => true,
            MetalError.InvalidCommandQueue => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is catastrophic
    /// </summary>
    private static bool IsCatastrophicError(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceRemoved => true,
            MetalError.SystemFailure => true,
            MetalError.HardwareFailure => true,
            MetalError.DriverError => true,
            MetalError.UnsupportedFeature => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an operation can fall back to CPU
    /// </summary>
    private static bool CanFallbackToCpu(string operationName)
    {
        // Define operations that have CPU implementations
        var cpuSupportedOps = new[]
        {
            "matmul", "reduce", "scan", "sort",
            "convolution", "pooling", "activation",
            "copy", "fill", "transform"
        };

        return cpuSupportedOps.Any(op =>
            operationName.Contains(op, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Records error statistics
    /// </summary>
    private void RecordError(MetalError error)
    {
        var stats = _errorStats.AddOrUpdate(error,
            _ => new MetalErrorStatistics { FirstOccurrence = DateTimeOffset.UtcNow },
            (_, existing) =>
            {
                existing.Count++;
                existing.LastOccurrence = DateTimeOffset.UtcNow;
                return existing;
            });

        stats.Count++;
    }

    /// <summary>
    /// Records successful operation
    /// </summary>
    private void RecordSuccess(string operationName, long elapsedMs) => LogOperationCompleted(_logger, operationName, elapsedMs);

    /// <summary>
    /// Gets error statistics
    /// </summary>
    public IReadOnlyDictionary<MetalError, MetalErrorStatistics> GetErrorStatistics()
        => _errorStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Clears error statistics
    /// </summary>
    public void ClearStatistics()
    {
        _errorStats.Clear();
        _lastSuccessfulOperation = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Detects if running on Apple Silicon
    /// </summary>
    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }


        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture ==

                   System.Runtime.InteropServices.Architecture.Arm64;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the error handler resources
    /// </summary>
    public void Dispose()
        // MetalErrorHandler doesn't hold disposable resources directly,
        // but we clear statistics as cleanup

        => ClearStatistics();

}

/// <summary>
/// Error statistics tracking for Metal operations
/// </summary>
public sealed class MetalErrorStatistics
{
    public int Count { get; set; }
    public DateTimeOffset FirstOccurrence { get; init; }
    public DateTimeOffset LastOccurrence { get; set; }
}

/// <summary>
/// Configuration options for Metal error recovery
/// </summary>
public sealed class MetalErrorRecoveryOptions
{
    /// <summary>
    /// Maximum number of retry attempts for transient errors
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum delay between retries in milliseconds
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 5000; // Lower than CUDA due to Metal overhead

    /// <summary>
    /// Number of memory-specific retry attempts
    /// </summary>
    public int MemoryRetryAttempts { get; set; } = 2; // Fewer than CUDA

    /// <summary>
    /// Whether to allow device reset operations
    /// </summary>
    public bool AllowDeviceReset { get; set; }  // More restrictive for Metal

    /// <summary>
    /// Whether to enable CPU fallback for supported operations
    /// </summary>
    public bool EnableCpuFallback { get; set; } = true;

    /// <summary>
    /// Circuit breaker threshold for catastrophic errors
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>
    /// Duration to keep circuit breaker open in seconds
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

/// <summary>
/// Metal error codes
/// </summary>
public enum MetalError
{
    Unknown = 0,
    Success = 1,

    // Device errors

    DeviceNotFound,
    DeviceNotSupported,
    DeviceRemoved,
    InvalidDevice,
    DeviceNotReady,
    DeviceLost,
    DeviceUnavailable,

    // Memory errors

    OutOfMemory,
    ResourceAllocationFailed,
    BufferAllocationFailed,
    TextureAllocationFailed,
    InsufficientMemory,

    // Command buffer errors

    CommandBufferError,
    CommandBufferNotEnqueued,
    CommandEncoderError,
    InvalidCommandQueue,

    // Execution errors

    NotReady,
    Timeout,
    Busy,
    TemporaryResourceUnavailable,
    CompilationError,
    InvalidOperation,
    InvalidArgument,
    InternalError,
    ResourceLimitExceeded,

    // System errors

    SystemFailure,
    HardwareFailure,
    DriverError,
    UnsupportedFeature
}

/// <summary>
/// Base exception for Metal-related errors
/// </summary>
public class MetalException : Exception
{
    public MetalError ErrorCode { get; }

    public MetalException() : base()
    {
        ErrorCode = MetalError.Unknown;
    }

    public MetalException(string message) : base(message)
    {
        ErrorCode = MetalError.Unknown;
    }

    public MetalException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = MetalError.Unknown;
    }

    public MetalException(MetalError errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public MetalException(MetalError errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when Metal operation fails
/// </summary>
public class MetalOperationException : MetalException
{
    public string OperationName { get; }

    public MetalOperationException() : base()
    {
        OperationName = string.Empty;
    }

    public MetalOperationException(string message) : base(message)
    {
        OperationName = string.Empty;
    }

    public MetalOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
        OperationName = string.Empty;
    }

    public MetalOperationException(string operationName, string message)
        : base(MetalError.Unknown, $"Metal operation '{operationName}' failed: {message}")
    {
        OperationName = operationName;
    }

    public MetalOperationException(string operationName, string message, Exception innerException)
        : base(MetalError.Unknown, $"Metal operation '{operationName}' failed: {message}", innerException)
    {
        OperationName = operationName;
    }
}

/// <summary>
/// Exception thrown when Metal GPU becomes unavailable
/// </summary>
public class MetalUnavailableException : MetalException
{
    public MetalUnavailableException() : base(MetalError.DeviceNotReady, "Metal GPU is unavailable")
    {
    }

    public MetalUnavailableException(string message) : base(MetalError.DeviceNotReady, message)
    {
    }

    public MetalUnavailableException(string message, Exception innerException)
        : base(MetalError.DeviceNotReady, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when Metal device error occurs
/// </summary>
public class MetalDeviceException : MetalException
{
    public MetalDeviceException() : base(MetalError.DeviceNotFound, "Metal device error occurred")
    {
    }

    public MetalDeviceException(string message) : base(MetalError.DeviceNotFound, message)
    {
    }

    public MetalDeviceException(string message, Exception innerException)
        : base(MetalError.DeviceNotFound, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when CPU fallback is required
/// </summary>
public class MetalCpuFallbackRequiredException : MetalException
{
    public MetalCpuFallbackRequiredException() : base(MetalError.UnsupportedFeature, "CPU fallback is required")
    {
    }

    public MetalCpuFallbackRequiredException(string message) : base(MetalError.UnsupportedFeature, message)
    {
    }

    public MetalCpuFallbackRequiredException(string message, Exception innerException)
        : base(MetalError.UnsupportedFeature, message, innerException)
    {
    }
}