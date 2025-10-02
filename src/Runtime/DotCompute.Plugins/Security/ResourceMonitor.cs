// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Monitors resource usage of sandboxed plugins and enforces limits.
/// </summary>
public class ResourceMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly ResourceLimits _globalLimits;
    private readonly ConcurrentDictionary<Guid, PluginResourceTracker> _trackers = new();
    private readonly Timer _monitoringTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceMonitor"/> class.
    /// </summary>
    public ResourceMonitor(ILogger logger, ResourceLimits globalLimits)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _globalLimits = globalLimits ?? throw new ArgumentNullException(nameof(globalLimits));

        // Initialize performance counters if available
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters");
        }

        // Start monitoring timer
        _monitoringTimer = new Timer(MonitorResources, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Registers a plugin for resource monitoring.
    /// </summary>
    public void RegisterPlugin(SandboxedPlugin plugin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        var tracker = new PluginResourceTracker(plugin.Id, plugin.Permissions.ResourceLimits, _logger);
        _ = _trackers.TryAdd(plugin.Id, tracker);

        _logger.LogDebugMessage("Registered plugin {plugin.Id} for resource monitoring");
    }

    /// <summary>
    /// Unregisters a plugin from resource monitoring.
    /// </summary>
    public void UnregisterPlugin(SandboxedPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (_trackers.TryRemove(plugin.Id, out var tracker))
        {
            tracker.Dispose();
            _logger.LogDebugMessage("Unregistered plugin {plugin.Id} from resource monitoring");
        }
    }

    /// <summary>
    /// Gets the current resource usage for a plugin.
    /// </summary>
    public ResourceUsage GetPluginUsage(Guid pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_trackers.TryGetValue(pluginId, out var tracker))
        {
            return tracker.GetCurrentUsage();
        }

        return new ResourceUsage();
    }

    /// <summary>
    /// Monitors a plugin for resource violations asynchronously.
    /// </summary>
    public async Task<string?> MonitorPluginAsync(SandboxedPlugin plugin, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        if (!_trackers.TryGetValue(plugin.Id, out var tracker))
        {
            return "Plugin not registered for monitoring";
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested && !plugin.IsDisposed)
            {
                var usage = tracker.GetCurrentUsage();


                if (usage.IsExceedingLimits)
                {
                    var violations = string.Join(", ", usage.ViolatedLimits);
                    _logger.LogWarningMessage($"Resource violations detected for plugin {plugin.Id}: {violations}");
                    return violations;
                }

                await Task.Delay(100, cancellationToken);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error monitoring plugin {plugin.Id}");
            return $"Monitoring error: {ex.Message}";
        }
    }

    /// <summary>
    /// Periodic monitoring callback that updates all plugin resource usage.
    /// </summary>
    private void MonitorResources(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Update CPU and memory counters if available
            var currentCpuUsage = GetCurrentCpuUsage();
            var currentMemoryUsage = GetCurrentMemoryUsage();

            // Update each plugin tracker
            _ = Parallel.ForEach(_trackers.Values, tracker =>
            {
                try
                {
                    tracker.UpdateUsage(currentCpuUsage, currentMemoryUsage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error updating resource usage for plugin {PluginId}", tracker.PluginId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during resource monitoring");
        }
    }

    /// <summary>
    /// Gets the current CPU usage percentage.
    /// </summary>
    private double GetCurrentCpuUsage()
    {
        try
        {
            if (_cpuCounter != null)
            {
                return _cpuCounter.NextValue();
            }

            // Fallback method using Process class
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100.0;
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Gets the current memory usage in MB.
    /// </summary>
    private long GetCurrentMemoryUsage()
    {
        try
        {
            if (_memoryCounter != null)
            {
                return (long)(_memoryCounter.NextValue() / (1024 * 1024));
            }

            // Fallback method using Process class
            var process = Process.GetCurrentProcess();
            return process.WorkingSet64 / (1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets resource usage statistics for all monitored plugins.
    /// </summary>
    public Dictionary<Guid, ResourceUsage> GetAllPluginUsage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Dictionary<Guid, ResourceUsage>();


        foreach (var kvp in _trackers)
        {
            try
            {
                result[kvp.Key] = kvp.Value.GetCurrentUsage();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting usage for plugin {PluginId}", kvp.Key);
            }
        }

        return result;
    }

    /// <summary>
    /// Disposes the resource monitor and stops monitoring.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        _monitoringTimer?.Dispose();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();

        foreach (var tracker in _trackers.Values)
        {
            tracker.Dispose();
        }

        _trackers.Clear();
    }
}

/// <summary>
/// Tracks resource usage for a specific plugin.
/// </summary>
internal class PluginResourceTracker(Guid pluginId, ResourceLimits limits, ILogger logger) : IDisposable
{
    private readonly Guid _pluginId = pluginId;
    private readonly ResourceLimits _limits = limits ?? throw new ArgumentNullException(nameof(limits));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Stopwatch _executionTimer = Stopwatch.StartNew();
    private readonly object _usageLock = new();


    private readonly ResourceUsage _currentUsage = new();
    private DateTime _lastUpdate = DateTime.UtcNow;
#pragma warning disable CS0649 // Field is never assigned to
    private readonly long _lastFileIOCount;
    private readonly long _lastNetworkIOCount;
#pragma warning restore CS0649
    private bool _disposed;
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>

    public Guid PluginId => _pluginId;

    /// <summary>
    /// Updates the current resource usage statistics.
    /// </summary>
    public void UpdateUsage(double cpuUsage, long memoryUsage)
    {
        if (_disposed)
        {
            return;
        }


        lock (_usageLock)
        {
            var now = DateTime.UtcNow;
            var deltaTime = (now - _lastUpdate).TotalSeconds;


            if (deltaTime < 0.1)
            {
                return; // Avoid too frequent updates
            }


            _currentUsage.CpuUsagePercent = cpuUsage;
            _currentUsage.MemoryUsageMB = memoryUsage;
            _currentUsage.ExecutionTime = _executionTimer.Elapsed;
            _currentUsage.ThreadCount = GetActiveThreadCount();

            // Update I/O counters (would need actual implementation)
            UpdateIOCounters();

            // Check for limit violations
            CheckLimitViolations();

            _lastUpdate = now;
        }
    }

    /// <summary>
    /// Gets the current resource usage.
    /// </summary>
    public ResourceUsage GetCurrentUsage()
    {
        if (_disposed)
        {
            return new ResourceUsage { IsExceedingLimits = true };
        }

        lock (_usageLock)
        {
            // Return a copy to avoid concurrency issues
            return new ResourceUsage
            {
                MemoryUsageMB = _currentUsage.MemoryUsageMB,
                CpuUsagePercent = _currentUsage.CpuUsagePercent,
                ThreadCount = _currentUsage.ThreadCount,
                FileIOOperations = _currentUsage.FileIOOperations,
                NetworkIOOperations = _currentUsage.NetworkIOOperations,
                ExecutionTime = _currentUsage.ExecutionTime,
                IsExceedingLimits = _currentUsage.IsExceedingLimits,
                ViolatedLimits = [.. _currentUsage.ViolatedLimits]
            };
        }
    }

    /// <summary>
    /// Gets the current number of active threads for this plugin.
    /// </summary>
    private static int GetActiveThreadCount()
    {
        try
        {
            // This is a simplified implementation
            // In practice, you'd need to track threads associated with the plugin's load context
            return Environment.CurrentManagedThreadId > 0 ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Updates I/O operation counters.
    /// </summary>
    private void UpdateIOCounters()
    {
        try
        {
            // This would track actual I/O operations performed by the plugin
            // For now, using placeholder values
            _currentUsage.FileIOOperations = _lastFileIOCount;
            _currentUsage.NetworkIOOperations = _lastNetworkIOCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating I/O counters for plugin {PluginId}", _pluginId);
        }
    }

    /// <summary>
    /// Checks for resource limit violations.
    /// </summary>
    private void CheckLimitViolations()
    {
        _currentUsage.ViolatedLimits.Clear();
        _currentUsage.IsExceedingLimits = false;

        // Check memory limit
        if (_currentUsage.MemoryUsageMB > _limits.MaxMemoryMB)
        {
            _currentUsage.ViolatedLimits.Add($"Memory usage ({_currentUsage.MemoryUsageMB} MB) exceeds limit ({_limits.MaxMemoryMB} MB)");
            _currentUsage.IsExceedingLimits = true;
        }

        // Check CPU limit
        if (_currentUsage.CpuUsagePercent > _limits.MaxCpuUsagePercent)
        {
            _currentUsage.ViolatedLimits.Add($"CPU usage ({_currentUsage.CpuUsagePercent:F1}%) exceeds limit ({_limits.MaxCpuUsagePercent}%)");
            _currentUsage.IsExceedingLimits = true;
        }

        // Check execution time limit
        if (_currentUsage.ExecutionTime.TotalSeconds > _limits.MaxExecutionTimeSeconds)
        {
            _currentUsage.ViolatedLimits.Add($"Execution time ({_currentUsage.ExecutionTime.TotalSeconds:F1}s) exceeds limit ({_limits.MaxExecutionTimeSeconds}s)");
            _currentUsage.IsExceedingLimits = true;
        }

        // Check thread limit
        if (_currentUsage.ThreadCount > _limits.MaxThreads)
        {
            _currentUsage.ViolatedLimits.Add($"Thread count ({_currentUsage.ThreadCount}) exceeds limit ({_limits.MaxThreads})");
            _currentUsage.IsExceedingLimits = true;
        }

        if (_currentUsage.IsExceedingLimits)
        {
            _logger.LogWarningMessage($"Resource limits violated for plugin {_pluginId}: {string.Join(", ", _currentUsage.ViolatedLimits)}");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _executionTimer?.Stop();
    }
}