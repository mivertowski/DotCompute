// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory
{

    /// <summary>
    /// Thread-safe statistics tracking for CUDA memory management with performance metrics
    /// </summary>
    public sealed class CudaMemoryStatistics : IDisposable
    {
        private readonly Timer _updateTimer;
        private readonly ILogger? _logger;
        private readonly object _lockObject = new();

        // Current statistics
        private long _totalMemoryBytes;
        private long _freeMemoryBytes;
        private long _usedMemoryBytes;
        private long _allocatedMemoryBytes;
        private int _allocationCount;
        private long _peakMemoryBytes;
        private long _totalAllocations;
        private long _totalDeallocations;

        // Performance tracking
        private long _totalTransferredBytes;
        private long _hostToDeviceTransfers;
        private long _deviceToHostTransfers;
        private readonly ConcurrentDictionary<string, long> _operationCounts = new();
        private readonly ConcurrentDictionary<string, TimeSpan> _operationTimes = new();

        public long TotalMemoryBytes => _totalMemoryBytes;
        public long FreeMemoryBytes => _freeMemoryBytes;
        public long UsedMemoryBytes => _usedMemoryBytes;
        public long AllocatedMemoryBytes => _allocatedMemoryBytes;
        public int AllocationCount => _allocationCount;
        public long PeakMemoryBytes => _peakMemoryBytes;
        public long TotalAllocations => _totalAllocations;
        public long TotalDeallocations => _totalDeallocations;

        // Memory pressure indicators
        public double MemoryPressure => _totalMemoryBytes > 0 ? (double)_usedMemoryBytes / _totalMemoryBytes : 0.0;
        public double FragmentationRatio => _allocationCount > 0 ? (double)(_usedMemoryBytes - _allocatedMemoryBytes) / _usedMemoryBytes : 0.0;

        private bool _disposed;

        public CudaMemoryStatistics(ILogger? logger = null, TimeSpan? updateInterval = null)
        {
            _logger = logger;
            var interval = updateInterval ?? TimeSpan.FromSeconds(1);

            _updateTimer = new Timer(UpdateStatistics, null, interval, interval);

            // Initial statistics update
            UpdateStatistics(null);
        }

        /// <summary>
        /// Records a memory allocation
        /// </summary>
        public void RecordAllocation(long sizeInBytes)
        {
            lock (_lockObject)
            {
                _allocatedMemoryBytes += sizeInBytes;
                _allocationCount++;
                _totalAllocations++;

                _peakMemoryBytes = Math.Max(_peakMemoryBytes, _allocatedMemoryBytes);
            }

            IncrementOperationCount("allocations");
        }

        /// <summary>
        /// Records a memory deallocation
        /// </summary>
        public void RecordDeallocation(long sizeInBytes)
        {
            lock (_lockObject)
            {
                _allocatedMemoryBytes = Math.Max(0, _allocatedMemoryBytes - sizeInBytes);
                _allocationCount = Math.Max(0, _allocationCount - 1);
                _totalDeallocations++;
            }

            IncrementOperationCount("deallocations");
        }

        /// <summary>
        /// Records a host-to-device transfer
        /// </summary>
        public void RecordHostToDeviceTransfer(long bytes)
        {
            _ = Interlocked.Add(ref _totalTransferredBytes, bytes);
            _ = Interlocked.Increment(ref _hostToDeviceTransfers);
            IncrementOperationCount("h2d_transfers");
        }

        /// <summary>
        /// Records a device-to-host transfer
        /// </summary>
        public void RecordDeviceToHostTransfer(long bytes)
        {
            _ = Interlocked.Add(ref _totalTransferredBytes, bytes);
            _ = Interlocked.Increment(ref _deviceToHostTransfers);
            IncrementOperationCount("d2h_transfers");
        }

        /// <summary>
        /// Records the execution time of an operation
        /// </summary>
        public void RecordOperationTime(string operation, TimeSpan duration) => _operationTimes.AddOrUpdate(operation, duration, (key, existing) => existing + duration);

        /// <summary>
        /// Gets current comprehensive statistics
        /// </summary>
        public CudaMemoryStatisticsSnapshot GetCurrentStatistics()
        {
            lock (_lockObject)
            {
                return new CudaMemoryStatisticsSnapshot
                {
                    TotalMemoryBytes = _totalMemoryBytes,
                    FreeMemoryBytes = _freeMemoryBytes,
                    UsedMemoryBytes = _usedMemoryBytes,
                    AllocatedMemoryBytes = _allocatedMemoryBytes,
                    AllocationCount = _allocationCount,
                    PeakMemoryBytes = _peakMemoryBytes,
                    TotalAllocations = _totalAllocations,
                    TotalDeallocations = _totalDeallocations,
                    TotalTransferredBytes = _totalTransferredBytes,
                    HostToDeviceTransfers = _hostToDeviceTransfers,
                    DeviceToHostTransfers = _deviceToHostTransfers,
                    MemoryPressure = MemoryPressure,
                    FragmentationRatio = FragmentationRatio,
                    MemoryUtilization = _totalMemoryBytes > 0 ? (double)_allocatedMemoryBytes / _totalMemoryBytes : 0.0,
                    OperationCounts = new Dictionary<string, long>(_operationCounts),
                    OperationTimes = new Dictionary<string, TimeSpan>(_operationTimes),
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Resets all statistics
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _allocatedMemoryBytes = 0;
                _allocationCount = 0;
                _peakMemoryBytes = 0;
                _totalAllocations = 0;
                _totalDeallocations = 0;
            }

            _ = Interlocked.Exchange(ref _totalTransferredBytes, 0);
            _ = Interlocked.Exchange(ref _hostToDeviceTransfers, 0);
            _ = Interlocked.Exchange(ref _deviceToHostTransfers, 0);

            _operationCounts.Clear();
            _operationTimes.Clear();

            UpdateStatistics(null);
        }

        private void UpdateStatistics(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
                if (result == CudaError.Success)
                {
                    lock (_lockObject)
                    {
                        _totalMemoryBytes = (long)total;
                        _freeMemoryBytes = (long)free;
                        _usedMemoryBytes = (long)(total - free);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update CUDA memory statistics");
            }
        }

        private void IncrementOperationCount(string operation) => _operationCounts.AddOrUpdate(operation, 1, (key, existing) => existing + 1);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _updateTimer?.Dispose();
                var stats = GetCurrentStatistics();

                _logger?.LogInformation(
                    "CUDA Memory Statistics Final Report: " +
                    "Total: {TotalMB}MB, Peak: {PeakMB}MB, " +
                    "Allocations: {Allocations}, Transfers: {Transfers}MB, " +
                    "Pressure: {Pressure:P1}",
                    stats.TotalMemoryBytes / (1024 * 1024),
                    stats.PeakMemoryBytes / (1024 * 1024),
                    stats.TotalAllocations,
                    stats.TotalTransferredBytes / (1024 * 1024),
                    stats.MemoryPressure);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during CUDA memory statistics disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Immutable snapshot of CUDA memory statistics at a point in time
    /// </summary>
    public sealed class CudaMemoryStatisticsSnapshot
    {
        public long TotalMemoryBytes { get; init; }
        public long FreeMemoryBytes { get; init; }
        public long UsedMemoryBytes { get; init; }
        public long AllocatedMemoryBytes { get; init; }
        public int AllocationCount { get; init; }
        public long PeakMemoryBytes { get; init; }
        public long TotalAllocations { get; init; }
        public long TotalDeallocations { get; init; }
        public long TotalTransferredBytes { get; init; }
        public long HostToDeviceTransfers { get; init; }
        public long DeviceToHostTransfers { get; init; }
        public double MemoryPressure { get; init; }
        public double FragmentationRatio { get; init; }
        public double MemoryUtilization { get; init; }
        public Dictionary<string, long> OperationCounts { get; init; } = [];
        public Dictionary<string, TimeSpan> OperationTimes { get; init; } = [];
        public DateTime Timestamp { get; init; }

        // Computed properties
        public long TotalMemoryMB => TotalMemoryBytes / (1024 * 1024);
        public long FreeMemoryMB => FreeMemoryBytes / (1024 * 1024);
        public long UsedMemoryMB => UsedMemoryBytes / (1024 * 1024);
        public long AllocatedMemoryMB => AllocatedMemoryBytes / (1024 * 1024);
        public long PeakMemoryMB => PeakMemoryBytes / (1024 * 1024);
        public long TotalTransferredMB => TotalTransferredBytes / (1024 * 1024);
        public long TotalTransfers => HostToDeviceTransfers + DeviceToHostTransfers;

        /// <summary>
        /// Gets a formatted string representation of the statistics
        /// </summary>
        public string ToSummaryString()
        {
            return $"CUDA Memory: {UsedMemoryMB}MB/{TotalMemoryMB}MB used ({MemoryPressure:P1}), " +
                   $"{AllocationCount} allocations ({AllocatedMemoryMB}MB), " +
                   $"Peak: {PeakMemoryMB}MB, Transfers: {TotalTransferredMB}MB";
        }
    }
}
