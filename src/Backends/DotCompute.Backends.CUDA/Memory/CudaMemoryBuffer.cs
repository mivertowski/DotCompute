// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA memory buffer implementation
/// </summary>
public sealed class CudaMemoryBuffer : ISyncMemoryBuffer
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private IntPtr _devicePointer;
    private bool _disposed;

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;
    public IntPtr DevicePointer => _devicePointer;

    public CudaMemoryBuffer(CudaContext context, long sizeInBytes, MemoryOptions options, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        SizeInBytes = sizeInBytes;
        Options = options;

        AllocateDeviceMemory();
    }
    
    /// <summary>
    /// Constructor for pooled buffers with existing device pointer
    /// </summary>
    public CudaMemoryBuffer(CudaContext context, IntPtr devicePointer, long sizeInBytes, MemoryOptions options, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }
        
        if (devicePointer == IntPtr.Zero)
        {
            throw new ArgumentException("Device pointer cannot be null", nameof(devicePointer));
        }

        SizeInBytes = sizeInBytes;
        Options = options;
        _devicePointer = devicePointer;
        
        _logger.LogDebug("Created CUDA buffer from existing device pointer {Pointer:X} ({Size}MB)",
            devicePointer.ToInt64(), sizeInBytes / (1024 * 1024));
    }

    private void AllocateDeviceMemory()
    {
        try
        {
            _context.MakeCurrent();

            // Use aligned allocation for optimal performance (RTX 2000 series)
            var alignedSize = ((ulong)SizeInBytes + 255) & ~255UL; // 256-byte alignment
            var result = CudaRuntime.cudaMalloc(ref _devicePointer, alignedSize);

            if (result != CudaError.Success)
            {
                // Try standard allocation if aligned fails
                result = CudaRuntime.cudaMalloc(ref _devicePointer, (ulong)SizeInBytes);
                
                if (result != CudaError.Success)
                {
                    var errorMsg = CudaRuntime.GetErrorString(result);
                    _logger.LogError("CUDA memory allocation failed: {Error}", errorMsg);
                    throw new MemoryException($"Failed to allocate CUDA memory: {errorMsg}");
                }
            }

            _logger.LogDebug("Allocated {Size}MB at device pointer {Pointer:X} (aligned: {AlignedSize}MB)",
                SizeInBytes / (1024 * 1024), _devicePointer.ToInt64(), alignedSize / (1024 * 1024));

            // Initialize memory to zero for safety and deterministic behavior
            result = CudaRuntime.cudaMemset(_devicePointer, 0, (ulong)SizeInBytes);

            if (result != CudaError.Success)
            {
                // Clean up on failure
                var freeResult = CudaRuntime.cudaFree(_devicePointer);
                _devicePointer = IntPtr.Zero;
                
                if (freeResult != CudaError.Success)
                {
                    _logger.LogWarning("Failed to clean up after allocation failure: {Error}", 
                        CudaRuntime.GetErrorString(freeResult));
                }
                
                throw new MemoryException($"Failed to zero-initialize CUDA memory: {CudaRuntime.GetErrorString(result)}");
            }
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Unexpected error during CUDA memory allocation");
            throw new MemoryException($"Failed to allocate {SizeInBytes} bytes of CUDA memory", ex);
        }
    }

    public unsafe void* GetHostPointer()
    {
        ThrowIfDisposed();

        // CUDA device memory cannot be directly accessed from host
        throw new NotSupportedException("Direct host access to CUDA device memory is not supported. Use memory copy operations instead.");
    }

    public unsafe Span<T> AsSpan<T>() where T : unmanaged
    {
        ThrowIfDisposed();

        // CUDA device memory cannot be directly accessed as a Span
        throw new NotSupportedException("Direct span access to CUDA device memory is not supported. Use memory copy operations instead.");
    }

    public ISyncMemoryBuffer Slice(long offset, long length)
    {
        ThrowIfDisposed();

        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        }

        if (offset + length > SizeInBytes)
        {
            throw new ArgumentException("Slice would exceed buffer bounds");
        }

        // Create a view that shares the same device memory
        return new CudaMemoryBufferView(this, offset, length, _logger);
    }

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentException($"Copy would exceed buffer bounds: {offset + bytesToCopy} > {SizeInBytes}");
        }

        // Pin source memory for optimal transfer
        using var handle = source.Pin();
        IntPtr srcPtr;
        IntPtr dstPtr;
        unsafe
        {
            srcPtr = new IntPtr(handle.Pointer);
            dstPtr = new IntPtr(_devicePointer.ToInt64() + offset);
        }

        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // Use async copy for better performance with larger transfers
            if (bytesToCopy >= 64 * 1024) // Use async for transfers >= 64KB
            {
                var result = CudaRuntime.cudaMemcpyAsync(dstPtr, srcPtr, (ulong)bytesToCopy, 
                    CudaMemcpyKind.HostToDevice, _context.Stream);
                CudaRuntime.CheckError(result, "Async memory copy from host");
                
                // Synchronize to ensure completion
                result = CudaRuntime.cudaStreamSynchronize(_context.Stream);
                CudaRuntime.CheckError(result, "Stream synchronization after host copy");
            }
            else
            {
                // Use synchronous copy for small transfers
                var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (ulong)bytesToCopy, CudaMemcpyKind.HostToDevice);
                CudaRuntime.CheckError(result, "Synchronous memory copy from host");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentException($"Copy would exceed buffer bounds: {offset + bytesToCopy} > {SizeInBytes}");
        }

        // Pin destination memory for optimal transfer
        using var handle = destination.Pin();
        IntPtr srcPtr;
        IntPtr dstPtr;
        unsafe
        {
            srcPtr = new IntPtr(_devicePointer.ToInt64() + offset);
            dstPtr = new IntPtr(handle.Pointer);
        }

        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // Use async copy for better performance with larger transfers
            if (bytesToCopy >= 64 * 1024) // Use async for transfers >= 64KB
            {
                var result = CudaRuntime.cudaMemcpyAsync(dstPtr, srcPtr, (ulong)bytesToCopy, 
                    CudaMemcpyKind.DeviceToHost, _context.Stream);
                CudaRuntime.CheckError(result, "Async memory copy to host");
                
                // Must synchronize for device-to-host to ensure data is available
                result = CudaRuntime.cudaStreamSynchronize(_context.Stream);
                CudaRuntime.CheckError(result, "Stream synchronization after host copy");
            }
            else
            {
                // Use synchronous copy for small transfers
                var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (ulong)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                CudaRuntime.CheckError(result, "Synchronous memory copy to host");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_devicePointer != IntPtr.Zero)
            {
                await Task.Run(() =>
                {
                    _context.MakeCurrent();
                    var result = CudaRuntime.cudaFree(_devicePointer);

                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to free CUDA memory: {Error}",
                            CudaRuntime.GetErrorString(result));
                    }
                    else
                    {
                        _logger.LogDebug("Freed {Size} bytes at device pointer {Pointer:X}",
                            SizeInBytes, _devicePointer.ToInt64());
                    }

                    _devicePointer = IntPtr.Zero;
                }).ConfigureAwait(false);
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA buffer disposal");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                if (_devicePointer != IntPtr.Zero)
                {
                    _context.MakeCurrent();
                    
                    // Ensure any pending operations complete before freeing
                    var syncResult = CudaRuntime.cudaDeviceSynchronize();
                    if (syncResult != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to synchronize device before freeing memory: {Error}",
                            CudaRuntime.GetErrorString(syncResult));
                    }
                    
                    var result = CudaRuntime.cudaFree(_devicePointer);

                    if (result != CudaError.Success)
                    {
                        _logger.LogError("Failed to free CUDA memory: {Error}. Potential memory leak!",
                            CudaRuntime.GetErrorString(result));
                    }
                    else
                    {
                        _logger.LogDebug("Freed {Size}MB at device pointer {Pointer:X}",
                            SizeInBytes / (1024 * 1024), _devicePointer.ToInt64());
                    }

                    _devicePointer = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during CUDA buffer disposal");
                // Don't rethrow during disposal to prevent further issues
            }
        }

        _disposed = true;
    }
    
    /// <summary>
    /// Finalizer to ensure memory is freed even if Dispose is not called
    /// </summary>
    ~CudaMemoryBuffer()
    {
        if (!_disposed && _devicePointer != IntPtr.Zero)
        {
            _logger.LogWarning("CUDA memory buffer finalized without explicit disposal. Size: {Size}MB. "
                + "This indicates a potential resource leak.", SizeInBytes / (1024 * 1024));
            Dispose(false);
        }
    }
}

/// <summary>
/// A view into a CUDA memory buffer (for slicing) with improved bounds checking
/// </summary>
internal sealed class CudaMemoryBufferView : ISyncMemoryBuffer
{
#pragma warning disable CA2213 // Disposable fields should be disposed - View doesn't own the parent buffer, so it shouldn't dispose it
    private readonly CudaMemoryBuffer _parent;
#pragma warning restore CA2213
    private readonly long _offset;
    private readonly ILogger _logger;
    
    public CudaMemoryBufferView(CudaMemoryBuffer parent, long offset, long length, ILogger logger)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SizeInBytes = length;
        
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        if (offset + length > parent.SizeInBytes)
            throw new ArgumentException("View would exceed parent buffer bounds");
    }

    public long SizeInBytes { get; private set; }
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;

    internal IntPtr DevicePointer => new(_parent.DevicePointer.ToInt64() + _offset);

    public unsafe void* GetHostPointer() => throw new NotSupportedException("Direct host access to CUDA device memory is not supported. Use memory copy operations instead.");

    public unsafe Span<T> AsSpan<T>() where T : unmanaged => throw new NotSupportedException("Direct span access to CUDA device memory is not supported. Use memory copy operations instead.");

    public ISyncMemoryBuffer Slice(long offset, long length)
    {
        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        }

        if (offset + length > SizeInBytes)
        {
            throw new ArgumentException("Slice would exceed buffer bounds");
        }

        return new CudaMemoryBufferView(_parent, _offset + offset, length, _logger);
    }

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentException("Copy would exceed buffer bounds");
        }

        // Delegate to parent with adjusted offset
        await _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;

        if (offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentException("Copy would exceed buffer bounds");
        }

        // Delegate to parent with adjusted offset
        await _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // Views don't own the memory, so nothing to dispose

    public void Dispose()
    {
        // Views don't own the memory, so nothing to dispose
    }
}
