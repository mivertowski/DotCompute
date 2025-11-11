// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Device;

namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Provides comprehensive information about a compute device's capabilities,
    /// including performance characteristics, supported features, and data types.
    /// </summary>
    public interface IDeviceCapabilities
    {
        /// <summary>
        /// Gets the compute capability version of the device.
        /// </summary>
        /// <value>The version indicating the level of compute capability support.</value>
        public Version ComputeCapability { get; }

        /// <summary>
        /// Gets the maximum number of work items that can be grouped together.
        /// </summary>
        /// <value>The maximum work group size supported by the device.</value>
        public int MaxWorkGroupSize { get; }

        /// <summary>
        /// Gets the maximum number of dimensions for work item indexing.
        /// </summary>
        /// <value>The maximum number of work item dimensions (typically 3).</value>
        public int MaxWorkItemDimensions { get; }

        /// <summary>
        /// Gets the maximum work item sizes for each dimension.
        /// </summary>
        /// <value>A read-only list containing the maximum work item size for each dimension.</value>
        public IReadOnlyList<long> MaxWorkItemSizes { get; }

        /// <summary>
        /// Gets the number of compute units (cores/processors) available on the device.
        /// </summary>
        /// <value>The number of parallel compute units.</value>
        public int ComputeUnits { get; }

        /// <summary>
        /// Gets the base clock frequency of the device in MHz.
        /// </summary>
        /// <value>The clock frequency in megahertz.</value>
        public int ClockFrequency { get; }

        /// <summary>
        /// Gets a bitfield of all supported device features.
        /// </summary>
        /// <value>A combination of feature flags indicating supported capabilities.</value>
        public DeviceFeatures SupportedFeatures { get; }

        /// <summary>
        /// Gets a bitfield of all supported data types.
        /// </summary>
        /// <value>A combination of data type flags indicating supported numeric types.</value>
        public DataTypeSupport SupportedDataTypes { get; }

        /// <summary>
        /// Checks if a specific feature is supported by the device.
        /// </summary>
        /// <param name="feature">The feature to check for support.</param>
        /// <returns>True if the feature is supported, false otherwise.</returns>
        public bool IsFeatureSupported(DeviceFeature feature);

        /// <summary>
        /// Gets whether the device supports nanosecond-precision hardware timers.
        /// </summary>
        /// <value>
        /// True if the device supports nanosecond timers (e.g., CUDA %%globaltimer on CC 6.0+),
        /// false if only microsecond precision is available (e.g., CUDA events on CC &lt; 6.0).
        /// </value>
        /// <remarks>
        /// <para>
        /// Nanosecond precision is available on:
        /// <list type="bullet">
        /// <item><description>CUDA: Compute Capability 6.0+ (%%globaltimer register)</description></item>
        /// <item><description>OpenCL: Platform-dependent (typically microsecond precision)</description></item>
        /// <item><description>CPU: Depends on Stopwatch resolution (~100ns typical)</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public bool SupportsNanosecondTimers { get; }

        /// <summary>
        /// Gets the hardware timer resolution in nanoseconds.
        /// </summary>
        /// <value>
        /// The minimum measurable time interval in nanoseconds. Lower values indicate higher precision.
        /// Typical values:
        /// <list type="bullet">
        /// <item><description>CUDA (CC 6.0+): 1 ns (%%globaltimer)</description></item>
        /// <item><description>CUDA (CC &lt; 6.0): 1,000 ns (CUDA events)</description></item>
        /// <item><description>OpenCL: 1,000 ns (clock() built-in)</description></item>
        /// <item><description>CPU: ~100 ns (Stopwatch)</description></item>
        /// </list>
        /// </value>
        public long TimerResolutionNanos { get; }

        /// <summary>
        /// Gets the GPU hardware clock frequency in Hertz (cycles per second).
        /// </summary>
        /// <value>
        /// The timer clock frequency in Hz. Typical values:
        /// <list type="bullet">
        /// <item><description>CUDA (nanosecond timer): 1,000,000,000 Hz (1 GHz)</description></item>
        /// <item><description>CUDA (event timer): 1,000,000 Hz (1 MHz)</description></item>
        /// <item><description>OpenCL: Platform-dependent</description></item>
        /// <item><description>CPU: Stopwatch.Frequency (typically 10 MHz)</description></item>
        /// </list>
        /// </value>
        /// <remarks>
        /// This frequency determines the timer resolution: resolution = 1 / frequency.
        /// A 1 GHz clock provides 1 ns resolution.
        /// </remarks>
        public long ClockFrequencyHz { get; }

        /// <summary>
        /// Gets whether the device supports thread-block level barrier synchronization.
        /// </summary>
        /// <value>
        /// True if the device supports thread-block barriers (e.g., __syncthreads() in CUDA).
        /// This is typically true for all modern GPU devices.
        /// </value>
        /// <remarks>
        /// Thread-block barriers are the most common synchronization primitive, available
        /// on virtually all GPU compute devices (CUDA CC 1.0+, OpenCL 1.0+).
        /// </remarks>
        public bool SupportsThreadBlockBarriers { get; }

        /// <summary>
        /// Gets whether the device supports grid-wide barrier synchronization across all thread blocks.
        /// </summary>
        /// <value>
        /// True if the device supports grid-wide barriers via cooperative kernel launch.
        /// Requires CUDA Compute Capability 6.0+ (Pascal or newer).
        /// </value>
        /// <remarks>
        /// <para>
        /// Grid-wide barriers enable synchronization across all threads in a kernel launch,
        /// essential for global reduction operations and multi-phase algorithms.
        /// </para>
        /// <para>
        /// Platform support:
        /// <list type="bullet">
        /// <item><description>CUDA: CC 6.0+ with cooperative launch</description></item>
        /// <item><description>OpenCL: Work-group barriers only (no grid-wide support)</description></item>
        /// <item><description>CPU: Emulated via thread synchronization</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public bool SupportsGridBarriers { get; }

        /// <summary>
        /// Gets whether the device supports named barriers (multiple barriers per thread block).
        /// </summary>
        /// <value>
        /// True if named barriers are supported. Requires CUDA CC 11.0+ or equivalent.
        /// </value>
        /// <remarks>
        /// <para>
        /// Named barriers allow up to 16 distinct synchronization points per thread block,
        /// enabling complex multi-phase algorithms without barrier ID conflicts.
        /// </para>
        /// <para>
        /// Example: Phase 1 uses barrier "load", Phase 2 uses barrier "compute", Phase 3 uses "store".
        /// </para>
        /// </remarks>
        public bool SupportsNamedBarriers { get; }

        /// <summary>
        /// Gets the maximum number of named barriers supported per thread block.
        /// </summary>
        /// <value>
        /// The maximum number of concurrent barriers. Typical values:
        /// <list type="bullet">
        /// <item><description>CUDA (CC 11.0+): 16 barriers per block</description></item>
        /// <item><description>CUDA (CC &lt; 11.0): 1 barrier per block (unnamed only)</description></item>
        /// <item><description>OpenCL: 1 barrier per work-group</description></item>
        /// </list>
        /// </value>
        /// <remarks>
        /// A value of 1 indicates only anonymous barriers are supported (single barrier per block).
        /// Values &gt; 1 indicate named barrier support with multiple concurrent barriers.
        /// </remarks>
        public int MaxNamedBarriersPerBlock { get; }

        /// <summary>
        /// Gets the maximum grid size (total threads) that can participate in a grid-wide barrier.
        /// </summary>
        /// <value>
        /// Maximum number of threads for cooperative launch, or 0 if grid barriers not supported.
        /// Calculated as: multiProcessorCount × maxThreadsPerMultiProcessor.
        /// </value>
        /// <remarks>
        /// <para>
        /// This limit is determined by the device's SM (Streaming Multiprocessor) resources.
        /// Exceeding this limit will cause cooperative launch to fail.
        /// </para>
        /// <para>
        /// Typical values by architecture:
        /// <list type="bullet">
        /// <item><description>Pascal (CC 6.0): ~50,000 threads</description></item>
        /// <item><description>Volta (CC 7.0): ~80,000 threads</description></item>
        /// <item><description>Ampere (CC 8.0): ~100,000 threads</description></item>
        /// <item><description>Ada Lovelace (CC 8.9): ~120,000 threads</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public int MaxCooperativeGridSize { get; }

        /// <summary>
        /// Gets whether the device supports warp-level synchronization primitives.
        /// </summary>
        /// <value>
        /// True if warp-level sync is available (e.g., __syncwarp() in CUDA CC 7.0+).
        /// </value>
        /// <remarks>
        /// <para>
        /// Warp-level synchronization provides ultra-fast (~1ns) barriers for the 32 threads
        /// within a CUDA warp, enabling efficient warp-synchronous programming patterns.
        /// </para>
        /// <para>
        /// Note: Prior to CUDA CC 7.0, warps were implicitly synchronous (lockstep execution).
        /// CC 7.0+ introduced independent thread scheduling, requiring explicit __syncwarp().
        /// </para>
        /// </remarks>
        public bool SupportsWarpBarriers { get; }

        /// <summary>
        /// Gets whether the device supports hardware acquire-release memory ordering.
        /// </summary>
        /// <value>
        /// True if the device supports native acquire-release semantics in hardware.
        /// Requires CUDA CC 7.0+ (Volta) or equivalent.
        /// </value>
        /// <remarks>
        /// <para>
        /// Acquire-release memory ordering provides causal consistency for lock-free algorithms:
        /// <list type="bullet">
        /// <item><description><strong>Release:</strong> All prior writes complete before the release operation</description></item>
        /// <item><description><strong>Acquire:</strong> All subsequent reads observe values after the acquire operation</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// When false, the provider emulates acquire-release using explicit fences,
        /// increasing overhead from 15% to 30-40%.
        /// </para>
        /// <para>
        /// Platform support:
        /// <list type="bullet">
        /// <item><description>CUDA: CC 7.0+ (Volta) native support</description></item>
        /// <item><description>CUDA: CC &lt; 7.0 emulated with __threadfence_*()</description></item>
        /// <item><description>OpenCL 2.0+: atomic_work_item_fence()</description></item>
        /// <item><description>CPU: Volatile + Interlocked operations</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public bool SupportsAcquireRelease { get; }

        /// <summary>
        /// Gets the default memory consistency model for the device.
        /// </summary>
        /// <value>
        /// The default consistency model (typically Relaxed for GPUs).
        /// </value>
        /// <remarks>
        /// <para>
        /// GPUs default to a relaxed memory model for maximum performance, where memory
        /// operations may be reordered freely. Applications requiring stronger guarantees
        /// must explicitly use fences or select a stricter consistency model.
        /// </para>
        /// <para>
        /// Typical defaults:
        /// <list type="bullet">
        /// <item><description>GPU (CUDA/OpenCL/Metal): Relaxed</description></item>
        /// <item><description>CPU: Varies by platform (often ReleaseAcquire or Sequential)</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public Abstractions.Memory.MemoryConsistencyModel DefaultMemoryConsistencyModel { get; }

        /// <summary>
        /// Gets whether the device supports system-wide memory fences for CPU-GPU synchronization.
        /// </summary>
        /// <value>
        /// True if system fences are supported (requires unified virtual addressing on CUDA).
        /// </value>
        /// <remarks>
        /// <para>
        /// System-wide fences ensure memory consistency across all processors in the system:
        /// <list type="bullet">
        /// <item><description>CPU threads see GPU writes</description></item>
        /// <item><description>Other GPUs see each other's writes</description></item>
        /// <item><description>Essential for zero-copy CPU-GPU data sharing</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Requirements:
        /// <list type="bullet">
        /// <item><description>CUDA: Unified Virtual Addressing (UVA), CC 2.0+</description></item>
        /// <item><description>OpenCL: Shared memory contexts (platform-dependent)</description></item>
        /// <item><description>Metal: Shared storage mode (Apple Silicon)</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> System fences are slowest (~200ns) due to PCIe latency.
        /// Use device-level fences when possible.
        /// </para>
        /// </remarks>
        public bool SupportsSystemWideFences { get; }

        /// <summary>
        /// Gets whether the device supports cooperative launch for grid-wide synchronization.
        /// </summary>
        /// <value>
        /// True if cooperative kernel launch is available (CUDA CC 6.0+).
        /// </value>
        /// <remarks>
        /// <para>
        /// Cooperative launch enables grid-wide barriers by guaranteeing all thread blocks
        /// execute concurrently on the device. This is a prerequisite for <see cref="SupportsGridBarriers"/>.
        /// </para>
        /// <para>
        /// Without cooperative launch, grid-wide barriers would deadlock because blocks
        /// execute sequentially when resources are limited.
        /// </para>
        /// </remarks>
        public bool SupportsCooperativeLaunch { get; }
    }
}
