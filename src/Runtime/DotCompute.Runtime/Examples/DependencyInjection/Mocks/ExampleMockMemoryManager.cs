// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Examples.DependencyInjection.Mocks;

/// <summary>
/// Example mock memory manager for DI demonstration
/// </summary>
internal class ExampleMockMemoryManager : IMemoryManager
{
    /// <inheritdoc />
    public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        var buffer = new ExampleMockMemoryBuffer(sizeInBytes, options);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <inheritdoc />
    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = new ExampleMockMemoryBuffer(source.Length * sizeof(int), options);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <inheritdoc />
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length) => new ExampleMockMemoryBuffer(length, buffer.Options);

    /// <inheritdoc />
    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var buffer = new ExampleMockMemoryBuffer(count * sizeof(int), MemoryOptions.None);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <inheritdoc />
    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        // Mock implementation - do nothing
    }

    /// <inheritdoc />
    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        // Mock implementation - do nothing
    }

    /// <inheritdoc />
    public void Free(IMemoryBuffer buffer) => buffer?.Dispose();

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}