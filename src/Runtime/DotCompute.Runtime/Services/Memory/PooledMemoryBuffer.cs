// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Memory buffer that belongs to a pool
/// </summary>
internal class PooledMemoryBuffer : IMemoryBuffer, IDisposable
{
    private readonly DefaultMemoryPool _pool;
    private bool _disposed;

    public PooledMemoryBuffer(DefaultMemoryPool pool, long sizeInBytes)
    {
        _pool = pool;
        SizeInBytes = sizeInBytes;
        Options = MemoryOptions.None;
    }

    public DefaultMemoryPool Pool => _pool;
    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        // Mock implementation
        => ValueTask.CompletedTask;

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        // Mock implementation
        => ValueTask.CompletedTask;

    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.ReturnAsync(this).GetAwaiter().GetResult();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _pool.ReturnAsync(this);
            _disposed = true;
        }
    }
}