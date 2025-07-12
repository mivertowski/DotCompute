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
    int Length { get; }
    
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    long SizeInBytes { get; }
    
    /// <summary>
    /// Gets whether the buffer is currently available on the host.
    /// </summary>
    bool IsOnHost { get; }
    
    /// <summary>
    /// Gets whether the buffer is currently available on the device.
    /// </summary>
    bool IsOnDevice { get; }
    
    /// <summary>
    /// Gets whether the buffer has been modified and needs synchronization.
    /// </summary>
    bool IsDirty { get; }
    
    /// <summary>
    /// Gets the buffer state for tracking transfers.
    /// </summary>
    BufferState State { get; }
    
    /// <summary>
    /// Gets a span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A span to the host memory.</returns>
    Span<T> AsSpan();
    
    /// <summary>
    /// Gets a read-only span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only span to the host memory.</returns>
    ReadOnlySpan<T> AsReadOnlySpan();
    
    /// <summary>
    /// Gets a memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A memory handle to the host memory.</returns>
    Memory<T> AsMemory();
    
    /// <summary>
    /// Gets a read-only memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only memory handle to the host memory.</returns>
    ReadOnlyMemory<T> AsReadOnlyMemory();
    
    /// <summary>
    /// Gets the device memory handle. Will trigger transfer if needed.
    /// </summary>
    /// <returns>The device memory handle.</returns>
    DeviceMemory GetDeviceMemory();
    
    /// <summary>
    /// Ensures the buffer is available on the host. Triggers transfer if needed.
    /// </summary>
    void EnsureOnHost();
    
    /// <summary>
    /// Ensures the buffer is available on the device. Triggers transfer if needed.
    /// </summary>
    void EnsureOnDevice();
    
    /// <summary>
    /// Asynchronously ensures the buffer is available on the host.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    ValueTask EnsureOnHostAsync(AcceleratorContext context = default);
    
    /// <summary>
    /// Asynchronously ensures the buffer is available on the device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default);
    
    /// <summary>
    /// Marks the buffer as modified on the host.
    /// </summary>
    void MarkHostDirty();
    
    /// <summary>
    /// Marks the buffer as modified on the device.
    /// </summary>
    void MarkDeviceDirty();
    
    /// <summary>
    /// Synchronizes the buffer state between host and device.
    /// </summary>
    void Synchronize();
    
    /// <summary>
    /// Asynchronously synchronizes the buffer state between host and device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    ValueTask SynchronizeAsync(AcceleratorContext context = default);
    
    /// <summary>
    /// Gets the underlying memory buffer. Required by CPU backend.
    /// </summary>
    /// <returns>A memory handle to the buffer.</returns>
    Memory<T> GetMemory();
    
    /// <summary>
    /// Copies data from a source memory into this buffer.
    /// </summary>
    /// <param name="source">The source data to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copies data from this buffer to a destination memory.
    /// </summary>
    /// <param name="destination">The destination memory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default);
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