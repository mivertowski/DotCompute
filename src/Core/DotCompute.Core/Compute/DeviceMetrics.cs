// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Aot;
using DotCompute.Core.Execution;
using PipelineMonitor = DotCompute.Core.Pipelines.PerformanceMonitor;

namespace DotCompute.Core.Compute;

/// <summary>
/// Default implementation of device metrics.
/// </summary>
public class DeviceMetrics : IDeviceMetrics
{
    private long _kernelExecutionCount;
    private long _totalComputeTimeMs;
    private readonly MemoryTransferStats _transferStats = new();

    /// <inheritdoc/>
    public double Utilization => PipelineMonitor.GetCpuUtilization();

    /// <inheritdoc/>
    public double MemoryUsage
    {
        get
        {
            var (workingSet, _, _) = PipelineMonitor.GetMemoryStats();
            var totalAvailable = Environment.WorkingSet;
            return totalAvailable > 0 ? (double)workingSet / totalAvailable : 0.0;
        }
    }

    /// <inheritdoc/>
    public double? Temperature => null; // Not available through standard APIs

    /// <inheritdoc/>
    public double? PowerConsumption => null; // Not available through standard APIs

    /// <inheritdoc/>
    public long KernelExecutionCount => _kernelExecutionCount;

    /// <inheritdoc/>
    public TimeSpan TotalComputeTime => TimeSpan.FromMilliseconds(_totalComputeTimeMs);

    /// <inheritdoc/>
    public TimeSpan AverageKernelTime
    {
        get
        {
            var count = _kernelExecutionCount;
            return count > 0
                ? TimeSpan.FromMilliseconds((double)_totalComputeTimeMs / count)
                : TimeSpan.Zero;
        }
    }

    /// <inheritdoc/>
    public IMemoryTransferStats TransferStats => _transferStats;

    /// <summary>
    /// Records a kernel execution.
    /// </summary>
    public void RecordKernelExecution(TimeSpan duration)
    {
        Interlocked.Increment(ref _kernelExecutionCount);
        Interlocked.Add(ref _totalComputeTimeMs, (long)duration.TotalMilliseconds);
    }

    /// <summary>
    /// Records a memory transfer.
    /// </summary>
    public void RecordMemoryTransfer(long bytes, TimeSpan duration, bool toDevice) => _transferStats.RecordTransfer(bytes, duration, toDevice);

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        _kernelExecutionCount = 0;
        _totalComputeTimeMs = 0;
        _transferStats.Reset();
    }
}

/// <summary>
/// Implementation of memory transfer statistics.
/// </summary>
public class MemoryTransferStats : IMemoryTransferStats
{
    private long _bytesToDevice;
    private long _bytesFromDevice;
    private long _transfersToDevice;
    private long _transfersFromDevice;
    private long _totalTransferTimeMs;
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public long BytesToDevice => _bytesToDevice;

    /// <inheritdoc/>
    public long BytesFromDevice => _bytesFromDevice;

    /// <inheritdoc/>
    public double AverageRateToDevice
    {
        get
        {
            lock (_lock)
            {
                if (_transfersToDevice == 0 || _totalTransferTimeMs == 0)
                {
                    return 0.0;
                }

                // Calculate GB/s
                var gbTransferred = _bytesToDevice / (1024.0 * 1024.0 * 1024.0);
                var seconds = _totalTransferTimeMs / 1000.0;
                return gbTransferred / seconds;
            }
        }
    }

    /// <inheritdoc/>
    public double AverageRateFromDevice
    {
        get
        {
            lock (_lock)
            {
                if (_transfersFromDevice == 0 || _totalTransferTimeMs == 0)
                {
                    return 0.0;
                }

                // Calculate GB/s
                var gbTransferred = _bytesFromDevice / (1024.0 * 1024.0 * 1024.0);
                var seconds = _totalTransferTimeMs / 1000.0;
                return gbTransferred / seconds;
            }
        }
    }

    /// <inheritdoc/>
    public TimeSpan TotalTransferTime => TimeSpan.FromMilliseconds(_totalTransferTimeMs);

    /// <summary>
    /// Records a memory transfer.
    /// </summary>
    public void RecordTransfer(long bytes, TimeSpan duration, bool toDevice)
    {
        lock (_lock)
        {
            if (toDevice)
            {
                _bytesToDevice += bytes;
                _transfersToDevice++;
            }
            else
            {
                _bytesFromDevice += bytes;
                _transfersFromDevice++;
            }

            _totalTransferTimeMs += (long)duration.TotalMilliseconds;
        }
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _bytesToDevice = 0;
            _bytesFromDevice = 0;
            _transfersToDevice = 0;
            _transfersFromDevice = 0;
            _totalTransferTimeMs = 0;
        }
    }
}
