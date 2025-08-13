// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Adapts the synchronous CudaMemoryManager to the async IMemoryManager interface.
/// </summary>
public class CudaAsyncMemoryManagerAdapter(CudaMemoryManager syncMemoryManager) : IMemoryManager
{
    private readonly CudaMemoryManager _syncMemoryManager = syncMemoryManager ?? throw new ArgumentNullException(nameof(syncMemoryManager));

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = _syncMemoryManager.Allocate(sizeInBytes, options);
        return new ValueTask<IMemoryBuffer>(buffer);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        cancellationToken.ThrowIfCancellationRequested();

        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = source.Length * elementSize;

        var buffer = _syncMemoryManager.Allocate(sizeInBytes, options);

        try
        {
            await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            _syncMemoryManager.Free(buffer);
            throw;
        }
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is ISyncMemoryBuffer syncBuffer)
        {
            return syncBuffer.Slice(offset, length);
        }

        throw new ArgumentException("Buffer must be a CUDA memory buffer", nameof(buffer));
    }

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            fixed (T* dataPtr = data)
            {
                if (buffer is ISyncMemoryBuffer syncBuffer)
                {
                    _syncMemoryManager.CopyFromHost(dataPtr, syncBuffer, sizeInBytes);
                }
                else
                {
                    throw new ArgumentException("Buffer must be a CUDA memory buffer", nameof(buffer));
                }
            }
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            fixed (T* dataPtr = data)
            {
                if (buffer is ISyncMemoryBuffer syncBuffer)
                {
                    _syncMemoryManager.CopyToHost(syncBuffer, dataPtr, sizeInBytes);
                }
                else
                {
                    throw new ArgumentException("Buffer must be a CUDA memory buffer", nameof(buffer));
                }
            }
        }
    }

    public void Free(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (buffer is ISyncMemoryBuffer syncBuffer)
        {
            _syncMemoryManager.Free(syncBuffer);
        }
        else
        {
            throw new ArgumentException("Buffer must be a CUDA memory buffer", nameof(buffer));
        }
    }
}
