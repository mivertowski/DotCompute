// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Interfaces.Device;
using DotCompute.Abstractions.Interfaces.Pipelines;
using MemoryLockMode = DotCompute.Abstractions.Interfaces.Pipelines.MemoryLockMode;
using MemoryLayoutHint = DotCompute.Abstractions.Interfaces.Pipelines.MemoryLayoutHint;
using MemoryManagerStats = DotCompute.Abstractions.Interfaces.Pipelines.MemoryManagerStats;
using MemoryPoolOptions = DotCompute.Abstractions.Interfaces.Pipelines.MemoryPoolOptions;
// MemoryLock<T> is generic and cannot be aliased without type arguments - use full name when needed
using MemoryHint = DotCompute.Abstractions.Pipelines.Enums.MemoryHint;
using DotCompute.Abstractions;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Default implementation of IPipelineMemoryManager.
    /// </summary>
    internal sealed class PipelineMemoryManager(IUnifiedMemoryManager memoryManager, IComputeDevice device) : IPipelineMemoryManager
    {
        private readonly IComputeDevice _device = device ?? throw new ArgumentNullException(nameof(device));
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
                    $"Failed to allocate {sizeInBytes} bytes for {elementCount} elements of type {typeof(T).Name}", ex);
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
                            $"Shared memory '{key}' exists but has different element count. " +
                            $"Expected: {elementCount}, Actual: {existingTyped.ElementCount}");
                    }
                    return existingTyped;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Shared memory '{key}' exists but has incompatible type. " +
                        $"Expected: {typeof(IPipelineMemory<T>).Name}, Actual: {existing.GetType().Name}");
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
                    $"Failed to allocate shared memory '{key}' for {elementCount} elements of type {typeof(T).Name}", ex);
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
                throw new InvalidOperationException($"Concurrent allocation conflict for shared memory key '{key}'");
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
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "Element count must be positive");
            }

            var elementSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            if (elementCount > long.MaxValue / elementSize)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount,
                    $"Element count would cause overflow for type {typeof(T).Name} with size {elementSize} bytes");
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
        private readonly IComputeDevice _device = device;
        private readonly bool _isFromPool = isFromPool;
        private readonly SemaphoreSlim _lockSemaphore = new(1, 1);
        private readonly object _disposeLock = new();
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
                    $"Failed to copy {source.Length} elements to device memory at offset {offset}", ex);
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
                    $"Failed to copy {actualCount} elements from device memory at offset {offset}", ex);
            }
        }

        /// <inheritdoc/>
        public IPipelineMemoryView<T> Slice(long offset, long length)
        {
            ThrowIfDisposed();

            if (offset + length > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Slice exceeds memory bounds");
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
                    "Cannot acquire read lock on write-only memory");
            }

            if ((mode == MemoryLockMode.ReadWrite || mode == MemoryLockMode.Exclusive) &&
                AccessMode == MemoryAccess.ReadOnly)
            {
                throw new InvalidOperationException(
                    "Cannot acquire write lock on read-only memory");
            }
        }

        private void ValidateCopyFromParameters(ReadOnlyMemory<T> source, long offset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative");
            }

            if (offset + source.Length > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(source),
                    $"Source data ({source.Length} elements) with offset {offset} exceeds memory bounds ({_elementCount} elements)");
            }

            if (AccessMode == MemoryAccess.ReadOnly)
            {
                throw new InvalidOperationException("Cannot write to read-only memory");
            }
        }

        private void ValidateCopyToParameters(Memory<T> destination, long offset, int? count)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative");
            }

            if (offset >= _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset,
                    $"Offset exceeds memory bounds ({_elementCount} elements)");
            }

            if (count.HasValue && count.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count.Value, "Count cannot be negative");
            }

            if (AccessMode == MemoryAccess.WriteOnly)
            {
                throw new InvalidOperationException("Cannot read from write-only memory");
            }
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PipelineMemory<>));
    }

    /// <summary>
    /// Implementation of IPipelineMemoryView.
    /// </summary>
    internal sealed class PipelineMemoryView<T> : IPipelineMemoryView<T> where T : unmanaged
    {
        public PipelineMemoryView(IPipelineMemory<T> parent, long offset, long length)
        {
            if (offset < 0 || length <= 0 || offset + length > parent.ElementCount)
            {
                throw new ArgumentOutOfRangeException("Invalid view bounds");
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
                throw new ArgumentOutOfRangeException("Invalid sub-view bounds");
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
            public required IDeviceMemory Memory { get; init; }
            public required long SizeInBytes { get; init; }
            public required DateTime CreatedAt { get; init; }
            public DateTime LastUsed { get; set; }
        }

        public MemoryPool(Type elementType, MemoryPoolOptions options)
        {
            _elementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pool = new ConcurrentQueue<PooledMemoryEntry>();

            // Setup cleanup timer for retention policies
            if (options.RetentionPolicy == PoolRetentionPolicy.TimeBasedRelease)
            {
                _cleanupTimer = new Timer(PerformScheduledCleanup, null,
                    TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            }
        }

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

        public void Return(IDeviceMemory memory)
        {
            if (_isDisposed || memory == null)
            {
                _ = memory?.DisposeAsync();
                return;
            }

            if (_pool.Count >= _options.MaxSize)
            {
                _ = memory.DisposeAsync();
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
                _ = entry.Memory.DisposeAsync();
                lock (_statsLock)
                {
                    _totalBytesPooled -= entry.SizeInBytes;
                }
            }
        }

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
                _ = entry.Memory.DisposeAsync();
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
