// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - Base memory manager has dynamic logging requirements

namespace DotCompute.Core.Memory
{

/// <summary>
/// Abstract base class for memory managers that provides common functionality
/// and eliminates duplicate implementations across backend memory managers.
/// Uses the template method pattern to allow backend-specific customization while
/// consolidating shared memory management patterns.
/// </summary>
/// <typeparam name="TBuffer">The type of memory buffer this manager creates.</typeparam>
/// <typeparam name="TStatistics">The type of statistics this manager tracks.</typeparam>
public abstract class BaseMemoryManager<TBuffer, TStatistics> : IMemoryManager, IDisposable 
    where TBuffer : class, IMemoryBuffer
    where TStatistics : class, new()
{
    private readonly ConcurrentDictionary<IMemoryBuffer, TBuffer> _trackedBuffers = new();
    private readonly object _statisticsLock = new();
    private readonly Timer? _memoryPressureTimer;
    private readonly ILogger? _logger;
    
    private long _totalAllocatedBytes;
    private int _allocationCount;
    private long _peakUsageBytes;
    private TStatistics _statistics;
    private bool _disposed;

    /// <summary>
    /// Gets the name of this memory manager for logging purposes.
    /// </summary>
    protected abstract string ManagerName { get; }

    /// <summary>
    /// Gets the maximum memory allocation size supported by this manager.
    /// Override to provide backend-specific limits.
    /// </summary>
    protected virtual long MaxAllocationSize => long.MaxValue;

    /// <summary>
    /// Gets the memory pressure threshold as a percentage (0.0 to 1.0).
    /// When exceeded, memory cleanup is triggered.
    /// </summary>
    protected virtual double MemoryPressureThreshold => 0.85;

    /// <summary>
    /// Gets whether memory pressure monitoring is enabled.
    /// Override to disable monitoring for backends that don't need it.
    /// </summary>
    protected virtual bool EnableMemoryPressureMonitoring => true;

    /// <summary>
    /// Gets the memory pressure monitoring interval.
    /// </summary>
    protected virtual TimeSpan MemoryPressureCheckInterval => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the current memory usage statistics.
    /// </summary>
    protected TStatistics Statistics 
    { 
        get
        {
            lock (_statisticsLock)
            {
                return _statistics;
            }
        }
    }

    /// <summary>
    /// Gets the total allocated memory in bytes.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);

    /// <summary>
    /// Gets the current allocation count.
    /// </summary>
    public int AllocationCount => _allocationCount;

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakUsageBytes => Interlocked.Read(ref _peakUsageBytes);

    /// <summary>
    /// Initializes a new instance of the BaseMemoryManager class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    protected BaseMemoryManager(ILogger? logger = null)
    {
        _logger = logger;
        _statistics = new TStatistics();

        if (EnableMemoryPressureMonitoring)
        {
            _memoryPressureTimer = new Timer(
                CheckMemoryPressure, 
                null, 
                MemoryPressureCheckInterval, 
                MemoryPressureCheckInterval);
        }

        _logger?.LogInformation("Initialized {ManagerName} memory manager", ManagerName);
    }

    #region IMemoryManager Implementation

    /// <inheritdoc/>
    public virtual async ValueTask<DotCompute.Abstractions.IBuffer<T>> CreateBufferAsync<T>(
        int elementCount,
        DotCompute.Core.Memory.MemoryLocation location,
        DotCompute.Core.Memory.MemoryAccess access = DotCompute.Core.Memory.MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if (elementCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be greater than zero");

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = (long)elementCount * elementSize;

        if (sizeInBytes > MaxAllocationSize)
            throw new ArgumentOutOfRangeException(nameof(elementCount), 
                $"Requested size {sizeInBytes} exceeds maximum allocation size {MaxAllocationSize}");

        try
        {
            _logger?.LogDebug("Allocating {Count} elements of type {Type} ({Size}MB) at {Location} with {Access} on {Manager}", 
                elementCount, typeof(T).Name, sizeInBytes / (1024 * 1024), location, access, ManagerName);

            // Delegate to backend-specific implementation
            var buffer = await CreateBufferInternalAsync<T>(elementCount, location, access, cancellationToken)
                .ConfigureAwait(false);

            // Update statistics
            UpdateAllocationStatistics(sizeInBytes, true);

            _logger?.LogDebug("Successfully allocated {Manager} buffer of {Count} {Type} elements ({Size}MB)", 
                ManagerName, elementCount, typeof(T).Name, sizeInBytes / (1024 * 1024));

            return buffer;
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger?.LogError(ex, "Failed to allocate {Manager} memory for {Count} {Type} elements", 
                ManagerName, elementCount, typeof(T).Name);
            throw new MemoryException($"Failed to allocate {elementCount} elements of type {typeof(T).Name} in {ManagerName} memory", ex);
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask<DotCompute.Abstractions.IBuffer<T>> CreateBufferAsync<T>(
        ReadOnlyMemory<T> data,
        DotCompute.Core.Memory.MemoryLocation location,
        DotCompute.Core.Memory.MemoryAccess access = DotCompute.Core.Memory.MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var buffer = await CreateBufferAsync<T>(data.Length, location, access, cancellationToken).ConfigureAwait(false);
        
        try
        {
            await buffer.CopyFromHostAsync(data, 0, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask CopyAsync<T>(
        DotCompute.Abstractions.IBuffer<T> source,
        DotCompute.Abstractions.IBuffer<T> destination,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long? elementCount = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var elementsToTransfer = elementCount ?? Math.Min(source.ElementCount - sourceOffset, destination.ElementCount - destinationOffset);
        
        if (elementsToTransfer <= 0)
            return;

        try
        {
            await CopyBufferInternalAsync(source, destination, sourceOffset, destinationOffset, elementsToTransfer, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger?.LogError(ex, "Failed to copy {Count} elements from {Source} to {Destination} in {Manager}", 
                elementsToTransfer, sourceOffset, destinationOffset, ManagerName);
            throw new MemoryException($"Failed to copy {elementsToTransfer} elements in {ManagerName}", ex);
        }
    }

    /// <inheritdoc/>
    public virtual DotCompute.Core.Memory.IMemoryStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return CreateMemoryStatistics();
    }

    /// <inheritdoc/>
    public abstract DotCompute.Core.Memory.MemoryLocation[] AvailableLocations { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            _logger?.LogInformation("Disposing {Manager} memory manager", ManagerName);

            // Stop memory pressure monitoring
            _memoryPressureTimer?.Dispose();

            // Dispose all tracked buffers
            await ForceCleanupAllBuffersAsync().ConfigureAwait(false);

            // Allow derived classes to perform additional cleanup
            await DisposeCoreAsync().ConfigureAwait(false);

            _logger?.LogInformation("{Manager} memory manager disposed", ManagerName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during {Manager} memory manager disposal", ManagerName);
        }
        finally
        {
            _disposed = true;
        }
    }

    // Legacy methods for backward compatibility
    /// <inheritdoc/>
    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (sizeInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be greater than zero");

        if (sizeInBytes > MaxAllocationSize)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), 
                $"Requested size {sizeInBytes} exceeds maximum allocation size {MaxAllocationSize}");

        try
        {
            _logger?.LogDebug("Allocating {Size}MB with options {Options} on {Manager}", 
                sizeInBytes / (1024 * 1024), options, ManagerName);

            // Delegate to backend-specific implementation
            var buffer = await AllocateBufferAsync(sizeInBytes, options, cancellationToken)
                .ConfigureAwait(false);

            // Track the buffer
            if (!_trackedBuffers.TryAdd(buffer, buffer))
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
                throw new MemoryException("Failed to track allocated buffer");
            }

            // Update statistics
            UpdateAllocationStatistics(sizeInBytes, true);

            _logger?.LogDebug("Successfully allocated {Manager} buffer of {Size}MB", 
                ManagerName, sizeInBytes / (1024 * 1024));

            return buffer;
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger?.LogError(ex, "Failed to allocate {Manager} memory", ManagerName);
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of {ManagerName} memory", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);

        try
        {
            await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (!_trackedBuffers.TryGetValue(buffer, out var typedBuffer))
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));

        return CreateBufferView(typedBuffer, offset, length);
    }

    /// <inheritdoc/>
    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

    /// <inheritdoc/>
    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (!_trackedBuffers.TryGetValue(buffer, out var typedBuffer))
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;

        if (sizeInBytes > buffer.SizeInBytes)
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));

        try
        {
            CopyToDeviceInternal(typedBuffer, data);
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger?.LogError(ex, "Failed to copy data to {Manager} device", ManagerName);
            throw new MemoryException($"Failed to copy data to {ManagerName} device", ex);
        }
    }

    /// <inheritdoc/>
    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (!_trackedBuffers.TryGetValue(buffer, out var typedBuffer))
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;

        if (sizeInBytes > buffer.SizeInBytes)
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));

        try
        {
            CopyFromDeviceInternal(data, typedBuffer);
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger?.LogError(ex, "Failed to copy data from {Manager} device", ManagerName);
            throw new MemoryException($"Failed to copy data from {ManagerName} device", ex);
        }
    }

    /// <inheritdoc/>
    public void Free(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_trackedBuffers.TryRemove(buffer, out var typedBuffer))
        {
            try
            {
                FreeBufferInternal(typedBuffer);
                UpdateAllocationStatistics(buffer.SizeInBytes, false);

                _logger?.LogDebug("Freed {Manager} buffer of {Size}MB", 
                    ManagerName, buffer.SizeInBytes / (1024 * 1024));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing {Manager} buffer during free", ManagerName);
                throw;
            }
        }
    }

    #endregion

    #region Template Methods - Override in Derived Classes

    /// <summary>
    /// Creates a buffer with the specified element count, location, and access mode.
    /// This method must be implemented by derived classes to provide backend-specific buffer creation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="elementCount">The number of elements.</param>
    /// <param name="location">The memory location.</param>
    /// <param name="access">The memory access mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the allocated buffer.</returns>
    protected abstract ValueTask<DotCompute.Abstractions.IBuffer<T>> CreateBufferInternalAsync<T>(
        int elementCount, 
        DotCompute.Core.Memory.MemoryLocation location, 
        DotCompute.Core.Memory.MemoryAccess access, 
        CancellationToken cancellationToken) where T : unmanaged;

    /// <summary>
    /// Copies data between buffers.
    /// Override this method to provide backend-specific buffer copying logic.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="sourceOffset">The source offset in elements.</param>
    /// <param name="destinationOffset">The destination offset in elements.</param>
    /// <param name="elementCount">The number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    protected abstract ValueTask CopyBufferInternalAsync<T>(
        DotCompute.Abstractions.IBuffer<T> source,
        DotCompute.Abstractions.IBuffer<T> destination,
        long sourceOffset,
        long destinationOffset,
        long elementCount,
        CancellationToken cancellationToken) where T : unmanaged;

    /// <summary>
    /// Creates memory statistics for the current state.
    /// Override this method to provide backend-specific statistics.
    /// </summary>
    /// <returns>Current memory statistics.</returns>
    protected abstract DotCompute.Core.Memory.IMemoryStatistics CreateMemoryStatistics();

    /// <summary>
    /// Performs additional async cleanup in derived classes.
    /// Override this method to dispose backend-specific resources asynchronously.
    /// </summary>
    protected virtual ValueTask DisposeCoreAsync()
    {
        DisposeCore();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Allocates a backend-specific buffer with the specified size and options.
    /// This method must be implemented by derived classes to provide backend-specific allocation logic.
    /// </summary>
    /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the allocated buffer.</returns>
    protected abstract ValueTask<TBuffer> AllocateBufferAsync(
        long sizeInBytes, 
        MemoryOptions options, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a view over the specified buffer.
    /// Override this method to provide backend-specific view creation logic.
    /// </summary>
    /// <param name="buffer">The parent buffer.</param>
    /// <param name="offset">The offset in bytes.</param>
    /// <param name="length">The length in bytes.</param>
    /// <returns>A view over the specified buffer region.</returns>
    protected abstract IMemoryBuffer CreateBufferView(TBuffer buffer, long offset, long length);

    /// <summary>
    /// Copies data from host memory to the device buffer.
    /// Override this method to provide backend-specific host-to-device copy logic.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="data">The source data span.</param>
    protected abstract void CopyToDeviceInternal<T>(TBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged;

    /// <summary>
    /// Copies data from the device buffer to host memory.
    /// Override this method to provide backend-specific device-to-host copy logic.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="data">The destination data span.</param>
    /// <param name="buffer">The source buffer.</param>
    protected abstract void CopyFromDeviceInternal<T>(Span<T> data, TBuffer buffer) where T : unmanaged;

    /// <summary>
    /// Frees the specified buffer and releases its resources.
    /// Override this method to provide backend-specific buffer cleanup logic.
    /// </summary>
    /// <param name="buffer">The buffer to free.</param>
    protected abstract void FreeBufferInternal(TBuffer buffer);

    /// <summary>
    /// Gets the current memory usage statistics.
    /// Override this method to provide backend-specific statistics.
    /// </summary>
    /// <returns>Current memory statistics.</returns>
    protected abstract MemoryStatistics GetMemoryStatistics();

    /// <summary>
    /// Handles memory pressure by performing cleanup operations.
    /// Override this method to provide backend-specific memory pressure handling.
    /// </summary>
    /// <param name="pressureLevel">The memory pressure level (0.0 to 1.0).</param>
    protected virtual void HandleMemoryPressure(double pressureLevel)
    {
        _logger?.LogInformation("Handling memory pressure at {Pressure:P1} in {Manager}", 
            pressureLevel, ManagerName);

        if (pressureLevel > 0.90)
        {
            // Aggressive cleanup for high memory pressure
            _logger?.LogWarning("High memory pressure detected: {Pressure:P1} in {Manager}", 
                pressureLevel, ManagerName);

            // Force garbage collection to release managed references
            GC.Collect(2, GCCollectionMode.Optimized, false);
        }
    }

    /// <summary>
    /// Updates the internal statistics when allocations change.
    /// Override this method to provide custom statistics tracking.
    /// </summary>
    /// <param name="deltaBytes">The change in allocated bytes (positive for allocation, negative for deallocation).</param>
    /// <param name="isAllocation">True if this is an allocation, false if it's a deallocation.</param>
    protected virtual void UpdateStatisticsInternal(long deltaBytes, bool isAllocation)
    {
        // Default implementation is empty - derived classes can override for custom statistics
    }

    #endregion

    #region Common Memory Management Functionality

    /// <summary>
    /// Updates allocation statistics and triggers memory pressure monitoring.
    /// </summary>
    /// <param name="deltaBytes">The change in allocated bytes.</param>
    /// <param name="isAllocation">True if this is an allocation, false if it's a deallocation.</param>
    private void UpdateAllocationStatistics(long deltaBytes, bool isAllocation)
    {
        if (isAllocation)
        {
            Interlocked.Add(ref _totalAllocatedBytes, deltaBytes);
            Interlocked.Increment(ref _allocationCount);

            // Update peak usage
            var currentUsage = Interlocked.Read(ref _totalAllocatedBytes);
            var currentPeak = Interlocked.Read(ref _peakUsageBytes);
            if (currentUsage > currentPeak)
            {
                Interlocked.Exchange(ref _peakUsageBytes, currentUsage);
            }
        }
        else
        {
            Interlocked.Add(ref _totalAllocatedBytes, -deltaBytes);
            Interlocked.Decrement(ref _allocationCount);
        }

        // Update custom statistics
        lock (_statisticsLock)
        {
            UpdateStatisticsInternal(isAllocation ? deltaBytes : -deltaBytes, isAllocation);
        }
    }

    /// <summary>
    /// Checks memory pressure and triggers cleanup if necessary.
    /// </summary>
    /// <param name="state">Timer state (not used).</param>
    private void CheckMemoryPressure(object? state)
    {
        if (_disposed) return;

        try
        {
            var stats = GetMemoryStatistics();
            if (stats.TotalMemoryBytes > 0)
            {
                var pressureLevel = (double)stats.UsedMemoryBytes / stats.TotalMemoryBytes;
                
                if (pressureLevel > MemoryPressureThreshold)
                {
                    HandleMemoryPressure(pressureLevel);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during memory pressure check in {Manager}", ManagerName);
        }
    }

    /// <summary>
    /// Gets a snapshot of currently tracked buffers for diagnostics.
    /// </summary>
    /// <returns>A collection of currently tracked buffer information.</returns>
    protected IEnumerable<(IMemoryBuffer Buffer, long SizeInBytes)> GetTrackedBuffers()
    {
        return _trackedBuffers.Keys.Select(buffer => (buffer, buffer.SizeInBytes));
    }

    /// <summary>
    /// Forces cleanup of all tracked buffers (used during disposal).
    /// </summary>
    protected void ForceCleanupAllBuffers()
    {
        _logger?.LogInformation("Force cleanup of {Count} tracked buffers in {Manager}", 
            _trackedBuffers.Count, ManagerName);

        foreach (var buffer in _trackedBuffers.Values)
        {
            try
            {
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing {Manager} buffer during cleanup", ManagerName);
            }
        }

        _trackedBuffers.Clear();
    }

    /// <summary>
    /// Forces cleanup of all tracked buffers asynchronously (used during disposal).
    /// </summary>
    protected async ValueTask ForceCleanupAllBuffersAsync()
    {
        _logger?.LogInformation("Force cleanup of {Count} tracked buffers in {Manager}", 
            _trackedBuffers.Count, ManagerName);

        var tasks = new List<ValueTask>();
        foreach (var buffer in _trackedBuffers.Values)
        {
            try
            {
                if (buffer is IAsyncDisposable asyncDisposable)
                {
                    tasks.Add(asyncDisposable.DisposeAsync());
                }
                else
                {
                    buffer.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing {Manager} buffer during cleanup", ManagerName);
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks.Select(t => t.AsTask())).ConfigureAwait(false);
        }

        _trackedBuffers.Clear();
    }

    /// <summary>
    /// Throws ObjectDisposedException if this manager has been disposed.
    /// </summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the memory manager and all tracked resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _logger?.LogInformation("Disposing {Manager} memory manager", ManagerName);

            // Stop memory pressure monitoring
            _memoryPressureTimer?.Dispose();

            // Dispose all tracked buffers
            ForceCleanupAllBuffers();

            // Allow derived classes to perform additional cleanup
            DisposeCore();

            _logger?.LogInformation("{Manager} memory manager disposed", ManagerName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during {Manager} memory manager disposal", ManagerName);
        }
        finally
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Performs additional cleanup in derived classes.
    /// Override this method to dispose backend-specific resources.
    /// </summary>
    protected virtual void DisposeCore()
    {
        // Default implementation is empty - derived classes can override for custom cleanup
    }

    #endregion
}

/// <summary>
/// Default statistics implementation for memory managers that don't need custom statistics.
/// </summary>
public class DefaultMemoryStatistics
{
    /// <summary>
    /// Gets or sets additional properties for backend-specific statistics.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Memory statistics returned by GetMemoryStatistics.
/// </summary>
public class MemoryStatistics
{
    /// <summary>
    /// Gets the total memory available in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; init; }

    /// <summary>
    /// Gets the currently used memory in bytes.
    /// </summary>
    public long UsedMemoryBytes { get; init; }

    /// <summary>
    /// Gets the available memory in bytes.
    /// </summary>
    public long AvailableMemoryBytes => TotalMemoryBytes - UsedMemoryBytes;

    /// <summary>
    /// Gets the number of active allocations.
    /// </summary>
    public int AllocationCount { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; init; }

    /// <summary>
    /// Gets the memory fragmentation percentage.
    /// </summary>
    public double FragmentationPercentage { get; init; }
}

}

#pragma warning restore CA1848
