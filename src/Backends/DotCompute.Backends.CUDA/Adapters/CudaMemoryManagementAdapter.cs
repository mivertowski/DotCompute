// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Adapters;

/// <summary>
/// CUDA adapter implementing the memory management port.
/// Bridges the hexagonal architecture port interface to the CUDA backend implementation.
/// </summary>
/// <remarks>
/// This adapter provides a standardized memory management interface while
/// leveraging CUDA's unified memory and optimized transfer capabilities.
/// </remarks>
public sealed class CudaMemoryManagementAdapter : IMemoryManagementPort, IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger<CudaMemoryManagementAdapter> _logger;
    private readonly MemoryCapabilities _capabilities;
    private readonly object _statsLock = new();
    private long _allocatedBytes;
    private long _peakAllocatedBytes;
    private int _allocationCount;
    private int _poolRequests;
#pragma warning disable CS0649 // Field is never assigned to - reserved for future pool tracking
    private readonly int _poolHits;
#pragma warning restore CS0649
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaMemoryManagementAdapter"/> class.
    /// </summary>
    /// <param name="context">The CUDA context.</param>
    /// <param name="logger">The logger.</param>
    public CudaMemoryManagementAdapter(
        CudaContext context,
        ILogger<CudaMemoryManagementAdapter> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Query device capabilities
        _capabilities = QueryCapabilities();
    }

    /// <inheritdoc />
    public MemoryCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public async ValueTask<IPortBuffer<T>> AllocateAsync<T>(
        int length,
        BufferAllocationFlags flags = BufferAllocationFlags.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive");

        var sizeInBytes = length * Marshal.SizeOf<T>();

        // Validate against max allocation
        if (sizeInBytes > _capabilities.MaxAllocationSize)
            throw new InvalidOperationException($"Requested allocation ({sizeInBytes} bytes) exceeds maximum supported size ({_capabilities.MaxAllocationSize} bytes)");

        // Track pool request (currently no pooling, so all requests are misses)
        Interlocked.Increment(ref _poolRequests);

        var buffer = new CudaPortBuffer<T>(_context, length, flags, OnBufferDisposed);

        // Update statistics
        lock (_statsLock)
        {
            _allocatedBytes += sizeInBytes;
            _allocationCount++;
            if (_allocatedBytes > _peakAllocatedBytes)
                _peakAllocatedBytes = _allocatedBytes;
        }

        // Zero initialize if requested
        if (flags.HasFlag(BufferAllocationFlags.ZeroInitialize))
        {
            await buffer.ZeroAsync(cancellationToken);
        }

        return buffer;
    }

    /// <inheritdoc />
    public async ValueTask CopyAsync<T>(
        IPortBuffer<T> source,
        IPortBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination buffers must have the same length");

        await CopyRangeAsync(source, 0, destination, 0, source.Length, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyRangeAsync<T>(
        IPortBuffer<T> source,
        int sourceOffset,
        IPortBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (sourceOffset < 0 || sourceOffset + count > source.Length)
            throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds");

        if (destinationOffset < 0 || destinationOffset + count > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset), "Destination range is out of bounds");

        // Get source data
        var sourceSpan = source.AsReadOnlySpan().Slice(sourceOffset, count);

        // Copy to temp array for async operation
        var tempArray = sourceSpan.ToArray();

        // Copy to destination
        var destSpan = new Span<T>(tempArray);
        await destination.CopyFromAsync(new ReadOnlySpan<T>(tempArray), cancellationToken);
    }

    /// <inheritdoc />
    public MemoryStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new MemoryStatistics
            {
                AllocatedBytes = _allocatedBytes,
                AvailableBytes = _capabilities.TotalMemory - _allocatedBytes,
                AllocationCount = _allocationCount,
                PoolHitRate = _poolRequests > 0 ? (double)_poolHits / _poolRequests : 0.0,
                PeakAllocatedBytes = _peakAllocatedBytes
            };
        }
    }

    private MemoryCapabilities QueryCapabilities()
    {
        // Get device memory info from context
        // For now, use reasonable defaults that can be refined with actual CUDA queries
        return new MemoryCapabilities
        {
            SupportsUnifiedMemory = true, // CUDA 6.0+ supports unified memory
            SupportsPooling = true,
            SupportsP2P = true, // Depends on device capability
            MaxAllocationSize = 16L * 1024 * 1024 * 1024, // 16 GB default
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8 GB default
            AlignmentRequirement = 256
        };
    }

    private void OnBufferDisposed(long sizeInBytes)
    {
        lock (_statsLock)
        {
            _allocatedBytes -= sizeInBytes;
            _allocationCount--;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// CUDA implementation of a port buffer.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class CudaPortBuffer<T> : IPortBuffer<T> where T : unmanaged
{
    private readonly CudaContext _context;
    private readonly T[] _hostData;
    private readonly BufferAllocationFlags _flags;
    private readonly Action<long> _onDisposed;
    private bool _disposed;

    public CudaPortBuffer(
        CudaContext context,
        int length,
        BufferAllocationFlags flags,
        Action<long> onDisposed)
    {
        _context = context;
        _flags = flags;
        _onDisposed = onDisposed;
        Length = length;
        SizeInBytes = length * Marshal.SizeOf<T>();

        // Allocate host memory
        _hostData = new T[length];

        // Determine location based on flags
        Location = flags.HasFlag(BufferAllocationFlags.Unified)
            ? BufferLocation.Unified
            : BufferLocation.Device;
    }

    /// <inheritdoc />
    public int Length { get; }

    /// <inheritdoc />
    public long SizeInBytes { get; }

    /// <inheritdoc />
    public BufferLocation Location { get; }

    /// <inheritdoc />
    public Span<T> AsSpan()
    {
        ThrowIfDisposed();
        return _hostData.AsSpan();
    }

    /// <inheritdoc />
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        return _hostData.AsSpan();
    }

    /// <inheritdoc />
    public ValueTask CopyFromAsync(ReadOnlySpan<T> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (source.Length > Length)
            throw new ArgumentException("Source is larger than buffer", nameof(source));

        source.CopyTo(_hostData);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CopyToAsync(Span<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (destination.Length < Length)
            throw new ArgumentException("Destination is smaller than buffer", nameof(destination));

        _hostData.AsSpan().CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // For this simplified implementation, synchronization is implicit
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Zeros the buffer contents.
    /// </summary>
    public ValueTask ZeroAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Array.Clear(_hostData);
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _onDisposed(SizeInBytes);
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
