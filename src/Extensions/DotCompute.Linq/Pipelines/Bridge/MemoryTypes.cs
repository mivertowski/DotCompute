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
    /// Gets an existing buffer by name.
    /// <returns>The unified buffer instance</returns>
    IUnifiedBuffer<T> GetBuffer<T>(string name) where T : unmanaged;
    /// Releases a buffer by name.
    /// <param name="name">The name identifier for the buffer to release</param>
    void ReleaseBuffer(string name);
    /// Gets the sizes of all managed buffers.
    /// <returns>A dictionary mapping buffer names to their sizes in bytes</returns>
    IReadOnlyDictionary<string, long> GetBufferSizes();
}
/// Unified buffer interface that provides cross-device memory access.
/// <typeparam name="T">The element type stored in the buffer</typeparam>
public interface IUnifiedBuffer<T> : IDisposable where T : unmanaged
    /// Gets the number of elements in the buffer.
    int Length { get; }
    /// Gets the total size of the buffer in bytes.
    long SizeInBytes { get; }
    /// Gets whether the buffer has been disposed.
    bool IsDisposed { get; }
    /// Gets a span view of the buffer data.
    /// <returns>A span covering the buffer data</returns>
    Span<T> GetSpan();
    /// Gets a memory view of the buffer data.
    /// <returns>A memory instance covering the buffer data</returns>
    Memory<T> GetMemory();
    /// Copies data from a source span into this buffer.
    /// <param name="source">The source data to copy</param>
    void CopyFrom(ReadOnlySpan<T> source);
    /// Copies data from this buffer to a destination span.
    /// <param name="destination">The destination span to copy to</param>
    void CopyTo(Span<T> destination);
    /// Asynchronously copies data from another unified buffer.
    /// <param name="source">The source buffer to copy from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the copy operation</returns>
    Task CopyFromAsync(IUnifiedBuffer<T> source, CancellationToken cancellationToken = default);
    /// Asynchronously copies data to another unified buffer.
    /// <param name="destination">The destination buffer to copy to</param>
    Task CopyToAsync(IUnifiedBuffer<T> destination, CancellationToken cancellationToken = default);
/// Non-generic base interface for unified buffers.
public interface IUnifiedBuffer : IDisposable
/// Memory usage statistics for tracking allocation and usage patterns.
public sealed class MemoryUsageStats
    /// Gets or sets the current memory usage in bytes.
    public required long CurrentUsage { get; init; }
    /// Gets or sets the peak memory usage in bytes.
    public required long PeakUsage { get; init; }
    /// Gets or sets the total allocated memory in bytes.
    public required long TotalAllocated { get; init; }
    /// Gets or sets the total deallocated memory in bytes.
    public required long TotalDeallocated { get; init; }
    /// Gets or sets the number of active allocations.
    public required int ActiveAllocations { get; init; }
    /// Gets or sets the total number of allocations performed.
    public required long TotalAllocations { get; init; }
    /// Gets or sets the memory utilization percentage.
    public required double UtilizationPercentage { get; init; }
    /// Gets or sets the memory fragmentation percentage.
    public required double FragmentationPercentage { get; init; }
    /// Gets the currently allocated bytes (alias for CurrentUsage).
    public long AllocatedBytes => CurrentUsage;
    /// Gets the peak allocated bytes (alias for PeakUsage).
    public long PeakBytes => PeakUsage;
    /// Gets the peak usage bytes (alias for PeakUsage).
    public long PeakUsageBytes => PeakUsage;
    /// Gets the average usage bytes (estimated from current usage).
    public long AverageUsageBytes => CurrentUsage;
    /// Gets the initial usage bytes (assumed to be 0).
    public long InitialUsageBytes => 0;
    /// Gets the final usage bytes (alias for CurrentUsage).
    public long FinalUsageBytes => CurrentUsage;
    /// Gets the total allocated bytes (alias for TotalAllocated).
    public long TotalAllocatedBytes => TotalAllocated;
    /// Gets the total deallocated bytes (alias for TotalDeallocated).
    public long TotalDeallocatedBytes => TotalDeallocated;
#endregion
#region Device Management Interfaces
/// Interface representing a compute device (CPU, GPU, etc.).
public interface IComputeDevice : IDisposable
    /// Gets the unique device identifier.
    string Id { get; }
    /// Gets the human-readable device name.
    string Name { get; }
    /// Gets the device type.
    DeviceType Type { get; }
    /// Gets whether the device is available for use.
    bool IsAvailable { get; }
    /// Gets the total device memory in bytes.
    long TotalMemory { get; }
    /// Gets the available device memory in bytes.
    long AvailableMemory { get; }
    /// Gets the number of compute units (cores, SMs, etc.).
    int ComputeUnits { get; }
    /// Gets device-specific properties and capabilities.
    IReadOnlyDictionary<string, object> Properties { get; }
    /// Gets the device capabilities.
    DeviceCapabilities Capabilities { get; }
/// Types of compute devices.
public enum DeviceType
    /// Central Processing Unit.
    CPU,
    /// Graphics Processing Unit (NVIDIA, AMD).
    GPU,
    /// Apple Metal Performance Shaders device.
    Metal,
    /// ROCm-compatible AMD device.
    ROCm,
    /// OpenCL-compatible device.
    OpenCL,
    /// Vulkan Compute-compatible device.
    Vulkan,
    /// FPGA or other accelerator device.
    Accelerator,
    /// Custom or unknown device type.
    Custom
/// Device capabilities and feature support information.
public sealed class DeviceCapabilities
    /// Gets or sets whether the device supports double-precision floating point.
    public required bool SupportsDoubles { get; init; }
    /// Gets or sets whether the device supports atomic operations.
    public required bool SupportsAtomics { get; init; }
    /// Gets or sets whether the device supports image/texture operations.
    public required bool SupportsImages { get; init; }
    /// Gets or sets the maximum work group size.
    public required int MaxWorkGroupSize { get; init; }
    /// Gets or sets the local memory size in bytes.
    public required long LocalMemorySize { get; init; }
    /// Gets or sets whether the device supports unified memory.
    public bool SupportsUnifiedMemory { get; init; } = false;
    /// Gets or sets whether the device supports peer-to-peer memory access.
    public bool SupportsPeerToPeer { get; init; } = false;
    /// Gets or sets the compute capability version (for CUDA devices).
    public string? ComputeCapability { get; init; }
    /// Gets or sets the maximum number of work groups per dimension.
    public int[]? MaxWorkGroupDimensions { get; init; }
    /// Gets or sets the maximum memory allocation size in bytes.
    public long? MaxMemoryAllocationSize { get; init; }
    /// Gets or sets the memory bandwidth in GB/s.
    public double? MemoryBandwidthGBps { get; init; }
    /// Gets or sets the theoretical peak compute performance in GFLOPS.
    public double? PeakComputeGFLOPS { get; init; }
    /// Gets or sets additional device-specific capability flags.
    public IReadOnlyDictionary<string, bool>? AdditionalCapabilities { get; init; }
#region Memory Extension Types
/// Extension of the TimeSpan struct to support additional operations needed for data transfer timing.
public static class TimeSpanExtensions
    /// Gets the total bytes transferred based on a time span and transfer rate.
    /// <param name="timeSpan">The time span of the transfer</param>
    /// <param name="bytesPerSecond">The transfer rate in bytes per second</param>
    /// <returns>The total bytes that could be transferred</returns>
    public static long TotalBytes(this TimeSpan timeSpan, long bytesPerSecond)
    {
        return (long)(timeSpan.TotalSeconds * bytesPerSecond);
    }
    /// Gets the total bytes for a collection of time spans assuming a default rate.
    /// <param name="timeSpans">The collection of time spans</param>
    /// <returns>The sum of bytes from all time spans</returns>
    public static long TotalBytes(this IEnumerable<TimeSpan> timeSpans)
        // Default assumption: 1 GB/s transfer rate for calculation
        const long defaultBytesPerSecond = 1_000_000_000;
        return timeSpans.Sum(ts => ts.TotalBytes(defaultBytesPerSecond));
/// Memory allocation patterns for optimization.
public enum MemoryAllocationPattern
    /// Allocate memory once and reuse.
    SingleAllocation,
    /// Multiple small allocations.
    MultipleSmallAllocations,
    /// Streaming allocation pattern.
    Streaming,
    /// Pool-based allocation.
    Pooled,
    /// On-demand allocation.
    OnDemand
/// Memory access characteristics for optimization.
public sealed class MemoryAccessCharacteristics
    /// Gets or sets the primary access pattern.
    public required DataAccessPattern PrimaryPattern { get; init; }
    /// Gets or sets the memory locality score (0-1, higher is better).
    public required double LocalityScore { get; init; }
    /// Gets or sets the cache hit ratio estimate (0-1).
    public required double CacheHitRatio { get; init; }
    /// Gets or sets the memory bandwidth utilization percentage.
    public required double BandwidthUtilization { get; init; }
    /// Gets or sets whether memory coalescing is possible.
    public required bool SupportsCoalescing { get; init; }
    /// Gets or sets the preferred memory alignment in bytes.
    public required int PreferredAlignment { get; init; }
/// Memory optimization recommendations.
public sealed class MemoryOptimizationRecommendation
    /// Gets or sets the recommendation type.
    public required MemoryOptimizationType Type { get; init; }
    /// Gets or sets the recommendation description.
    public required string Description { get; init; }
    /// Gets or sets the expected memory savings in bytes.
    public required long ExpectedSavingsBytes { get; init; }
    /// Gets or sets the expected performance improvement percentage.
    public required double ExpectedPerformanceImprovement { get; init; }
    /// Gets or sets the implementation complexity.
    public required ImplementationComplexity Complexity { get; init; }
    /// Gets or sets specific implementation steps.
    public IReadOnlyList<string>? ImplementationSteps { get; init; }
/// Types of memory optimizations.
public enum MemoryOptimizationType
    /// Reduce memory allocation frequency.
    ReduceAllocations,
    /// Improve memory locality.
    ImproveLocality,
    /// Enable memory pooling.
    EnablePooling,
    /// Optimize memory alignment.
    OptimizeAlignment,
    /// Reduce memory fragmentation.
    ReduceFragmentation,
    /// Enable memory compression.
    EnableCompression,
    /// Use pinned memory for transfers.
    UsePinnedMemory,
    /// Implement memory prefetching.
    EnablePrefetching
/// Implementation complexity levels.
public enum ImplementationComplexity
    /// Trivial implementation.
    Trivial,
    /// Simple implementation.
    Simple,
    /// Moderate complexity.
    Moderate,
    /// Complex implementation.
    Complex,
    /// Very complex implementation.
    VeryComplex
#region Device Discovery and Management
/// Device discovery service for finding available compute devices.
public interface IDeviceDiscoveryService
    /// Discovers all available compute devices.
    /// <returns>A list of discovered devices</returns>
    Task<IReadOnlyList<IComputeDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
    /// Discovers devices of a specific type.
    /// <param name="deviceType">The type of devices to discover</param>
    /// <returns>A list of devices of the specified type</returns>
    Task<IReadOnlyList<IComputeDevice>> DiscoverDevicesAsync(DeviceType deviceType, CancellationToken cancellationToken = default);
    /// Gets the default device for a given type.
    /// <param name="deviceType">The device type</param>
    /// <returns>The default device, or null if none available</returns>
    IComputeDevice? GetDefaultDevice(DeviceType deviceType);
    /// Gets a device by its identifier.
    /// <param name="deviceId">The device identifier</param>
    /// <returns>The device, or null if not found</returns>
    IComputeDevice? GetDevice(string deviceId);
/// Device selection criteria for automatic device selection.
public sealed class DeviceSelectionCriteria
    /// Gets or sets the preferred device types in order of preference.
    public IReadOnlyList<DeviceType>? PreferredTypes { get; init; }
    /// Gets or sets the minimum memory requirement in bytes.
    public long? MinimumMemoryBytes { get; init; }
    /// Gets or sets the minimum compute units requirement.
    public int? MinimumComputeUnits { get; init; }
    /// Gets or sets required capabilities.
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }
    /// Gets or sets whether to prefer devices with higher memory.
    public bool PreferHighMemory { get; init; } = true;
    /// Gets or sets whether to prefer devices with more compute units.
    public bool PreferHighCompute { get; init; } = true;
    /// Gets or sets custom scoring criteria.
    public Func<IComputeDevice, double>? CustomScorer { get; init; }
/// Device selector service for choosing optimal devices based on criteria.
public interface IDeviceSelector
    /// Selects the best device based on the given criteria.
    /// <param name="criteria">The selection criteria</param>
    /// <param name="availableDevices">The available devices to choose from</param>
    /// <returns>The selected device, or null if none match the criteria</returns>
    IComputeDevice? SelectDevice(DeviceSelectionCriteria criteria, IReadOnlyList<IComputeDevice> availableDevices);
    /// Ranks devices based on the given criteria.
    /// <param name="criteria">The ranking criteria</param>
    /// <param name="availableDevices">The available devices to rank</param>
    /// <returns>Devices ranked from best to worst match</returns>
    IReadOnlyList<IComputeDevice> RankDevices(DeviceSelectionCriteria criteria, IReadOnlyList<IComputeDevice> availableDevices);
}

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
