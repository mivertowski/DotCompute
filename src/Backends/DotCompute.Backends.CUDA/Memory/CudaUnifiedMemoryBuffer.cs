// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA unified memory buffer implementation with automatic host/device migration.
/// Optimized for RTX 2000 series (8GB VRAM) with zero-copy operations where possible.
/// </summary>
public sealed class CudaUnifiedMemoryBuffer : IMemoryBuffer
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly bool _isUnifiedMemory;
    private IntPtr _devicePointer;
    private IntPtr _hostPointer; // For pinned memory mappings
    private bool _disposed;

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;
    public IntPtr DevicePointer => _devicePointer;
    public bool IsUnifiedMemory => _isUnifiedMemory;

    // Performance tracking
    private long _hostToDeviceTransfers;
    private long _deviceToHostTransfers;
    private long _totalBytesTransferred;

    public CudaUnifiedMemoryBuffer(
        CudaContext context, 
        IntPtr devicePointer, 
        long sizeInBytes, 
        MemoryOptions options,
        bool isUnifiedMemory,
        ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (devicePointer == IntPtr.Zero)
            throw new ArgumentException("Device pointer cannot be null", nameof(devicePointer));
        
        if (sizeInBytes <= 0)
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));

        _devicePointer = devicePointer;
        SizeInBytes = sizeInBytes;
        Options = options;
        _isUnifiedMemory = isUnifiedMemory;

        // Initialize memory to zero for safety
        InitializeMemory();
        
        _logger.LogDebug("Created CUDA {MemoryType} buffer of {Size}MB at {Pointer:X}",
            isUnifiedMemory ? "unified" : "device", 
            sizeInBytes / (1024 * 1024), 
            devicePointer.ToInt64());
    }

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source, 
        long offset = 0, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
            throw new ArgumentException("Copy would exceed buffer bounds");

        using var handle = source.Pin();
        IntPtr srcPtr;
        IntPtr dstPtr;
        
        unsafe
        {
            srcPtr = new IntPtr(handle.Pointer);
            dstPtr = new IntPtr(_devicePointer.ToInt64() + offset);
        }

        if (_isUnifiedMemory)
        {
            // For unified memory, we can copy directly with potential prefetching
            await CopyFromHostUnifiedAsync(srcPtr, dstPtr, bytesToCopy, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // For device memory, use standard async copy
            await CopyFromHostDeviceAsync(srcPtr, dstPtr, bytesToCopy, cancellationToken)
                .ConfigureAwait(false);
        }

        Interlocked.Increment(ref _hostToDeviceTransfers);
        Interlocked.Add(ref _totalBytesTransferred, bytesToCopy);
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination, 
        long offset = 0, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
            throw new ArgumentException("Copy would exceed buffer bounds");

        using var handle = destination.Pin();
        IntPtr srcPtr;
        IntPtr dstPtr;
        
        unsafe
        {
            srcPtr = new IntPtr(_devicePointer.ToInt64() + offset);
            dstPtr = new IntPtr(handle.Pointer);
        }

        if (_isUnifiedMemory)
        {
            // For unified memory, potentially zero-copy access
            await CopyToHostUnifiedAsync(srcPtr, dstPtr, bytesToCopy, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // For device memory, use standard async copy
            await CopyToHostDeviceAsync(srcPtr, dstPtr, bytesToCopy, cancellationToken)
                .ConfigureAwait(false);
        }

        Interlocked.Increment(ref _deviceToHostTransfers);
        Interlocked.Add(ref _totalBytesTransferred, bytesToCopy);
    }

    /// <summary>
    /// Creates a view over a portion of this buffer
    /// </summary>
    public CudaUnifiedMemoryBuffer CreateView(long offset, long length)
    {
        ThrowIfDisposed();
        
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        
        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        
        if (offset + length > SizeInBytes)
            throw new ArgumentException("View would exceed buffer bounds");

        var viewPointer = new IntPtr(_devicePointer.ToInt64() + offset);
        return new CudaUnifiedMemoryBuffer(_context, viewPointer, length, Options, _isUnifiedMemory, _logger);
    }

    /// <summary>
    /// Prefetches data to device for better performance
    /// </summary>
    public async ValueTask PrefetchToDeviceAsync(CancellationToken cancellationToken = default)
    {
        if (!_isUnifiedMemory) return; // Only works with unified memory
        
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemPrefetchAsync(
                _devicePointer, 
                (ulong)SizeInBytes, 
                _context.DeviceId, 
                _context.Stream);
                
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to prefetch unified memory to device: {Error}",
                    CudaRuntime.GetErrorString(result));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prefetches data to host (CPU) for better performance
    /// </summary>
    public async ValueTask PrefetchToHostAsync(CancellationToken cancellationToken = default)
    {
        if (!_isUnifiedMemory) return; // Only works with unified memory
        
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemPrefetchAsync(
                _devicePointer, 
                (ulong)SizeInBytes, 
                -1, // -1 means host
                _context.Stream);
                
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to prefetch unified memory to host: {Error}",
                    CudaRuntime.GetErrorString(result));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets performance statistics for this buffer
    /// </summary>
    public CudaBufferStatistics GetStatistics()
    {
        return new CudaBufferStatistics
        {
            SizeInBytes = SizeInBytes,
            IsUnifiedMemory = _isUnifiedMemory,
            HostToDeviceTransfers = _hostToDeviceTransfers,
            DeviceToHostTransfers = _deviceToHostTransfers,
            TotalBytesTransferred = _totalBytesTransferred,
            MemoryEfficiency = CalculateMemoryEfficiency()
        };
    }

    private void InitializeMemory()
    {
        try
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemset(_devicePointer, 0, (ulong)SizeInBytes);
            
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to zero-initialize CUDA memory: {Error}",
                    CudaRuntime.GetErrorString(result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during memory initialization");
        }
    }

    private async ValueTask CopyFromHostUnifiedAsync(
        IntPtr srcPtr, IntPtr dstPtr, long bytesToCopy, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // For unified memory, we can use direct memory copy which may be faster
            // than going through CUDA runtime for small transfers
            if (bytesToCopy < 64 * 1024) // < 64KB - use direct copy
            {
                unsafe
                {
                    Buffer.MemoryCopy(srcPtr.ToPointer(), dstPtr.ToPointer(), bytesToCopy, bytesToCopy);
                }
            }
            else
            {
                // Use CUDA async copy for larger transfers
                var result = CudaRuntime.cudaMemcpyAsync(
                    dstPtr, srcPtr, (ulong)bytesToCopy, 
                    CudaMemcpyKind.HostToDevice, _context.Stream);
                
                CudaRuntime.CheckError(result, "Host to unified memory copy");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CopyFromHostDeviceAsync(
        IntPtr srcPtr, IntPtr dstPtr, long bytesToCopy, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemcpyAsync(
                dstPtr, srcPtr, (ulong)bytesToCopy, 
                CudaMemcpyKind.HostToDevice, _context.Stream);
            
            CudaRuntime.CheckError(result, "Host to device memory copy");
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CopyToHostUnifiedAsync(
        IntPtr srcPtr, IntPtr dstPtr, long bytesToCopy, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // For unified memory, small reads can be direct
            if (bytesToCopy < 64 * 1024) // < 64KB - use direct copy
            {
                unsafe
                {
                    Buffer.MemoryCopy(srcPtr.ToPointer(), dstPtr.ToPointer(), bytesToCopy, bytesToCopy);
                }
            }
            else
            {
                // Use CUDA async copy for larger transfers
                var result = CudaRuntime.cudaMemcpyAsync(
                    dstPtr, srcPtr, (ulong)bytesToCopy, 
                    CudaMemcpyKind.DeviceToHost, _context.Stream);
                
                CudaRuntime.CheckError(result, "Unified memory to host copy");
                
                // Synchronize to ensure data is available
                _context.Synchronize();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CopyToHostDeviceAsync(
        IntPtr srcPtr, IntPtr dstPtr, long bytesToCopy, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemcpyAsync(
                dstPtr, srcPtr, (ulong)bytesToCopy, 
                CudaMemcpyKind.DeviceToHost, _context.Stream);
            
            CudaRuntime.CheckError(result, "Device to host memory copy");
            
            // Synchronize to ensure data is available
            _context.Synchronize();
        }, cancellationToken).ConfigureAwait(false);
    }

    private double CalculateMemoryEfficiency()
    {
        var totalTransfers = _hostToDeviceTransfers + _deviceToHostTransfers;
        if (totalTransfers == 0) return 1.0;

        // Efficiency based on unified memory usage and transfer patterns
        if (_isUnifiedMemory)
        {
            // Unified memory is more efficient for frequently accessed data
            var accessPattern = (double)totalTransfers / (SizeInBytes / 1024); // Transfers per KB
            return Math.Min(1.0, 1.0 / (1.0 + accessPattern * 0.1));
        }
        else
        {
            // Device memory efficiency based on transfer size
            var avgTransferSize = _totalBytesTransferred / (double)totalTransfers;
            var largeTransferBonus = Math.Min(1.0, avgTransferSize / (1024 * 1024)); // Bonus for >1MB transfers
            return 0.7 + (0.3 * largeTransferBonus); // Base 70% + bonus
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            if (_devicePointer != IntPtr.Zero)
            {
                await Task.Run(() =>
                {
                    _context.MakeCurrent();
                    
                    var result = _isUnifiedMemory ? 
                        CudaRuntime.cudaFree(_devicePointer) : 
                        CudaRuntime.cudaFree(_devicePointer);

                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to free CUDA memory: {Error}",
                            CudaRuntime.GetErrorString(result));
                    }
                    else
                    {
                        var stats = GetStatistics();
                        _logger.LogDebug(
                            "Freed CUDA {MemoryType} buffer: {Size}MB, {H2D} H2D transfers, " +
                            "{D2H} D2H transfers, {TotalBytes}MB transferred, {Efficiency:P1} efficiency",
                            _isUnifiedMemory ? "unified" : "device",
                            SizeInBytes / (1024 * 1024),
                            stats.HostToDeviceTransfers,
                            stats.DeviceToHostTransfers,
                            stats.TotalBytesTransferred / (1024 * 1024),
                            stats.MemoryEfficiency);
                    }

                    _devicePointer = IntPtr.Zero;
                }).ConfigureAwait(false);
            }

            if (_hostPointer != IntPtr.Zero)
            {
                CudaRuntime.cudaFreeHost(_hostPointer);
                _hostPointer = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA unified buffer disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_devicePointer != IntPtr.Zero)
            {
                _context.MakeCurrent();
                
                var result = CudaRuntime.cudaFree(_devicePointer);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to free CUDA memory: {Error}",
                        CudaRuntime.GetErrorString(result));
                }

                _devicePointer = IntPtr.Zero;
            }

            if (_hostPointer != IntPtr.Zero)
            {
                CudaRuntime.cudaFreeHost(_hostPointer);
                _hostPointer = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA unified buffer disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Statistics for CUDA buffer performance tracking
/// </summary>
public readonly struct CudaBufferStatistics
{
    public long SizeInBytes { get; init; }
    public bool IsUnifiedMemory { get; init; }
    public long HostToDeviceTransfers { get; init; }
    public long DeviceToHostTransfers { get; init; }
    public long TotalBytesTransferred { get; init; }
    public double MemoryEfficiency { get; init; }
    
    public long TotalTransfers => HostToDeviceTransfers + DeviceToHostTransfers;
    public double AverageTransferSize => TotalTransfers > 0 ? 
        TotalBytesTransferred / (double)TotalTransfers : 0.0;
}