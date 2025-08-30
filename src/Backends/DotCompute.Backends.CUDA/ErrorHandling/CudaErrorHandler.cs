// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Polly;

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// Production-grade CUDA error handler with retry logic, graceful degradation,
/// and comprehensive error recovery strategies.
/// </summary>
public sealed class CudaErrorHandler : IDisposable
{
    private readonly ILogger<CudaErrorHandler> _logger;
    private readonly ConcurrentDictionary<CudaError, ErrorStatistics> _errorStats;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IAsyncPolicy _memoryRetryPolicy;
    private readonly IAsyncPolicy<bool> _circuitBreakerPolicy;
    private readonly ErrorRecoveryOptions _options;
    private volatile bool _gpuAvailable = true;
    private DateTimeOffset _lastSuccessfulOperation = DateTimeOffset.UtcNow;

    public CudaErrorHandler(
        ILogger<CudaErrorHandler> logger,
        ErrorRecoveryOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ErrorRecoveryOptions();
        _errorStats = new ConcurrentDictionary<CudaError, ErrorStatistics>();
        
        _retryPolicy = CreateRetryPolicy();
        _memoryRetryPolicy = CreateMemoryRetryPolicy();
        _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
    }

    /// <summary>
    /// Gets whether GPU is currently available.
    /// </summary>
    public bool IsGpuAvailable => _gpuAvailable;

    /// <summary>
    /// Gets time since last successful operation.
    /// </summary>
    public TimeSpan TimeSinceLastSuccess => DateTimeOffset.UtcNow - _lastSuccessfulOperation;

    /// <summary>
    /// Creates the main retry policy for transient errors.
    /// </summary>
    private IAsyncPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<CudaException>(ex => IsTransientError(ex.ErrorCode))
            .WaitAndRetryAsync(
                _options.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromMilliseconds(
                    Math.Min(100 * Math.Pow(2, retryAttempt), _options.MaxRetryDelayMs)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    var ex = exception as CudaException;
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms for error: {Error}",
                        retryCount, _options.MaxRetryAttempts, timespan.TotalMilliseconds,
                        ex?.ErrorCode ?? CudaError.Unknown);
                    
                    RecordError(ex?.ErrorCode ?? CudaError.Unknown);
                });
    }

    /// <summary>
    /// Creates specialized retry policy for memory allocation errors.
    /// </summary>
    private IAsyncPolicy CreateMemoryRetryPolicy()
    {
        return Policy
            .Handle<CudaException>(ex => IsMemoryError(ex.ErrorCode))
            .WaitAndRetryAsync(
                _options.MemoryRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                onRetry: async (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Memory allocation retry {RetryCount}, attempting cleanup...",
                        retryCount);
                    
                    // Trigger memory cleanup
                    await TriggerMemoryCleanupAsync();
                });
    }

    /// <summary>
    /// Creates circuit breaker policy for catastrophic failures.
    /// </summary>
    private IAsyncPolicy<bool> CreateCircuitBreakerPolicy()
    {
        return Policy<bool>
            .Handle<CudaException>(ex => IsCatastrophicError(ex.ErrorCode))
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
                durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                onBreak: (result, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Duration}s due to repeated failures",
                        duration.TotalSeconds);
                    _gpuAvailable = false;
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset, GPU operations resuming");
                    _gpuAvailable = true;
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open, testing GPU availability");
                });
    }

    /// <summary>
    /// Executes a CUDA operation with comprehensive error handling.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check circuit breaker
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            var canExecute = await _circuitBreakerPolicy.ExecuteAsync(
                async () => 
                {
                    // Quick GPU health check
                    if (!_gpuAvailable)
                    {

                        return false;
                    }


                    var result = CudaRuntime.cudaGetLastError();
                    return result == CudaError.Success;
                });
#pragma warning restore CS1998

            if (!canExecute)
            {
                throw new CudaUnavailableException(
                    "GPU operations are currently unavailable due to repeated failures");
            }

            // Execute with retry
            var stopwatch = Stopwatch.StartNew();
            
            var result = await _retryPolicy.ExecuteAsync(
                async ct => await operation(), cancellationToken);
            
            stopwatch.Stop();
            
            // Record success
            _lastSuccessfulOperation = DateTimeOffset.UtcNow;
            RecordSuccess(operationName, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (CudaException cudaEx)
        {
            return await HandleCudaExceptionAsync<T>(cudaEx, operation, operationName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Operation}", operationName);
            throw new CudaOperationException($"Operation '{operationName}' failed", ex);
        }
    }

    /// <summary>
    /// Handles CUDA-specific exceptions with recovery strategies.
    /// </summary>
    private async Task<T> HandleCudaExceptionAsync<T>(
        CudaException cudaEx,
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        _logger.LogError(cudaEx, "CUDA error in {Operation}: {Error}",
            operationName, cudaEx.ErrorCode);

        // Try recovery strategies based on error type
        if (IsMemoryError(cudaEx.ErrorCode))
        {
            return await HandleMemoryErrorAsync(operation, operationName, cancellationToken);
        }
        
        if (IsDeviceError(cudaEx.ErrorCode))
        {
            return await HandleDeviceErrorAsync(operation, operationName, cancellationToken);
        }
        
        if (_options.EnableCpuFallback && CanFallbackToCpu(operationName))
        {
            return await FallbackToCpuAsync<T>(operationName);
        }

        throw new CudaOperationException(
            $"Operation '{operationName}' failed with error: {cudaEx.ErrorCode}", cudaEx);
    }

    /// <summary>
    /// Handles memory-related errors with cleanup and retry.
    /// </summary>
    private async Task<T> HandleMemoryErrorAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Attempting memory error recovery for {Operation}", operationName);

        try
        {
            return await _memoryRetryPolicy.ExecuteAsync(
                async ct =>
                {
                    // Force garbage collection
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                    
                    // Retry operation
                    return await operation();
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory error recovery failed for {Operation}", operationName);
            
            if (_options.EnableCpuFallback)
            {
                return await FallbackToCpuAsync<T>(operationName);
            }
            
            throw;
        }
    }

    /// <summary>
    /// Handles device-related errors with reset and retry.
    /// </summary>
    private async Task<T> HandleDeviceErrorAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Attempting device error recovery for {Operation}", operationName);

        try
        {
            // Reset device if allowed
            if (_options.AllowDeviceReset)
            {
                await ResetDeviceAsync();
                
                // Retry operation after reset
                return await operation();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device error recovery failed for {Operation}", operationName);
        }

        if (_options.EnableCpuFallback)
        {
            return await FallbackToCpuAsync<T>(operationName);
        }

        throw new CudaDeviceException($"Device error in operation '{operationName}'");
    }

    /// <summary>
    /// Falls back to CPU execution when GPU fails.
    /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<T> FallbackToCpuAsync<T>(string operationName)
    {
        _logger.LogInformation("Falling back to CPU for {Operation}", operationName);
        
        // This would invoke CPU-based implementation
        // For now, throw to indicate fallback is needed
        throw new CpuFallbackRequiredException(
            $"Operation '{operationName}' requires CPU fallback");
    }
#pragma warning restore CS1998

    /// <summary>
    /// Triggers memory cleanup on device.
    /// </summary>
    private async Task TriggerMemoryCleanupAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Clear error state
                _ = CudaRuntime.cudaGetLastError();
                
                // Synchronize device
                var result = CudaRuntime.cudaDeviceSynchronize();
                
                if (result == CudaError.Success)
                {
                    // Trigger memory compaction if available
                    _ = CudaRuntime.cudaMemGetInfo(out nuint free, out nuint total);
                    
                    _logger.LogInformation(
                        "Memory cleanup completed. Free: {Free:N0} MB, Total: {Total:N0} MB",
                        free / (1024 * 1024), total / (1024 * 1024));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory cleanup failed");
            }
        });
    }

    /// <summary>
    /// Resets the CUDA device.
    /// </summary>
    private async Task ResetDeviceAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.LogWarning("Resetting CUDA device...");
                
                var result = CudaRuntime.cudaDeviceReset();
                
                if (result == CudaError.Success)
                {
                    _logger.LogInformation("Device reset successful");
                    _gpuAvailable = true;
                }
                else
                {
                    _logger.LogError("Device reset failed: {Error}", result);
                    _gpuAvailable = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device reset exception");
                _gpuAvailable = false;
            }
        });
    }

    /// <summary>
    /// Determines if an error is transient and can be retried.
    /// </summary>
    private static bool IsTransientError(CudaError error)
    {
        return error switch
        {
            CudaError.NotReady => true,
            CudaError.Timeout => true,
            CudaError.LaunchTimeout => true,
            CudaError.PeerAccessNotEnabled => true,
            CudaError.StreamCaptureWrongThread => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is memory-related.
    /// </summary>
    private static bool IsMemoryError(CudaError error)
    {
        return error switch
        {
            CudaError.MemoryAllocation => true,
            CudaError.OutOfMemory => true,
            CudaError.InsufficientDriver => true,
            CudaError.SharedObjectInitFailed => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is device-related.
    /// </summary>
    private static bool IsDeviceError(CudaError error)
    {
        return error switch
        {
            CudaError.NoDevice => true,
            CudaError.InvalidDevice => true,
            CudaError.DeviceAlreadyInUse => true,
            CudaError.EccUncorrectable => true,
            CudaError.HardwareStackError => true,
            CudaError.IllegalInstruction => true,
            CudaError.IllegalAddress => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error is catastrophic.
    /// </summary>
    private static bool IsCatastrophicError(CudaError error)
    {
        return error switch
        {
            CudaError.EccUncorrectable => true,
            CudaError.HardwareStackError => true,
            CudaError.IllegalInstruction => true,
            CudaError.SystemNotReady => true,
            CudaError.SystemDriverMismatch => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an operation can fall back to CPU.
    /// </summary>
    private static bool CanFallbackToCpu(string operationName)
    {
        // Define operations that have CPU implementations
        var cpuSupportedOps = new[]
        {
            "matmul", "reduce", "scan", "sort", "fft",
            "convolution", "pooling", "activation"
        };
        
        return cpuSupportedOps.Any(op => 
            operationName.Contains(op, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Records error statistics.
    /// </summary>
    private void RecordError(CudaError error)
    {
        var stats = _errorStats.AddOrUpdate(error,
            _ => new ErrorStatistics { FirstOccurrence = DateTimeOffset.UtcNow },
            (_, existing) =>
            {
                existing.Count++;
                existing.LastOccurrence = DateTimeOffset.UtcNow;
                return existing;
            });
        
        stats.Count++;
    }

    /// <summary>
    /// Records successful operation.
    /// </summary>
    private void RecordSuccess(string operationName, long elapsedMs)
    {
        _logger.LogDebug("Operation {Operation} completed in {ElapsedMs}ms",
            operationName, elapsedMs);
    }

    /// <summary>
    /// Gets error statistics.
    /// </summary>
    public IReadOnlyDictionary<CudaError, ErrorStatistics> GetErrorStatistics() => _errorStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Clears error statistics.
    /// </summary>
    public void ClearStatistics()
    {
        _errorStats.Clear();
        _lastSuccessfulOperation = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Disposes the error handler resources.
    /// </summary>
    public void Dispose()
    {
        // CudaErrorHandler doesn't hold disposable resources directly,
        // but we clear statistics as cleanup
        ClearStatistics();
    }

    /// <summary>
    /// Error statistics tracking.
    /// </summary>
    public sealed class ErrorStatistics
    {
        public int Count { get; set; }
        public DateTimeOffset FirstOccurrence { get; init; }
        public DateTimeOffset LastOccurrence { get; set; }
    }
}

/// <summary>
/// Error recovery configuration options.
/// </summary>
public sealed class ErrorRecoveryOptions
{
    public int MaxRetryAttempts { get; init; } = 3;
    public int MaxRetryDelayMs { get; init; } = 5000;
    public int MemoryRetryAttempts { get; init; } = 2;
    public int CircuitBreakerThreshold { get; init; } = 5;
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
    public bool EnableCpuFallback { get; init; } = true;
    public bool AllowDeviceReset { get; init; }

    public bool EnableDiagnosticLogging { get; init; } = true;
}

/// <summary>
/// Exception thrown when CUDA is unavailable.
/// </summary>
public sealed class CudaUnavailableException : Exception
{
    public CudaUnavailableException(string message) : base(message) { }
    public CudaUnavailableException(string message, Exception inner) : base(message, inner) { }
    public CudaUnavailableException()
    {
    }
}

/// <summary>
/// Exception thrown for CUDA operations.
/// </summary>
public sealed class CudaOperationException : Exception
{
    public CudaOperationException(string message) : base(message) { }
    public CudaOperationException(string message, Exception inner) : base(message, inner) { }
    public CudaOperationException()
    {
    }
}

/// <summary>
/// Exception thrown for device errors.
/// </summary>
public sealed class CudaDeviceException : Exception
{
    public CudaDeviceException(string message) : base(message) { }
    public CudaDeviceException(string message, Exception inner) : base(message, inner) { }
    public CudaDeviceException()
    {
    }
}

/// <summary>
/// Exception indicating CPU fallback is required.
/// </summary>
public sealed class CpuFallbackRequiredException : Exception
{
    public CpuFallbackRequiredException(string message) : base(message) { }
    public CpuFallbackRequiredException()
    {
    }
    public CpuFallbackRequiredException(string message, Exception innerException) : base(message, innerException)
    {
    }
}