// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using DotCompute.Core.Abstractions;
using DotCompute.Core.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA memory buffer implementation
/// </summary>
public class CudaMemoryBuffer : IMemoryBuffer
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private IntPtr _devicePointer;
    private bool _disposed;

    public long SizeInBytes { get; }
    public MemoryFlags Flags { get; }
    public bool IsDisposed => _disposed;
    public IntPtr DevicePointer => _devicePointer;

    public CudaMemoryBuffer(CudaContext context, long sizeInBytes, MemoryFlags flags, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        SizeInBytes = sizeInBytes;
        Flags = flags;

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

            // Initialize memory to zero if requested
            if (Flags.HasFlag(MemoryFlags.ZeroInitialized))
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

    public IMemoryBuffer Slice(long offset, long length)
    {
        ThrowIfDisposed();
        
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        
        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        
        if (offset + length > SizeInBytes)
            throw new ArgumentException("Slice would exceed buffer bounds");

        // Create a view that shares the same device memory
        return new CudaMemoryBufferView(this, offset, length, _logger);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryBuffer));
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
internal class CudaMemoryBufferView : IMemoryBuffer
{
    private readonly CudaMemoryBuffer _parent;
    private readonly long _offset;
    private readonly ILogger _logger;

    public long SizeInBytes { get; }
    public MemoryFlags Flags => _parent.Flags;
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

    public IMemoryBuffer Slice(long offset, long length)
    {
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        
        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero", nameof(length));
        
        if (offset + length > SizeInBytes)
            throw new ArgumentException("Slice would exceed buffer bounds");

        return new CudaMemoryBufferView(_parent, _offset + offset, length, _logger);
    }

    public void Dispose()
    {
        // Views don't own the memory, so nothing to dispose
    }
}