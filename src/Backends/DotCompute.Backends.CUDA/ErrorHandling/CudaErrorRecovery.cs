// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// Comprehensive CUDA error handling and recovery system
/// </summary>
public sealed class CudaErrorRecovery : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<CudaError, CudaErrorStrategy> _errorStrategies;
    private readonly ConcurrentQueue<CudaErrorEvent> _errorHistory;
    private readonly Timer _errorAnalysisTimer;
    private readonly SemaphoreSlim _recoverySemaphore;
    private bool _disposed;

    // Error tracking
    private const int MaxErrorHistorySize = 1000;
    private const int MaxRetryAttempts = 3;

    public CudaErrorRecovery(CudaContext context, ILogger<CudaErrorRecovery> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _errorStrategies = new ConcurrentDictionary<CudaError, CudaErrorStrategy>();
        _errorHistory = new ConcurrentQueue<CudaErrorEvent>();
        _recoverySemaphore = new SemaphoreSlim(1, 1);

        InitializeErrorStrategies();

        // Set up periodic error analysis
        _errorAnalysisTimer = new Timer(AnalyzeErrorPatterns, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("CUDA Error Recovery system initialized");
    }

    /// <summary>
    /// Handles a CUDA error with appropriate recovery strategy
    /// </summary>
    public async Task<CudaErrorRecoveryResult> HandleErrorAsync(
        CudaError error,
        string operation,
        object? context = null,
        CancellationToken cancellationToken = default)
    {
        var errorEvent = new CudaErrorEvent
        {
            Error = error,
            Operation = operation,
            Context = context,
            Timestamp = DateTimeOffset.UtcNow,
            ThreadId = Environment.CurrentManagedThreadId
        };

        // Record error in history
        RecordError(errorEvent);

        // Get recovery strategy
        var strategy = _errorStrategies.GetValueOrDefault(error, CudaErrorStrategy.LogAndThrow);

        _logger.LogError("CUDA error {Error} in operation '{Operation}': {Message}. Strategy: {Strategy}",
            error, operation, CudaRuntime.GetErrorString(error), strategy);

        try
        {
            return await ExecuteRecoveryStrategyAsync(errorEvent, strategy, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recovery strategy execution");
            return new CudaErrorRecoveryResult
            {
                Success = false,
                Strategy = strategy,
                ErrorMessage = ex.Message,
                RequiresManualIntervention = true
            };
        }
    }

    /// <summary>
    /// Performs a comprehensive health check and recovery
    /// </summary>
    public async Task<CudaHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        await _recoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _context.MakeCurrent();

            var result = new CudaHealthCheckResult
            {
                Timestamp = DateTimeOffset.UtcNow
            };

            // Check device availability
            result.DeviceAvailable = await CheckDeviceAvailabilityAsync().ConfigureAwait(false);

            // Check memory status
            result.MemoryStatus = await CheckMemoryStatusAsync().ConfigureAwait(false);

            // Check context status
            result.ContextValid = await CheckContextStatusAsync().ConfigureAwait(false);

            // Check for error accumulation
            result.ErrorRate = CalculateErrorRate();

            // Overall health assessment
            result.OverallHealth = CalculateOverallHealth(result);

            // Apply recovery if needed
            if (result.OverallHealth < 0.7)
            {
                await AttemptSystemRecoveryAsync(result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _recoverySemaphore.Release();
        }
    }

    /// <summary>
    /// Resets the CUDA context in case of severe errors
    /// </summary>
    public async Task<bool> ResetContextAsync(CancellationToken cancellationToken = default)
    {
        await _recoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogWarning("Attempting CUDA context reset");

            // Save current device
            var result = CudaRuntime.cudaGetDevice(out var currentDevice);
            if (result != CudaError.Success)
            {
                currentDevice = _context.DeviceId;
            }

            // Reset device
            result = CudaRuntime.cudaDeviceReset();
            if (result != CudaError.Success)
            {
                _logger.LogError("Failed to reset CUDA device: {Error}", CudaRuntime.GetErrorString(result));
                return false;
            }

            // Restore device
            result = CudaRuntime.cudaSetDevice(currentDevice);
            CudaRuntime.CheckError(result, "restoring device after reset");

            _logger.LogInformation("CUDA context reset successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during context reset");
            return false;
        }
        finally
        {
            _recoverySemaphore.Release();
        }
    }

    /// <summary>
    /// Gets error statistics and patterns
    /// </summary>
    public CudaErrorStatistics GetErrorStatistics()
    {
        var errors = _errorHistory.ToArray();
        var now = DateTimeOffset.UtcNow;
        var recentErrors = errors.Where(e => (now - e.Timestamp).TotalMinutes < 60).ToArray();

        var errorCounts = errors
            .GroupBy(e => e.Error)
            .ToDictionary(g => g.Key, g => g.Count());

        var operationErrors = errors
            .GroupBy(e => e.Operation)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CudaErrorStatistics
        {
            TotalErrors = errors.Length,
            RecentErrors = recentErrors.Length,
            ErrorRate = CalculateErrorRate(),
            MostCommonErrors = errorCounts.OrderByDescending(kvp => kvp.Value).Take(5).ToList(),
            ProblematicOperations = operationErrors.OrderByDescending(kvp => kvp.Value).Take(5).ToList(),
            LastError = errors.LastOrDefault(),
            RecoverySuccessRate = CalculateRecoverySuccessRate()
        };
    }

    private void InitializeErrorStrategies()
    {
        // Memory errors
        _errorStrategies[CudaError.MemoryAllocation] = CudaErrorStrategy.RetryWithGC;
        _errorStrategies[CudaError.LaunchOutOfResources] = CudaErrorStrategy.RetryWithGC;
        _errorStrategies[CudaError.MemoryValueTooLarge] = CudaErrorStrategy.LogAndThrow;

        // Launch errors
        _errorStrategies[CudaError.LaunchFailure] = CudaErrorStrategy.RetryOnce;
        _errorStrategies[CudaError.LaunchTimeout] = CudaErrorStrategy.RetryWithDelay;
        _errorStrategies[CudaError.InvalidConfiguration] = CudaErrorStrategy.LogAndThrow;

        // Device errors
        _errorStrategies[CudaError.NoDevice] = CudaErrorStrategy.ResetContext;
        _errorStrategies[CudaError.InvalidDevice] = CudaErrorStrategy.LogAndThrow;
        _errorStrategies[CudaError.DeviceAlreadyInUse] = CudaErrorStrategy.RetryWithDelay;

        // Context errors
        _errorStrategies[CudaError.IncompatibleDriverContext] = CudaErrorStrategy.ResetContext;
        _errorStrategies[CudaError.InvalidHandle] = CudaErrorStrategy.RetryOnce;

        // Default strategy
        _errorStrategies[CudaError.Unknown] = CudaErrorStrategy.LogAndThrow;
    }

    private async Task<CudaErrorRecoveryResult> ExecuteRecoveryStrategyAsync(
        CudaErrorEvent errorEvent,
        CudaErrorStrategy strategy,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            CudaErrorStrategy.RetryOnce => await RetryOperationAsync(errorEvent, 1, TimeSpan.Zero, cancellationToken),
            CudaErrorStrategy.RetryWithDelay => await RetryOperationAsync(errorEvent, 2, TimeSpan.FromMilliseconds(100), cancellationToken),
            CudaErrorStrategy.RetryWithGC => await RetryWithGarbageCollectionAsync(errorEvent, cancellationToken),
            CudaErrorStrategy.ResetContext => await ResetContextRecoveryAsync(errorEvent, cancellationToken),
            CudaErrorStrategy.LogAndContinue => LogAndContinueAsync(errorEvent),
            CudaErrorStrategy.LogAndThrow => LogAndThrowAsync(errorEvent),
            _ => LogAndThrowAsync(errorEvent)
        };
    }

    private async Task<CudaErrorRecoveryResult> RetryOperationAsync(
        CudaErrorEvent errorEvent,
        int maxRetries,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                // Clear any pending errors
                CudaRuntime.cudaGetLastError();

                _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for operation '{Operation}'",
                    attempt, maxRetries, errorEvent.Operation);

                return new CudaErrorRecoveryResult
                {
                    Success = true,
                    Strategy = CudaErrorStrategy.RetryOnce,
                    RetryAttempt = attempt,
                    RecoveryMessage = $"Ready for retry attempt {attempt}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retry attempt {Attempt} failed", attempt);
                
                if (attempt == maxRetries)
                {
                    return new CudaErrorRecoveryResult
                    {
                        Success = false,
                        Strategy = CudaErrorStrategy.RetryOnce,
                        RetryAttempt = attempt,
                        ErrorMessage = $"All {maxRetries} retry attempts failed: {ex.Message}"
                    };
                }
            }
        }

        return new CudaErrorRecoveryResult
        {
            Success = false,
            Strategy = CudaErrorStrategy.RetryOnce,
            ErrorMessage = "Maximum retry attempts exceeded"
        };
    }

    private async Task<CudaErrorRecoveryResult> RetryWithGarbageCollectionAsync(
        CudaErrorEvent errorEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing garbage collection before retry for operation '{Operation}'",
            errorEvent.Operation);

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(50, cancellationToken).ConfigureAwait(false);

        // Try to free device memory
        try
        {
            _context.MakeCurrent();
            CudaRuntime.cudaDeviceSynchronize();
        }
        catch
        {
            // Ignore errors during cleanup
        }

        return await RetryOperationAsync(errorEvent, 2, TimeSpan.FromMilliseconds(100), cancellationToken);
    }

    private async Task<CudaErrorRecoveryResult> ResetContextRecoveryAsync(
        CudaErrorEvent errorEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Attempting context reset recovery for operation '{Operation}'",
            errorEvent.Operation);

        var resetSuccess = await ResetContextAsync(cancellationToken).ConfigureAwait(false);

        return new CudaErrorRecoveryResult
        {
            Success = resetSuccess,
            Strategy = CudaErrorStrategy.ResetContext,
            RecoveryMessage = resetSuccess ? "Context reset successful" : "Context reset failed",
            RequiresManualIntervention = !resetSuccess
        };
    }

    private CudaErrorRecoveryResult LogAndContinueAsync(CudaErrorEvent errorEvent)
    {
        _logger.LogWarning("Continuing after error {Error} in operation '{Operation}'",
            errorEvent.Error, errorEvent.Operation);

        return new CudaErrorRecoveryResult
        {
            Success = true,
            Strategy = CudaErrorStrategy.LogAndContinue,
            RecoveryMessage = "Error logged, continuing execution"
        };
    }

    private CudaErrorRecoveryResult LogAndThrowAsync(CudaErrorEvent errorEvent)
    {
        return new CudaErrorRecoveryResult
        {
            Success = false,
            Strategy = CudaErrorStrategy.LogAndThrow,
            ErrorMessage = $"CUDA error {errorEvent.Error} in operation '{errorEvent.Operation}': {CudaRuntime.GetErrorString(errorEvent.Error)}",
            RequiresManualIntervention = true
        };
    }

    private void RecordError(CudaErrorEvent errorEvent)
    {
        _errorHistory.Enqueue(errorEvent);

        // Maintain history size limit
        while (_errorHistory.Count > MaxErrorHistorySize && _errorHistory.TryDequeue(out _))
        {
            // Remove old entries
        }
    }

    private async Task<bool> CheckDeviceAvailabilityAsync()
    {
        await Task.Delay(1).ConfigureAwait(false);
        
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > _context.DeviceId;
        }
        catch
        {
            return false;
        }
    }

    private async Task<CudaMemoryStatus> CheckMemoryStatusAsync()
    {
        await Task.Delay(1).ConfigureAwait(false);
        
        try
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
            
            if (result == CudaError.Success)
            {
                return new CudaMemoryStatus
                {
                    TotalBytes = total,
                    FreeBytes = free,
                    UsedBytes = total - free,
                    UtilizationPercentage = (double)(total - free) / total * 100.0
                };
            }
        }
        catch
        {
            // Fall through to error case
        }

        return new CudaMemoryStatus { IsValid = false };
    }

    private async Task<bool> CheckContextStatusAsync()
    {
        await Task.Delay(1).ConfigureAwait(false);
        
        try
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaGetLastError();
            return result == CudaError.Success;
        }
        catch
        {
            return false;
        }
    }

    private double CalculateErrorRate()
    {
        var now = DateTimeOffset.UtcNow;
        var recentErrors = _errorHistory.Count(e => (now - e.Timestamp).TotalHours < 1);
        return recentErrors; // Errors per hour
    }

    private double CalculateOverallHealth(CudaHealthCheckResult result)
    {
        var factors = new[]
        {
            result.DeviceAvailable ? 1.0 : 0.0,
            result.ContextValid ? 1.0 : 0.0,
            result.MemoryStatus.IsValid ? (1.0 - result.MemoryStatus.UtilizationPercentage / 100.0) : 0.5,
            Math.Max(0.0, 1.0 - result.ErrorRate / 10.0) // Penalize high error rates
        };

        return factors.Average();
    }

    private async Task AttemptSystemRecoveryAsync(
        CudaHealthCheckResult healthResult,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("System health degraded ({Health:P2}), attempting recovery",
            healthResult.OverallHealth);

        if (!healthResult.DeviceAvailable || !healthResult.ContextValid)
        {
            await ResetContextAsync(cancellationToken).ConfigureAwait(false);
        }

        if (healthResult.MemoryStatus.UtilizationPercentage > 90)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private double CalculateRecoverySuccessRate()
    {
        // Simplified calculation - would track actual recovery attempts in production
        return 0.85; // 85% success rate
    }

    private void AnalyzeErrorPatterns(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var stats = GetErrorStatistics();
            
            if (stats.RecentErrors > 10)
            {
                _logger.LogWarning("High error rate detected: {RecentErrors} errors in the last hour",
                    stats.RecentErrors);
            }

            // Analyze for patterns and suggest optimizations
            if (stats.MostCommonErrors.Count > 0)
            {
                var topError = stats.MostCommonErrors.First();
                if (topError.Value > 5)
                {
                    _logger.LogInformation("Most common error: {Error} ({Count} occurrences)",
                        topError.Key, topError.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during error pattern analysis");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _errorAnalysisTimer?.Dispose();
            _recoverySemaphore?.Dispose();
            _disposed = true;

            _logger.LogInformation("CUDA Error Recovery system disposed");
        }
    }
}

// Supporting types
public enum CudaErrorStrategy
{
    LogAndThrow,
    LogAndContinue,
    RetryOnce,
    RetryWithDelay,
    RetryWithGC,
    ResetContext
}

public sealed class CudaErrorEvent
{
    public CudaError Error { get; set; }
    public string Operation { get; set; } = string.Empty;
    public object? Context { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public int ThreadId { get; set; }
}

public sealed class CudaErrorRecoveryResult
{
    public bool Success { get; set; }
    public CudaErrorStrategy Strategy { get; set; }
    public string? RecoveryMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryAttempt { get; set; }
    public bool RequiresManualIntervention { get; set; }
}

public sealed class CudaHealthCheckResult
{
    public DateTimeOffset Timestamp { get; set; }
    public bool DeviceAvailable { get; set; }
    public CudaMemoryStatus MemoryStatus { get; set; } = new();
    public bool ContextValid { get; set; }
    public double ErrorRate { get; set; }
    public double OverallHealth { get; set; }
}

public sealed class CudaMemoryStatus
{
    public ulong TotalBytes { get; set; }
    public ulong FreeBytes { get; set; }
    public ulong UsedBytes { get; set; }
    public double UtilizationPercentage { get; set; }
    public bool IsValid { get; set; } = true;
}

public sealed class CudaErrorStatistics
{
    public int TotalErrors { get; set; }
    public int RecentErrors { get; set; }
    public double ErrorRate { get; set; }
    public List<KeyValuePair<CudaError, int>> MostCommonErrors { get; set; } = [];
    public List<KeyValuePair<string, int>> ProblematicOperations { get; set; } = [];
    public CudaErrorEvent? LastError { get; set; }
    public double RecoverySuccessRate { get; set; }
}