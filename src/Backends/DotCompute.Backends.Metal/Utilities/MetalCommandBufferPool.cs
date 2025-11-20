// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Pool for managing Metal command buffers to reduce allocation overhead.
/// </summary>
public sealed class MetalCommandBufferPool : IDisposable
{
    private readonly IntPtr _commandQueue;
    private readonly ILogger<MetalCommandBufferPool> _logger;
    private readonly ConcurrentDictionary<IntPtr, CommandBufferInfo> _activeBuffers = new();
    private readonly int _maxPoolSize;
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

        _logger.LogDebug("Command buffer pool created with max size: {MaxSize}", maxPoolSize);
    }

    /// <summary>
    /// Gets a command buffer from the pool or creates a new one.
    /// NOTE: Metal command buffers are one-shot objects and cannot be reused after commit.
    /// This method always creates a fresh command buffer.
    /// </summary>
    /// <returns>A command buffer handle.</returns>
    public IntPtr GetCommandBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        // IMPORTANT: Metal command buffers cannot be reused after commit.
        // They are one-shot objects. Always create a fresh buffer.
        // Attempting to reuse causes: "failed assertion _status < MTLCommandBufferStatusCommitted"
        var buffer = MetalNative.CreateCommandBuffer(_commandQueue);
        if (buffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal command buffer");
        }
        _logger.LogDebug("Command buffer created: 0x{Buffer:X}", buffer.ToInt64());

        // Track the active buffer
        var info = new CommandBufferInfo
        {
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow
        };

        if (!_activeBuffers.TryAdd(buffer, info))
        {
            _logger.LogWarning("Command buffer tracking failed: 0x{Buffer:X}", buffer.ToInt64());
        }

        return buffer;
    }

    /// <summary>
    /// Returns a command buffer to the pool for cleanup.
    /// NOTE: Metal command buffers cannot be reused after commit, so they are always disposed.
    /// This method exists for API compatibility and proper tracking cleanup.
    /// </summary>
    /// <param name="buffer">The command buffer to dispose.</param>
    public void ReturnCommandBuffer(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero || _disposed > 0)
        {
            return;
        }

        // Remove from active tracking
        if (!_activeBuffers.TryRemove(buffer, out _))
        {
            _logger.LogWarning("Command buffer untracked: 0x{Buffer:X}", buffer.ToInt64());
        }

        // IMPORTANT: Metal command buffers cannot be reused after commit.
        // Always dispose them. Never return to pool.
        MetalNative.ReleaseCommandBuffer(buffer);
        _logger.LogDebug("Command buffer disposed: 0x{Buffer:X}", buffer.ToInt64());
    }

    /// <summary>
    /// Performs cleanup of stale buffers from the pool.
    /// Since command buffers are no longer pooled for reuse, this only cleans up leaked active buffers.
    /// </summary>
    public void Cleanup()
    {
        if (_disposed > 0)
        {
            return;
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
                _logger.LogWarning("Stale active command buffer detected: age {Age}, buffer 0x{Buffer:X}",
                    DateTime.UtcNow - bufferInfo.CreatedAt, bufferPtr.ToInt64());
                MetalNative.ReleaseCommandBuffer(bufferPtr);
            }
        }

        if (staleActive.Count > 0)
        {
            _logger.LogDebug("Command buffer cleanup: {CleanedCount} leaked buffers cleaned", staleActive.Count);
        }
    }

    /// <summary>
    /// Gets statistics about the command buffer pool.
    /// Note: Since command buffers are no longer pooled for reuse, AvailableBuffers and CurrentPoolSize are always 0.
    /// </summary>
#pragma warning disable CA1721 // Property name conflicts with method - both exist for API compatibility
    public CommandBufferPoolStats Stats => new()
    {
        AvailableBuffers = 0, // No longer pooling buffers for reuse
        ActiveBuffers = _activeBuffers.Count,
        MaxPoolSize = _maxPoolSize,
        CurrentPoolSize = 0 // No longer pooling buffers for reuse
    };

#pragma warning disable CA1024 // Method form intentional for API compatibility with callers expecting method syntax
    /// <summary>
    /// Gets statistics about the command buffer pool (API compatibility method).
    /// </summary>
    public CommandBufferPoolStats GetStats() => Stats;
#pragma warning restore CA1024
#pragma warning restore CA1721

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _logger.LogDebug("Command buffer pool disposing...");

        // Dispose all active buffers
        foreach (var buffer in _activeBuffers.Keys.ToList())
        {
            if (_activeBuffers.TryRemove(buffer, out _))
            {
                MetalNative.ReleaseCommandBuffer(buffer);
            }
        }

        _logger.LogDebug("Command buffer pool disposed");
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
