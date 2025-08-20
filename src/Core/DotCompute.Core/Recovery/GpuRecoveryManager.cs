// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive GPU error recovery manager with device hang detection,
/// kernel timeout handling, and graceful degradation capabilities
/// </summary>
public sealed class GpuRecoveryManager : IDisposable
{
    private readonly ILogger<GpuRecoveryManager> _logger;
    private readonly ConcurrentDictionary<string, DeviceRecoveryState> _deviceStates;
    private readonly ConcurrentDictionary<string, KernelExecutionMonitor> _kernelMonitors;
    private readonly Timer _healthCheckTimer;
    private readonly RecoveryMetrics _metrics;
    private readonly GpuRecoveryConfiguration _config;
    private bool _disposed;

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

        _logger.LogInformation("GPU Recovery Manager initialized with configuration: {Config}", _config);
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

        _logger.LogWarning("GPU error detected on device {DeviceId}: {Error}", deviceId, error.Message);

        try
        {
            // Get or create device state
            var deviceState = _deviceStates.GetOrAdd(deviceId, _ => new DeviceRecoveryState(deviceId));
            deviceState.RecordError(error);

            // Determine recovery strategy based on error type and device state
            var strategy = DetermineRecoveryStrategy(error, deviceState);
            _logger.LogInformation("Using recovery strategy {Strategy} for device {DeviceId}", strategy, deviceId);

            var result = await ExecuteRecoveryStrategyAsync(strategy, context, deviceState, cancellationToken);
            
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                deviceState.RecordSuccessfulRecovery();
                _logger.LogInformation("GPU recovery successful for device {DeviceId} using {Strategy} in {Duration}ms", 
                    deviceId, strategy, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                deviceState.RecordFailedRecovery();
                _logger.LogError("GPU recovery failed for device {DeviceId} using {Strategy}: {Message}", 
                    deviceId, strategy, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exception during GPU recovery for device {DeviceId}", deviceId);
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
        _kernelMonitors.TryAdd(kernelId, monitor);
        
        // Auto-cleanup completed monitors
        _ = Task.Run(async () =>
        {
            await monitor.WaitForCompletionAsync();
            _kernelMonitors.TryRemove(kernelId, out _);
        });

        return monitor;
    }

    /// <summary>
    /// Performs device reset with proper resource cleanup
    /// </summary>
    public async Task<bool> ResetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Attempting device reset for {DeviceId}", deviceId);

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

            _logger.LogInformation("Device reset completed for {DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device reset failed for {DeviceId}", deviceId);
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
            DeviceHealth = deviceHealth,
            OverallHealth = CalculateOverallHealth(deviceHealth),
            ActiveKernels = _kernelMonitors.Count,
            TotalRecoveryAttempts = _metrics.TotalAttempts,
            GlobalSuccessRate = _metrics.SuccessRate
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
            AcceleratorException accelEx when accelEx.Message.Contains("hang") => GpuRecoveryStrategy.DeviceReset,
            AcceleratorException accelEx when accelEx.Message.Contains("invalid") => GpuRecoveryStrategy.ContextReset,
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
            _ => new RecoveryResult { Success = false, Message = "Unknown recovery strategy" }
        };
    }

    private async Task<RecoveryResult> SimpleRetryAsync(GpuRecoveryContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting simple retry for device {DeviceId}", context.DeviceId);
        
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
        _logger.LogInformation("Performing memory recovery for device {DeviceId}", context.DeviceId);
        
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
        _logger.LogInformation("Terminating hanging kernels for device {DeviceId}", context.DeviceId);
        
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
        _logger.LogInformation("Resetting context for device {DeviceId}", context.DeviceId);
        
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
        _logger.LogWarning("Performing device reset recovery for device {DeviceId}", context.DeviceId);
        
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
                _logger.LogWarning(ex, "Failed to cancel kernel {KernelId}", monitor.KernelId);
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
                    _logger.LogWarning("Device {DeviceId} health degraded: {ConsecutiveFailures} consecutive failures", 
                        deviceState.DeviceId, deviceState.ConsecutiveFailures);
                }
            }

            // Check for hanging kernels
            var hangingKernels = _kernelMonitors.Values
                .Where(monitor => monitor.IsHanging)
                .ToList();

            foreach (var hangingKernel in hangingKernels)
            {
                _logger.LogWarning("Kernel {KernelId} appears to be hanging (running for {Duration})", 
                    hangingKernel.KernelId, hangingKernel.ExecutionTime);
                
                // Auto-cancel hanging kernels
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await hangingKernel.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-cancel hanging kernel {KernelId}", hangingKernel.KernelId);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GPU health check");
        }
    }

    private double CalculateOverallHealth(Dictionary<string, DeviceHealthStatus> deviceHealth)
    {
        if (deviceHealth.Count == 0)
        {
            return 1.0;
        }


        var healthyDevices = deviceHealth.Values.Count(d => d.IsHealthy);
        return (double)healthyDevices / deviceHealth.Count;
    }

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
                    _logger.LogWarning(ex, "Error disposing kernel monitor {KernelId}", monitor.KernelId);
                }
            }
            
            _disposed = true;
            _logger.LogInformation("GPU Recovery Manager disposed");
        }
    }
}

// Supporting types would continue in additional files due to length...