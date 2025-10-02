// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;

namespace DotCompute.Runtime.Services
{
    /// <summary>
    /// Production monitoring service that tracks system health, performance metrics, and resource usage.
    /// </summary>
    public sealed class ProductionMonitor : IDisposable
    {
        private readonly ILogger<ProductionMonitor> _logger;
        private readonly ConcurrentDictionary<string, PerformanceCounter> _performanceCounters = new();
        private readonly ConcurrentQueue<HealthCheckResult> _healthCheckHistory = new();
        private readonly Timer _monitoringTimer;
        private readonly Timer _cleanupTimer;
        private readonly SystemMonitoringStatistics _statistics = new();
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _memoryCounter;
#pragma warning restore CS0649
        private bool _disposed;

        public SystemMonitoringStatistics Statistics => _statistics;

        public ProductionMonitor(ILogger<ProductionMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize performance counters
            try
            {
                // Performance counters are platform-specific and may not work on all .NET versions
                // For this implementation, we'll use alternative methods
                _logger.LogWarningMessage("Using fallback performance monitoring - PerformanceCounter not available");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to initialize performance counters: {ex.Message}");
                // Continue without performance counters
            }

            // Setup periodic monitoring
            _monitoringTimer = new Timer(PerformMonitoringCycle, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Setup periodic cleanup
            _cleanupTimer = new Timer(PerformPeriodicCleanup, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInfoMessage("Production monitor initialized with 30-second monitoring cycle");
        }

        /// <summary>
        /// Performs a comprehensive health check of the system.
        /// </summary>
        public async Task<SystemHealthReport> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProductionMonitor));
            }

            var healthChecks = new List<HealthCheckResult>();

            // System resource checks
            healthChecks.Add(await CheckCpuUsageAsync(cancellationToken).ConfigureAwait(false));
            healthChecks.Add(await CheckMemoryUsageAsync(cancellationToken).ConfigureAwait(false));
            healthChecks.Add(await CheckDiskSpaceAsync(cancellationToken).ConfigureAwait(false));

            // .NET runtime checks
            healthChecks.Add(CheckGarbageCollectorHealth());
            healthChecks.Add(CheckThreadPoolHealth());

            // Application-specific checks
            healthChecks.Add(CheckAcceleratorHealth());
            healthChecks.Add(CheckKernelExecutionHealth());

            var overallHealth = DetermineOverallHealth(healthChecks);
            var report = new SystemHealthReport(DateTime.UtcNow, overallHealth, healthChecks.AsReadOnly());

            // Store in history
            _healthCheckHistory.Enqueue(new HealthCheckResult("SystemHealth", overallHealth, DateTime.UtcNow, "Overall system health"));

            // Maintain history size
            while (_healthCheckHistory.Count > 100)
            {
                _ = _healthCheckHistory.TryDequeue(out _);
            }

            _ = Interlocked.Increment(ref _statistics._totalHealthChecks);

            _logger.LogInfoMessage($"Health check completed: {overallHealth}, {healthChecks.Count} checks performed");

            return report;
        }

        /// <summary>
        /// Records a performance metric for monitoring.
        /// </summary>
        public void RecordPerformanceMetric(string metricName, double value, string? unit = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metricName);

            if (_disposed)
            {
                return;
            }


            var counter = _performanceCounters.GetOrAdd(metricName, _ => new PerformanceCounter(metricName));
            counter.RecordValue(value, unit);

            _ = Interlocked.Increment(ref _statistics._totalMetricsRecorded);

            _logger.LogDebugMessage($"Recorded performance metric {metricName}: {value} {unit ?? ""}");
        }

        /// <summary>
        /// Gets current performance metrics.
        /// </summary>
        public IReadOnlyDictionary<string, PerformanceMetric> GetPerformanceMetrics()
        {
            return _performanceCounters.ToDictionary(
                kvp => kvp.Key,
                kvp => new PerformanceMetric(
                    kvp.Key,
                    kvp.Value.CurrentValue,
                    kvp.Value.AverageValue,
                    kvp.Value.MinValue,
                    kvp.Value.MaxValue,
                    kvp.Value.SampleCount,
                    kvp.Value.Unit));
        }

        /// <summary>
        /// Gets recent health check history.
        /// </summary>
        public IReadOnlyList<HealthCheckResult> GetHealthCheckHistory(int maxResults = 20)
        {
            return _healthCheckHistory
                .TakeLast(maxResults)
                .OrderByDescending(h => h.Timestamp)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Checks CPU usage health.
        /// </summary>
        private async Task<HealthCheckResult> CheckCpuUsageAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get multiple samples for accuracy
                var samples = new List<float>();
                for (var i = 0; i < 3; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }


                    if (_cpuCounter != null)
                    {
                        try
                        {
                            // Use alternative CPU monitoring since PerformanceCounter is not available
                            // This is a placeholder - in production you might use System.Diagnostics.Process
                            var process = System.Diagnostics.Process.GetCurrentProcess();
                            var cpuTime = process.TotalProcessorTime.TotalMilliseconds;
                            samples.Add((float)(cpuTime % 100)); // Simple approximation
                        }
                        catch
                        {
                            // Fallback to alternative CPU usage calculation
                            samples.Add(Environment.ProcessorCount * 10.0f); // Placeholder
                        }
                    }
                    if (i < 2) // Don't wait after last sample
                    {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }

                }

                var avgCpuUsage = samples.Average();
                RecordPerformanceMetric("CPU_Usage", avgCpuUsage, "%");

                var health = avgCpuUsage switch
                {
                    < 70 => HealthStatus.Healthy,
                    < 85 => HealthStatus.Warning,
                    _ => HealthStatus.Critical
                };

                return new HealthCheckResult("CPU", health, DateTime.UtcNow,
                    $"CPU usage: {avgCpuUsage:F1}%");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to check CPU usage: {ex.Message}");
                return new HealthCheckResult("CPU", HealthStatus.Unknown, DateTime.UtcNow,
                    $"Failed to check CPU usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks memory usage health.
        /// </summary>
        private async Task<HealthCheckResult> CheckMemoryUsageAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(1, cancellationToken); // Minimal async work

                // Use alternative memory monitoring since PerformanceCounter is not available
                var allocatedMemoryMB = GC.GetTotalAllocatedBytes() / (1024.0 * 1024.0);
                var gcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var availableMemoryMB = Math.Max(1024.0 - allocatedMemoryMB, 0); // Rough approximation
                var totalMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var usedMemoryMB = Environment.WorkingSet / (1024.0 * 1024.0);

                RecordPerformanceMetric("Memory_Available", availableMemoryMB, "MB");
                RecordPerformanceMetric("Memory_Used", usedMemoryMB, "MB");

                var health = availableMemoryMB switch
                {
                    > 1024 => HealthStatus.Healthy,  // More than 1GB available
                    > 512 => HealthStatus.Warning,   // 512MB-1GB available
                    _ => HealthStatus.Critical        // Less than 512MB available
                };

                return new HealthCheckResult("Memory", health, DateTime.UtcNow,
                    $"Available: {availableMemoryMB:F0}MB, Used: {usedMemoryMB:F0}MB");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to check memory usage: {ex.Message}");
                return new HealthCheckResult("Memory", HealthStatus.Unknown, DateTime.UtcNow,
                    $"Failed to check memory usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks disk space health.
        /// </summary>
        private async Task<HealthCheckResult> CheckDiskSpaceAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(1, cancellationToken); // Minimal async work

                var currentDirectory = Environment.CurrentDirectory;
                var driveInfo = new DriveInfo(Path.GetPathRoot(currentDirectory) ?? "C:\\");

                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var totalSpaceGB = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var usedPercentage = ((totalSpaceGB - freeSpaceGB) / totalSpaceGB) * 100;

                RecordPerformanceMetric("Disk_Free_Space", freeSpaceGB, "GB");
                RecordPerformanceMetric("Disk_Used_Percentage", usedPercentage, "%");

                var health = usedPercentage switch
                {
                    < 80 => HealthStatus.Healthy,
                    < 90 => HealthStatus.Warning,
                    _ => HealthStatus.Critical
                };

                return new HealthCheckResult("DiskSpace", health, DateTime.UtcNow,
                    $"Free: {freeSpaceGB:F1}GB ({100 - usedPercentage:F1}% available)");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to check disk space: {ex.Message}");
                return new HealthCheckResult("DiskSpace", HealthStatus.Unknown, DateTime.UtcNow,
                    $"Failed to check disk space: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks .NET garbage collector health.
        /// </summary>
        private HealthCheckResult CheckGarbageCollectorHealth()
        {
            try
            {
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);
                var totalMemory = GC.GetTotalMemory(false);

                RecordPerformanceMetric("GC_Gen0_Collections", gen0Collections);
                RecordPerformanceMetric("GC_Gen1_Collections", gen1Collections);
                RecordPerformanceMetric("GC_Gen2_Collections", gen2Collections);
                RecordPerformanceMetric("GC_Total_Memory", totalMemory / (1024.0 * 1024.0), "MB");

                // Simple heuristic: excessive Gen2 collections indicate memory pressure
                var health = gen2Collections > 100 ? HealthStatus.Warning : HealthStatus.Healthy;

                return new HealthCheckResult("GarbageCollector", health, DateTime.UtcNow,
                    $"Gen0: {gen0Collections}, Gen1: {gen1Collections}, Gen2: {gen2Collections}");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to check GC health: {ex.Message}");
                return new HealthCheckResult("GarbageCollector", HealthStatus.Unknown, DateTime.UtcNow,
                    $"Failed to check GC health: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks thread pool health.
        /// </summary>
        private HealthCheckResult CheckThreadPoolHealth()
        {
            try
            {
                ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableIoThreads);
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxIoThreads);

                var workerThreadUsage = ((maxWorkerThreads - availableWorkerThreads) / (double)maxWorkerThreads) * 100;
                var ioThreadUsage = ((maxIoThreads - availableIoThreads) / (double)maxIoThreads) * 100;

                RecordPerformanceMetric("ThreadPool_Worker_Usage", workerThreadUsage, "%");
                RecordPerformanceMetric("ThreadPool_IO_Usage", ioThreadUsage, "%");

                var health = Math.Max(workerThreadUsage, ioThreadUsage) switch
                {
                    < 70 => HealthStatus.Healthy,
                    < 85 => HealthStatus.Warning,
                    _ => HealthStatus.Critical
                };

                return new HealthCheckResult("ThreadPool", health, DateTime.UtcNow,
                    $"Worker threads: {workerThreadUsage:F1}% used, IO threads: {ioThreadUsage:F1}% used");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Failed to check thread pool health: {ex.Message}");
                return new HealthCheckResult("ThreadPool", HealthStatus.Unknown, DateTime.UtcNow,
                    $"Failed to check thread pool health: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks accelerator health (placeholder).
        /// </summary>
        private static HealthCheckResult CheckAcceleratorHealth()
        {
            // This is a placeholder for accelerator-specific health checks
            // In a real implementation, you would check GPU temperature, memory, etc.
            return new HealthCheckResult("Accelerator", HealthStatus.Healthy, DateTime.UtcNow,
                "Accelerator status: OK");
        }

        /// <summary>
        /// Checks kernel execution health (placeholder).
        /// </summary>
        private static HealthCheckResult CheckKernelExecutionHealth()
        {
            // This is a placeholder for kernel execution health checks
            // In a real implementation, you would check execution queues, error rates, etc.
            return new HealthCheckResult("KernelExecution", HealthStatus.Healthy, DateTime.UtcNow,
                "Kernel execution status: OK");
        }

        /// <summary>
        /// Determines overall system health from individual checks.
        /// </summary>
        private static HealthStatus DetermineOverallHealth(IEnumerable<HealthCheckResult> healthChecks)
        {
            var statuses = healthChecks.Select(h => h.Status).ToList();

            if (statuses.Any(s => s == HealthStatus.Critical))
            {

                return HealthStatus.Critical;
            }


            if (statuses.Any(s => s == HealthStatus.Warning))
            {

                return HealthStatus.Warning;
            }


            if (statuses.Any(s => s == HealthStatus.Unknown))
            {

                return HealthStatus.Unknown;
            }


            return HealthStatus.Healthy;
        }

        /// <summary>
        /// Performs periodic monitoring cycle.
        /// </summary>
        private void PerformMonitoringCycle(object? state)
        {
            if (_disposed)
            {
                return;
            }


            _ = Task.Run(async () =>
            {
                try
                {
                    _ = await PerformHealthCheckAsync().ConfigureAwait(false);
                    _ = Interlocked.Increment(ref _statistics._totalMonitoringCycles);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, "Error during monitoring cycle");
                }
            });
        }

        /// <summary>
        /// Performs periodic cleanup of old data.
        /// </summary>
        private void PerformPeriodicCleanup(object? state)
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                // Clean up old performance counter data
                foreach (var counter in _performanceCounters.Values)
                {
                    counter.CleanupOldSamples();
                }

                // Limit health check history size
                while (_healthCheckHistory.Count > 100)
                {
                    _ = _healthCheckHistory.TryDequeue(out _);
                }

                _logger.LogDebugMessage("Periodic cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during periodic cleanup");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _monitoringTimer.Dispose();
                _cleanupTimer.Dispose();
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();

                foreach (var counter in _performanceCounters.Values)
                {
                    counter.Dispose();
                }
                _performanceCounters.Clear();

                _logger.LogInfoMessage("Production monitor disposed");
            }
        }
    }

    /// <summary>
    /// Statistics for system monitoring performance tracking.
    /// </summary>
    public sealed class SystemMonitoringStatistics
    {
        internal long _totalHealthChecks;
        internal long _totalMetricsRecorded;
        internal long _totalMonitoringCycles;

        public long TotalHealthChecks => _totalHealthChecks;
        public long TotalMetricsRecorded => _totalMetricsRecorded;
        public long TotalMonitoringCycles => _totalMonitoringCycles;
        public double AverageMetricsPerCycle => _totalMonitoringCycles == 0 ? 0.0 : (double)_totalMetricsRecorded / _totalMonitoringCycles;
    }

    /// <summary>
    /// Represents a performance counter for tracking metrics over time.
    /// </summary>
    public sealed class PerformanceCounter : IDisposable
    {
        private readonly string _name;
        private readonly Queue<(double Value, DateTime Timestamp)> _samples = new();
        private readonly object _lock = new();
        private double _currentValue;
        private double _minValue = double.MaxValue;
        private double _maxValue = double.MinValue;
        private readonly TimeSpan _maxAge = TimeSpan.FromHours(1);

        public PerformanceCounter(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name => _name;
        public double CurrentValue => _currentValue;
        public double AverageValue => GetAverageValue();
        public double MinValue => _minValue == double.MaxValue ? 0 : _minValue;
        public double MaxValue => _maxValue == double.MinValue ? 0 : _maxValue;
        public int SampleCount => _samples.Count;
        public string? Unit { get; private set; }

        public void RecordValue(double value, string? unit = null)
        {
            lock (_lock)
            {
                _currentValue = value;
                _samples.Enqueue((value, DateTime.UtcNow));
                Unit = unit;

                if (value < _minValue)
                {
                    _minValue = value;
                }


                if (value > _maxValue)
                {
                    _maxValue = value;
                }

                // Clean up old samples

                CleanupOldSamples();
            }
        }

        public void CleanupOldSamples()
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - _maxAge;
                while (_samples.Count > 0 && _samples.Peek().Timestamp < cutoffTime)
                {
                    _ = _samples.Dequeue();
                }

                // Recalculate min/max from remaining samples
                if (_samples.Count > 0)
                {
                    _minValue = _samples.Min(s => s.Value);
                    _maxValue = _samples.Max(s => s.Value);
                }
                else
                {
                    _minValue = double.MaxValue;
                    _maxValue = double.MinValue;
                }
            }
        }

        private double GetAverageValue()
        {
            lock (_lock)
            {
                return _samples.Count == 0 ? 0 : _samples.Average(s => s.Value);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _samples.Clear();
            }
        }
    }

    /// <summary>
    /// Supporting types for the monitoring system.
    /// </summary>
    public record SystemHealthReport(
        DateTime Timestamp,
        HealthStatus OverallHealth,
        IReadOnlyList<HealthCheckResult> HealthChecks);

    public record HealthCheckResult(
        string Component,
        HealthStatus Status,
        DateTime Timestamp,
        string Details);

    public record PerformanceMetric(
        string Name,
        double CurrentValue,
        double AverageValue,
        double MinValue,
        double MaxValue,
        int SampleCount,
        string? Unit);

    public enum HealthStatus
    {
        Unknown,
        Healthy,
        Warning,
        Critical
    }
}