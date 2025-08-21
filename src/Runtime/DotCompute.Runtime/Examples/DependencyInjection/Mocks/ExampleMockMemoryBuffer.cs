// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Examples.DependencyInjection.Mocks;

/// <summary>
/// Example mock memory buffer for DI demonstration
/// </summary>
/// <param name="size">The size of the buffer in bytes</param>
/// <param name="options">The memory options</param>
internal class ExampleMockMemoryBuffer(long size, MemoryOptions options) : IMemoryBuffer
{
    /// <inheritdoc />
    public long SizeInBytes { get; } = size;

    /// <inheritdoc />
    public MemoryOptions Options { get; } = options;

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<Memory<byte>> GetMemoryAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new Memory<byte>(new byte[SizeInBytes]));

    /// <inheritdoc />
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}