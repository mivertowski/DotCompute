// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Core.Memory;
using DotCompute.Core.Device.Interfaces;
using DotCompute.Abstractions;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Default implementation of IPipelineMemoryManager.
    /// </summary>
    internal sealed class PipelineMemoryManager : IPipelineMemoryManager
    {
        private readonly IMemoryManager _memoryManager;
        private readonly IComputeDevice _device;
        private readonly ConcurrentDictionary<string, object> _sharedMemories;
        private readonly ConcurrentDictionary<Type, MemoryPool> _pools;
        private readonly Lock _statsLock = new();

        private long _totalAllocatedBytes;
        private long _currentUsedBytes;
        private long _peakUsedBytes;
        private int _activeAllocationCount;
        private long _totalAllocationCount;
        private double _cacheHitRate;
        private bool _isDisposed;

        public PipelineMemoryManager(IMemoryManager memoryManager, IComputeDevice device)
        {
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _sharedMemories = new();
            _pools = new();
        }

        /// <inheritdoc/>
        public async ValueTask<IPipelineMemory<T>> AllocateAsync<T>(
            long elementCount,
            MemoryHint hint = MemoryHint.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();

            var sizeInBytes = elementCount * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            // Try to get from pool first
            if (_pools.TryGetValue(typeof(T), out var pool))
            {
                var pooledMemory = pool.TryRent(sizeInBytes);
                if (pooledMemory != null)
                {
                    UpdateCacheHitRate(true);
                    return new PipelineMemory<T>(pooledMemory, elementCount, _device, true);
                }
            }

            UpdateCacheHitRate(false);

            // Allocate new memory
            var memoryAccess = hint switch
            {
                MemoryHint.Sequential => MemoryAccess.ReadWrite,
                MemoryHint.Random => MemoryAccess.ReadWrite,
                MemoryHint.Temporary => MemoryAccess.ReadWrite,
                MemoryHint.Persistent => MemoryAccess.ReadWrite,
                MemoryHint.Pinned => MemoryAccess.ReadWrite,
                _ => MemoryAccess.ReadWrite
            };

            var deviceMemory = await _device.AllocateMemoryAsync(sizeInBytes, memoryAccess, cancellationToken);

            // Update statistics
            lock (_statsLock)
            {
                _totalAllocatedBytes += sizeInBytes;
                _currentUsedBytes += sizeInBytes;
                _peakUsedBytes = Math.Max(_peakUsedBytes, _currentUsedBytes);
                _activeAllocationCount++;
                _totalAllocationCount++;
            }

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

            // Check if shared memory already exists
            if (_sharedMemories.TryGetValue(key, out var existing))
            {
                if (existing is IPipelineMemory<T> existingTyped)
                {
                    return existingTyped;
                }
            }

            // Allocate new shared memory
            var memory = await AllocateAsync<T>(elementCount, hint, cancellationToken);
            _ = _sharedMemories.TryAdd(key, memory);

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

            var pool = new MemoryPool(typeof(T), options);
            _ = _pools.TryAdd(typeof(T), pool);
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
    /// Implementation of IPipelineMemory.
    /// </summary>
    internal sealed class PipelineMemory<T> : IPipelineMemory<T> where T : unmanaged
    {
        private readonly IDeviceMemory _deviceMemory;
        private readonly long _elementCount;
        private readonly IComputeDevice _device;
        private readonly bool _isFromPool;
        private readonly SemaphoreSlim _lockSemaphore;
        private volatile bool _isLocked;
        private bool _isDisposed;

        public PipelineMemory(IDeviceMemory deviceMemory, long elementCount, IComputeDevice device, bool isFromPool)
        {
            _deviceMemory = deviceMemory;
            _elementCount = elementCount;
            _device = device;
            _isFromPool = isFromPool;
            _lockSemaphore = new SemaphoreSlim(1, 1);
        }

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

            await _lockSemaphore.WaitAsync(cancellationToken);

            _isLocked = true;

            Action unlockAction = () =>
            {
                _isLocked = false;
                _ = _lockSemaphore.Release();
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

            var byteOffset = offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            await _deviceMemory.WriteAsync(source, byteOffset, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask CopyToAsync(
            Memory<T> destination,
            long offset = 0,
            int? count = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var actualCount = count ?? Math.Min(destination.Length, (int)(_elementCount - offset));
            var actualDestination = destination[..actualCount];
            var byteOffset = offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            await _deviceMemory.ReadAsync(actualDestination, byteOffset, cancellationToken);
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

            _lockSemaphore?.Dispose();

            if (!_isFromPool)
            {
                await _deviceMemory.DisposeAsync();
            }

            _isDisposed = true;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PipelineMemory<T>));
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
    /// Simple memory pool implementation.
    /// </summary>
    internal sealed class MemoryPool : IDisposable
    {
        private readonly Type _elementType;
        private readonly MemoryPoolOptions _options;
        private readonly ConcurrentQueue<IDeviceMemory> _pool;
        private readonly Lock _statsLock = new();
        private long _totalRented;
        private long _totalReturned;
        private bool _isDisposed;

        public MemoryPool(Type elementType, MemoryPoolOptions options)
        {
            _elementType = elementType;
            _options = options;
            _pool = new();
        }

        public IDeviceMemory? TryRent(long sizeInBytes)
        {
            if (_isDisposed)
            {
                return null;
            }

            if (_pool.TryDequeue(out var memory))
            {
                lock (_statsLock)
                {
                    _totalRented++;
                }
                return memory;
            }

            return null;
        }

        public void Return(IDeviceMemory memory)
        {
            if (_isDisposed || _pool.Count >= _options.MaxSize)
            {
                _ = (memory?.DisposeAsync());
                return;
            }

            _pool.Enqueue(memory);

            lock (_statsLock)
            {
                _totalReturned++;
            }
        }

        public void Trim()
        {
            var targetSize = _options.InitialSize;
            while (_pool.Count > targetSize && _pool.TryDequeue(out var memory))
            {
                _ = (memory?.DisposeAsync());
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

            while (_pool.TryDequeue(out var memory))
            {
                _ = (memory?.DisposeAsync());
            }

            _isDisposed = true;
        }
    }
}
