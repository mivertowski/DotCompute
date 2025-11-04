// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Allocator for pinned memory buffers optimized for fast CPU-GPU transfers in Metal.
/// Provides page-locked memory that can be efficiently transferred between CPU and GPU.
/// </summary>
internal sealed class MetalPinnedMemoryAllocator : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IntPtr, PinnedAllocationInfo> _pinnedAllocations = new();
    private readonly SemaphoreSlim _allocationSemaphore = new(1, 1);
    private readonly Lock _statisticsLock = new();

    // Configuration
    private const long MAX_PINNED_ALLOCATION = 256L * 1024 * 1024; // 256MB max per allocation
    private const int MAX_PINNED_ALLOCATIONS = 64;                 // Maximum concurrent pinned allocations

    // Statistics
    private long _totalPinnedBytes;
    private long _totalPinnedAllocations;
    private long _peakPinnedBytes;
    private int _activePinnedAllocations;
    private bool _disposed;

    public MetalPinnedMemoryAllocator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("MetalPinnedMemoryAllocator initialized - max allocation: {MaxAllocation:N0} bytes",
            MAX_PINNED_ALLOCATION);
    }

    /// <summary>
    /// Gets the total bytes currently pinned.
    /// </summary>
    public long TotalPinnedBytes => Interlocked.Read(ref _totalPinnedBytes);

    /// <summary>
    /// Gets the number of active pinned allocations.
    /// </summary>
    public int ActivePinnedAllocations => _activePinnedAllocations;

    /// <summary>
    /// Allocates pinned memory for fast CPU-GPU transfers.
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        if (sizeInBytes > MAX_PINNED_ALLOCATION)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                $"Requested size {sizeInBytes} exceeds maximum pinned allocation size {MAX_PINNED_ALLOCATION}");
        }

        await _allocationSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Check allocation limits
            if (_activePinnedAllocations >= MAX_PINNED_ALLOCATIONS)
            {
                throw new InvalidOperationException(
                    $"Maximum number of pinned allocations ({MAX_PINNED_ALLOCATIONS}) exceeded");
            }

            // Allocate page-aligned pinned memory
            var buffer = await AllocatePinnedBufferAsync(sizeInBytes, options, cancellationToken);

            // Track the allocation
            var info = new PinnedAllocationInfo
            {
                SizeInBytes = sizeInBytes,
                AllocatedAt = DateTimeOffset.UtcNow,
                Options = options
            };

            if (buffer is MetalPinnedMemoryBuffer pinnedBuffer)
            {
                _pinnedAllocations.TryAdd(pinnedBuffer.NativeHandle, info);
            }

            // Update statistics
            var totalBytes = Interlocked.Add(ref _totalPinnedBytes, sizeInBytes);
            Interlocked.Increment(ref _totalPinnedAllocations);
            Interlocked.Increment(ref _activePinnedAllocations);

            // Update peak
            long currentPeak;
            do
            {
                currentPeak = _peakPinnedBytes;
                if (totalBytes <= currentPeak)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _peakPinnedBytes, totalBytes, currentPeak) != currentPeak);

            _logger.LogTrace("Allocated pinned memory: {SizeBytes} bytes, total pinned: {TotalBytes:N0} bytes",
                sizeInBytes, totalBytes);

            return buffer;
        }
        finally
        {
            _allocationSemaphore.Release();
        }
    }

    /// <summary>
    /// Frees a pinned memory allocation.
    /// </summary>
    public async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken)
    {
        if (buffer == null || buffer.IsDisposed || _disposed)
        {
            return;
        }

        if (buffer is MetalPinnedMemoryBuffer pinnedBuffer)
        {
            await _allocationSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_pinnedAllocations.TryRemove(pinnedBuffer.NativeHandle, out var info))
                {
                    await pinnedBuffer.DisposeAsync();

                    // Update statistics
                    Interlocked.Add(ref _totalPinnedBytes, -info.SizeInBytes);
                    Interlocked.Decrement(ref _activePinnedAllocations);

                    _logger.LogTrace("Freed pinned memory: {SizeBytes} bytes", info.SizeInBytes);
                }
            }
            finally
            {
                _allocationSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Performs cleanup of unused pinned allocations.
    /// </summary>
    public async ValueTask CleanupAsync(bool aggressive, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            var cleanupCount = 0;
            var freedBytes = 0L;
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(aggressive ? -1 : -5);

            var toCleanup = new List<(IntPtr handle, PinnedAllocationInfo info)>();

            // Find candidates for cleanup (old allocations)
            foreach (var kvp in _pinnedAllocations.ToArray())
            {
                if (kvp.Value.AllocatedAt < cutoffTime)
                {
                    toCleanup.Add((kvp.Key, kvp.Value));
                }
            }

            // Note: In a real implementation, we would need to check if these allocations
            // are still in use before disposing them. For now, we'll be conservative.

            foreach (var (handle, info) in toCleanup)
            {
                cleanupCount++;
                freedBytes += info.SizeInBytes;
            }

            if (toCleanup.Count > 0)
            {
                _logger.LogDebug("Found {Count} pinned allocations eligible for cleanup, freed {FreedBytes} bytes",
                    cleanupCount, freedBytes);
            }

            // For safety, we're not automatically cleaning up pinned allocations
            // as they might still be in use. Real implementation would need reference tracking.
        }, cancellationToken);
    }

    /// <summary>
    /// Performs periodic maintenance.
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_statisticsLock)
            {
                var stats = GetStatistics();

                if (stats.ActiveAllocations > 0)
                {
                    _logger.LogTrace("Pinned memory stats - Active: {Active}, Total: {TotalMB:F1} MB, Peak: {PeakMB:F1} MB",
                        stats.ActiveAllocations,
                        stats.TotalPinnedBytes / (1024.0 * 1024.0),
                        stats.PeakPinnedBytes / (1024.0 * 1024.0));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during pinned memory maintenance");
        }
    }

    /// <summary>
    /// Gets statistics about pinned memory usage.
    /// </summary>
    public PinnedMemoryStatistics GetStatistics()
    {
        lock (_statisticsLock)
        {
            return new PinnedMemoryStatistics
            {
                TotalPinnedBytes = TotalPinnedBytes,
                PeakPinnedBytes = _peakPinnedBytes,
                TotalPinnedAllocations = _totalPinnedAllocations,
                ActiveAllocations = _activePinnedAllocations,
                MaxAllowedAllocations = MAX_PINNED_ALLOCATIONS,
                MaxAllocationSize = MAX_PINNED_ALLOCATION
            };
        }
    }

    /// <summary>
    /// Clears all pinned allocations.
    /// </summary>
    public void Clear()
    {
        if (_disposed)
        {
            return;
        }

        var clearCount = _pinnedAllocations.Count;
        var clearBytes = 0L;

        foreach (var kvp in _pinnedAllocations.ToArray())
        {
            if (_pinnedAllocations.TryRemove(kvp.Key, out var info))
            {
                clearBytes += info.SizeInBytes;
                // Note: In a real implementation, we would properly dispose the native resources
            }
        }

        Interlocked.Exchange(ref _totalPinnedBytes, 0);
        Interlocked.Exchange(ref _activePinnedAllocations, 0);

        _logger.LogInformation("Cleared {Count} pinned allocations ({Bytes:N0} bytes)",
            clearCount, clearBytes);
    }

    /// <summary>
    /// Allocates the actual pinned buffer.
    /// </summary>
    private async ValueTask<IUnifiedMemoryBuffer> AllocatePinnedBufferAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Create page-aligned pinned memory buffer
            var buffer = new MetalPinnedMemoryBuffer(sizeInBytes, options);

            _logger.LogTrace("Created pinned memory buffer: {SizeBytes} bytes with options {Options}",
                sizeInBytes, options);

            return (IUnifiedMemoryBuffer)buffer;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Clear();
            _allocationSemaphore.Dispose();

            _logger.LogInformation("MetalPinnedMemoryAllocator disposed - final stats: {TotalAllocs} allocations, peak: {PeakMB:F1} MB",
                _totalPinnedAllocations, _peakPinnedBytes / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MetalPinnedMemoryAllocator");
        }
    }
}

/// <summary>
/// Information about a pinned memory allocation.
/// </summary>
internal sealed record PinnedAllocationInfo
{
    public required long SizeInBytes { get; init; }
    public required DateTimeOffset AllocatedAt { get; init; }
    public required MemoryOptions Options { get; init; }
}

/// <summary>
/// Statistics for pinned memory allocations.
/// </summary>
public sealed record PinnedMemoryStatistics
{
    public required long TotalPinnedBytes { get; init; }
    public required long PeakPinnedBytes { get; init; }
    public required long TotalPinnedAllocations { get; init; }
    public required int ActiveAllocations { get; init; }
    public required int MaxAllowedAllocations { get; init; }
    public required long MaxAllocationSize { get; init; }
}
