using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Models;

/// <summary>
/// Configuration options for memory management and allocation
/// </summary>
public sealed class MemoryOptions
{
    /// <summary>
    /// Gets or sets the memory optimization level
    /// </summary>
    public MemoryOptimizationLevel OptimizationLevel { get; set; } = MemoryOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets whether to enable memory pooling
    /// </summary>
    public bool EnablePooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the initial pool size in bytes
    /// </summary>
    public long InitialPoolSize { get; set; } = 64 * 1024 * 1024; // 64MB

    /// <summary>
    /// Gets or sets the maximum pool size in bytes
    /// </summary>
    public long MaxPoolSize { get; set; } = 512 * 1024 * 1024; // 512MB

    /// <summary>
    /// Gets or sets whether to enable memory prefetching
    /// </summary>
    public bool EnablePrefetching { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory access pattern hint
    /// </summary>
    public MemoryAccessPattern AccessPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>
    /// Gets or sets whether to use pinned memory for host allocations
    /// </summary>
    public bool UsePinnedMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets the alignment requirement for memory allocations
    /// </summary>
    public int Alignment { get; set; } = 64; // 64-byte alignment for SIMD

    /// <summary>
    /// Gets or sets whether to enable zero-copy operations where possible
    /// </summary>
    public bool EnableZeroCopy { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory transfer type preference
    /// </summary>
    public MemoryTransferType PreferredTransferType { get; set; } = MemoryTransferType.HostToDevice;

    /// <summary>
    /// Creates a new instance of MemoryOptions with default values
    /// </summary>
    public MemoryOptions() { }

    /// <summary>
    /// Creates a copy of the current MemoryOptions
    /// </summary>
    /// <returns>A new MemoryOptions instance with the same values</returns>
    public MemoryOptions Clone()
    {
        return new MemoryOptions
        {
            OptimizationLevel = OptimizationLevel,
            EnablePooling = EnablePooling,
            InitialPoolSize = InitialPoolSize,
            MaxPoolSize = MaxPoolSize,
            EnablePrefetching = EnablePrefetching,
            AccessPattern = AccessPattern,
            UsePinnedMemory = UsePinnedMemory,
            Alignment = Alignment,
            EnableZeroCopy = EnableZeroCopy,
            PreferredTransferType = PreferredTransferType
        };
    }
}
