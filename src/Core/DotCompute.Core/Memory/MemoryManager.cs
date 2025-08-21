// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// Provides unified memory management across different compute backends.
    /// </summary>
    public interface IMemoryManager : IAsyncDisposable
    {
        /// <summary>
        /// Creates a buffer with the specified size and location.
        /// </summary>
        public ValueTask<Abstractions.IBuffer<T>> CreateBufferAsync<T>(
            int elementCount,
            MemoryLocation location,
            MemoryAccess access = MemoryAccess.ReadWrite,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Creates a buffer from existing data.
        /// </summary>
        public ValueTask<Abstractions.IBuffer<T>> CreateBufferAsync<T>(
            ReadOnlyMemory<T> data,
            MemoryLocation location,
            MemoryAccess access = MemoryAccess.ReadWrite,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Copies data between buffers.
        /// </summary>
        public ValueTask CopyAsync<T>(
            Abstractions.IBuffer<T> source,
            Abstractions.IBuffer<T> destination,
            long sourceOffset = 0,
            long destinationOffset = 0,
            long? elementCount = null,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Gets memory usage statistics.
        /// </summary>
        public IMemoryStatistics GetStatistics();

        /// <summary>
        /// Gets available memory locations.
        /// </summary>
        public MemoryLocation[] AvailableLocations { get; }
    }

    // Note: IBuffer<T> interface moved to DotCompute.Abstractions to avoid conflicts
    // All buffer implementations should use DotCompute.Abstractions.IBuffer<T>

    /// <summary>
    /// Memory locations where buffers can be allocated.
    /// </summary>
    public enum MemoryLocation
    {
        /// <summary>
        /// Host (CPU) memory.
        /// </summary>
        Host,

        /// <summary>
        /// Device (GPU) memory.
        /// </summary>
        Device,

        /// <summary>
        /// Pinned host memory for faster transfers.
        /// </summary>
        HostPinned,

        /// <summary>
        /// Unified memory accessible by both host and device.
        /// </summary>
        Unified,

        /// <summary>
        /// Managed memory with automatic migration.
        /// </summary>
        Managed
    }

    /// <summary>
    /// Memory access modes.
    /// </summary>
    [Flags]
    public enum MemoryAccess
    {
        /// <summary>
        /// Read-only access.
        /// </summary>
        ReadOnly = 1,

        /// <summary>
        /// Write-only access.
        /// </summary>
        WriteOnly = 2,

        /// <summary>
        /// Read-write access.
        /// </summary>
        ReadWrite = ReadOnly | WriteOnly,

        /// <summary>
        /// Host access for debugging.
        /// </summary>
        HostAccess = 4
    }

    /// <summary>
    /// Memory mapping modes.
    /// </summary>
    public enum MemoryMapMode
    {
        /// <summary>
        /// Read-only mapping.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// Write-only mapping.
        /// </summary>
        WriteOnly,

        /// <summary>
        /// Read-write mapping.
        /// </summary>
        ReadWrite
    }

    /// <summary>
    /// Represents a mapped memory region.
    /// </summary>
    public interface IMemoryMapping<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Gets the mapped memory span.
        /// </summary>
        public Span<T> Span { get; }

        /// <summary>
        /// Gets the mapping mode.
        /// </summary>
        public MemoryMapMode Mode { get; }

        /// <summary>
        /// Gets whether the mapping is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Flushes any pending writes.
        /// </summary>
        public void Flush();
    }

    /// <summary>
    /// Memory usage statistics.
    /// </summary>
    public interface IMemoryStatistics
    {
        /// <summary>
        /// Gets total allocated memory in bytes.
        /// </summary>
        public long TotalAllocatedBytes { get; }

        /// <summary>
        /// Gets available memory in bytes.
        /// </summary>
        public long AvailableBytes { get; }

        /// <summary>
        /// Gets peak memory usage in bytes.
        /// </summary>
        public long PeakUsageBytes { get; }

        /// <summary>
        /// Gets allocation count.
        /// </summary>
        public int AllocationCount { get; }

        /// <summary>
        /// Gets memory fragmentation percentage.
        /// </summary>
        public double FragmentationPercentage { get; }

        /// <summary>
        /// Gets memory usage by location.
        /// </summary>
        public IReadOnlyDictionary<MemoryLocation, long> UsageByLocation { get; }
    }
}
