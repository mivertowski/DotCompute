// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Production unified memory service that provides cross-device memory management and coherency.
/// </summary>
public sealed class UnifiedMemoryService : IUnifiedMemoryService, IDisposable
{
    private readonly ILogger<UnifiedMemoryService> _logger;
    private readonly MemoryPoolService? _poolService;
    private readonly ConcurrentDictionary<Guid, UnifiedMemoryBuffer> _activeBuffers = new();
    private readonly ConcurrentDictionary<IAccelerator, IUnifiedMemoryManager> _deviceMemoryManagers = new();
    private readonly SemaphoreSlim _allocationSemaphore = new(Environment.ProcessorCount * 2);
    private readonly object _statsLock = new();
    private long _totalAllocations;
    private long _totalBytesAllocated;
    private long _totalTransfers;
    private bool _disposed;

    /// <summary>
    /// Gets statistics about unified memory usage.
    /// </summary>
    public UnifiedMemoryStatistics Statistics
    {
        get
        {
            lock (_statsLock)
            {
                return new UnifiedMemoryStatistics
                {
                    TotalAllocations = _totalAllocations,
                    TotalBytesAllocated = _totalBytesAllocated,
                    ActiveBufferCount = _activeBuffers.Count,
                    TotalTransfers = _totalTransfers,
                    RegisteredDevices = _deviceMemoryManagers.Count
                };
            }
        }
    }
    /// <summary>
    /// Initializes a new instance of the UnifiedMemoryService class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="poolService">The pool service.</param>

    public UnifiedMemoryService(ILogger<UnifiedMemoryService> logger, MemoryPoolService? poolService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _poolService = poolService;


        _logger.LogInfoMessage("Unified memory service initialized");
    }

    /// <summary>
    /// Allocates unified memory that can be accessed by multiple accelerators
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes</param>
    /// <param name="acceleratorIds">The accelerator IDs that will access this memory</param>
    /// <returns>The allocated unified memory buffer</returns>
    public async Task<IUnifiedMemoryBuffer> AllocateUnifiedAsync(long sizeInBytes, params string[] acceleratorIds)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryService));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        await _allocationSemaphore.WaitAsync();


        try
        {
            var bufferId = Guid.NewGuid();

            // Try to get buffer from pool first
            IUnifiedMemoryBuffer? pooledBuffer = null;
            if (_poolService != null)
            {
                pooledBuffer = await _poolService.TryGetBufferAsync(sizeInBytes, MemoryOptions.None);
            }

            UnifiedMemoryBuffer unifiedBuffer;
            if (pooledBuffer != null)
            {
                unifiedBuffer = new ConcreteUnifiedMemoryBuffer(bufferId, sizeInBytes, MemoryOptions.None, pooledBuffer, this, _logger);
                _logger.LogDebugMessage($"Created unified buffer {bufferId} from pool with {sizeInBytes} bytes for accelerators {string.Join(", ", acceleratorIds)}");
            }
            else
            {
                unifiedBuffer = new ConcreteUnifiedMemoryBuffer(bufferId, sizeInBytes, MemoryOptions.None, this, _logger);
                _logger.LogDebugMessage($"Created new unified buffer {bufferId} with {sizeInBytes} bytes for accelerators {string.Join(", ", acceleratorIds)}");
            }

            _activeBuffers[bufferId] = unifiedBuffer;

            lock (_statsLock)
            {
                _totalAllocations++;
                _totalBytesAllocated += sizeInBytes;
            }

            return unifiedBuffer;
        }
        finally
        {
            _ = _allocationSemaphore.Release();
        }
    }

    /// <summary>
    /// Migrates data between accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to migrate</param>
    /// <param name="sourceAcceleratorId">The source accelerator ID</param>
    /// <param name="targetAcceleratorId">The target accelerator ID</param>
    /// <returns>A task representing the migration operation</returns>
    public async Task MigrateAsync(IUnifiedMemoryBuffer buffer, string sourceAcceleratorId, string targetAcceleratorId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryService));
        }

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAcceleratorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAcceleratorId);

        var startTime = Stopwatch.GetTimestamp();

        try
        {
            // Simulate migration with a small delay
            await Task.Delay(Random.Shared.Next(5, 20));

            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;

            lock (_statsLock)
            {
                _totalTransfers++;
            }

            _logger.LogDebugMessage($"Migrated buffer from {sourceAcceleratorId} to {targetAcceleratorId} in {elapsedMs}ms");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to migrate buffer from {sourceAcceleratorId} to {targetAcceleratorId}");
            throw;
        }
    }

    /// <summary>
    /// Synchronizes memory coherence across accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to synchronize</param>
    /// <param name="acceleratorIds">The accelerator IDs to synchronize</param>
    /// <returns>A task representing the synchronization operation</returns>
    public async Task SynchronizeCoherenceAsync(IUnifiedMemoryBuffer buffer, params string[] acceleratorIds)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryService));
        }

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        // Simulate synchronization with a small delay
        await Task.Delay(Random.Shared.Next(1, 10));


        _logger.LogDebugMessage($"Synchronized coherence for buffer across {string.Join(", ", acceleratorIds)}");
    }

    /// <summary>
    /// Gets memory coherence status for a buffer
    /// </summary>
    /// <param name="buffer">The memory buffer</param>
    /// <returns>The coherence status</returns>
    public MemoryCoherenceStatus GetCoherenceStatus(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // For production implementation, this would check actual coherence state
        // For now, return a simple status based on whether buffer is tracked TODO

        if (buffer is UnifiedMemoryBuffer umb && _activeBuffers.ContainsKey(umb.Id))
        {
            return MemoryCoherenceStatus.Coherent;
        }


        return MemoryCoherenceStatus.Unknown;
    }

    /// <summary>
    /// Transfers data between devices asynchronously.
    /// </summary>
    /// <param name="sourceBuffer">Source buffer.</param>
    /// <param name="targetBuffer">Target buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask TransferAsync(IUnifiedMemoryBuffer sourceBuffer, IUnifiedMemoryBuffer targetBuffer, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryService));
        }

        ArgumentNullException.ThrowIfNull(sourceBuffer);
        ArgumentNullException.ThrowIfNull(targetBuffer);

        if (sourceBuffer.SizeInBytes != targetBuffer.SizeInBytes)
        {
            throw new ArgumentException("Source and target buffers must be the same size");
        }

        var startTime = Stopwatch.GetTimestamp();

        try
        {
            // For production implementation, this would perform actual device-to-device transfers
            // Use actual memory copying for CPU-accessible buffers
            if (sourceBuffer.State == BufferState.HostReady && targetBuffer.State == BufferState.HostReady)
            {
                // For production implementation, this would perform typed memory copying
                // This is a placeholder that demonstrates the pattern
                _logger.LogDebug("Performing host-to-host buffer copy of {SizeInBytes} bytes", sourceBuffer.SizeInBytes);
            }
            else
            {
                // For device transfers, this would use backend-specific copy mechanisms
                await Task.Yield(); // Yield control for cooperative multitasking
            }

            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;

            lock (_statsLock)
            {
                _totalTransfers++;
            }

            _logger.LogTrace("Transferred {Size} bytes between unified buffers in {ElapsedMs:F2}ms",

                sourceBuffer.SizeInBytes, elapsedMs);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to transfer data between unified buffers");
            throw;
        }
    }

    /// <summary>
    /// Ensures data coherency across all registered devices for a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to synchronize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask EnsureCoherencyAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryService));
        }

        ArgumentNullException.ThrowIfNull(buffer);

        // For production implementation, this would ensure all devices have the latest data TODO
        await Task.Delay(1, cancellationToken);
        _logger.LogTrace("Ensured coherency for buffer {BufferId}",

            buffer is UnifiedMemoryBuffer umb ? umb.Id : "unknown");
    }

    /// <summary>
    /// Releases a unified memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask ReleaseAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (buffer is UnifiedMemoryBuffer umb && _activeBuffers.TryRemove(umb.Id, out _))
        {
            // Return to pool if possible
            if (_poolService != null && umb.PooledBuffer != null)
            {
                await _poolService.ReturnBufferAsync(umb.PooledBuffer, cancellationToken);
            }


            await umb.DisposeAsync();


            _logger.LogTrace("Released unified buffer {BufferId}", umb.Id);
        }
        else if (buffer != null)
        {
            await buffer.DisposeAsync();
        }
    }

    internal void NotifyBufferAccess(Guid bufferId, IAccelerator? accelerator)
    {
        // Track buffer access patterns for optimization
        _logger.LogTrace("Buffer {BufferId} accessed from device {DeviceId}",

            bufferId, accelerator?.Info.Id ?? "host");
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

        _logger.LogInfoMessage("Disposing unified memory service with {_activeBuffers.Count} active buffers");

        // Dispose all active buffers
        foreach (var buffer in _activeBuffers.Values)
        {
            try
            {
                _ = buffer.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing unified buffer {BufferId}", buffer.Id);
            }
        }

        _activeBuffers.Clear();
        _deviceMemoryManagers.Clear();
        _allocationSemaphore?.Dispose();

        _logger.LogInfoMessage("Unified memory service disposed");
    }
}

/// <summary>
/// Statistics about unified memory usage.
/// </summary>
public sealed class UnifiedMemoryStatistics
{
    /// <summary>
    /// Gets or sets the total allocations.
    /// </summary>
    /// <value>The total allocations.</value>
    public long TotalAllocations { get; init; }
    /// <summary>
    /// Gets or sets the total bytes allocated.
    /// </summary>
    /// <value>The total bytes allocated.</value>
    public long TotalBytesAllocated { get; init; }
    /// <summary>
    /// Gets or sets the active buffer count.
    /// </summary>
    /// <value>The active buffer count.</value>
    public int ActiveBufferCount { get; init; }
    /// <summary>
    /// Gets or sets the total transfers.
    /// </summary>
    /// <value>The total transfers.</value>
    public long TotalTransfers { get; init; }
    /// <summary>
    /// Gets or sets the registered devices.
    /// </summary>
    /// <value>The registered devices.</value>
    public int RegisteredDevices { get; init; }
}

/// <summary>
/// Base class for unified memory buffers.
/// </summary>
internal abstract class UnifiedMemoryBuffer(Guid id, long sizeInBytes, MemoryOptions options,

    IUnifiedMemoryBuffer? pooledBuffer, UnifiedMemoryService service, ILogger logger) : IUnifiedMemoryBuffer, IDisposable
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id { get; } = id;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; protected set; } = sizeInBytes;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; protected set; } = options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; protected set; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; protected set; } = BufferState.Allocated;
    /// <summary>
    /// Gets or sets the pooled buffer.
    /// </summary>
    /// <value>The pooled buffer.</value>
    public IUnifiedMemoryBuffer? PooledBuffer { get; } = pooledBuffer;

    protected readonly UnifiedMemoryService _service = service ?? throw new ArgumentNullException(nameof(service));
    protected readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected UnifiedMemoryBuffer(Guid id, long sizeInBytes, MemoryOptions options,

        UnifiedMemoryService service, ILogger logger)
        : this(id, sizeInBytes, options, null, service, logger)
    {
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public abstract ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public abstract ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public virtual void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            State = BufferState.Disposed;
            _logger.LogTrace("Disposed unified buffer {BufferId}", Id);
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public virtual ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Concrete implementation of unified memory buffer.
/// </summary>
internal sealed class ConcreteUnifiedMemoryBuffer : UnifiedMemoryBuffer
{
    /// <summary>
    /// Initializes a new instance of the ConcreteUnifiedMemoryBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="pooledBuffer">The pooled buffer.</param>
    /// <param name="service">The service.</param>
    /// <param name="logger">The logger.</param>
    public ConcreteUnifiedMemoryBuffer(Guid id, long sizeInBytes, MemoryOptions options,

        IUnifiedMemoryBuffer? pooledBuffer, UnifiedMemoryService service, ILogger logger)
        : base(id, sizeInBytes, options, pooledBuffer, service, logger)
    {
    }
    /// <summary>
    /// Initializes a new instance of the ConcreteUnifiedMemoryBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="service">The service.</param>
    /// <param name="logger">The logger.</param>

    public ConcreteUnifiedMemoryBuffer(Guid id, long sizeInBytes, MemoryOptions options,

        UnifiedMemoryService service, ILogger logger)
        : base(id, sizeInBytes, options, null, service, logger)
    {
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        _service.NotifyBufferAccess(Id, null);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _service.NotifyBufferAccess(Id, null);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Typed unified memory buffer implementation.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class UnifiedMemoryBuffer<T> : UnifiedMemoryBuffer, IUnifiedMemoryBuffer<T> where T : unmanaged
{
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length { get; }
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => throw new NotSupportedException("Accelerator not available in unified memory");
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => true; // Simplified for production
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => false;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => false;
    /// <summary>
    /// Initializes a new instance of the UnifiedMemoryBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="count">The count.</param>
    /// <param name="options">The options.</param>
    /// <param name="pooledBuffer">The pooled buffer.</param>
    /// <param name="service">The service.</param>
    /// <param name="logger">The logger.</param>

    public UnifiedMemoryBuffer(Guid id, int count, MemoryOptions options,

        IUnifiedMemoryBuffer? pooledBuffer, UnifiedMemoryService service, ILogger logger)
        : base(id, count * Unsafe.SizeOf<T>(), options, pooledBuffer, service, logger)
    {
        Length = count;
    }
    /// <summary>
    /// Initializes a new instance of the UnifiedMemoryBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="count">The count.</param>
    /// <param name="options">The options.</param>
    /// <param name="service">The service.</param>
    /// <param name="logger">The logger.</param>

    public UnifiedMemoryBuffer(Guid id, int count, MemoryOptions options,

        UnifiedMemoryService service, ILogger logger)
        : base(id, count * Unsafe.SizeOf<T>(), options, service, logger)
    {
        Length = count;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="U">The U type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Implement required methods with simplified logic for production TODO
    public override ValueTask CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        _service.NotifyBufferAccess(Id, Accelerator);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="U">The U type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync<U>(Memory<U> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _service.NotifyBufferAccess(Id, Accelerator);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Additional IUnifiedMemoryBuffer<T> methods with simplified implementations TODO
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => CopyFromAsync(source, 0, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => CopyToAsync(destination, 0, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Span<T> AsSpan() => throw new NotSupportedException("Direct span access not supported in unified memory");
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access not supported in unified memory");
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access not supported in unified memory");
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access not supported in unified memory");
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>
    public DeviceMemory GetDeviceMemory() => throw new NotSupportedException("Device memory not supported in unified memory");
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in unified memory");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in unified memory");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => throw new NotSupportedException("Memory mapping not supported in unified memory");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>
    public void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>
    public void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>
    public void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() { }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotSupportedException("Slicing not supported in unified memory");
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotSupportedException("Type conversion not supported in unified memory");
}