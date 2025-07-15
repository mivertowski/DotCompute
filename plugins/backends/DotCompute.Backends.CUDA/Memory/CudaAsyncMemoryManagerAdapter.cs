// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Adapts the synchronous CudaMemoryManager to the async IMemoryManager interface.
/// </summary>
public class CudaAsyncMemoryManagerAdapter : IMemoryManager
{
    private readonly CudaMemoryManager _syncMemoryManager;
    
    public CudaAsyncMemoryManagerAdapter(CudaMemoryManager syncMemoryManager)
    {
        _syncMemoryManager = syncMemoryManager ?? throw new ArgumentNullException(nameof(syncMemoryManager));
    }
    
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
}