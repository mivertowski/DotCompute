// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Pipelines.Bridge;

#region Memory Management Interfaces

/// <summary>
/// Memory manager for pipeline-specific memory allocation and management.
/// </summary>
public interface IPipelineMemoryManager : IDisposable
{
    /// <summary>
    /// Allocates a unified buffer with the specified name and size.
    /// </summary>
    /// <typeparam name="T">The element type for the buffer</typeparam>
    /// <param name="name">The name identifier for the buffer</param>
    /// <param name="size">The number of elements to allocate</param>
    /// <returns>A unified buffer instance</returns>
    IUnifiedBuffer<T> AllocateBuffer<T>(string name, int size) where T : unmanaged;

    /// <summary>
    /// Gets an existing buffer by name.
    /// </summary>
    /// <typeparam name="T">The element type for the buffer</typeparam>
    /// <param name="name">The name identifier for the buffer</param>
    /// <returns>The unified buffer instance</returns>
    IUnifiedBuffer<T> GetBuffer<T>(string name) where T : unmanaged;

    /// <summary>
    /// Releases a buffer by name.
    /// </summary>
    /// <param name="name">The name identifier for the buffer to release</param>
    void ReleaseBuffer(string name);

    /// <summary>
    /// Gets the sizes of all managed buffers.
    /// </summary>
    /// <returns>A dictionary mapping buffer names to their sizes in bytes</returns>
    IReadOnlyDictionary<string, long> GetBufferSizes();
}

/// <summary>
/// Unified buffer interface that provides cross-device memory access.
/// </summary>
/// <typeparam name="T">The element type stored in the buffer</typeparam>
public interface IUnifiedBuffer<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the total size of the buffer in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets a span view of the buffer data.
    /// </summary>
    /// <returns>A span covering the buffer data</returns>
    Span<T> GetSpan();

    /// <summary>
    /// Gets a memory view of the buffer data.
    /// </summary>
    /// <returns>A memory instance covering the buffer data</returns>
    Memory<T> GetMemory();

    /// <summary>
    /// Copies data from a source span into this buffer.
    /// </summary>
    /// <param name="source">The source data to copy</param>
    void CopyFrom(ReadOnlySpan<T> source);

    /// <summary>
    /// Copies data from this buffer to a destination span.
    /// </summary>
    /// <param name="destination">The destination span to copy to</param>
    void CopyTo(Span<T> destination);

    /// <summary>
    /// Asynchronously copies data from another unified buffer.
    /// </summary>
    /// <param name="source">The source buffer to copy from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the copy operation</returns>
    Task CopyFromAsync(IUnifiedBuffer<T> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously copies data to another unified buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to copy to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the copy operation</returns>
    Task CopyToAsync(IUnifiedBuffer<T> destination, CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic base interface for unified buffers.
/// </summary>
public interface IUnifiedBuffer : IDisposable
{
    /// <summary>
    /// Gets the total size of the buffer in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}

/// <summary>
/// Memory usage statistics for tracking allocation and usage patterns.
/// </summary>
public sealed class MemoryUsageStats
{
    /// <summary>
    /// Gets or sets the current memory usage in bytes.
    /// </summary>
    public required long CurrentUsage { get; init; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public required long PeakUsage { get; init; }

    /// <summary>
    /// Gets or sets the total allocated memory in bytes.
    /// </summary>
    public required long TotalAllocated { get; init; }

    /// <summary>
    /// Gets or sets the total deallocated memory in bytes.
    /// </summary>
    public required long TotalDeallocated { get; init; }

    /// <summary>
    /// Gets or sets the number of active allocations.
    /// </summary>
    public required int ActiveAllocations { get; init; }

    /// <summary>
    /// Gets or sets the total number of allocations performed.
    /// </summary>
    public required long TotalAllocations { get; init; }

    /// <summary>
    /// Gets or sets the memory utilization percentage.
    /// </summary>
    public required double UtilizationPercentage { get; init; }

    /// <summary>
    /// Gets or sets the memory fragmentation percentage.
    /// </summary>
    public required double FragmentationPercentage { get; init; }
}

#endregion

#region Device Management Interfaces

/// <summary>
/// Interface representing a compute device (CPU, GPU, etc.).
/// </summary>
public interface IComputeDevice : IDisposable
{
    /// <summary>
    /// Gets the unique device identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable device name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the device type.
    /// </summary>
    DeviceType Type { get; }

    /// <summary>
    /// Gets whether the device is available for use.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the total device memory in bytes.
    /// </summary>
    long TotalMemory { get; }

    /// <summary>
    /// Gets the available device memory in bytes.
    /// </summary>
    long AvailableMemory { get; }

    /// <summary>
    /// Gets the number of compute units (cores, SMs, etc.).
    /// </summary>
    int ComputeUnits { get; }

    /// <summary>
    /// Gets device-specific properties and capabilities.
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }

    /// <summary>
    /// Gets the device capabilities.
    /// </summary>
    DeviceCapabilities Capabilities { get; }
}

/// <summary>
/// Types of compute devices.
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// Central Processing Unit.
    /// </summary>
    CPU,

    /// <summary>
    /// Graphics Processing Unit (NVIDIA, AMD).
    /// </summary>
    GPU,

    /// <summary>
    /// Apple Metal Performance Shaders device.
    /// </summary>
    Metal,

    /// <summary>
    /// ROCm-compatible AMD device.
    /// </summary>
    ROCm,

    /// <summary>
    /// OpenCL-compatible device.
    /// </summary>
    OpenCL,

    /// <summary>
    /// Vulkan Compute-compatible device.
    /// </summary>
    Vulkan,

    /// <summary>
    /// FPGA or other accelerator device.
    /// </summary>
    Accelerator,

    /// <summary>
    /// Custom or unknown device type.
    /// </summary>
    Custom
}

/// <summary>
/// Device capabilities and feature support information.
/// </summary>
public sealed class DeviceCapabilities
{
    /// <summary>
    /// Gets or sets whether the device supports double-precision floating point.
    /// </summary>
    public required bool SupportsDoubles { get; init; }

    /// <summary>
    /// Gets or sets whether the device supports atomic operations.
    /// </summary>
    public required bool SupportsAtomics { get; init; }

    /// <summary>
    /// Gets or sets whether the device supports image/texture operations.
    /// </summary>
    public required bool SupportsImages { get; init; }

    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public required int MaxWorkGroupSize { get; init; }

    /// <summary>
    /// Gets or sets the local memory size in bytes.
    /// </summary>
    public required long LocalMemorySize { get; init; }

    /// <summary>
    /// Gets or sets whether the device supports unified memory.
    /// </summary>
    public bool SupportsUnifiedMemory { get; init; } = false;

    /// <summary>
    /// Gets or sets whether the device supports peer-to-peer memory access.
    /// </summary>
    public bool SupportsPeerToPeer { get; init; } = false;

    /// <summary>
    /// Gets or sets the compute capability version (for CUDA devices).
    /// </summary>
    public string? ComputeCapability { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of work groups per dimension.
    /// </summary>
    public int[]? MaxWorkGroupDimensions { get; init; }

    /// <summary>
    /// Gets or sets the maximum memory allocation size in bytes.
    /// </summary>
    public long? MaxMemoryAllocationSize { get; init; }

    /// <summary>
    /// Gets or sets the memory bandwidth in GB/s.
    /// </summary>
    public double? MemoryBandwidthGBps { get; init; }

    /// <summary>
    /// Gets or sets the theoretical peak compute performance in GFLOPS.
    /// </summary>
    public double? PeakComputeGFLOPS { get; init; }

    /// <summary>
    /// Gets or sets additional device-specific capability flags.
    /// </summary>
    public IReadOnlyDictionary<string, bool>? AdditionalCapabilities { get; init; }
}

#endregion

#region Memory Extension Types

/// <summary>
/// Extension of the TimeSpan struct to support additional operations needed for data transfer timing.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Gets the total bytes transferred based on a time span and transfer rate.
    /// </summary>
    /// <param name="timeSpan">The time span of the transfer</param>
    /// <param name="bytesPerSecond">The transfer rate in bytes per second</param>
    /// <returns>The total bytes that could be transferred</returns>
    public static long TotalBytes(this TimeSpan timeSpan, long bytesPerSecond)
    {
        return (long)(timeSpan.TotalSeconds * bytesPerSecond);
    }

    /// <summary>
    /// Gets the total bytes for a collection of time spans assuming a default rate.
    /// </summary>
    /// <param name="timeSpans">The collection of time spans</param>
    /// <returns>The sum of bytes from all time spans</returns>
    public static long TotalBytes(this IEnumerable<TimeSpan> timeSpans)
    {
        // Default assumption: 1 GB/s transfer rate for calculation
        const long defaultBytesPerSecond = 1_000_000_000;
        return timeSpans.Sum(ts => ts.TotalBytes(defaultBytesPerSecond));
    }
}

/// <summary>
/// Memory allocation patterns for optimization.
/// </summary>
public enum MemoryAllocationPattern
{
    /// <summary>
    /// Allocate memory once and reuse.
    /// </summary>
    SingleAllocation,

    /// <summary>
    /// Multiple small allocations.
    /// </summary>
    MultipleSmallAllocations,

    /// <summary>
    /// Streaming allocation pattern.
    /// </summary>
    Streaming,

    /// <summary>
    /// Pool-based allocation.
    /// </summary>
    Pooled,

    /// <summary>
    /// On-demand allocation.
    /// </summary>
    OnDemand
}

/// <summary>
/// Memory access characteristics for optimization.
/// </summary>
public sealed class MemoryAccessCharacteristics
{
    /// <summary>
    /// Gets or sets the primary access pattern.
    /// </summary>
    public required DataAccessPattern PrimaryPattern { get; init; }

    /// <summary>
    /// Gets or sets the memory locality score (0-1, higher is better).
    /// </summary>
    public required double LocalityScore { get; init; }

    /// <summary>
    /// Gets or sets the cache hit ratio estimate (0-1).
    /// </summary>
    public required double CacheHitRatio { get; init; }

    /// <summary>
    /// Gets or sets the memory bandwidth utilization percentage.
    /// </summary>
    public required double BandwidthUtilization { get; init; }

    /// <summary>
    /// Gets or sets whether memory coalescing is possible.
    /// </summary>
    public required bool SupportsCoalescing { get; init; }

    /// <summary>
    /// Gets or sets the preferred memory alignment in bytes.
    /// </summary>
    public required int PreferredAlignment { get; init; }
}

/// <summary>
/// Memory optimization recommendations.
/// </summary>
public sealed class MemoryOptimizationRecommendation
{
    /// <summary>
    /// Gets or sets the recommendation type.
    /// </summary>
    public required MemoryOptimizationType Type { get; init; }

    /// <summary>
    /// Gets or sets the recommendation description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the expected memory savings in bytes.
    /// </summary>
    public required long ExpectedSavingsBytes { get; init; }

    /// <summary>
    /// Gets or sets the expected performance improvement percentage.
    /// </summary>
    public required double ExpectedPerformanceImprovement { get; init; }

    /// <summary>
    /// Gets or sets the implementation complexity.
    /// </summary>
    public required ImplementationComplexity Complexity { get; init; }

    /// <summary>
    /// Gets or sets specific implementation steps.
    /// </summary>
    public IReadOnlyList<string>? ImplementationSteps { get; init; }
}

/// <summary>
/// Types of memory optimizations.
/// </summary>
public enum MemoryOptimizationType
{
    /// <summary>
    /// Reduce memory allocation frequency.
    /// </summary>
    ReduceAllocations,

    /// <summary>
    /// Improve memory locality.
    /// </summary>
    ImproveLocality,

    /// <summary>
    /// Enable memory pooling.
    /// </summary>
    EnablePooling,

    /// <summary>
    /// Optimize memory alignment.
    /// </summary>
    OptimizeAlignment,

    /// <summary>
    /// Reduce memory fragmentation.
    /// </summary>
    ReduceFragmentation,

    /// <summary>
    /// Enable memory compression.
    /// </summary>
    EnableCompression,

    /// <summary>
    /// Use pinned memory for transfers.
    /// </summary>
    UsePinnedMemory,

    /// <summary>
    /// Implement memory prefetching.
    /// </summary>
    EnablePrefetching
}

/// <summary>
/// Implementation complexity levels.
/// </summary>
public enum ImplementationComplexity
{
    /// <summary>
    /// Trivial implementation.
    /// </summary>
    Trivial,

    /// <summary>
    /// Simple implementation.
    /// </summary>
    Simple,

    /// <summary>
    /// Moderate complexity.
    /// </summary>
    Moderate,

    /// <summary>
    /// Complex implementation.
    /// </summary>
    Complex,

    /// <summary>
    /// Very complex implementation.
    /// </summary>
    VeryComplex
}

#endregion

#region Device Discovery and Management

/// <summary>
/// Device discovery service for finding available compute devices.
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Discovers all available compute devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of discovered devices</returns>
    Task<IReadOnlyList<IComputeDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers devices of a specific type.
    /// </summary>
    /// <param name="deviceType">The type of devices to discover</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of devices of the specified type</returns>
    Task<IReadOnlyList<IComputeDevice>> DiscoverDevicesAsync(DeviceType deviceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default device for a given type.
    /// </summary>
    /// <param name="deviceType">The device type</param>
    /// <returns>The default device, or null if none available</returns>
    IComputeDevice? GetDefaultDevice(DeviceType deviceType);

    /// <summary>
    /// Gets a device by its identifier.
    /// </summary>
    /// <param name="deviceId">The device identifier</param>
    /// <returns>The device, or null if not found</returns>
    IComputeDevice? GetDevice(string deviceId);
}

/// <summary>
/// Device selection criteria for automatic device selection.
/// </summary>
public sealed class DeviceSelectionCriteria
{
    /// <summary>
    /// Gets or sets the preferred device types in order of preference.
    /// </summary>
    public IReadOnlyList<DeviceType>? PreferredTypes { get; init; }

    /// <summary>
    /// Gets or sets the minimum memory requirement in bytes.
    /// </summary>
    public long? MinimumMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets the minimum compute units requirement.
    /// </summary>
    public int? MinimumComputeUnits { get; init; }

    /// <summary>
    /// Gets or sets required capabilities.
    /// </summary>
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }

    /// <summary>
    /// Gets or sets whether to prefer devices with higher memory.
    /// </summary>
    public bool PreferHighMemory { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to prefer devices with more compute units.
    /// </summary>
    public bool PreferHighCompute { get; init; } = true;

    /// <summary>
    /// Gets or sets custom scoring criteria.
    /// </summary>
    public Func<IComputeDevice, double>? CustomScorer { get; init; }
}

/// <summary>
/// Device selector service for choosing optimal devices based on criteria.
/// </summary>
public interface IDeviceSelector
{
    /// <summary>
    /// Selects the best device based on the given criteria.
    /// </summary>
    /// <param name="criteria">The selection criteria</param>
    /// <param name="availableDevices">The available devices to choose from</param>
    /// <returns>The selected device, or null if none match the criteria</returns>
    IComputeDevice? SelectDevice(DeviceSelectionCriteria criteria, IReadOnlyList<IComputeDevice> availableDevices);

    /// <summary>
    /// Ranks devices based on the given criteria.
    /// </summary>
    /// <param name="criteria">The ranking criteria</param>
    /// <param name="availableDevices">The available devices to rank</param>
    /// <returns>Devices ranked from best to worst match</returns>
    IReadOnlyList<IComputeDevice> RankDevices(DeviceSelectionCriteria criteria, IReadOnlyList<IComputeDevice> availableDevices);
}

#endregion
/// <summary>
/// Data access patterns for optimization analysis.
/// </summary>
public enum DataAccessPattern
{
    /// <summary>
    /// Sequential access pattern.
    /// </summary>
    Sequential,

    /// <summary>
    /// Random access pattern.
    /// </summary>
    Random,

    /// <summary>
    /// Streaming access pattern.
    /// </summary>
    Streaming,

    /// <summary>
    /// Sparse access pattern.
    /// </summary>
    Sparse,

    /// <summary>
    /// Cache-friendly access pattern.
    /// </summary>
    CacheFriendly
}
