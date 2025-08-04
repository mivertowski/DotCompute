// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA memory manager implementation
/// </summary>
public class CudaMemoryManager : ISyncMemoryManager
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ISyncMemoryBuffer, CudaMemoryBuffer> _buffers;
    private bool _disposed;

    public CudaMemoryManager(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _buffers = new ConcurrentDictionary<ISyncMemoryBuffer, CudaMemoryBuffer>();
    }

    public ISyncMemoryBuffer Allocate(long sizeInBytes, MemoryOptions options = MemoryOptions.None)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        try
        {
            _logger.LogDebug("Allocating {Size} bytes of CUDA memory with options {Options}", sizeInBytes, options);

            var buffer = new CudaMemoryBuffer(_context, sizeInBytes, options, _logger);

            if (!_buffers.TryAdd(buffer, buffer))
            {
                buffer.Dispose();
                throw new MemoryException("Failed to track allocated buffer");
            }

            _logger.LogDebug("Successfully allocated CUDA buffer of {Size} bytes", sizeInBytes);
            return buffer;
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
            _logger.LogError(ex, "Failed to allocate CUDA memory");
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of CUDA memory", ex);
        }
    }

    public ISyncMemoryBuffer AllocateAligned(long sizeInBytes, int alignment, MemoryOptions options = MemoryOptions.None)
    {
        ThrowIfDisposed();

        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentException("Alignment must be a power of 2", nameof(alignment));
        }

        // CUDA allocations are already aligned to at least 256 bytes
        // For specific alignment requirements, we may need to over-allocate
        var alignedSize = ((sizeInBytes + alignment - 1) / alignment) * alignment;

        return Allocate(alignedSize, options);
    }

    public void Copy(ISyncMemoryBuffer source, ISyncMemoryBuffer destination, long sizeInBytes, long sourceOffset = 0, long destinationOffset = 0)
    {
        ThrowIfDisposed();
        ValidateCopyParameters(source, destination, sizeInBytes, sourceOffset, destinationOffset);

        try
        {
            var srcBuffer = GetCudaBuffer(source);
            var dstBuffer = GetCudaBuffer(destination);

            var srcPtr = new IntPtr(srcBuffer.DevicePointer.ToInt64() + sourceOffset);
            var dstPtr = new IntPtr(dstBuffer.DevicePointer.ToInt64() + destinationOffset);

            _logger.LogDebug("Copying {Size} bytes from GPU buffer to GPU buffer", sizeInBytes);

            var result = CudaRuntime.cudaMemcpyAsync(
                dstPtr,
                srcPtr,
                (ulong)sizeInBytes,
                CudaMemcpyKind.DeviceToDevice,
                _context.Stream);

            CudaRuntime.CheckError(result, "GPU to GPU copy");
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
            _logger.LogError(ex, "Failed to copy memory between GPU buffers");
            throw new MemoryException("Failed to copy memory between GPU buffers", ex);
        }
    }

    public unsafe void CopyFromHost(void* source, ISyncMemoryBuffer destination, long sizeInBytes, long destinationOffset = 0)
    {
        ThrowIfDisposed();

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ValidateBuffer(destination, sizeInBytes, destinationOffset);

        try
        {
            var dstBuffer = GetCudaBuffer(destination);
            var dstPtr = new IntPtr(dstBuffer.DevicePointer.ToInt64() + destinationOffset);

            _logger.LogDebug("Copying {Size} bytes from host to GPU", sizeInBytes);

            var result = CudaRuntime.cudaMemcpyAsync(
                dstPtr,
                new IntPtr(source),
                (ulong)sizeInBytes,
                CudaMemcpyKind.HostToDevice,
                _context.Stream);

            CudaRuntime.CheckError(result, "Host to GPU copy");
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
            _logger.LogError(ex, "Failed to copy memory from host to GPU");
            throw new MemoryException("Failed to copy memory from host to GPU", ex);
        }
    }

    public unsafe void CopyToHost(ISyncMemoryBuffer source, void* destination, long sizeInBytes, long sourceOffset = 0)
    {
        ThrowIfDisposed();

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        ValidateBuffer(source, sizeInBytes, sourceOffset);

        try
        {
            var srcBuffer = GetCudaBuffer(source);
            var srcPtr = new IntPtr(srcBuffer.DevicePointer.ToInt64() + sourceOffset);

            _logger.LogDebug("Copying {Size} bytes from GPU to host", sizeInBytes);

            var result = CudaRuntime.cudaMemcpyAsync(
                new IntPtr(destination),
                srcPtr,
                (ulong)sizeInBytes,
                CudaMemcpyKind.DeviceToHost,
                _context.Stream);

            CudaRuntime.CheckError(result, "GPU to host copy");

            // Synchronize to ensure copy completes before returning
            _context.Synchronize();
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
            _logger.LogError(ex, "Failed to copy memory from GPU to host");
            throw new MemoryException("Failed to copy memory from GPU to host", ex);
        }
    }

    public void Fill(ISyncMemoryBuffer buffer, byte value, long sizeInBytes, long offset = 0)
    {
        ThrowIfDisposed();
        ValidateBuffer(buffer, sizeInBytes, offset);

        try
        {
            var cudaBuffer = GetCudaBuffer(buffer);
            var ptr = new IntPtr(cudaBuffer.DevicePointer.ToInt64() + offset);

            _logger.LogDebug("Filling {Size} bytes with value {Value}", sizeInBytes, value);

            var result = CudaRuntime.cudaMemsetAsync(ptr, value, (ulong)sizeInBytes, _context.Stream);
            CudaRuntime.CheckError(result, "Memory fill");
        }
        catch (Exception ex) when (!(ex is MemoryException))
        {
            _logger.LogError(ex, "Failed to fill GPU memory");
            throw new MemoryException("Failed to fill GPU memory", ex);
        }
    }

    public void Zero(ISyncMemoryBuffer buffer)
    {
        ThrowIfDisposed();

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        Fill(buffer, 0, buffer.SizeInBytes);
    }

    public void Free(ISyncMemoryBuffer buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (_buffers.TryRemove(buffer, out var cudaBuffer))
        {
            cudaBuffer.Dispose();
            _logger.LogDebug("Freed CUDA buffer of {Size} bytes", buffer.SizeInBytes);
        }
    }

    public MemoryStatistics GetStatistics()
    {
        ThrowIfDisposed();

        try
        {
            CudaRuntime.cudaMemGetInfo(out var free, out var total);

            long allocated = 0;
            int allocationCount = 0;

            foreach (var buffer in _buffers.Values)
            {
                allocated += buffer.SizeInBytes;
                allocationCount++;
            }

            return new MemoryStatistics
            {
                TotalMemory = (long)total,
                UsedMemory = (long)(total - free),
                FreeMemory = (long)free,
                AllocatedMemory = allocated,
                AllocationCount = allocationCount,
                PeakMemory = (long)(total - free) // Approximation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory statistics");
            throw new MemoryException("Failed to get CUDA memory statistics", ex);
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Resetting CUDA memory manager");

        foreach (var buffer in _buffers.Values)
        {
            buffer.Dispose();
        }

        _buffers.Clear();
    }

    private CudaMemoryBuffer GetCudaBuffer(ISyncMemoryBuffer buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (_buffers.TryGetValue(buffer, out var cudaBuffer))
        {
            return cudaBuffer;
        }

        throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));
    }

    private static void ValidateBuffer(ISyncMemoryBuffer buffer, long sizeInBytes, long offset)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        if (offset + sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Operation would exceed buffer bounds");
        }
    }

    private static void ValidateCopyParameters(ISyncMemoryBuffer source, ISyncMemoryBuffer destination,
        long sizeInBytes, long sourceOffset, long destinationOffset)
    {
        ValidateBuffer(source, sizeInBytes, sourceOffset);
        ValidateBuffer(destination, sizeInBytes, destinationOffset);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryManager));
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
            Reset();
            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA memory manager disposal");
        }
    }
}
