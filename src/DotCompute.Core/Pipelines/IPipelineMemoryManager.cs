using System;
using System.Threading;
using System.Threading.Tasks;

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
    ValueTask<IPipelineMemory<T>> AllocateAsync<T>(
        long elementCount,
        MemoryHint hint = MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Allocates memory with a specific key for sharing between stages.
    /// </summary>
    ValueTask<IPipelineMemory<T>> AllocateSharedAsync<T>(
        string key,
        long elementCount,
        MemoryHint hint = MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets shared memory by key.
    /// </summary>
    IPipelineMemory<T>? GetShared<T>(string key) where T : unmanaged;

    /// <summary>
    /// Transfers memory ownership between stages.
    /// </summary>
    ValueTask TransferAsync<T>(
        IPipelineMemory<T> memory,
        string fromStage,
        string toStage,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Creates a memory view (non-owning reference).
    /// </summary>
    IPipelineMemoryView<T> CreateView<T>(
        IPipelineMemory<T> memory,
        long offset = 0,
        long? length = null) where T : unmanaged;

    /// <summary>
    /// Optimizes memory layout for better access patterns.
    /// </summary>
    ValueTask<IPipelineMemory<T>> OptimizeLayoutAsync<T>(
        IPipelineMemory<T> memory,
        MemoryLayoutHint layoutHint,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets current memory usage statistics.
    /// </summary>
    MemoryManagerStats GetStats();

    /// <summary>
    /// Forces garbage collection of unused memory.
    /// </summary>
    ValueTask CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a memory pool for efficient allocation.
    /// </summary>
    void RegisterPool<T>(MemoryPoolOptions options) where T : unmanaged;
}

/// <summary>
/// Represents allocated memory in the pipeline.
/// </summary>
public interface IPipelineMemory<T> : IAsyncDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the memory identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the element count.
    /// </summary>
    long ElementCount { get; }

    /// <summary>
    /// Gets the size in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets whether the memory is currently locked.
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    /// Gets the memory access mode.
    /// </summary>
    MemoryAccess AccessMode { get; }

    /// <summary>
    /// Gets the device where memory is allocated.
    /// </summary>
    IComputeDevice Device { get; }

    /// <summary>
    /// Locks the memory for exclusive access.
    /// </summary>
    ValueTask<MemoryLock<T>> LockAsync(
        MemoryLockMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data to this memory.
    /// </summary>
    ValueTask CopyFromAsync(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this memory.
    /// </summary>
    ValueTask CopyToAsync(
        Memory<T> destination,
        long offset = 0,
        int? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a sub-region view of this memory.
    /// </summary>
    IPipelineMemoryView<T> Slice(long offset, long length);

    /// <summary>
    /// Ensures memory is synchronized across devices.
    /// </summary>
    ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-owning view of pipeline memory.
/// </summary>
public interface IPipelineMemoryView<T> where T : unmanaged
{
    /// <summary>
    /// Gets the offset in the parent memory.
    /// </summary>
    long Offset { get; }

    /// <summary>
    /// Gets the view length.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets the parent memory.
    /// </summary>
    IPipelineMemory<T> Parent { get; }

    /// <summary>
    /// Creates a sub-view.
    /// </summary>
    IPipelineMemoryView<T> Slice(long offset, long length);
}

/// <summary>
/// Represents a lock on pipeline memory.
/// </summary>
public readonly struct MemoryLock<T> : IDisposable where T : unmanaged
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
            throw new InvalidOperationException("Cannot get writable span for read-only lock.");
        
        // Implementation would depend on the specific memory backend
        throw new NotImplementedException("GetSpan requires specific memory implementation.");
    }

    /// <summary>
    /// Gets a read-only span for direct memory access (if supported).
    /// </summary>
    public ReadOnlySpan<T> GetReadOnlySpan()
    {
        // Implementation would depend on the specific memory backend
        throw new NotImplementedException("GetReadOnlySpan requires specific memory implementation.");
    }

    public void Dispose()
    {
        _unlockAction?.Invoke();
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