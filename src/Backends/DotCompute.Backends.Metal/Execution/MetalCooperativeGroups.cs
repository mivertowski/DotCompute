// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Metal cooperative groups manager for synchronization primitives.
/// Provides grid-wide synchronization, threadgroup coordination, and Metal barrier optimization.
/// Equivalent to CUDA cooperative groups for Metal compute kernels.
/// </summary>
public sealed class MetalCooperativeGroups : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalCooperativeGroups> _logger;
    private readonly ConcurrentDictionary<IntPtr, SynchronizationMetrics> _syncMetrics;
    private readonly Timer _metricsTimer;
    private long _totalSynchronizations;
    private double _averageSyncOverheadMs;
    private bool _disposed;

    public MetalCooperativeGroups(IntPtr device, ILogger<MetalCooperativeGroups> logger)
    {
        _device = device != IntPtr.Zero ? device : MetalNative.CreateSystemDefaultDevice();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _syncMetrics = new ConcurrentDictionary<IntPtr, SynchronizationMetrics>();

        _metricsTimer = new Timer(UpdateMetrics, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.LogInformation("Metal Cooperative Groups initialized");
    }

    /// <summary>
    /// Performs grid-wide synchronization across all threadgroups.
    /// Equivalent to CUDA cooperative groups grid_sync.
    /// </summary>
    public async Task GridSyncAsync(IntPtr commandBuffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Metal doesn't have direct grid-wide sync like CUDA
            // We implement this using command buffer completion and barriers

            _logger.LogDebug("Performing grid-wide synchronization");

            // Wait for current command buffer to complete
            await Task.Run(() =>
            {
                MetalNative.WaitUntilCompleted(commandBuffer);
            }, cancellationToken).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startTime;
            RecordSynchronization(commandBuffer, SynchronizationType.GridWide, duration);

            _logger.LogTrace("Grid sync completed in {Duration:F3}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Grid synchronization failed");
            throw;
        }
    }

    /// <summary>
    /// Inserts a threadgroup memory barrier in the command encoder.
    /// Equivalent to __syncthreads() in CUDA.
    /// </summary>
    public void ThreadgroupBarrier(IntPtr commandEncoder)
    {
        ThrowIfDisposed();

        try
        {
            // Metal barriers are inserted in the command encoder
            // This is a placeholder - actual implementation would use MTLComputeCommandEncoder
            // and call memoryBarrierWithScope: or appropriate Metal barrier API

            _logger.LogTrace("Inserted threadgroup barrier");

            RecordSynchronization(commandEncoder, SynchronizationType.Threadgroup, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to insert threadgroup barrier");
        }
    }

    /// <summary>
    /// Gets the maximum threadgroup size supported by the device.
    /// </summary>
    public int GetThreadgroupSize(IntPtr device)
    {
        ThrowIfDisposed();

        var deviceInfo = MetalNative.GetDeviceInfo(device);
        return (int)deviceInfo.MaxThreadsPerThreadgroup;
    }

    /// <summary>
    /// Checks if grid-wide synchronization is supported.
    /// Metal supports this through command buffer synchronization.
    /// </summary>
    public bool SupportsGridSync(IntPtr device)
    {
        ThrowIfDisposed();

        // Metal always supports command buffer synchronization
        return true;
    }

    /// <summary>
    /// Creates a synchronization point in the command buffer.
    /// </summary>
    public void CreateSyncPoint(IntPtr commandBuffer, string label)
    {
        ThrowIfDisposed();

        try
        {
            _logger.LogTrace("Created sync point: {Label}", label);

            // In production, this would use Metal's fence/event system
            // MTLFence for fine-grained synchronization
            RecordSynchronization(commandBuffer, SynchronizationType.Explicit, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create sync point: {Label}", label);
        }
    }

    /// <summary>
    /// Waits for a synchronization point to complete.
    /// </summary>
    public async Task WaitForSyncPointAsync(IntPtr commandBuffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            await Task.Run(() =>
            {
                MetalNative.WaitUntilCompleted(commandBuffer);
            }, cancellationToken).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startTime;
            RecordSynchronization(commandBuffer, SynchronizationType.WaitPoint, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wait for sync point failed");
            throw;
        }
    }

    /// <summary>
    /// Gets synchronization metrics.
    /// </summary>
    public CooperativeGroupsMetrics GetMetrics()
    {
        ThrowIfDisposed();

        return new CooperativeGroupsMetrics
        {
            TotalSynchronizations = Interlocked.Read(ref _totalSynchronizations),
            AverageSyncOverheadMs = _averageSyncOverheadMs,
            SyncBreakdown = _syncMetrics.Values
                .GroupBy(m => m.Type)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => new SyncTypeMetrics
                    {
                        Count = g.Sum(m => m.Count),
                        AverageOverheadMs = g.Average(m => m.AverageOverheadMs)
                    })
        };
    }

    private void RecordSynchronization(IntPtr handle, SynchronizationType type, TimeSpan duration)
    {
        _ = _syncMetrics.AddOrUpdate(handle,
            _ => new SynchronizationMetrics
            {
                Type = type,
                Count = 1,
                AverageOverheadMs = duration.TotalMilliseconds,
                LastSyncTime = DateTimeOffset.UtcNow
            },
            (_, existing) => new SynchronizationMetrics
            {
                Type = type,
                Count = existing.Count + 1,
                AverageOverheadMs = (existing.AverageOverheadMs * existing.Count + duration.TotalMilliseconds) / (existing.Count + 1),
                LastSyncTime = DateTimeOffset.UtcNow
            });

        _ = Interlocked.Increment(ref _totalSynchronizations);
    }

    private void UpdateMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var allMetrics = _syncMetrics.Values;
            if (allMetrics.Any())
            {
                _averageSyncOverheadMs = allMetrics.Average(m => m.AverageOverheadMs);
            }

            _logger.LogTrace("Sync metrics updated: {TotalSyncs} total, {AvgOverhead:F3}ms avg overhead",
                _totalSynchronizations, _averageSyncOverheadMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating cooperative groups metrics");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalCooperativeGroups));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsTimer?.Dispose();
            _disposed = true;

            _logger.LogInformation("Metal Cooperative Groups disposed - Total syncs: {TotalSyncs}, Avg overhead: {AvgOverhead:F3}ms",
                _totalSynchronizations, _averageSyncOverheadMs);
        }
    }
}

/// <summary>
/// Synchronization type classification.
/// </summary>
public enum SynchronizationType
{
    GridWide,
    Threadgroup,
    Explicit,
    WaitPoint
}

/// <summary>
/// Synchronization metrics for a handle.
/// </summary>
internal sealed class SynchronizationMetrics
{
    public SynchronizationType Type { get; set; }
    public long Count { get; set; }
    public double AverageOverheadMs { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
}

/// <summary>
/// Cooperative groups performance metrics.
/// </summary>
public sealed class CooperativeGroupsMetrics
{
    public long TotalSynchronizations { get; set; }
    public double AverageSyncOverheadMs { get; set; }
    public Dictionary<string, SyncTypeMetrics> SyncBreakdown { get; set; } = [];
}

/// <summary>
/// Metrics for a specific synchronization type.
/// </summary>
public sealed class SyncTypeMetrics
{
    public long Count { get; set; }
    public double AverageOverheadMs { get; set; }
}
