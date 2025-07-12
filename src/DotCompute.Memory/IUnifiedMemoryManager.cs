using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Memory.Benchmarks;

namespace DotCompute.Memory;

/// <summary>
/// Unified memory manager interface that bridges the gap between Core and Abstractions interfaces.
/// Provides both synchronous and asynchronous operations for maximum compatibility.
/// </summary>
public interface IUnifiedMemoryManager : IMemoryManager, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Creates a unified buffer with both host and device memory coordination.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements.</param>
    /// <param name="options">Memory options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unified buffer.</returns>
    public ValueTask<UnifiedBuffer<T>> CreateUnifiedBufferAsync<T>(
        int length,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Creates a unified buffer from existing data.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source data.</param>
    /// <param name="options">Memory options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unified buffer.</returns>
    public ValueTask<UnifiedBuffer<T>> CreateUnifiedBufferFromAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Gets the memory pool for the specified type.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The memory pool.</returns>
    public MemoryPool<T> GetPool<T>() where T : unmanaged;
    
    /// <summary>
    /// Gets memory statistics and performance metrics.
    /// </summary>
    /// <returns>Memory statistics.</returns>
    public MemoryManagerStats GetStats();
    
    /// <summary>
    /// Handles memory pressure by releasing unused resources.
    /// </summary>
    /// <param name="pressure">The memory pressure level (0.0 to 1.0).</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public ValueTask HandleMemoryPressureAsync(double pressure);
    
    /// <summary>
    /// Compacts all memory pools and releases unused memory.
    /// </summary>
    /// <returns>The number of bytes released.</returns>
    public ValueTask<long> CompactAsync();
    
    /// <summary>
    /// Runs performance benchmarks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Benchmark results.</returns>
    public ValueTask<MemoryBenchmarkResults> RunBenchmarksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Memory manager statistics.
/// </summary>
public readonly struct MemoryManagerStats : IEquatable<MemoryManagerStats>
{
    /// <summary>
    /// Total allocated bytes across all pools.
    /// </summary>
    public long TotalAllocatedBytes { get; init; }
    
    /// <summary>
    /// Total retained bytes in pools.
    /// </summary>
    public long TotalRetainedBytes { get; init; }
    
    /// <summary>
    /// Total number of allocations.
    /// </summary>
    public long TotalAllocations { get; init; }
    
    /// <summary>
    /// Total number of buffer reuses.
    /// </summary>
    public long TotalReuses { get; init; }
    
    /// <summary>
    /// Overall efficiency ratio.
    /// </summary>
    public double EfficiencyRatio { get; init; }
    
    /// <summary>
    /// Available device memory.
    /// </summary>
    public long AvailableDeviceMemory { get; init; }
    
    /// <summary>
    /// Total device memory.
    /// </summary>
    public long TotalDeviceMemory { get; init; }
    
    /// <summary>
    /// Number of active unified buffers.
    /// </summary>
    public int ActiveUnifiedBuffers { get; init; }
    
    /// <summary>
    /// Number of active memory pools.
    /// </summary>
    public int ActiveMemoryPools { get; init; }
    
    /// <summary>
    /// Memory utilization percentage.
    /// </summary>
    public double MemoryUtilization => TotalDeviceMemory > 0 ? 
        (double)(TotalDeviceMemory - AvailableDeviceMemory) / TotalDeviceMemory : 0.0;
    
    /// <summary>
    /// Pool efficiency percentage.
    /// </summary>
    public double PoolEfficiency => TotalAllocations > 0 ? 
        (double)TotalReuses / TotalAllocations : 0.0;

    public override bool Equals(object? obj) => obj is MemoryManagerStats other && Equals(other);
    public bool Equals(MemoryManagerStats other)
    {
        return TotalAllocatedBytes == other.TotalAllocatedBytes &&
               TotalRetainedBytes == other.TotalRetainedBytes &&
               TotalAllocations == other.TotalAllocations &&
               TotalReuses == other.TotalReuses &&
               EfficiencyRatio.Equals(other.EfficiencyRatio) &&
               AvailableDeviceMemory == other.AvailableDeviceMemory &&
               TotalDeviceMemory == other.TotalDeviceMemory &&
               ActiveUnifiedBuffers == other.ActiveUnifiedBuffers &&
               ActiveMemoryPools == other.ActiveMemoryPools;
    }
    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(TotalAllocatedBytes, TotalRetainedBytes, TotalAllocations, TotalReuses),
        HashCode.Combine(EfficiencyRatio, AvailableDeviceMemory, TotalDeviceMemory),
        HashCode.Combine(ActiveUnifiedBuffers, ActiveMemoryPools));

    public static bool operator ==(MemoryManagerStats left, MemoryManagerStats right) => left.Equals(right);
    public static bool operator !=(MemoryManagerStats left, MemoryManagerStats right) => !left.Equals(right);
}