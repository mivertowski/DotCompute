// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA memory buffer implementation
/// </summary>
public class CudaMemoryBuffer : ISyncMemoryBuffer
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

    private void AllocateDeviceMemory()
    {
        try
        {
            _context.MakeCurrent();

            var result = CudaRuntime.cudaMalloc(ref _devicePointer, (ulong)SizeInBytes);

            if (result != CudaError.Success)
            {
                throw new MemoryException($"Failed to allocate CUDA memory: {CudaRuntime.GetErrorString(result)}");
            }

            _logger.LogDebug("Allocated {Size} bytes at device pointer {Pointer:X}",
                SizeInBytes, _devicePointer.ToInt64());

            // Initialize memory to zero by default
            if (true) // Always zero-initialize for safety
            {
                result = CudaRuntime.cudaMemset(_devicePointer, 0, (ulong)SizeInBytes);

                if (result != CudaError.Success)
                {
                    // Clean up on failure
                    CudaRuntime.cudaFree(_devicePointer);
                    _devicePointer = IntPtr.Zero;
                    throw new MemoryException($"Failed to zero-initialize CUDA memory: {CudaRuntime.GetErrorString(result)}");
                }
            }
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
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
            throw new ArgumentException("Copy would exceed buffer bounds");
        }

        // Pin source memory
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
            var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (ulong)bytesToCopy, CudaMemcpyKind.HostToDevice);
            CudaRuntime.CheckError(result, "Memory copy from host");
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
            throw new ArgumentException("Copy would exceed buffer bounds");
        }

        // Pin destination memory
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
            var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (ulong)bytesToCopy, CudaMemcpyKind.DeviceToHost);
            CudaRuntime.CheckError(result, "Memory copy to host");
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryBuffer));
        }
    }

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
        if (_disposed)
        {
            return;
        }

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
                else
                {
                    _logger.LogDebug("Freed {Size} bytes at device pointer {Pointer:X}",
                        SizeInBytes, _devicePointer.ToInt64());
                }

                _devicePointer = IntPtr.Zero;
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA buffer disposal");
        }
    }
}

/// <summary>
/// A view into a CUDA memory buffer (for slicing)
/// </summary>
internal sealed class CudaMemoryBufferView : ISyncMemoryBuffer
{
    private readonly CudaMemoryBuffer _parent;
    private readonly long _offset;
    private readonly ILogger _logger;

    public long SizeInBytes { get; }
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;

    internal IntPtr DevicePointer => new IntPtr(_parent.DevicePointer.ToInt64() + _offset);

    public CudaMemoryBufferView(CudaMemoryBuffer parent, long offset, long length, ILogger logger)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _offset = offset;
        SizeInBytes = length;
    }

    public unsafe void* GetHostPointer()
    {
        throw new NotSupportedException("Direct host access to CUDA device memory is not supported. Use memory copy operations instead.");
    }

    public unsafe Span<T> AsSpan<T>() where T : unmanaged
    {
        throw new NotSupportedException("Direct span access to CUDA device memory is not supported. Use memory copy operations instead.");
    }

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

    public ValueTask DisposeAsync()
    {
        // Views don't own the memory, so nothing to dispose
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // Views don't own the memory, so nothing to dispose
    }
}
