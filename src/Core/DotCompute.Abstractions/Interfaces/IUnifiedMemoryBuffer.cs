// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions;

/// <summary>
/// Unified memory buffer interface that combines the best features from all buffer implementations.
/// This is the ONLY memory buffer interface in the entire solution.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
public interface IUnifiedMemoryBuffer<T> : IUnifiedMemoryBuffer where T : unmanaged
{
    // Core Properties (additional to base interface)

    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the number of elements in the buffer (alias for Length).
    /// </summary>
    public int ElementCount => Length;

    /// <summary>
    /// Gets the accelerator this buffer is associated with.
    /// </summary>
    public IAccelerator Accelerator { get; }

    // State Management

    /// <summary>
    /// Gets whether the buffer is currently available on the host.
    /// </summary>
    public bool IsOnHost { get; }

    /// <summary>
    /// Gets whether the buffer is currently available on the device.
    /// </summary>
    public bool IsOnDevice { get; }

    /// <summary>
    /// Gets whether the buffer has been modified and needs synchronization.
    /// </summary>
    public bool IsDirty { get; }

    // Host Memory Access

    /// <summary>
    /// Gets a span to the host memory. Will trigger transfer if needed.
    /// </summary>
    public Span<T> AsSpan();

    /// <summary>
    /// Gets a read-only span to the host memory. Will trigger transfer if needed.
    /// </summary>
    public ReadOnlySpan<T> AsReadOnlySpan();

    /// <summary>
    /// Gets a memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    public Memory<T> AsMemory();

    /// <summary>
    /// Gets a read-only memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    public ReadOnlyMemory<T> AsReadOnlyMemory();

    // Device Memory Access

    /// <summary>
    /// Gets the device memory handle. Will trigger transfer if needed.
    /// </summary>
    public DeviceMemory GetDeviceMemory();

    // Memory Mapping (for direct access)

    /// <summary>
    /// Maps this buffer to host memory for direct access.
    /// </summary>
    /// <param name="mode">The mapping mode.</param>
    /// <returns>A mapped memory region.</returns>
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite);

    /// <summary>
    /// Maps a portion of this buffer to host memory for direct access.
    /// </summary>
    /// <param name="offset">The offset to start mapping at.</param>
    /// <param name="length">The number of elements to map.</param>
    /// <param name="mode">The mapping mode.</param>
    /// <returns>A mapped memory region.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite);

    /// <summary>
    /// Asynchronously maps this buffer to host memory.
    /// </summary>
    /// <param name="mode">The mapping mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that returns the mapped memory region.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default);

    // Synchronization

    /// <summary>
    /// Ensures the buffer is available on the host. Triggers transfer if needed.
    /// </summary>
    public void EnsureOnHost();

    /// <summary>
    /// Ensures the buffer is available on the device. Triggers transfer if needed.
    /// </summary>
    public void EnsureOnDevice();

    /// <summary>
    /// Asynchronously ensures the buffer is available on the host.
    /// </summary>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures the buffer is available on the device.
    /// </summary>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes the buffer state between host and device.
    /// </summary>
    public void Synchronize();

    /// <summary>
    /// Asynchronously synchronizes the buffer state between host and device.
    /// </summary>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the buffer as modified on the host.
    /// </summary>
    public void MarkHostDirty();

    /// <summary>
    /// Marks the buffer as modified on the device.
    /// </summary>
    public void MarkDeviceDirty();

    // Copy Operations

    /// <summary>
    /// Copies data from a source memory into this buffer.
    /// </summary>
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this buffer to a destination memory.
    /// </summary>
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this buffer to another buffer.
    /// </summary>
    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this buffer to another buffer with specified ranges.
    /// </summary>
    public ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default);

    // Fill Operations

    /// <summary>
    /// Fills this buffer with a specified value.
    /// </summary>
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills a portion of this buffer with a specified value.
    /// </summary>
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default);

    // View and Slice Operations

    /// <summary>
    /// Creates a slice of this buffer.
    /// </summary>
    /// <param name="offset">The offset in elements.</param>
    /// <param name="length">The length of the slice in elements.</param>
    /// <returns>A slice of this buffer.</returns>
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length);

    /// <summary>
    /// Creates a view of this buffer with a different element type.
    /// </summary>
    /// <typeparam name="TNew">The new element type.</typeparam>
    /// <returns>A view of this buffer as the new type.</returns>
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged;
}

/// <summary>
/// Non-generic base interface for memory buffers.
/// </summary>
public interface IUnifiedMemoryBuffer : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the memory allocation options.
    /// </summary>
    public MemoryOptions Options { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed { get; }

    /// <summary>
    /// Gets the current state of the buffer.
    /// </summary>
    public BufferState State { get; }

    /// <summary>
    /// Copies data from a source memory into this buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source memory to copy from.</param>
    /// <param name="offset">The offset in bytes where to start copying.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies data from this buffer to a destination memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="destination">The destination memory to copy to.</param>
    /// <param name="offset">The offset in bytes where to start copying from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged;
}
