// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// Represents a unified buffer that can exist on both host and device memory.
/// Provides zero-copy operations and lazy transfer semantics.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IMemoryBuffer<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the length of the buffer in elements.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

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

    /// <summary>
    /// Gets the buffer state for tracking transfers.
    /// </summary>
    public BufferState State { get; }

    /// <summary>
    /// Gets a span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A span to the host memory.</returns>
    public Span<T> AsSpan();

    /// <summary>
    /// Gets a read-only span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only span to the host memory.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan();

    /// <summary>
    /// Gets a memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A memory handle to the host memory.</returns>
    public Memory<T> AsMemory();

    /// <summary>
    /// Gets a read-only memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only memory handle to the host memory.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory();

    /// <summary>
    /// Gets the device memory handle. Will trigger transfer if needed.
    /// </summary>
    /// <returns>The device memory handle.</returns>
    public DeviceMemory GetDeviceMemory();

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
    /// <param name="context">The accelerator context to use for the async operation.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures the buffer is available on the device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the buffer as modified on the host.
    /// </summary>
    public void MarkHostDirty();

    /// <summary>
    /// Marks the buffer as modified on the device.
    /// </summary>
    public void MarkDeviceDirty();

    /// <summary>
    /// Synchronizes the buffer state between host and device.
    /// </summary>
    public void Synchronize();

    /// <summary>
    /// Asynchronously synchronizes the buffer state between host and device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the underlying memory buffer. Required by CPU backend.
    /// </summary>
    /// <returns>A memory handle to the buffer.</returns>
    public Memory<T> GetMemory();

    /// <summary>
    /// Copies data from a source memory into this buffer.
    /// </summary>
    /// <param name="source">The source data to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this buffer to a destination memory.
    /// </summary>
    /// <param name="destination">The destination memory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the state of a memory buffer.
/// </summary>
public enum BufferState
{
    /// <summary>
    /// Buffer is uninitialized.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// Buffer is only on host.
    /// </summary>
    HostOnly,

    /// <summary>
    /// Buffer is only on device.
    /// </summary>
    DeviceOnly,

    /// <summary>
    /// Buffer is synchronized between host and device.
    /// </summary>
    Synchronized,

    /// <summary>
    /// Buffer is dirty on host (device copy is stale).
    /// </summary>
    HostDirty,

    /// <summary>
    /// Buffer is dirty on device (host copy is stale).
    /// </summary>
    DeviceDirty
}
