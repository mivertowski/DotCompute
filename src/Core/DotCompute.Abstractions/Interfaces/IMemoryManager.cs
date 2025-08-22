// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

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

        /// <summary>
        /// Allocates memory for a specific number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="count">The number of elements to allocate.</param>
        /// <returns>A memory buffer for the allocated elements.</returns>
        public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged;

        /// <summary>
        /// Copies data from host memory to a device buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="data">The source data span.</param>
        public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged;

        /// <summary>
        /// Copies data from a device buffer to host memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="data">The destination data span.</param>
        /// <param name="buffer">The source buffer.</param>
        public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged;

        /// <summary>
        /// Frees a memory buffer.
        /// </summary>
        /// <param name="buffer">The buffer to free.</param>
        public void Free(IMemoryBuffer buffer);
    }

    /// <summary>
    /// Represents a memory buffer on an accelerator.
    /// </summary>
    public interface IMemoryBuffer : IAsyncDisposable, IDisposable
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
        /// Gets whether the buffer has been disposed.
        /// </summary>
        public bool IsDisposed { get; }

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
    /// Specifies memory allocation and management options.
    /// </summary>
    [Flags]
    public enum MemoryOptions
    {
        /// <summary>
        /// No specific options.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pin memory to prevent garbage collection movement.
        /// </summary>
        Pinned = 1,

        /// <summary>
        /// Align memory for optimal performance.
        /// </summary>
        Aligned = 2,

        /// <summary>
        /// Use write-combined memory for better streaming performance.
        /// </summary>
        WriteCombined = 4,

        /// <summary>
        /// Pre-allocate memory to avoid allocation overhead.
        /// </summary>
        PreAllocated = 8,

        /// <summary>
        /// Use persistent memory that survives context switches.
        /// </summary>
        Persistent = 16,

        /// <summary>
        /// Enable memory compression if supported.
        /// </summary>
        Compressed = 32,

        /// <summary>
        /// Use high-bandwidth memory if available.
        /// </summary>
        HighBandwidth = 64,

        /// <summary>
        /// Enable automatic memory migration between host and device.
        /// </summary>
        AutoMigrate = 128,

        /// <summary>
        /// Memory is read-only.
        /// </summary>
        ReadOnly = 256,

        /// <summary>
        /// Memory is write-only.
        /// </summary>
        WriteOnly = 512,

        /// <summary>
        /// Memory should be allocated in host-visible memory if possible.
        /// </summary>
        HostVisible = 1024,

        /// <summary>
        /// Memory should be cached if possible.
        /// </summary>
        Cached = 2048,

        /// <summary>
        /// Memory will be used for atomic operations.
        /// </summary>
        Atomic = 4096,

        /// <summary>
        /// Use lazy synchronization between host and device.
        /// </summary>
        LazySync = 8192,

        /// <summary>
        /// Prefer pooled allocation for better performance.
        /// </summary>
        PreferPooled = 16384,

        /// <summary>
        /// Clear memory securely when disposed.
        /// </summary>
        SecureClear = 32768
    }
}
