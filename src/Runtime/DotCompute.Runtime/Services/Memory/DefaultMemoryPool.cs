// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Default implementation of memory pool
/// </summary>
internal class DefaultMemoryPool : IMemoryPool
{
    private readonly string _acceleratorId;
    private readonly long _maxSize;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private long _totalSize;
    private long _usedSize;
    private long _allocationCount;
    private long _deallocationCount;
    private long _totalBytesAllocated;
    private long _totalBytesDeallocated;
    private long _peakUsage;
    private int _defragmentationCount;
    private bool _disposed;

    public DefaultMemoryPool(string acceleratorId, long initialSize, long maxSize, ILogger logger)
    {
        _acceleratorId = acceleratorId;
        _totalSize = initialSize;
        _maxSize = maxSize;
        _logger = logger;
    }

    public string AcceleratorId => _acceleratorId;
    public long TotalSize => _totalSize;
    public long AvailableSize => _totalSize - _usedSize;
    public long UsedSize => _usedSize;

    public async Task<IMemoryBuffer> AllocateAsync(long sizeInBytes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultMemoryPool));
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));
        }

        lock (_lock)
        {
            // Check if we need to grow the pool
            if (_usedSize + sizeInBytes > _totalSize)
            {
                var newSize = Math.Min(_maxSize, (long)(_totalSize * 1.5));
                if (newSize > _totalSize && _usedSize + sizeInBytes <= newSize)
                {
                    _totalSize = newSize;
                    _logger.LogDebug("Expanded memory pool for {AcceleratorId} to {NewSizeMB}MB",
                        _acceleratorId, _totalSize / 1024 / 1024);
                }
                else if (_usedSize + sizeInBytes > _totalSize)
                {
                    throw new OutOfMemoryException(
                        $"Cannot allocate {sizeInBytes} bytes. Pool size: {_totalSize}, Used: {_usedSize}");
                }
            }

            _usedSize += sizeInBytes;
            _allocationCount++;
            _totalBytesAllocated += sizeInBytes;
            _peakUsage = Math.Max(_peakUsage, _usedSize);
        }

        // Create a mock memory buffer for this example
        var buffer = new PooledMemoryBuffer(this, sizeInBytes);

        await Task.CompletedTask; // Placeholder for async allocation
        return buffer;
    }

    public Task ReturnAsync(IMemoryBuffer buffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultMemoryPool));
        }

        if (buffer is PooledMemoryBuffer pooledBuffer && pooledBuffer.Pool == this)
        {
            lock (_lock)
            {
                _usedSize -= buffer.SizeInBytes;
                _deallocationCount++;
                _totalBytesDeallocated += buffer.SizeInBytes;
            }
        }

        return Task.CompletedTask;
    }

    public Task DefragmentAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultMemoryPool));
        }

        lock (_lock)
        {
            _defragmentationCount++;
            _logger.LogTrace("Defragmented memory pool for accelerator {AcceleratorId}", _acceleratorId);
        }

        return Task.CompletedTask;
    }

    public MemoryPoolStatistics GetStatistics()
    {
        lock (_lock)
        {
            var avgAllocationSize = _allocationCount > 0
                ? (double)_totalBytesAllocated / _allocationCount
                : 0.0;

            return new MemoryPoolStatistics
            {
                AllocationCount = _allocationCount,
                DeallocationCount = _deallocationCount,
                TotalBytesAllocated = _totalBytesAllocated,
                TotalBytesDeallocated = _totalBytesDeallocated,
                PeakMemoryUsage = _peakUsage,
                AverageAllocationSize = avgAllocationSize,
                DefragmentationCount = _defragmentationCount
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("Disposing memory pool for accelerator {AcceleratorId}", _acceleratorId);
            _disposed = true;
        }
    }
}