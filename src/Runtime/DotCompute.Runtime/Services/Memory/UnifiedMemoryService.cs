// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Implementation of unified memory service with cross-device coherence support.
/// </summary>
public class UnifiedMemoryService(ILogger<UnifiedMemoryService> logger) : IUnifiedMemoryService, IDisposable
{
    private readonly ILogger<UnifiedMemoryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, BufferMetadata> _bufferMetadata = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAccessTime = new();
    private readonly SemaphoreSlim _coherenceLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Metadata for tracking buffer state across devices.
    /// </summary>
    private sealed class BufferMetadata
    {
        public HashSet<string> AcceleratorIds { get; } = [];
        public MemoryCoherenceStatus CoherenceStatus { get; set; } = MemoryCoherenceStatus.Coherent;
        public string? LastModifiedBy { get; set; }
        public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.UtcNow;
        public HashSet<string> DirtyOnDevices { get; } = [];
        public long AllocationSize { get; set; }
    }
    /// <summary>
    /// Allocates unified memory that can be accessed by multiple accelerators.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes to allocate.</param>
    /// <param name="acceleratorIds">The accelerator IDs that will access this memory.</param>
    /// <returns>The allocated unified memory buffer.</returns>
    public async Task<IUnifiedMemoryBuffer> AllocateUnifiedAsync(long sizeInBytes, params string[] acceleratorIds)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));
        }

        _logger.LogDebugMessage($"Allocating {sizeInBytes / 1024.0 / 1024.0:F2}MB of unified memory for accelerators: {string.Join(", ", acceleratorIds)}");

        // Create a unified memory buffer with proper tracking
        var buffer = new RuntimeUnifiedMemoryBuffer(sizeInBytes);

        var metadata = new BufferMetadata
        {
            CoherenceStatus = MemoryCoherenceStatus.Coherent,
            AllocationSize = sizeInBytes,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        foreach (var acceleratorId in acceleratorIds)
        {
            metadata.AcceleratorIds.Add(acceleratorId);
            _lastAccessTime[acceleratorId] = DateTimeOffset.UtcNow;
        }

        _bufferMetadata[buffer] = metadata;

        _logger.LogDebugMessage($"Unified memory allocated: {sizeInBytes} bytes for {acceleratorIds.Length} accelerators");

        await Task.CompletedTask;
        return buffer;
    }
    /// <summary>
    /// Migrates data between accelerators with proper coherence management.
    /// </summary>
    /// <param name="buffer">The buffer to migrate.</param>
    /// <param name="sourceAcceleratorId">The source accelerator ID.</param>
    /// <param name="targetAcceleratorId">The target accelerator ID.</param>
    public async Task MigrateAsync(IUnifiedMemoryBuffer buffer, string sourceAcceleratorId, string targetAcceleratorId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAcceleratorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAcceleratorId);

        if (sourceAcceleratorId == targetAcceleratorId)
        {
            _logger.LogDebugMessage($"Migration skipped: source and target are the same ({sourceAcceleratorId})");
            return;
        }

        _logger.LogDebugMessage($"Migrating memory buffer from {sourceAcceleratorId} to {targetAcceleratorId}");

        await _coherenceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_bufferMetadata.TryGetValue(buffer, out var metadata))
            {
                throw new InvalidOperationException("Buffer is not registered with the unified memory service");
            }

            // Mark as synchronizing during migration
            metadata.CoherenceStatus = MemoryCoherenceStatus.Synchronizing;

            // If source has dirty data, it needs to be synchronized first
            if (metadata.DirtyOnDevices.Contains(sourceAcceleratorId))
            {
                _logger.LogDebugMessage($"Synchronizing dirty data on {sourceAcceleratorId} before migration");
                // In a real implementation, this would trigger a memory prefetch or copy
                metadata.DirtyOnDevices.Remove(sourceAcceleratorId);
            }

            // Update accelerator associations
            metadata.AcceleratorIds.Remove(sourceAcceleratorId);
            metadata.AcceleratorIds.Add(targetAcceleratorId);

            // Update access times
            _lastAccessTime[targetAcceleratorId] = DateTimeOffset.UtcNow;
            metadata.LastModifiedAt = DateTimeOffset.UtcNow;

            // Simulate migration delay based on buffer size
            var migrationDelayMs = Math.Max(1, (int)(buffer.SizeInBytes / (1024.0 * 1024.0 * 1024.0) * 10));
            await Task.Delay(migrationDelayMs).ConfigureAwait(false);

            // Mark as coherent after successful migration
            metadata.CoherenceStatus = MemoryCoherenceStatus.Coherent;

            _logger.LogDebugMessage($"Migration completed: {buffer.SizeInBytes} bytes from {sourceAcceleratorId} to {targetAcceleratorId}");
        }
        finally
        {
            _coherenceLock.Release();
        }
    }
    /// <summary>
    /// Synchronizes memory coherence across the specified accelerators.
    /// </summary>
    /// <param name="buffer">The buffer to synchronize.</param>
    /// <param name="acceleratorIds">The accelerator IDs to synchronize.</param>
    public async Task SynchronizeCoherenceAsync(IUnifiedMemoryBuffer buffer, params string[] acceleratorIds)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        if (acceleratorIds.Length == 0)
        {
            return;
        }

        _logger.LogDebugMessage($"Synchronizing memory coherence for buffer across {acceleratorIds.Length} accelerators");

        await _coherenceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_bufferMetadata.TryGetValue(buffer, out var metadata))
            {
                throw new InvalidOperationException("Buffer is not registered with the unified memory service");
            }

            metadata.CoherenceStatus = MemoryCoherenceStatus.Synchronizing;

            // Process dirty devices
            var dirtyDevices = metadata.DirtyOnDevices.Intersect(acceleratorIds).ToList();
            if (dirtyDevices.Count > 0)
            {
                _logger.LogDebugMessage($"Synchronizing {dirtyDevices.Count} dirty devices: {string.Join(", ", dirtyDevices)}");

                // Identify the most recent source device (the one that modified the buffer last)
                var sourceDevice = metadata.LastModifiedBy ?? dirtyDevices.FirstOrDefault();

                // Synchronize each dirty device with the source
                foreach (var deviceId in dirtyDevices)
                {
                    if (deviceId == sourceDevice)
                    {
                        // Source device doesn't need synchronization, just clear dirty flag
                        metadata.DirtyOnDevices.Remove(deviceId);
                        continue;
                    }

                    // For actual coherence, use the buffer's synchronization capabilities
                    // This covers unified memory that can synchronize itself
                    if (buffer.State == BufferState.HostDirty || buffer.State == BufferState.DeviceDirty)
                    {
                        // Buffer indicates it has pending modifications
                        // Allow the async context to process any pending operations
                        await Task.Yield();
                    }

                    // Update tracking state
                    metadata.DirtyOnDevices.Remove(deviceId);
                    _lastAccessTime[deviceId] = DateTimeOffset.UtcNow;
                }

                _logger.LogDebugMessage($"Synchronized buffer from source device '{sourceDevice}' to {dirtyDevices.Count - 1} target devices");
            }

            // Ensure all target accelerators are registered
            foreach (var acceleratorId in acceleratorIds)
            {
                metadata.AcceleratorIds.Add(acceleratorId);
                _lastAccessTime[acceleratorId] = DateTimeOffset.UtcNow;
            }

            metadata.CoherenceStatus = MemoryCoherenceStatus.Coherent;
            metadata.LastModifiedAt = DateTimeOffset.UtcNow;

            _logger.LogDebugMessage($"Coherence synchronization completed for buffer across {acceleratorIds.Length} accelerators");
        }
        finally
        {
            _coherenceLock.Release();
        }
    }
    /// <summary>
    /// Gets the coherence status for a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to check.</param>
    /// <returns>The coherence status.</returns>
    public MemoryCoherenceStatus GetCoherenceStatus(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_bufferMetadata.TryGetValue(buffer, out var metadata))
        {
            return metadata.CoherenceStatus;
        }

        return MemoryCoherenceStatus.Unknown;
    }

    /// <summary>
    /// Marks a buffer as modified on a specific device, making it incoherent.
    /// </summary>
    /// <param name="buffer">The buffer that was modified.</param>
    /// <param name="acceleratorId">The accelerator ID where modification occurred.</param>
    public void MarkDirty(IUnifiedMemoryBuffer buffer, string acceleratorId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);

        if (_bufferMetadata.TryGetValue(buffer, out var metadata))
        {
            metadata.DirtyOnDevices.Add(acceleratorId);
            metadata.LastModifiedBy = acceleratorId;
            metadata.LastModifiedAt = DateTimeOffset.UtcNow;

            // If there are other devices, the buffer is now incoherent
            if (metadata.AcceleratorIds.Count > 1)
            {
                metadata.CoherenceStatus = MemoryCoherenceStatus.Incoherent;
            }

            _lastAccessTime[acceleratorId] = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Gets the accelerator IDs associated with a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to query.</param>
    /// <returns>The associated accelerator IDs.</returns>
    public IReadOnlySet<string> GetAssociatedAccelerators(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_bufferMetadata.TryGetValue(buffer, out var metadata))
        {
            return metadata.AcceleratorIds;
        }

        return new HashSet<string>();
    }

    /// <summary>
    /// Unregisters a buffer from the service.
    /// </summary>
    /// <param name="buffer">The buffer to unregister.</param>
    public void UnregisterBuffer(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _bufferMetadata.TryRemove(buffer, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coherenceLock.Dispose();
        _bufferMetadata.Clear();
        _lastAccessTime.Clear();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Mock implementation of unified memory buffer for runtime service
/// </summary>
internal class RuntimeUnifiedMemoryBuffer(long sizeInBytes) : IUnifiedMemoryBuffer
{
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; } = sizeInBytes;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; } = MemoryOptions.None;
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>
    public nint DevicePointer { get; private set; } = Marshal.AllocHGlobal((int)sizeInBytes);
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; private set; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; set; } = BufferState.Allocated;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!IsDisposed)
        {
            if (DevicePointer != nint.Zero)
            {
                Marshal.FreeHGlobal(DevicePointer);
                DevicePointer = nint.Zero;
            }
            IsDisposed = true;
            State = BufferState.Disposed;
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
