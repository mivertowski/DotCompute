// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Manages memory allocation and transfer between pipeline stages.
/// </summary>
public interface IPipelineMemoryManager : IAsyncDisposable
{
    /// <summary>
    /// Allocates memory for pipeline use.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="elementCount">Number of elements.</param>
    /// <param name="hint">Memory allocation hint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Allocated memory handle.</returns>
    public ValueTask<IPipelineMemory<T>> AllocateAsync<T>(
        long elementCount,
        MemoryHint hint = MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Allocates memory with a specific key for sharing between stages.
    /// </summary>
    public ValueTask<IPipelineMemory<T>> AllocateSharedAsync<T>(
        string key,
        long elementCount,
        MemoryHint hint = MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets shared memory by key.
    /// </summary>
    public IPipelineMemory<T>? GetShared<T>(string key) where T : unmanaged;

    /// <summary>
    /// Transfers memory ownership between stages.
    /// </summary>
    public ValueTask TransferAsync<T>(
        IPipelineMemory<T> memory,
        string fromStage,
        string toStage,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Creates a memory view (non-owning reference).
    /// </summary>
    public IPipelineMemoryView<T> CreateView<T>(
        IPipelineMemory<T> memory,
        long offset = 0,
        long? length = null) where T : unmanaged;

    /// <summary>
    /// Optimizes memory layout for better access patterns.
    /// </summary>
    public ValueTask<IPipelineMemory<T>> OptimizeLayoutAsync<T>(
        IPipelineMemory<T> memory,
        MemoryLayoutHint layoutHint,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets current memory usage statistics.
    /// </summary>
    public MemoryManagerStats GetStats();

    /// <summary>
    /// Forces garbage collection of unused memory.
    /// </summary>
    public ValueTask CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a memory pool for efficient allocation.
    /// </summary>
    public void RegisterPool<T>(MemoryPoolOptions options) where T : unmanaged;
}

/// <summary>
/// Represents allocated memory in the pipeline.
/// </summary>
public interface IPipelineMemory<T> : IAsyncDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the memory identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the element count.
    /// </summary>
    public long ElementCount { get; }

    /// <summary>
    /// Gets the size in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets whether the memory is currently locked.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Gets the memory access mode.
    /// </summary>
    public Memory.MemoryAccess AccessMode { get; }

    /// <summary>
    /// Gets the device where memory is allocated.
    /// </summary>
    public IComputeDevice Device { get; }

    /// <summary>
    /// Locks the memory for exclusive access.
    /// </summary>
    public ValueTask<MemoryLock<T>> LockAsync(
        MemoryLockMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data to this memory.
    /// </summary>
    public ValueTask CopyFromAsync(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this memory.
    /// </summary>
    public ValueTask CopyToAsync(
        Memory<T> destination,
        long offset = 0,
        int? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a sub-region view of this memory.
    /// </summary>
    public IPipelineMemoryView<T> Slice(long offset, long length);

    /// <summary>
    /// Ensures memory is synchronized across devices.
    /// </summary>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-owning view of pipeline memory.
/// </summary>
public interface IPipelineMemoryView<T> where T : unmanaged
{
    /// <summary>
    /// Gets the offset in the parent memory.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the view length.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the parent memory.
    /// </summary>
    public IPipelineMemory<T> Parent { get; }

    /// <summary>
    /// Creates a sub-view.
    /// </summary>
    public IPipelineMemoryView<T> Slice(long offset, long length);
}

/// <summary>
/// Represents a lock on pipeline memory.
/// </summary>
public readonly struct MemoryLock<T> : IDisposable, IEquatable<MemoryLock<T>> where T : unmanaged
{
    private readonly IPipelineMemory<T> _memory;
    private readonly MemoryLockMode _mode;
    private readonly Action? _unlockAction;

    internal MemoryLock(IPipelineMemory<T> memory, MemoryLockMode mode, Action? unlockAction)
    {
        _memory = memory;
        _mode = mode;
        _unlockAction = unlockAction;
    }

    /// <summary>
    /// Gets the locked memory.
    /// </summary>
    public IPipelineMemory<T> Memory => _memory;

    /// <summary>
    /// Gets the lock mode.
    /// </summary>
    public MemoryLockMode Mode => _mode;

    /// <summary>
    /// Gets a span for direct memory access (if supported).
    /// </summary>
    public Span<T> GetSpan()
    {
        if (_mode == MemoryLockMode.ReadOnly)
        {
            throw new InvalidOperationException("Cannot get writable span for read-only lock.");
        }

        // Try to get span from memory if it supports direct access
        if (_memory is IPipelineMemoryWithDirectAccess<T> directAccessMemory)
        {
            return directAccessMemory.GetSpan();
        }

        // For memory types that don't support direct access (e.g., GPU memory),
        // we need to fail gracefully
        throw new NotSupportedException(
            $"Direct span access is not supported for memory type '{_memory.GetType().Name}'. " +
            "Use CopyToAsync/CopyFromAsync for data transfer instead.");
    }

    /// <summary>
    /// Gets a read-only span for direct memory access (if supported).
    /// </summary>
    public ReadOnlySpan<T> GetReadOnlySpan()
    {
        // Try to get read-only span from memory if it supports direct access
        if (_memory is IPipelineMemoryWithDirectAccess<T> directAccessMemory)
        {
            return directAccessMemory.GetReadOnlySpan();
        }

        // For memory types that don't support direct access (e.g., GPU memory),
        // we need to fail gracefully
        throw new NotSupportedException(
            $"Direct span access is not supported for memory type '{_memory.GetType().Name}'. " +
            "Use CopyToAsync for data retrieval instead.");
    }

    public void Dispose()
    {
        _unlockAction?.Invoke();
    }

    public override bool Equals(object? obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(MemoryLock<T> left, MemoryLock<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MemoryLock<T> left, MemoryLock<T> right)
    {
        return !(left == right);
    }

    public bool Equals(MemoryLock<T> other)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Memory lock modes.
/// </summary>
public enum MemoryLockMode
{
    /// <summary>
    /// Read-only access.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Read-write access.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Exclusive write access.
    /// </summary>
    Exclusive
}

/// <summary>
/// Memory layout optimization hints.
/// </summary>
public enum MemoryLayoutHint
{
    /// <summary>
    /// No specific layout optimization.
    /// </summary>
    None,

    /// <summary>
    /// Optimize for sequential access.
    /// </summary>
    Sequential,

    /// <summary>
    /// Optimize for strided access.
    /// </summary>
    Strided,

    /// <summary>
    /// Optimize for 2D tile access.
    /// </summary>
    Tiled2D,

    /// <summary>
    /// Optimize for 3D block access.
    /// </summary>
    Blocked3D,

    /// <summary>
    /// Pack data for minimal size.
    /// </summary>
    Packed,

    /// <summary>
    /// Align for SIMD operations.
    /// </summary>
    SimdAligned
}

/// <summary>
/// Memory manager statistics.
/// </summary>
public sealed class MemoryManagerStats
{
    /// <summary>
    /// Gets total allocated memory.
    /// </summary>
    public required long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Gets current used memory.
    /// </summary>
    public required long CurrentUsedBytes { get; init; }

    /// <summary>
    /// Gets peak memory usage.
    /// </summary>
    public required long PeakUsedBytes { get; init; }

    /// <summary>
    /// Gets number of active allocations.
    /// </summary>
    public required int ActiveAllocationCount { get; init; }

    /// <summary>
    /// Gets total allocation count.
    /// </summary>
    public required long TotalAllocationCount { get; init; }

    /// <summary>
    /// Gets cache hit rate.
    /// </summary>
    public required double CacheHitRate { get; init; }

    /// <summary>
    /// Gets pool efficiency.
    /// </summary>
    public required double PoolEfficiency { get; init; }

    /// <summary>
    /// Gets fragmentation percentage.
    /// </summary>
    public required double FragmentationPercentage { get; init; }
}

/// <summary>
/// Options for memory pool configuration.
/// </summary>
public sealed class MemoryPoolOptions
{
    /// <summary>
    /// Gets or sets the initial pool size.
    /// </summary>
    public int InitialSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum pool size.
    /// </summary>
    public int MaxSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the block size in bytes.
    /// </summary>
    public long BlockSize { get; set; }

    /// <summary>
    /// Gets or sets whether to pre-allocate blocks.
    /// </summary>
    public bool PreAllocate { get; set; }

    /// <summary>
    /// Gets or sets the retention policy.
    /// </summary>
    public PoolRetentionPolicy RetentionPolicy { get; set; } = PoolRetentionPolicy.Adaptive;
}

/// <summary>
/// Memory pool retention policies.
/// </summary>
public enum PoolRetentionPolicy
{
    /// <summary>
    /// Keep all allocated blocks.
    /// </summary>
    KeepAll,

    /// <summary>
    /// Release blocks after timeout.
    /// </summary>
    TimeBasedRelease,

    /// <summary>
    /// Keep most recently used blocks.
    /// </summary>
    LeastRecentlyUsed,

    /// <summary>
    /// Adaptive based on usage patterns.
    /// </summary>
    Adaptive
}

/// <summary>
/// Interface for pipeline memory implementations that support direct span access.
/// This is typically only supported for host (CPU) memory, not GPU memory.
/// </summary>
public interface IPipelineMemoryWithDirectAccess<T> : IPipelineMemory<T> where T : unmanaged
{
    /// <summary>
    /// Gets a direct span to the underlying memory for read-write access.
    /// </summary>
    /// <remarks>
    /// This method should only be called when holding an appropriate memory lock.
    /// </remarks>
    public Span<T> GetSpan();

    /// <summary>
    /// Gets a direct read-only span to the underlying memory.
    /// </summary>
    /// <remarks>
    /// This method should only be called when holding an appropriate memory lock.
    /// </remarks>
    public ReadOnlySpan<T> GetReadOnlySpan();
}
