// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Core.Memory;

/// <summary>
/// Provides unified memory management across different compute backends.
/// </summary>
public interface IMemoryManager : IAsyncDisposable
{
    /// <summary>
    /// Creates a buffer with the specified size and location.
    /// </summary>
    ValueTask<IBuffer<T>> CreateBufferAsync<T>(
        int elementCount,
        MemoryLocation location,
        MemoryAccess access = MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Creates a buffer from existing data.
    /// </summary>
    ValueTask<IBuffer<T>> CreateBufferAsync<T>(
        ReadOnlyMemory<T> data,
        MemoryLocation location,
        MemoryAccess access = MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies data between buffers.
    /// </summary>
    ValueTask CopyAsync<T>(
        IBuffer<T> source,
        IBuffer<T> destination,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long? elementCount = null,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    IMemoryStatistics GetStatistics();

    /// <summary>
    /// Gets available memory locations.
    /// </summary>
    MemoryLocation[] AvailableLocations { get; }
}

/// <summary>
/// Represents a typed memory buffer.
/// </summary>
public interface IBuffer<T> : IAsyncDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    int ElementCount { get; }

    /// <summary>
    /// Gets the size in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets the memory location.
    /// </summary>
    MemoryLocation Location { get; }

    /// <summary>
    /// Gets the access mode.
    /// </summary>
    MemoryAccess Access { get; }

    /// <summary>
    /// Reads data from the buffer.
    /// </summary>
    ValueTask<T[]> ReadAsync(
        int offset = 0,
        int? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    ValueTask WriteAsync(
        ReadOnlyMemory<T> data,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills the buffer with a value.
    /// </summary>
    ValueTask FillAsync(
        T value,
        int offset = 0,
        int? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps the buffer for direct access.
    /// </summary>
    ValueTask<IMemoryMapping<T>> MapAsync(
        MemoryMapMode mode = MemoryMapMode.ReadWrite,
        int offset = 0,
        int? count = null,
        CancellationToken cancellationToken = default);
}

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
    Span<T> Span { get; }

    /// <summary>
    /// Gets the mapping mode.
    /// </summary>
    MemoryMapMode Mode { get; }

    /// <summary>
    /// Gets whether the mapping is valid.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Flushes any pending writes.
    /// </summary>
    void Flush();
}

/// <summary>
/// Memory usage statistics.
/// </summary>
public interface IMemoryStatistics
{
    /// <summary>
    /// Gets total allocated memory in bytes.
    /// </summary>
    long TotalAllocatedBytes { get; }

    /// <summary>
    /// Gets available memory in bytes.
    /// </summary>
    long AvailableBytes { get; }

    /// <summary>
    /// Gets peak memory usage in bytes.
    /// </summary>
    long PeakUsageBytes { get; }

    /// <summary>
    /// Gets allocation count.
    /// </summary>
    int AllocationCount { get; }

    /// <summary>
    /// Gets memory fragmentation percentage.
    /// </summary>
    double FragmentationPercentage { get; }

    /// <summary>
    /// Gets memory usage by location.
    /// </summary>
    IReadOnlyDictionary<MemoryLocation, long> UsageByLocation { get; }
}