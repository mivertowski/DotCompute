// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Manages memory allocation and transfer for an accelerator.
/// </summary>
public interface IMemoryManager
{
    /// <summary>
    /// Allocates memory on the accelerator.
    /// </summary>
    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates memory and copies data from host.
    /// </summary>
    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Creates a view over existing memory.
    /// </summary>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length);
}

/// <summary>
/// Represents a memory buffer on an accelerator.
/// </summary>
public interface IMemoryBuffer : IAsyncDisposable
{
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the memory flags.
    /// </summary>
    public MemoryOptions Options { get; }

    /// <summary>
    /// Copies data from host memory to this buffer.
    /// </summary>
    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies data from this buffer to host memory.
    /// </summary>
    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged;
}

/// <summary>
/// Memory allocation flags.
/// </summary>
[Flags]
public enum MemoryOptions
{
    /// <summary>
    /// No special flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Memory is read-only.
    /// </summary>
    ReadOnly = 1,

    /// <summary>
    /// Memory is write-only.
    /// </summary>
    WriteOnly = 2,

    /// <summary>
    /// Memory should be allocated in host-visible memory if possible.
    /// </summary>
    HostVisible = 4,

    /// <summary>
    /// Memory should be cached if possible.
    /// </summary>
    Cached = 8,

    /// <summary>
    /// Memory will be used for atomic operations.
    /// </summary>
    Atomic = 16
}
