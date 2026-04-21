// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Device;
using DotCompute.Abstractions.Interfaces.Pipelines;
// MemoryLock<T> is generic and cannot be aliased without type arguments - use full name when needed
using MemoryHint = DotCompute.Abstractions.Pipelines.Enums.MemoryHint;
using MemoryLayoutHint = DotCompute.Abstractions.Interfaces.Pipelines.MemoryLayoutHint;
using MemoryLockMode = DotCompute.Abstractions.Interfaces.Pipelines.MemoryLockMode;
using MemoryManagerStats = DotCompute.Abstractions.Interfaces.Pipelines.MemoryManagerStats;
using MemoryPoolOptions = DotCompute.Abstractions.Interfaces.Pipelines.MemoryPoolOptions;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Default implementation of IPipelineMemoryManager.
    /// </summary>
    internal sealed class PipelineMemoryManager(IComputeDevice device) : IPipelineMemoryManager
    {
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
        private readonly IComputeDevice _device = device ?? throw new ArgumentNullException(
            nameof(device),
            "IComputeDevice is required for PipelineMemoryManager — pass a device obtained from IAccelerator.Device or IAcceleratorProvider.");
#pragma warning restore CA2213
        private readonly ConcurrentDictionary<string, object> _sharedMemories = new();
        private readonly ConcurrentDictionary<Type, MemoryPool> _pools = new();
        private readonly Lock _statsLock = new();

        private long _totalAllocatedBytes;
        private long _currentUsedBytes;
        private long _peakUsedBytes;
        private int _activeAllocationCount;
        private long _totalAllocationCount;
        private double _cacheHitRate;
        private bool _isDisposed;

        /// <inheritdoc/>
        public async ValueTask<IPipelineMemory<T>> AllocateAsync<T>(
            long elementCount,
            MemoryHint hint = MemoryHint.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ValidateAllocationParameters<T>(elementCount);

            var sizeInBytes = CalculateSizeInBytes<T>(elementCount);

            // Try to get from pool first with size-aware lookup
            var poolKey = CreatePoolKey<T>(sizeInBytes);
            if (_pools.TryGetValue(poolKey, out var pool))
            {
                var pooledMemory = pool.TryRent(sizeInBytes);
                if (pooledMemory != null)
                {
                    UpdateCacheHitRate(true);
                    return new PipelineMemory<T>(pooledMemory, elementCount, _device, true);
                }
            }

            UpdateCacheHitRate(false);

            // Determine optimal memory access pattern based on hints
            var memoryAccess = DetermineMemoryAccess(hint);

            IDeviceMemory deviceMemory;
            try
            {
                deviceMemory = await _device.AllocateMemoryAsync(sizeInBytes, memoryAccess, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Pipeline memory allocation failed: device '{_device.Name}' could not allocate {sizeInBytes:N0} bytes ({elementCount:N0} x {typeof(T).Name}, access={memoryAccess}). " +
                    $"Current usage: {_currentUsedBytes:N0}/{_totalAllocatedBytes:N0} bytes, {_activeAllocationCount} active allocations. " +
                    $"Likely causes: out-of-memory, fragmented pool, or oversized request. See inner exception for device-specific detail.", ex);
            }

            // Update statistics atomically
            UpdateAllocationStatistics(sizeInBytes);

            return new PipelineMemory<T>(deviceMemory, elementCount, _device, false);
        }

        /// <inheritdoc/>
        public async ValueTask<IPipelineMemory<T>> AllocateSharedAsync<T>(
            string key,
            long elementCount,
            MemoryHint hint = MemoryHint.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ValidateSharedAllocationParameters<T>(key, elementCount);

            // Thread-safe check for existing shared memory with type validation
            if (_sharedMemories.TryGetValue(key, out var existing))
            {
                if (existing is IPipelineMemory<T> existingTyped)
                {
                    // Validate that existing memory has compatible size
                    if (existingTyped.ElementCount != elementCount)
                    {
                        throw new InvalidOperationException(
                            $"Shared memory key '{key}' was previously allocated with {existingTyped.ElementCount:N0} elements of {typeof(T).Name}, but AllocateSharedAsync requested {elementCount:N0}. " +
                            $"Either use the same element count on every call for this key, use a different key for differently-sized buffers, or release the existing allocation before requesting a new size.");
                    }
                    return existingTyped;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Shared memory key '{key}' is already registered as {existing.GetType().Name}, but AllocateSharedAsync<{typeof(T).Name}> expects {typeof(IPipelineMemory<T>).Name}. " +
                        $"Shared-memory keys are unique per type — choose a distinct key for each element type, or release the existing allocation before rebinding the key.");
                }
            }

            // Allocate new shared memory with error handling
            IPipelineMemory<T> memory;
            try
            {
                memory = await AllocateAsync<T>(elementCount, hint, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Shared memory allocation for key '{key}' failed: could not allocate {elementCount:N0} elements of {typeof(T).Name} on device '{_device.Name}'. See inner exception for the underlying allocation failure.", ex);
            }

            // Atomic add operation with conflict detection
            if (!_sharedMemories.TryAdd(key, memory))
            {
                // Another thread allocated the same key, dispose our allocation and return theirs
                await memory.DisposeAsync();
                if (_sharedMemories.TryGetValue(key, out var concurrentAllocation) &&
                    concurrentAllocation is IPipelineMemory<T> concurrentTyped)
                {
                    return concurrentTyped;
                }
                throw new InvalidOperationException(
                    $"Concurrent allocation conflict for shared memory key '{key}': another thread registered a non-{typeof(T).Name} allocation under this key between our TryAdd attempt and the fallback lookup. Retry AllocateSharedAsync or serialize shared-memory creation for this key.");
            }

            return memory;
        }

        /// <inheritdoc/>
        public IPipelineMemory<T>? GetShared<T>(string key) where T : unmanaged
        {
            ThrowIfDisposed();

            if (_sharedMemories.TryGetValue(key, out var memory) && memory is IPipelineMemory<T> typedMemory)
            {
                return typedMemory;
            }

            return null;
        }

        /// <inheritdoc/>
        public async ValueTask TransferAsync<T>(
            IPipelineMemory<T> memory,
            string fromStage,
            string toStage,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();

            // For now, just ensure synchronization
            await memory.SynchronizeAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public IPipelineMemoryView<T> CreateView<T>(
            IPipelineMemory<T> memory,
            long offset = 0,
            long? length = null) where T : unmanaged
        {
            ThrowIfDisposed();

            var actualLength = length ?? (memory.ElementCount - offset);
            return new PipelineMemoryView<T>(memory, offset, actualLength);
        }

        /// <inheritdoc/>
        public async ValueTask<IPipelineMemory<T>> OptimizeLayoutAsync<T>(
            IPipelineMemory<T> memory,
            MemoryLayoutHint layoutHint,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            await Task.Yield();

            // For now, return the same memory as optimization is complex
            // Real implementation would reorganize memory layout
            return memory;
        }

        /// <inheritdoc/>
        public MemoryManagerStats GetStats()
        {
            ThrowIfDisposed();

            lock (_statsLock)
            {
                var poolEfficiency = CalculatePoolEfficiency();
                var fragmentation = CalculateFragmentation();

                return new MemoryManagerStats
                {
                    TotalAllocatedBytes = _totalAllocatedBytes,
                    CurrentUsedBytes = _currentUsedBytes,
                    PeakUsedBytes = _peakUsedBytes,
                    ActiveAllocationCount = _activeAllocationCount,
                    TotalAllocationCount = _totalAllocationCount,
                    CacheHitRate = _cacheHitRate,
                    PoolEfficiency = poolEfficiency,
                    FragmentationPercentage = fragmentation
                };
            }
        }

        /// <inheritdoc/>
        public async ValueTask CollectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.Yield();

            // Force GC collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Clean up pools
            foreach (var pool in _pools.Values)
            {
                pool.Trim();
            }
        }

        /// <inheritdoc/>
        public void RegisterPool<T>(MemoryPoolOptions options) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(options);

            if (options.BlockSize <= 0)
            {
                options.BlockSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>() * 1024; // Default to 1K elements
            }

            var poolKey = CreatePoolKey<T>(options.BlockSize);
            var pool = new MemoryPool(typeof(T), options);

            if (!_pools.TryAdd(poolKey, pool))
            {
                // Pool already exists for this type and size, dispose the new one
                pool.Dispose();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            // Dispose all shared memories
            foreach (var memory in _sharedMemories.Values)
            {
                if (memory is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (memory is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _sharedMemories.Clear();

            // Dispose all pools
            foreach (var pool in _pools.Values)
            {
                pool.Dispose();
            }

            _pools.Clear();
            _isDisposed = true;
        }

        private static void ValidateAllocationParameters<T>(long elementCount) where T : unmanaged
        {
            if (elementCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount,
                    $"Element count must be greater than zero (received {elementCount} for type {typeof(T).Name}). For zero-length buffers use a sentinel or skip the allocation entirely.");
            }

            var elementSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            if (elementCount > long.MaxValue / elementSize)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount,
                    $"Allocation would overflow Int64: {elementCount:N0} elements x {elementSize} bytes/element for type {typeof(T).Name} exceeds long.MaxValue ({long.MaxValue:N0}). Reduce element count or split into multiple buffers.");
            }
        }

        private static void ValidateSharedAllocationParameters<T>(string key, long elementCount) where T : unmanaged
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateAllocationParameters<T>(elementCount);
        }

        private static long CalculateSizeInBytes<T>(long elementCount) where T : unmanaged
        {
            var elementSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            return elementCount * elementSize;
        }

        private static Type CreatePoolKey<T>(long sizeHint) where T : unmanaged
            // For now, use type-based pooling. In future, consider size-based bins
            => typeof(T);

        private static MemoryAccess DetermineMemoryAccess(MemoryHint hint)
        {
            // Analyze hint flags to determine optimal memory access pattern
            if (hint.HasFlag(MemoryHint.ReadHeavy) && !hint.HasFlag(MemoryHint.WriteHeavy))
            {
                return MemoryAccess.ReadOnly;
            }

            if (hint.HasFlag(MemoryHint.WriteHeavy) && !hint.HasFlag(MemoryHint.ReadHeavy))
            {
                return MemoryAccess.WriteOnly;
            }

            // Default to ReadWrite for maximum flexibility
            return MemoryAccess.ReadWrite;
        }

        private void UpdateAllocationStatistics(long sizeInBytes)
        {
            lock (_statsLock)
            {
                _totalAllocatedBytes += sizeInBytes;
                _currentUsedBytes += sizeInBytes;
                _peakUsedBytes = Math.Max(_peakUsedBytes, _currentUsedBytes);
                _activeAllocationCount++;
                _totalAllocationCount++;
            }
        }

        private void UpdateCacheHitRate(bool hit)
        {
            lock (_statsLock)
            {
                // Simple exponential moving average
                var alpha = 0.1;
                _cacheHitRate = _cacheHitRate * (1 - alpha) + (hit ? 1.0 : 0.0) * alpha;
            }
        }

        private double CalculatePoolEfficiency()
        {
            if (_pools.IsEmpty)
            {
                return 0.0;
            }

            var totalEfficiency = _pools.Values.Sum(pool => pool.GetEfficiency());
            return totalEfficiency / _pools.Count;
        }

        private double CalculateFragmentation()
        {
            // Simplified fragmentation calculation
            if (_totalAllocatedBytes == 0)
            {
                return 0.0;
            }

            var wastedBytes = _totalAllocatedBytes - _currentUsedBytes;
            return (double)wastedBytes / _totalAllocatedBytes * 100.0;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PipelineMemoryManager));
    }

    /// <summary>
    /// Implementation of IPipelineMemory with production-grade memory management.
    /// </summary>
    internal sealed class PipelineMemory<T>(IDeviceMemory deviceMemory, long elementCount, IComputeDevice device, bool isFromPool) : IPipelineMemory<T> where T : unmanaged
    {
        private readonly IDeviceMemory _deviceMemory = deviceMemory;
        private readonly long _elementCount = elementCount;
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
        private readonly IComputeDevice _device = device;
#pragma warning restore CA2213
        private readonly bool _isFromPool = isFromPool;
        private readonly SemaphoreSlim _lockSemaphore = new(1, 1);
        private readonly Lock _disposeLock = new();
        private volatile bool _isLocked;
        private volatile bool _isDisposed;

        /// <inheritdoc/>
        public string Id { get; } = Guid.NewGuid().ToString();

        /// <inheritdoc/>
        public long ElementCount => _elementCount;

        /// <inheritdoc/>
        public long SizeInBytes => _deviceMemory.SizeInBytes;

        /// <inheritdoc/>
        public bool IsLocked => _isLocked;

        /// <inheritdoc/>
        public MemoryAccess AccessMode => _deviceMemory.AccessMode;

        /// <inheritdoc/>
        public IComputeDevice Device => _device;

        /// <inheritdoc/>
        public async ValueTask<MemoryLock<T>> LockAsync(
            MemoryLockMode mode,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                await _lockSemaphore.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Handle race condition where disposal occurs during lock acquisition
                ThrowIfDisposed();
                throw;
            }

            // Validate access mode compatibility
            ValidateLockModeCompatibility(mode);

            _isLocked = true;

            Action unlockAction = () =>
            {
                if (!_isDisposed)
                {
                    _isLocked = false;
                    try
                    {
                        _ = _lockSemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Semaphore was disposed, which is fine during cleanup
                    }
                }
            };

            return new MemoryLock<T>(this, mode, unlockAction);
        }

        /// <inheritdoc/>
        public async ValueTask CopyFromAsync(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateCopyFromParameters(source, offset);

            var byteOffset = offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            try
            {
                await _deviceMemory.WriteAsync(source, byteOffset, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Host-to-device copy failed: could not write {source.Length:N0} elements of {typeof(T).Name} to device memory at element offset {offset} (byte offset {byteOffset}). Buffer element count: {_elementCount:N0}. See inner exception for transport-level detail.", ex);
            }
        }

        /// <inheritdoc/>
        public async ValueTask CopyToAsync(
            Memory<T> destination,
            long offset = 0,
            int? count = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateCopyToParameters(destination, offset, count);

            var actualCount = count ?? Math.Min(destination.Length, (int)(_elementCount - offset));
            var actualDestination = destination[..actualCount];
            var byteOffset = offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            try
            {
                await _deviceMemory.ReadAsync(actualDestination, byteOffset, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Device-to-host copy failed: could not read {actualCount:N0} elements of {typeof(T).Name} from device memory at element offset {offset} (byte offset {byteOffset}). Buffer element count: {_elementCount:N0}, destination capacity: {destination.Length:N0}. See inner exception for transport-level detail.", ex);
            }
        }

        /// <inheritdoc/>
        public IPipelineMemoryView<T> Slice(long offset, long length)
        {
            ThrowIfDisposed();

            if (offset + length > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Slice range [{offset}, {offset + length}) exceeds buffer length {_elementCount}. Slice(offset, length) must satisfy offset + length <= buffer.ElementCount.");
            }

            return new PipelineMemoryView<T>(this, offset, length);
        }

        /// <inheritdoc/>
        public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _device.SynchronizeAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_isDisposed)
                {
                    return;
                }
                _isDisposed = true;
            }

            // Ensure no locks are held during disposal
            if (_isLocked)
            {
                try
                {
                    _ = await _lockSemaphore.WaitAsync(TimeSpan.FromMilliseconds(100));
                }
                catch (TimeoutException)
                {
                    // Log warning: memory being disposed while locked
                }
                finally
                {
                    _isLocked = false;
                }
            }

            _lockSemaphore?.Dispose();

            if (!_isFromPool)
            {
                try
                {
                    await _deviceMemory.DisposeAsync();
                }
                catch (Exception)
                {
                    // Swallow disposal exceptions to prevent cascading failures
                }
            }
        }

        private void ValidateLockModeCompatibility(MemoryLockMode mode)
        {
            if (mode == MemoryLockMode.ReadOnly && AccessMode == MemoryAccess.WriteOnly)
            {
                throw new InvalidOperationException(
                    $"Cannot acquire a {mode} lock: this pipeline memory was allocated with AccessMode=WriteOnly. Allocate with ReadOnly or ReadWrite access if you need to read from it.");
            }

            if ((mode == MemoryLockMode.ReadWrite || mode == MemoryLockMode.Exclusive) &&
                AccessMode == MemoryAccess.ReadOnly)
            {
                throw new InvalidOperationException(
                    $"Cannot acquire a {mode} lock: this pipeline memory was allocated with AccessMode=ReadOnly. Allocate with WriteOnly or ReadWrite access if you need to write to it.");
            }
        }

        private void ValidateCopyFromParameters(ReadOnlyMemory<T> source, long offset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset,
                    $"CopyFromAsync offset must be non-negative (got {offset}). Offset is in elements of {typeof(T).Name}.");
            }

            if (offset + source.Length > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(source),
                    $"Source copy range [{offset}, {offset + source.Length}) exceeds buffer length {_elementCount}. Reduce source length to at most {_elementCount - offset} elements, or increase buffer capacity at allocation time.");
            }

            if (AccessMode == MemoryAccess.ReadOnly)
            {
                throw new InvalidOperationException(
                    "Cannot write to this pipeline memory: AccessMode=ReadOnly. Allocate with MemoryAccess.WriteOnly or MemoryAccess.ReadWrite (via MemoryHint.WriteHeavy or ReadWrite) to perform host-to-device copies.");
            }
        }

        private void ValidateCopyToParameters(Memory<T> destination, long offset, int? count)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset,
                    $"CopyToAsync offset must be non-negative (got {offset}). Offset is in elements of {typeof(T).Name}.");
            }

            if (offset >= _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset,
                    $"CopyToAsync offset {offset} is past the end of the buffer ({_elementCount} elements of {typeof(T).Name}). Valid offsets are in the range [0, {_elementCount - 1}].");
            }

            if (count.HasValue && count.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count.Value,
                    $"CopyToAsync count must be non-negative when specified (got {count.Value}). Pass null to copy as many elements as fit in destination.");
            }

            if (AccessMode == MemoryAccess.WriteOnly)
            {
                throw new InvalidOperationException(
                    "Cannot read from this pipeline memory: AccessMode=WriteOnly. Allocate with MemoryAccess.ReadOnly or MemoryAccess.ReadWrite (via MemoryHint.ReadHeavy or ReadWrite) to perform device-to-host copies.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }
    }

    /// <summary>
    /// Implementation of IPipelineMemoryView.
    /// </summary>
    internal sealed class PipelineMemoryView<T> : IPipelineMemoryView<T> where T : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the PipelineMemoryView class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        public PipelineMemoryView(IPipelineMemory<T> parent, long offset, long length)
        {
            if (offset < 0 || length <= 0 || offset + length > parent.ElementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"PipelineMemoryView bounds [{offset}, {offset + length}) are invalid for a parent buffer of {parent.ElementCount} elements. Requires: offset >= 0, length > 0, offset + length <= parent.ElementCount.");
            }

            Parent = parent;
            Offset = offset;
            Length = length;
        }

        /// <inheritdoc/>
        public long Offset { get; }

        /// <inheritdoc/>
        public long Length { get; }

        /// <inheritdoc/>
        public IPipelineMemory<T> Parent { get; }

        /// <inheritdoc/>
        public IPipelineMemoryView<T> Slice(long offset, long length)
        {
            if (offset < 0 || length <= 0 || offset + length > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"Sub-slice bounds [{offset}, {offset + length}) are invalid for a view of length {Length}. Requires: offset >= 0, length > 0, offset + length <= view.Length.");
            }

            return new PipelineMemoryView<T>(Parent, Offset + offset, length);
        }
    }

    /// <summary>
    /// Production-grade memory pool implementation with size tracking and lifecycle management.
    /// </summary>
    internal sealed class MemoryPool : IDisposable
    {
        private readonly Type _elementType;
        private readonly MemoryPoolOptions _options;
        private readonly ConcurrentQueue<PooledMemoryEntry> _pool;
        private readonly Lock _statsLock = new();
        private readonly Timer? _cleanupTimer;
        private long _totalRented;
        private long _totalReturned;
        private long _totalBytesPooled;
        private volatile bool _isDisposed;

        private sealed class PooledMemoryEntry
        {
            /// <summary>
            /// Gets or sets the memory.
            /// </summary>
            /// <value>The memory.</value>
            public required IDeviceMemory Memory { get; init; }
            /// <summary>
            /// Gets or sets the size in bytes.
            /// </summary>
            /// <value>The size in bytes.</value>
            public required long SizeInBytes { get; init; }
            /// <summary>
            /// Gets or sets the created at.
            /// </summary>
            /// <value>The created at.</value>
            public required DateTime CreatedAt { get; init; }
            /// <summary>
            /// Gets or sets the last used.
            /// </summary>
            /// <value>The last used.</value>
            public DateTime LastUsed { get; set; }
        }
        /// <summary>
        /// Initializes a new instance of the MemoryPool class.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="options">The options.</param>

        public MemoryPool(Type elementType, MemoryPoolOptions options)
        {
            ArgumentNullException.ThrowIfNull(elementType);
            ArgumentNullException.ThrowIfNull(options);

            _elementType = elementType;
            _options = options;
            _pool = new ConcurrentQueue<PooledMemoryEntry>();

            // Setup cleanup timer for retention policies
            if (options.RetentionPolicy == PoolRetentionPolicy.TimeBasedRelease)
            {
                _cleanupTimer = new Timer(PerformScheduledCleanup, null,
                    TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            }
        }
        /// <summary>
        /// Attempts to rent.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>

        public IDeviceMemory? TryRent(long sizeInBytes)
        {
            if (_isDisposed)
            {
                return null;
            }

            // Try to find memory with compatible size (within 25% tolerance)
            var maxAttempts = Math.Min(_pool.Count, 10); // Limit search to prevent blocking
            for (var i = 0; i < maxAttempts; i++)
            {
                if (_pool.TryDequeue(out var entry))
                {
                    var sizeDiff = Math.Abs(entry.SizeInBytes - sizeInBytes);
                    var tolerance = sizeInBytes * 0.25; // 25% size tolerance

                    if (sizeDiff <= tolerance)
                    {
                        // Good match, update statistics and return
                        lock (_statsLock)
                        {
                            _totalRented++;
                            _totalBytesPooled -= entry.SizeInBytes;
                        }
                        entry.LastUsed = DateTime.UtcNow;
                        return entry.Memory;
                    }
                    else
                    {
                        // Size mismatch, put it back and continue searching
                        _pool.Enqueue(entry);
                    }
                }
                else
                {
                    break; // Pool is empty
                }
            }

            return null;
        }
        /// <summary>
        /// Performs return.
        /// </summary>
        /// <param name="memory">The memory.</param>

        public void Return(IDeviceMemory memory)
        {
            if (_isDisposed || memory == null)
            {
                // Fire and forget disposal - acceptable for cleanup
                _ = memory?.DisposeAsync().AsTask();
                return;
            }

            if (_pool.Count >= _options.MaxSize)
            {
                // Fire and forget disposal - acceptable for pool overflow
                _ = memory.DisposeAsync().AsTask();
                return;
            }

            var entry = new PooledMemoryEntry
            {
                Memory = memory,
                SizeInBytes = memory.SizeInBytes,
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            _pool.Enqueue(entry);

            lock (_statsLock)
            {
                _totalReturned++;
                _totalBytesPooled += memory.SizeInBytes;
            }
        }
        /// <summary>
        /// Performs trim.
        /// </summary>

        public void Trim()
        {
            if (_isDisposed)
            {
                return;
            }

            var targetSize = _options.InitialSize;
            var entriesToRemove = new List<PooledMemoryEntry>();

            // Collect entries to remove based on retention policy
            while (_pool.Count > targetSize && _pool.TryDequeue(out var entry))
            {
                if (ShouldRetainEntry(entry))
                {
                    _pool.Enqueue(entry); // Put back if should retain
                    break; // Stop trimming to avoid infinite loop
                }
                else
                {
                    entriesToRemove.Add(entry);
                }
            }

            // Dispose removed entries
            foreach (var entry in entriesToRemove)
            {
                // Fire and forget disposal - acceptable for cleanup
                _ = entry.Memory.DisposeAsync().AsTask();
                lock (_statsLock)
                {
                    _totalBytesPooled -= entry.SizeInBytes;
                }
            }
        }
        /// <summary>
        /// Gets the efficiency.
        /// </summary>
        /// <returns>The efficiency.</returns>

        public double GetEfficiency()
        {
            lock (_statsLock)
            {
                if (_totalRented == 0)
                {
                    return 1.0;
                }

                return (double)_totalReturned / _totalRented;
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _cleanupTimer?.Dispose();

            while (_pool.TryDequeue(out var entry))
            {
                // Fire and forget disposal - acceptable during pool disposal
                _ = entry.Memory.DisposeAsync().AsTask();
            }

            lock (_statsLock)
            {
                _totalBytesPooled = 0;
            }
        }

        private bool ShouldRetainEntry(PooledMemoryEntry entry)
        {
            return _options.RetentionPolicy switch
            {
                PoolRetentionPolicy.KeepAll => true,
                PoolRetentionPolicy.TimeBasedRelease =>
                    DateTime.UtcNow - entry.LastUsed < TimeSpan.FromMinutes(30),
                PoolRetentionPolicy.LeastRecentlyUsed =>
                    DateTime.UtcNow - entry.LastUsed < TimeSpan.FromMinutes(15),
                PoolRetentionPolicy.Adaptive =>
                    GetEfficiency() > 0.5 || DateTime.UtcNow - entry.LastUsed < TimeSpan.FromMinutes(10),
                _ => true
            };
        }

        private void PerformScheduledCleanup(object? state)
        {
            if (!_isDisposed)
            {
                Trim();
            }
        }
    }
}
