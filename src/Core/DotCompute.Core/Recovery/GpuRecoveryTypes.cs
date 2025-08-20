// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// GPU recovery strategies
/// </summary>
public enum GpuRecoveryStrategy
{
    SimpleRetry,
    MemoryRecovery,
    KernelTermination,
    ContextReset,
    DeviceReset
}

/// <summary>
/// Context information for GPU recovery operations
/// </summary>
public class GpuRecoveryContext
{
    public string DeviceId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Exception Error { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for GPU recovery behavior
/// </summary>
public class GpuRecoveryConfiguration
{
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan DefaultKernelTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public int MaxConsecutiveFailures { get; set; } = 3;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableAutoRecovery { get; set; } = true;
    public bool EnableKernelTimeoutDetection { get; set; } = true;
    public bool EnableMemoryPressureDetection { get; set; } = true;

    public static GpuRecoveryConfiguration Default => new();

    public override string ToString()

        => $"HealthCheck={HealthCheckInterval}, KernelTimeout={DefaultKernelTimeout}, MaxFailures={MaxConsecutiveFailures}";
}

/// <summary>
/// Tracks recovery state for a specific GPU device
/// </summary>
public class DeviceRecoveryState
{
    private readonly ConcurrentQueue<Exception> _recentErrors = new();
    private readonly object _lock = new();
    private DateTimeOffset _lastErrorTime = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    private int _totalRecoveryAttempts;
    private int _successfulRecoveries;

    public string DeviceId { get; }
    public bool IsHealthy { get; private set; } = true;
    public Exception? LastError { get; private set; }
    public int ConsecutiveFailures => _consecutiveFailures;
    public int TotalRecoveryAttempts => _totalRecoveryAttempts;
    public int SuccessfulRecoveries => _successfulRecoveries;
    public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;

    public DeviceRecoveryState(string deviceId)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
    }

    public void RecordError(Exception error)
    {
        lock (_lock)
        {
            LastError = error;
            _lastErrorTime = DateTimeOffset.UtcNow;
            _consecutiveFailures++;
            _recentErrors.Enqueue(error);

            // Keep only recent errors (last 50)
            while (_recentErrors.Count > 50 && _recentErrors.TryDequeue(out _))
            {
                // Remove old errors
            }

            IsHealthy = _consecutiveFailures < 3;
        }
    }

    public void RecordSuccessfulRecovery()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _totalRecoveryAttempts++;
            _successfulRecoveries++;
            IsHealthy = true;
        }
    }

    public void RecordFailedRecovery()
    {
        lock (_lock)
        {
            _totalRecoveryAttempts++;
            IsHealthy = false;
        }
    }

    public double CalculateErrorRate()
    {
        var now = DateTimeOffset.UtcNow;
        var recentErrors = _recentErrors.ToArray()
            .Count(e => (now - _lastErrorTime).TotalMinutes < 60);


        return recentErrors; // Errors per hour
    }

    public void UpdateHealthCheck()
    {
        LastHealthCheck = DateTimeOffset.UtcNow;

        // Auto-recovery after some time without errors

        if (_consecutiveFailures > 0 && (DateTimeOffset.UtcNow - _lastErrorTime).TotalMinutes > 30)
        {
            lock (_lock)
            {
                _consecutiveFailures = Math.Max(0, _consecutiveFailures - 1);
                IsHealthy = _consecutiveFailures < 3;
            }
        }
    }

    public async Task ResetAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                IsHealthy = true;
                LastError = null;

                // Clear recent errors

                while (_recentErrors.TryDequeue(out _))
                {
                    // Clear queue
                }
            }
        });
    }

    public async Task ResetContextAsync()
    {
        // Platform-specific context reset would be implemented here
        await Task.Delay(100);


        lock (_lock)
        {
            _consecutiveFailures = Math.Max(0, _consecutiveFailures - 1);
            IsHealthy = _consecutiveFailures < 3;
        }
    }
}

/// <summary>
/// Monitors kernel execution for hangs and timeouts
/// </summary>
public interface IKernelExecutionMonitor : IDisposable
{
    string KernelId { get; }
    string DeviceId { get; }
    TimeSpan ExecutionTime { get; }
    bool IsHanging { get; }
    bool IsCompleted { get; }


    Task CancelAsync(CancellationToken cancellationToken = default);
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    void MarkCompleted();
}

/// <summary>
/// Implementation of kernel execution monitoring
/// </summary>
public class KernelExecutionMonitor : IKernelExecutionMonitor
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DateTimeOffset _startTime;
    private readonly TimeSpan _timeout;
    private volatile bool _completed;
    private volatile bool _disposed;

    public string KernelId { get; }
    public string DeviceId { get; }
    public TimeSpan ExecutionTime => DateTimeOffset.UtcNow - _startTime;
    public bool IsHanging => !_completed && ExecutionTime > _timeout;
    public bool IsCompleted => _completed;

    public KernelExecutionMonitor(string kernelId, TimeSpan timeout, ILogger logger, string deviceId = "unknown")
    {
        KernelId = kernelId ?? throw new ArgumentNullException(nameof(kernelId));
        DeviceId = deviceId;
        _timeout = timeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTimeOffset.UtcNow;
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _completed)
        {
            return;
        }


        _logger.LogInformation("Cancelling kernel execution {KernelId}", KernelId);


        _cancellationTokenSource.Cancel();
        _completed = true;


        await Task.Delay(100, cancellationToken); // Allow cleanup time
    }

    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (!_completed && !_disposed && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    public void MarkCompleted()
    {
        _completed = true;
        _logger.LogDebug("Kernel {KernelId} execution completed in {Duration}ms", KernelId, ExecutionTime.TotalMilliseconds);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Dispose();
            _completed = true;
            _disposed = true;
        }
    }
}

/// <summary>
/// Health status information for a device
/// </summary>
public class DeviceHealthStatus
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public double ErrorRate { get; set; }
    public Exception? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalRecoveryAttempts { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public DateTimeOffset LastHealthCheck { get; set; }
}

/// <summary>
/// Comprehensive health report for all monitored devices
/// </summary>
public class DeviceHealthReport
{
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, DeviceHealthStatus> DeviceHealth { get; set; } = new();
    public double OverallHealth { get; set; }
    public int ActiveKernels { get; set; }
    public long TotalRecoveryAttempts { get; set; }
    public double GlobalSuccessRate { get; set; }

    public override string ToString()

        => $"Health={OverallHealth:P2}, Devices={DeviceHealth.Count}, ActiveKernels={ActiveKernels}, SuccessRate={GlobalSuccessRate:P2}";
}