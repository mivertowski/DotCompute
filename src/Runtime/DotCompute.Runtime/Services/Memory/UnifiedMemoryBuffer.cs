// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Mock unified memory buffer implementation
/// </summary>
internal class UnifiedMemoryBuffer : IMemoryBuffer, IDisposable
{
    public UnifiedMemoryBuffer(long sizeInBytes)
    {
        SizeInBytes = sizeInBytes;
        Options = MemoryOptions.None;
    }

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed { get; private set; }

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        // Mock implementation
        => ValueTask.CompletedTask;

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        // Mock implementation
        => ValueTask.CompletedTask;

    public void Dispose() => IsDisposed = true;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}