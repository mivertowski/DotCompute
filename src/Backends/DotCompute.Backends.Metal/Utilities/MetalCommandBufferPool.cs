// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Pool for managing Metal command buffers to reduce allocation overhead.
/// </summary>
public sealed class MetalCommandBufferPool : IDisposable
{
    private readonly IntPtr _commandQueue;
    private readonly ILogger<MetalCommandBufferPool> _logger;
    private readonly ConcurrentQueue<IntPtr> _availableBuffers = new();
    private readonly ConcurrentDictionary<IntPtr, CommandBufferInfo> _activeBuffers = new();
    private readonly int _maxPoolSize;
    private int _currentPoolSize;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the MetalCommandBufferPool.
    /// </summary>
    /// <param name="commandQueue">The Metal command queue to create buffers from.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="maxPoolSize">Maximum number of buffers to keep in pool.</param>
    public MetalCommandBufferPool(IntPtr commandQueue, ILogger<MetalCommandBufferPool> logger, int maxPoolSize = 16)
    {
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPoolSize = maxPoolSize;

        _logger.CommandBufferPoolCreated(maxPoolSize);
    }

    /// <summary>
    /// Gets a command buffer from the pool or creates a new one.
    /// </summary>
    /// <returns>A command buffer handle.</returns>
    public IntPtr GetCommandBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);


        // Try to get a buffer from the pool first
        if (_availableBuffers.TryDequeue(out var buffer))
        {
            _ = Interlocked.Decrement(ref _currentPoolSize);
            _logger.CommandBufferReused(buffer.ToInt64());
        }
        else
        {
            // Create a new buffer
            buffer = MetalNative.CreateCommandBuffer(_commandQueue);
            if (buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Metal command buffer");
            }
            _logger.CommandBufferCreated(buffer.ToInt64());
        }

        // Track the active buffer
        var info = new CommandBufferInfo
        {
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow
        };

        if (!_activeBuffers.TryAdd(buffer, info))
        {
            _logger.CommandBufferTrackingFailed(buffer.ToInt64());
        }

        return buffer;
    }

    /// <summary>
    /// Returns a command buffer to the pool for reuse.
    /// </summary>
    /// <param name="buffer">The command buffer to return.</param>
    public void ReturnCommandBuffer(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero || _disposed > 0)
        {
            return;
        }

        // Remove from active tracking
        if (!_activeBuffers.TryRemove(buffer, out var info))
        {
            _logger.CommandBufferUntracked(buffer.ToInt64());
        }

        // Check if we should return to pool or dispose
        var currentPoolSize = _currentPoolSize;
        if (currentPoolSize < _maxPoolSize && !IsBufferStale(info))
        {
            // Return to pool for reuse
            _availableBuffers.Enqueue(buffer);
            _ = Interlocked.Increment(ref _currentPoolSize);
            _logger.CommandBufferReturned(buffer.ToInt64());
        }
        else
        {
            // Pool is full or buffer is stale, dispose it
            MetalNative.ReleaseCommandBuffer(buffer);
            _logger.CommandBufferDisposed(buffer.ToInt64());
        }
    }

    /// <summary>
    /// Performs cleanup of stale buffers from the pool.
    /// </summary>
    public void Cleanup()
    {
        if (_disposed > 0)
        {
            return;
        }

        var cleaned = 0;
        var currentSize = _availableBuffers.Count;

        // Clean up stale buffers from the pool

        for (var i = 0; i < currentSize; i++)
        {
            if (_availableBuffers.TryDequeue(out var buffer))
            {
                MetalNative.ReleaseCommandBuffer(buffer);
                cleaned++;
                _ = Interlocked.Decrement(ref _currentPoolSize);
            }
        }

        if (cleaned > 0)
        {
            _logger.CommandBufferPoolCleanup(cleaned);
        }

        // Clean up long-running active buffers (potential leaks)
        var cutoff = DateTime.UtcNow.AddMinutes(-5); // 5 minutes
        var staleActive = _activeBuffers
            .Where(kvp => kvp.Value.LastUsed < cutoff)
            .ToList();

        foreach (var (bufferPtr, bufferInfo) in staleActive)
        {
            if (_activeBuffers.TryRemove(bufferPtr, out _))
            {
                _logger.StaleActiveCommandBuffer(
                    DateTime.UtcNow - bufferInfo.CreatedAt, bufferPtr.ToInt64());
                MetalNative.ReleaseCommandBuffer(bufferPtr);
            }
        }
    }

    /// <summary>
    /// Gets statistics about the command buffer pool.
    /// </summary>
    public CommandBufferPoolStats GetStats()
    {
        return new CommandBufferPoolStats
        {
            AvailableBuffers = _availableBuffers.Count,
            ActiveBuffers = _activeBuffers.Count,
            MaxPoolSize = _maxPoolSize,
            CurrentPoolSize = _currentPoolSize
        };
    }

    private static bool IsBufferStale(CommandBufferInfo? info)
    {
        if (info == null)
        {
            return true;
        }

        // Consider buffers older than 1 minute as stale
        var age = DateTime.UtcNow - info.CreatedAt;
        return age.TotalMinutes > 1;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _logger.CommandBufferPoolDisposing();

        // Dispose all available buffers
        while (_availableBuffers.TryDequeue(out var buffer))
        {
            MetalNative.ReleaseCommandBuffer(buffer);
        }

        // Dispose all active buffers
        foreach (var buffer in _activeBuffers.Keys.ToList())
        {
            if (_activeBuffers.TryRemove(buffer, out _))
            {
                MetalNative.ReleaseCommandBuffer(buffer);
            }
        }

        _logger.CommandBufferPoolDisposed();
    }

    /// <summary>
    /// Information about a command buffer.
    /// </summary>
    private sealed class CommandBufferInfo
    {
        public DateTime CreatedAt { get; init; }
        public DateTime LastUsed { get; set; }
    }
}

/// <summary>
/// Statistics about a command buffer pool.
/// </summary>
public sealed class CommandBufferPoolStats
{
    /// <summary>
    /// Number of buffers available in the pool.
    /// </summary>
    public int AvailableBuffers { get; init; }

    /// <summary>
    /// Number of buffers currently in use.
    /// </summary>
    public int ActiveBuffers { get; init; }

    /// <summary>
    /// Maximum size of the pool.
    /// </summary>
    public int MaxPoolSize { get; init; }

    /// <summary>
    /// Current size of the pool.
    /// </summary>
    public int CurrentPoolSize { get; init; }

    /// <summary>
    /// Pool utilization as a percentage.
    /// </summary>
    public double Utilization => MaxPoolSize > 0 ? (double)CurrentPoolSize / MaxPoolSize * 100.0 : 0.0;
}