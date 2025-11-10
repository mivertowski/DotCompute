// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

// Import organized validation and benchmark types
global using DotCompute.Core.Memory.P2P.Types;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Backward compatibility facade for P2P validation and benchmark types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This file provides unified namespace access to validation and benchmark types.
    /// Types have been organized into focused files for maintainability:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Types.P2PValidationResults"/> - Validation result types and statistics</item>
    /// <item><see cref="Types.P2PBenchmarkTypes"/> - Benchmark options and result types</item>
    /// </list>
    /// <para>
    /// The global using directive ensures all types remain accessible in the
    /// DotCompute.Core.Memory.P2P namespace without breaking existing code.
    /// </para>
    /// <para>
    /// <see cref="MockBuffer{T}"/> remains in this file as an internal test utility.
    /// </para>
    /// </remarks>

    #region Mock Buffer Implementation

    /// <summary>
    /// Mock buffer implementation for testing P2P operations without real GPU hardware.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lightweight test double implementing <see cref="IUnifiedMemoryBuffer{T}"/> for
    /// unit testing P2P validation and transfer logic without GPU dependencies.
    /// </para>
    /// <para>
    /// All operations are no-ops that complete immediately. Useful for:
    /// - Unit testing P2P validation logic
    /// - CI/CD pipelines without GPU hardware
    /// - Algorithm development and debugging
    /// </para>
    /// </remarks>
    internal sealed class MockBuffer<T>(IAccelerator accelerator, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        /// <summary>
        /// Gets the buffer length in elements.
        /// </summary>
        public int Length { get; } = length;

        /// <summary>
        /// Gets the buffer size in bytes.
        /// </summary>
        public long SizeInBytes { get; } = length * Unsafe.SizeOf<T>();

        /// <summary>
        /// Gets the accelerator this buffer is associated with.
        /// </summary>
        public IAccelerator Accelerator { get; } = accelerator;

        /// <summary>
        /// Gets the memory options (always None for mock buffers).
        /// </summary>
        public MemoryOptions Options => MemoryOptions.None;

        /// <summary>
        /// Gets whether the buffer is disposed (always false for mock buffers).
        /// </summary>
        public bool IsDisposed => false;

        /// <summary>
        /// Gets or sets the current buffer state.
        /// </summary>
        public BufferState State { get; set; } = BufferState.HostReady;

        /// <summary>
        /// Gets whether the buffer is available on the host.
        /// </summary>
        public bool IsOnHost => State is BufferState.HostReady or BufferState.HostDirty;

        /// <summary>
        /// Gets whether the buffer is available on the device.
        /// </summary>
        public bool IsOnDevice => State is BufferState.DeviceReady or BufferState.DeviceDirty;

        /// <summary>
        /// Gets whether the buffer has unsynchronized modifications.
        /// </summary>
        public bool IsDirty => State is BufferState.HostDirty or BufferState.DeviceDirty;

        // Copy operations - all no-ops
        public static ValueTask CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public static ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public static Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => Task.CompletedTask;

        public static ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public static Task ClearAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IUnifiedMemoryBuffer<T> Slice(int offset, int count)
            => new MockBuffer<T>(Accelerator, count);

        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
            => new MockBuffer<TNew>(Accelerator, (int)(SizeInBytes / Unsafe.SizeOf<TNew>()));

        // Memory access methods - return empty spans/memory
        public Span<T> AsSpan() => [];
        public ReadOnlySpan<T> AsReadOnlySpan() => [];
        public Memory<T> AsMemory() => Memory<T>.Empty;
        public ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;

        // Synchronization methods
        public void EnsureOnHost() => State = BufferState.HostReady;
        public void EnsureOnDevice() => State = BufferState.DeviceReady;

        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }

        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }

        public void Synchronize() { }

        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public void MarkHostDirty() => State = BufferState.HostDirty;
        public void MarkDeviceDirty() => State = BufferState.DeviceDirty;

        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public MappedMemory<T> Map(MapMode mode) => MappedMemory<T>.Invalid;

        public MappedMemory<T> MapRange(int offset, int count, MapMode mode) => MappedMemory<T>.Invalid;

        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(MappedMemory<T>.Invalid);

        public ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long destinationOffset, CancellationToken cancellationToken = default) where TSource : unmanaged
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long sourceOffset, CancellationToken cancellationToken = default) where TDest : unmanaged
            => ValueTask.CompletedTask;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}
