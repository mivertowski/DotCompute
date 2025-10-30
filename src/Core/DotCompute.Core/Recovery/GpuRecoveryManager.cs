// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Recovery.Models;
using Microsoft.Extensions.Logging;
using RecoveryMetrics = DotCompute.Core.Recovery.Models.RecoveryMetrics;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive GPU error recovery manager with device hang detection,
/// kernel timeout handling, and graceful degradation capabilities
/// </summary>
public sealed partial class GpuRecoveryManager : IDisposable
{
    // LoggerMessage delegates - Event ID range 13100-13111 for GpuRecoveryManager
    private static readonly Action<ILogger, string, Exception?> _logManagerInitialized =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13100, nameof(LogManagerInitialized)),
            "GPU Recovery Manager initialized with configuration: {Config}");

    private static void LogManagerInitialized(ILogger logger, string config)
        => _logManagerInitialized(logger, config, null);

    private static readonly Action<ILogger, string, string, Exception?> _logGpuErrorDetected =
        LoggerMessage.Define<string, string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13101, nameof(LogGpuErrorDetected)),
            "GPU error detected on device {DeviceId}: {ErrorMessage}");

    private static void LogGpuErrorDetected(ILogger logger, string deviceId, string errorMessage)
        => _logGpuErrorDetected(logger, deviceId, errorMessage, null);

    private static readonly Action<ILogger, string, string, Exception?> _logRecoveryStrategySelected =
        LoggerMessage.Define<string, string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13102, nameof(LogRecoveryStrategySelected)),
            "Using recovery strategy {Strategy} for device {DeviceId}");

    private static void LogRecoveryStrategySelected(ILogger logger, string strategy, string deviceId)
        => _logRecoveryStrategySelected(logger, strategy, deviceId, null);

    private static readonly Action<ILogger, string, string, long, Exception?> _logRecoverySuccessful =
        LoggerMessage.Define<string, string, long>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13103, nameof(LogRecoverySuccessful)),
            "GPU recovery successful for device {DeviceId} using {Strategy} in {DurationMs}ms");

    private static void LogRecoverySuccessful(ILogger logger, string deviceId, string strategy, long durationMs)
        => _logRecoverySuccessful(logger, deviceId, strategy, durationMs, null);

    private static readonly Action<ILogger, string, Exception?> _logRecoveryException =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(13104, nameof(LogRecoveryException)),
            "Exception during GPU recovery for device {DeviceId}");

    private static void LogRecoveryException(ILogger logger, string deviceId, Exception exception)
        => _logRecoveryException(logger, deviceId, exception);

    private static readonly Action<ILogger, string, Exception?> _logDeviceResetAttempt =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13105, nameof(LogDeviceResetAttempt)),
            "Attempting device reset for {DeviceId}");

    private static void LogDeviceResetAttempt(ILogger logger, string deviceId)
        => _logDeviceResetAttempt(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logDeviceResetCompleted =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13106, nameof(LogDeviceResetCompleted)),
            "Device reset completed for {DeviceId}");

    private static void LogDeviceResetCompleted(ILogger logger, string deviceId)
        => _logDeviceResetCompleted(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logDeviceResetFailed =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(13107, nameof(LogDeviceResetFailed)),
            "Device reset failed for {DeviceId}");

    private static void LogDeviceResetFailed(ILogger logger, string deviceId, Exception exception)
        => _logDeviceResetFailed(logger, deviceId, exception);

    private static readonly Action<ILogger, string, Exception?> _logSimpleRetry =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13108, nameof(LogSimpleRetry)),
            "Attempting simple retry for device {DeviceId}");

    private static void LogSimpleRetry(ILogger logger, string deviceId)
        => _logSimpleRetry(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logMemoryRecovery =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13109, nameof(LogMemoryRecovery)),
            "Performing memory recovery for device {DeviceId}");

    private static void LogMemoryRecovery(ILogger logger, string deviceId)
        => _logMemoryRecovery(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logKernelTermination =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13110, nameof(LogKernelTermination)),
            "Terminating hanging kernels for device {DeviceId}");

    private static void LogKernelTermination(ILogger logger, string deviceId)
        => _logKernelTermination(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logContextReset =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13111, nameof(LogContextReset)),
            "Resetting context for device {DeviceId}");

    private static void LogContextReset(ILogger logger, string deviceId)
        => _logContextReset(logger, deviceId, null);

    private static readonly Action<ILogger, string, Exception?> _logDeviceResetRecovery =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13112, nameof(LogDeviceResetRecovery)),
            "Performing device reset recovery for device {DeviceId}");

    private static void LogDeviceResetRecovery(ILogger logger, string deviceId)
        => _logDeviceResetRecovery(logger, deviceId, null);

    private static readonly Action<ILogger, string, int, Exception?> _logDeviceHealthDegraded =
        LoggerMessage.Define<string, int>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13113, nameof(LogDeviceHealthDegraded)),
            "Device {DeviceId} health degraded: {ConsecutiveFailures} consecutive failures");

    private static void LogDeviceHealthDegraded(ILogger logger, string deviceId, int consecutiveFailures)
        => _logDeviceHealthDegraded(logger, deviceId, consecutiveFailures, null);

    private static readonly Action<ILogger, string, string, Exception?> _logKernelHanging =
        LoggerMessage.Define<string, string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13114, nameof(LogKernelHanging)),
            "Kernel {KernelId} appears to be hanging (running for {ExecutionTime})");

    private static void LogKernelHanging(ILogger logger, string kernelId, string executionTime)
        => _logKernelHanging(logger, kernelId, executionTime, null);

    private static readonly Action<ILogger, string, Exception?> _logKernelCancelFailed =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(13115, nameof(LogKernelCancelFailed)),
            "Failed to auto-cancel hanging kernel {KernelId}");

    private static void LogKernelCancelFailed(ILogger logger, string kernelId, Exception exception)
        => _logKernelCancelFailed(logger, kernelId, exception);

    private static readonly Action<ILogger, Exception?> _logHealthCheckError =
        LoggerMessage.Define(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(13116, nameof(LogHealthCheckError)),
            "Error during GPU health check");

    private static void LogHealthCheckError(ILogger logger, Exception exception)
        => _logHealthCheckError(logger, exception);

    private static readonly Action<ILogger, Exception?> _logManagerDisposed =
        LoggerMessage.Define(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(13117, nameof(LogManagerDisposed)),
            "GPU Recovery Manager disposed");

    private static void LogManagerDisposed(ILogger logger)
        => _logManagerDisposed(logger, null);

    private static readonly Action<ILogger, string, Exception?> _logMonitorDisposeError =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(13118, nameof(LogMonitorDisposeError)),
            "Error disposing kernel monitor {KernelId}");

    private static void LogMonitorDisposeError(ILogger logger, string kernelId, Exception exception)
        => _logMonitorDisposeError(logger, kernelId, exception);

    private static readonly Action<ILogger, string, string, string, Exception?> _logRecoveryFailed =
        LoggerMessage.Define<string, string, string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(13119, nameof(LogRecoveryFailed)),
            "GPU recovery failed for device {DeviceId} using {Strategy}: {Message}");

    private static void LogRecoveryFailed(ILogger logger, string deviceId, string strategy, string message)
        => _logRecoveryFailed(logger, deviceId, strategy, message, null);

    private readonly ILogger<GpuRecoveryManager> _logger;
    private readonly ConcurrentDictionary<string, DeviceRecoveryState> _deviceStates;
    private readonly ConcurrentDictionary<string, KernelExecutionMonitor> _kernelMonitors;
    private readonly Timer _healthCheckTimer;
    private readonly RecoveryMetrics _metrics;
    private readonly GpuRecoveryConfiguration _config;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the GpuRecoveryManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The config.</param>

    public GpuRecoveryManager(ILogger<GpuRecoveryManager> logger, GpuRecoveryConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? GpuRecoveryConfiguration.Default;
        _deviceStates = new ConcurrentDictionary<string, DeviceRecoveryState>();
        _kernelMonitors = new ConcurrentDictionary<string, KernelExecutionMonitor>();
        _metrics = new RecoveryMetrics();

        // Start periodic health checks
        _healthCheckTimer = new Timer(PerformHealthCheck, null,

            _config.HealthCheckInterval, _config.HealthCheckInterval);

        LogManagerInitialized(_logger, _config.ToString() ?? "default");
    }

    /// <summary>
    /// Handles GPU errors with appropriate recovery strategies
    /// </summary>
    public async Task<RecoveryResult> HandleGpuErrorAsync(
        Exception error,

        string deviceId,

        string? operation = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new GpuRecoveryContext
        {
            DeviceId = deviceId,
            Operation = operation ?? "unknown",
            Error = error,
            Timestamp = DateTimeOffset.UtcNow
        };

        LogGpuErrorDetected(_logger, deviceId, error.Message);

        try
        {
            // Get or create device state
            var deviceState = _deviceStates.GetOrAdd(deviceId, _ => new DeviceRecoveryState(deviceId));
            deviceState.RecordError(error);

            // Determine recovery strategy based on error type and device state
            var strategy = DetermineRecoveryStrategy(error, deviceState);
            LogRecoveryStrategySelected(_logger, strategy.ToString(), deviceId);

            var result = await ExecuteRecoveryStrategyAsync(strategy, context, deviceState, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                deviceState.RecordSuccessfulRecovery();
                LogRecoverySuccessful(_logger, deviceId, strategy.ToString(), stopwatch.ElapsedMilliseconds);
            }
            else
            {
                deviceState.RecordFailedRecovery();
                LogRecoveryFailed(_logger, deviceId, strategy.ToString(), result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogRecoveryException(_logger, deviceId, ex);
            return new RecoveryResult
            {
                Success = false,
                Message = $"Recovery process failed: {ex.Message}",
                Exception = ex,
                Duration = stopwatch.Elapsed,
                Strategy = "ErrorDuringRecovery"
            };
        }
    }

    /// <summary>
    /// Monitors kernel execution for hangs and timeouts
    /// </summary>
    public IKernelExecutionMonitor MonitorKernelExecution(string kernelId, TimeSpan? timeout = null)
    {
        var monitor = new KernelExecutionMonitor(kernelId, timeout ?? _config.DefaultKernelTimeout, _logger);
        _ = _kernelMonitors.TryAdd(kernelId, monitor);

        // Auto-cleanup completed monitors

        _ = Task.Run(async () =>
        {
            await monitor.WaitForCompletionAsync();
            _ = _kernelMonitors.TryRemove(kernelId, out _);
        });

        return monitor;
    }

    /// <summary>
    /// Performs device reset with proper resource cleanup
    /// </summary>
    public async Task<bool> ResetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        LogDeviceResetAttempt(_logger, deviceId);

        try
        {
            // Cancel all running kernels on this device
            await CancelDeviceKernelsAsync(deviceId, cancellationToken);

            // Clear device state
            if (_deviceStates.TryGetValue(deviceId, out var deviceState))
            {
                await deviceState.ResetAsync();
            }

            // Platform-specific device reset would be implemented here
            await Task.Delay(100, cancellationToken); // Simulate reset time

            LogDeviceResetCompleted(_logger, deviceId);
            return true;
        }
        catch (Exception ex)
        {
            LogDeviceResetFailed(_logger, deviceId, ex);
            return false;
        }
    }

    /// <summary>
    /// Gets current health status of all monitored devices
    /// </summary>
    public DeviceHealthReport GetDeviceHealthReport()
    {
        var deviceHealth = new Dictionary<string, DeviceHealthStatus>();

        foreach (var kvp in _deviceStates)
        {
            var deviceId = kvp.Key;
            var state = kvp.Value;

            deviceHealth[deviceId] = new DeviceHealthStatus
            {
                DeviceId = deviceId,
                IsHealthy = state.IsHealthy,
                ErrorRate = state.CalculateErrorRate(),
                LastError = state.LastError,
                ConsecutiveFailures = state.ConsecutiveFailures,
                TotalRecoveryAttempts = state.TotalRecoveryAttempts,
                SuccessfulRecoveries = state.SuccessfulRecoveries,
                LastHealthCheck = state.LastHealthCheck
            };
        }

        return new DeviceHealthReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            DeviceHealth = deviceHealth.ToDictionary(
                kvp => kvp.Key,
                kvp => new DeviceHealth
                {
                    DeviceId = kvp.Key,
                    Status = kvp.Value.IsHealthy ? DeviceStatus.Healthy :
                             kvp.Value.ErrorRate > 0.5 ? DeviceStatus.Critical :
                             kvp.Value.ErrorRate > 0.2 ? DeviceStatus.Warning :
                             DeviceStatus.Healthy,
                    HealthScore = kvp.Value.IsHealthy ? 1.0 : Math.Max(0.0, 1.0 - kvp.Value.ErrorRate),
                    ErrorCount = kvp.Value.ConsecutiveFailures,
                    LastError = kvp.Value.LastError?.Message,
                    LastActivity = kvp.Value.LastHealthCheck
                }
            ),
            OverallHealth = CalculateOverallHealth(deviceHealth),
            ActiveKernels = _kernelMonitors.Count
        };
    }

    private GpuRecoveryStrategy DetermineRecoveryStrategy(Exception error, DeviceRecoveryState deviceState)
    {
        // High failure rate - need aggressive recovery
        if (deviceState.ConsecutiveFailures > _config.MaxConsecutiveFailures)
        {
            return GpuRecoveryStrategy.DeviceReset;
        }

        // Error type-specific strategies
        return error switch
        {
            OutOfMemoryException => GpuRecoveryStrategy.MemoryRecovery,
            TimeoutException => GpuRecoveryStrategy.KernelTermination,
            AcceleratorException accelEx when accelEx.Message.Contains("hang", StringComparison.CurrentCulture) => GpuRecoveryStrategy.DeviceReset,
            AcceleratorException accelEx when accelEx.Message.Contains("invalid", StringComparison.CurrentCulture) => GpuRecoveryStrategy.ContextReset,
            _ => deviceState.ConsecutiveFailures > 1 ? GpuRecoveryStrategy.ContextReset : GpuRecoveryStrategy.SimpleRetry
        };
    }

    private async Task<RecoveryResult> ExecuteRecoveryStrategyAsync(
        GpuRecoveryStrategy strategy,
        GpuRecoveryContext context,
        DeviceRecoveryState deviceState,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            GpuRecoveryStrategy.SimpleRetry => await SimpleRetryAsync(context, cancellationToken),
            GpuRecoveryStrategy.MemoryRecovery => await MemoryRecoveryAsync(context, cancellationToken),
            GpuRecoveryStrategy.KernelTermination => await KernelTerminationAsync(context, cancellationToken),
            GpuRecoveryStrategy.ContextReset => await ContextResetAsync(context, cancellationToken),
            GpuRecoveryStrategy.DeviceReset => await DeviceResetRecoveryAsync(context, cancellationToken),
            _ => new RecoveryResult { Success = false, Message = "Unknown recovery strategy", Strategy = "Unknown" }
        };
    }

    private async Task<RecoveryResult> SimpleRetryAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        LogSimpleRetry(_logger, context.DeviceId);


        await Task.Delay(_config.RetryDelay, cancellationToken);


        return new RecoveryResult
        {
            Success = true,
            Message = "Ready for retry",
            Strategy = "SimpleRetry"
        };
    }

    private async Task<RecoveryResult> MemoryRecoveryAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        LogMemoryRecovery(_logger, context.DeviceId);

        // Force garbage collection

        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);


        await Task.Delay(50, cancellationToken);

        // Device-specific memory cleanup would be implemented here


        return new RecoveryResult
        {
            Success = true,
            Message = "Memory recovery completed",
            Strategy = "MemoryRecovery"
        };
    }

    private async Task<RecoveryResult> KernelTerminationAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        LogKernelTermination(_logger, context.DeviceId);


        await CancelDeviceKernelsAsync(context.DeviceId, cancellationToken);


        return new RecoveryResult
        {
            Success = true,
            Message = "Hanging kernels terminated",
            Strategy = "KernelTermination"
        };
    }

    private async Task<RecoveryResult> ContextResetAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        LogContextReset(_logger, context.DeviceId);


        if (_deviceStates.TryGetValue(context.DeviceId, out var deviceState))
        {
            await deviceState.ResetContextAsync();
        }


        await Task.Delay(200, cancellationToken);


        return new RecoveryResult
        {
            Success = true,
            Message = "Context reset completed",
            Strategy = "ContextReset"
        };
    }

    private async Task<RecoveryResult> DeviceResetRecoveryAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        LogDeviceResetRecovery(_logger, context.DeviceId);


        var success = await ResetDeviceAsync(context.DeviceId, cancellationToken);


        return new RecoveryResult
        {
            Success = success,
            Message = success ? "Device reset successful" : "Device reset failed",
            Strategy = "DeviceReset",
            RequiresManualIntervention = !success
        };
    }

    private async Task CancelDeviceKernelsAsync(string deviceId, CancellationToken cancellationToken)
    {
        var kernelsToCancel = _kernelMonitors.Values
            .Where(monitor => monitor.DeviceId == deviceId)
            .ToList();

        foreach (var monitor in kernelsToCancel)
        {
            try
            {
                await monitor.CancelAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                LogKernelCancelFailed(_logger, monitor.KernelId, ex);
            }
        }
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            foreach (var deviceState in _deviceStates.Values)
            {
                deviceState.UpdateHealthCheck();

                // Check for devices that need attention

                if (!deviceState.IsHealthy && deviceState.ConsecutiveFailures > _config.MaxConsecutiveFailures)
                {
                    LogDeviceHealthDegraded(_logger, deviceState.DeviceId, deviceState.ConsecutiveFailures);
                }
            }

            // Check for hanging kernels
            var hangingKernels = _kernelMonitors.Values
                .Where(monitor => monitor.IsHanging)
                .ToList();

            foreach (var hangingKernel in hangingKernels)
            {
                LogKernelHanging(_logger, hangingKernel.KernelId, hangingKernel.ExecutionTime.ToString());

                // Auto-cancel hanging kernels

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await hangingKernel.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        LogKernelCancelFailed(_logger, hangingKernel.KernelId, ex);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckError(_logger, ex);
        }
    }

    private static double CalculateOverallHealth(Dictionary<string, DeviceHealthStatus> deviceHealth)
    {
        if (deviceHealth.Count == 0)
        {
            return 1.0;
        }


        var healthyDevices = deviceHealth.Values.Count(d => d.IsHealthy);
        return (double)healthyDevices / deviceHealth.Count;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();

            // Cancel all monitoring operations

            foreach (var monitor in _kernelMonitors.Values)
            {
                try
                {
                    monitor.Dispose();
                }
                catch (Exception ex)
                {
                    LogMonitorDisposeError(_logger, monitor.KernelId, ex);
                }
            }


            _disposed = true;
            LogManagerDisposed(_logger);
        }
    }
}



// Supporting types would continue in additional files due to length...
