// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Adapters;

/// <summary>
/// Metal adapter implementing the memory management port.
/// Bridges the hexagonal architecture port interface to the Metal backend implementation.
/// </summary>
/// <remarks>
/// This adapter provides a standardized memory management interface while
/// leveraging Metal's unified memory architecture on Apple Silicon.
/// </remarks>
public sealed class MetalMemoryManagementAdapter : IMemoryManagementPort, IDisposable
{
    private readonly ILogger<MetalMemoryManagementAdapter> _logger;
    private readonly MemoryCapabilities _capabilities;
    private readonly object _statsLock = new();
    private long _allocatedBytes;
    private long _peakAllocatedBytes;
    private int _allocationCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryManagementAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MetalMemoryManagementAdapter(ILogger<MetalMemoryManagementAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        if (sizeInBytes > _capabilities.MaxAllocationSize)
            throw new InvalidOperationException($"Requested allocation ({sizeInBytes} bytes) exceeds maximum supported size ({_capabilities.MaxAllocationSize} bytes)");

        var buffer = new MetalPortBuffer<T>(length, flags, OnBufferDisposed);

        lock (_statsLock)
        {
            _allocatedBytes += sizeInBytes;
            _allocationCount++;
            if (_allocatedBytes > _peakAllocatedBytes)
                _peakAllocatedBytes = _allocatedBytes;
        }

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
    public ValueTask CopyRangeAsync<T>(
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

        // Copy via host memory (Metal's unified memory makes this efficient on Apple Silicon)
        var sourceSpan = source.AsReadOnlySpan().Slice(sourceOffset, count);
        var destSpan = destination.AsSpan().Slice(destinationOffset, count);
        sourceSpan.CopyTo(destSpan);

        return ValueTask.CompletedTask;
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
                PoolHitRate = 0.0,
                PeakAllocatedBytes = _peakAllocatedBytes
            };
        }
    }

    private static MemoryCapabilities QueryCapabilities()
    {
        // Metal capabilities - Apple Silicon has unified memory
        return new MemoryCapabilities
        {
            SupportsUnifiedMemory = true, // Apple Silicon unified memory
            SupportsPooling = true,
            SupportsP2P = false, // Metal doesn't support P2P in the same way as CUDA
            MaxAllocationSize = 32L * 1024 * 1024 * 1024, // 32 GB (high-end Apple Silicon)
            TotalMemory = 16L * 1024 * 1024 * 1024, // 16 GB default
            AlignmentRequirement = 16 // Metal page alignment
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
/// Metal implementation of a port buffer.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class MetalPortBuffer<T> : IPortBuffer<T> where T : unmanaged
{
    private readonly T[] _data;
    private readonly Action<long> _onDisposed;
    private bool _disposed;

    public MetalPortBuffer(int length, BufferAllocationFlags flags, Action<long> onDisposed)
    {
        _onDisposed = onDisposed;
        Length = length;
        SizeInBytes = length * Marshal.SizeOf<T>();
        _data = new T[length];

        // Metal uses unified memory on Apple Silicon
        Location = flags.HasFlag(BufferAllocationFlags.Unified)
            ? BufferLocation.Unified
            : BufferLocation.Unified; // Always unified on Apple Silicon
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
        return _data.AsSpan();
    }

    /// <inheritdoc />
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        return _data.AsSpan();
    }

    /// <inheritdoc />
    public ValueTask CopyFromAsync(ReadOnlySpan<T> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (source.Length > Length)
            throw new ArgumentException("Source is larger than buffer", nameof(source));

        source.CopyTo(_data);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CopyToAsync(Span<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (destination.Length < Length)
            throw new ArgumentException("Destination is smaller than buffer", nameof(destination));

        _data.AsSpan().CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // Unified memory - no explicit sync needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Zeros the buffer contents.
    /// </summary>
    public ValueTask ZeroAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Array.Clear(_data);
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
