// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Health;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Profiling;
using DotCompute.Abstractions.Recovery;
using DotCompute.Abstractions.Timing;

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Represents a compute accelerator device.
    /// </summary>
    public interface IAccelerator : IAsyncDisposable
    {
        /// <summary>Gets device information.</summary>
        public AcceleratorInfo Info { get; }

        /// <summary>Gets the accelerator type.</summary>
        public AcceleratorType Type { get; }

        /// <summary>Gets the device type as a string (e.g., "CPU", "GPU", "TPU").</summary>
        public string DeviceType { get; }

        /// <summary>Gets memory manager for this accelerator.</summary>
        public IUnifiedMemoryManager Memory { get; }

        /// <summary>Gets the memory manager for this accelerator (alias for Memory).</summary>
        public IUnifiedMemoryManager MemoryManager { get; }

        /// <summary>Gets the accelerator context.</summary>
        public AcceleratorContext Context { get; }

        /// <summary>Gets whether the accelerator is available for use.</summary>
        public bool IsAvailable { get; }

        /// <summary>Compiles a kernel for execution.</summary>
        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Synchronizes all pending operations.</summary>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a comprehensive health snapshot of the device including sensor readings,
        /// health status, and performance metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the device health snapshot.</returns>
        /// <remarks>
        /// <para>
        /// This method queries the device for real-time health information including:
        /// - Temperature, power consumption, and utilization
        /// - Memory usage and availability
        /// - Error counts and device status
        /// - Throttling information
        /// </para>
        ///
        /// <para>
        /// <b>Performance:</b> Typically takes 1-10ms to collect all metrics.
        /// For high-frequency monitoring, consider caching snapshots for 100-500ms.
        /// </para>
        ///
        /// <para>
        /// <b>Orleans Integration:</b> Use for grain placement decisions and health monitoring.
        /// </para>
        /// </remarks>
        public ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current sensor readings from the device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the collection of available sensor readings.</returns>
        /// <remarks>
        /// This is a lighter-weight alternative to <see cref="GetHealthSnapshotAsync"/>
        /// when you only need raw sensor data without health scoring or status analysis.
        /// </remarks>
        public ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a comprehensive profiling snapshot of accelerator performance.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the profiling snapshot.</returns>
        /// <remarks>
        /// <para>
        /// This method provides detailed performance metrics including:
        /// - Kernel execution statistics (average, min, max, percentiles)
        /// - Memory operation statistics (bandwidth, transfer times)
        /// - Device utilization metrics
        /// - Performance trends and bottlenecks
        /// - Optimization recommendations
        /// </para>
        ///
        /// <para>
        /// <b>Performance:</b> Typically takes 1-5ms to collect all metrics.
        /// Profiling overhead is minimal (&lt;1%) when not actively querying.
        /// </para>
        ///
        /// <para>
        /// <b>Use Cases:</b> Performance optimization, backend selection, monitoring dashboards.
        /// </para>
        /// </remarks>
        public ValueTask<ProfilingSnapshot> GetProfilingSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current profiling metrics from the device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the collection of profiling metrics.</returns>
        /// <remarks>
        /// This is a lighter-weight alternative to <see cref="GetProfilingSnapshotAsync"/>
        /// when you only need raw metrics without statistics or recommendations.
        /// </remarks>
        public ValueTask<IReadOnlyList<ProfilingMetric>> GetProfilingMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the accelerator device to a clean state.
        /// </summary>
        /// <param name="options">Reset configuration options. If null, uses <see cref="ResetOptions.Default"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing detailed information about the reset operation.</returns>
        /// <remarks>
        /// <para>
        /// Device reset clears device state and optionally reinitializes resources.
        /// The extent of reset depends on <see cref="ResetOptions.ResetType"/>:
        /// </para>
        ///
        /// <list type="bullet">
        /// <item><see cref="ResetType.Soft"/>: Flush queues, clear pending operations (1-10ms)</item>
        /// <item><see cref="ResetType.Context"/>: Reset context, clear caches (10-50ms)</item>
        /// <item><see cref="ResetType.Hard"/>: Full reset, clear all memory (50-200ms)</item>
        /// <item><see cref="ResetType.Full"/>: Complete reinitialization (200-1000ms)</item>
        /// </list>
        ///
        /// <para>
        /// <b>Important</b>: <see cref="ResetType.Hard"/> and <see cref="ResetType.Full"/> invalidate
        /// all existing UnifiedBuffer instances. The application must recreate buffers
        /// after reset completes.
        /// </para>
        ///
        /// <para>
        /// <b>Orleans Integration</b>: Use during grain deactivation, error recovery, or when
        /// transferring device ownership between grains. Consider <see cref="ResetOptions.GrainDeactivation"/>
        /// for typical grain lifecycle scenarios.
        /// </para>
        ///
        /// <para>
        /// <b>Error Recovery</b>: When recovering from device errors or hangs, use
        /// <see cref="ResetOptions.ErrorRecovery"/> which forces a hard reset and clears all state.
        /// </para>
        ///
        /// <para>
        /// <b>Thread Safety</b>: Reset operations are thread-safe. Concurrent operations
        /// will wait for completion or be cancelled depending on <see cref="ResetOptions.WaitForCompletion"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="TimeoutException">Thrown if reset exceeds <see cref="ResetOptions.Timeout"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if reset cannot be performed (e.g., device lost).</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the accelerator has been disposed.</exception>
        public ValueTask<ResetResult> ResetAsync(ResetOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the timing provider for this accelerator, if supported.
        /// </summary>
        /// <returns>
        /// An <see cref="ITimingProvider"/> instance if the accelerator supports GPU-native timing,
        /// or null if timing features are not available for this device.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Timing provider availability depends on the backend and device capabilities:
        /// <list type="bullet">
        /// <item><description>CUDA (CC 6.0+): Nanosecond precision via %%globaltimer register</description></item>
        /// <item><description>CUDA (CC &lt; 6.0): Microsecond precision via CUDA events</description></item>
        /// <item><description>OpenCL: Platform-dependent (typically microsecond precision)</description></item>
        /// <item><description>CPU: Stopwatch-based timing (~100ns resolution)</description></item>
        /// <item><description>Metal: Platform-dependent timing support</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Usage Example:</strong>
        /// <code>
        /// var timingProvider = accelerator.GetTimingProvider();
        /// if (timingProvider != null)
        /// {
        ///     var timestamp = await timingProvider.GetGpuTimestampAsync();
        ///     var calibration = await timingProvider.CalibrateAsync(sampleCount: 100);
        ///     long cpuTime = calibration.GpuToCpuTime(timestamp);
        /// }
        /// else
        /// {
        ///     // Fallback to CPU-based timing
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public ITimingProvider? GetTimingProvider();
    }

    /// <summary>
    /// Represents information about an accelerator device.
    /// </summary>
    public class AcceleratorInfo
    {
        /// <summary>Gets or sets the unique identifier for this device.</summary>
        public required string Id { get; init; }

        /// <summary>Gets or sets the friendly name of this device.</summary>
        public required string Name { get; init; }

        /// <summary>Gets or sets the device type (e.g., "CPU", "GPU", "TPU").</summary>
        public required string DeviceType { get; init; }

        /// <summary>Gets or sets the vendor name.</summary>
        public required string Vendor { get; init; }

        /// <summary>Gets or sets the device type for legacy compatibility.</summary>
        public string Type => DeviceType;

        /// <summary>Gets or sets the driver version.</summary>
        public string? DriverVersion { get; init; }

        /// <summary>
        /// Legacy constructor for backward compatibility with tests.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(AcceleratorType type, string name, string driverVersion, long memorySize)
        {
            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = "Unknown";
            DriverVersion = driverVersion;
            TotalMemory = memorySize;
            AvailableMemory = memorySize;
            MaxSharedMemoryPerBlock = memorySize / 4;
            MaxMemoryAllocationSize = memorySize;
            LocalMemorySize = memorySize / 8;
            IsUnifiedMemory = type == AcceleratorType.CPU;
            MaxThreadsPerBlock = 1024; // Default value for legacy compatibility

            // Initialize new properties based on accelerator type

            MaxComputeUnits = type switch
            {
                AcceleratorType.CPU => Environment.ProcessorCount,
                AcceleratorType.GPU => 16, // Reasonable default for GPU
                _ => 8
            };
            GlobalMemorySize = memorySize;
            SupportsFloat64 = type == AcceleratorType.CPU; // CPUs typically have full double precision
            SupportsInt64 = true; // Most modern accelerators support 64-bit integers
        }

        /// <summary>
        /// Constructor for tests with full parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(string name, string vendor, string driverVersion, AcceleratorType type,
                              double computeCapability, int maxThreadsPerBlock, int maxSharedMemory,
                              long totalMemory, long availableMemory)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrEmpty(vendor))
            {
                throw new ArgumentException("Vendor cannot be null or empty", nameof(vendor));
            }

            if (string.IsNullOrEmpty(driverVersion))
            {
                throw new ArgumentException("DriverVersion cannot be null or empty", nameof(driverVersion));
            }

            if (computeCapability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(computeCapability), "ComputeCapability must be positive");
            }

            if (maxThreadsPerBlock <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxThreadsPerBlock), "MaxThreadsPerBlock must be positive");
            }

            if (maxSharedMemory < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSharedMemory), "MaxSharedMemory cannot be negative");
            }

            if (totalMemory <= 0 || availableMemory <= 0 || availableMemory > totalMemory)
            {
                throw new ArgumentException("Invalid memory sizes");
            }

            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = vendor;
            DriverVersion = driverVersion;
            TotalMemory = totalMemory;
            AvailableMemory = availableMemory;
            MaxSharedMemoryPerBlock = maxSharedMemory;
            MaxMemoryAllocationSize = totalMemory;
            LocalMemorySize = totalMemory / 8;
            IsUnifiedMemory = type == AcceleratorType.CPU;
            MaxThreadsPerBlock = maxThreadsPerBlock;
            ComputeCapability = new Version((int)computeCapability, (int)((computeCapability % 1) * 10));

            // Initialize new properties with sensible defaults

            MaxComputeUnits = 16; // Reasonable default
            GlobalMemorySize = totalMemory;
            SupportsFloat64 = true; // Assume support for modern devices
            SupportsInt64 = true;
        }

        /// <summary>
        /// Extended constructor for tests.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(AcceleratorType type, string name, string driverVersion, long memorySize,
                              int computeUnits, int maxClockFrequency, Version? computeCapability,
                              long maxSharedMemoryPerBlock, bool isUnifiedMemory)
        {
            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = "Unknown";
            DriverVersion = driverVersion;
            TotalMemory = memorySize;
            AvailableMemory = memorySize;
            MaxSharedMemoryPerBlock = maxSharedMemoryPerBlock;
            MaxMemoryAllocationSize = memorySize;
            LocalMemorySize = memorySize / 8;
            IsUnifiedMemory = isUnifiedMemory;
            ComputeUnits = computeUnits;
            MaxClockFrequency = maxClockFrequency;
            ComputeCapability = computeCapability;

            // Initialize new properties with sensible defaults

            MaxComputeUnits = computeUnits;
            GlobalMemorySize = memorySize;
            SupportsFloat64 = true; // Assume support unless specified otherwise
            SupportsInt64 = true;
        }

        /// <summary>Gets or sets the compute capability or version.</summary>
        public Version? ComputeCapability { get; init; }

        /// <summary>
        /// Gets the compute capability as a non-nullable version for test compatibility.
        /// Returns a default version (6.0) if ComputeCapability is null.
        /// </summary>
        public Version ComputeCapabilityVersionSafe => ComputeCapability ?? new Version(6, 0);

        /// <summary>Gets or sets the total device memory in bytes.</summary>
        public long TotalMemory { get; init; }

        /// <summary>Gets or sets the total device memory in bytes (alias for TotalMemory).</summary>
        public long MemorySize => TotalMemory;

        /// <summary>Gets or sets the available device memory in bytes.</summary>
        public long AvailableMemory { get; init; }

        /// <summary>Gets or sets the maximum shared memory per block in bytes.</summary>
        public long MaxSharedMemoryPerBlock { get; init; }

        /// <summary>Gets or sets the maximum memory allocation size in bytes.</summary>
        public long MaxMemoryAllocationSize { get; init; }

        /// <summary>Gets or sets the local memory size in bytes.</summary>
        public long LocalMemorySize { get; init; }

        /// <summary>Gets or sets whether the device uses unified memory.</summary>
        public bool IsUnifiedMemory { get; init; }

        /// <summary>Gets or sets the number of compute units.</summary>
        public int ComputeUnits { get; init; }

        /// <summary>Gets or sets the maximum clock frequency in MHz.</summary>
        public int MaxClockFrequency { get; init; }

        /// <summary>Gets or sets the maximum threads per block.</summary>
        public int MaxThreadsPerBlock { get; init; }

        /// <summary>Gets or sets the device index (for multi-device systems).</summary>
        public int DeviceIndex { get; init; }

        /// <summary>Gets or sets device-specific capabilities.</summary>
        public Dictionary<string, object>? Capabilities { get; init; }

        /// <summary>
        /// Parameterless constructor for test compatibility.
        /// Creates a default AcceleratorInfo for testing.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo()
        {
            Id = "test_device";
            Name = "Test Device";
            DeviceType = "Test";
            Vendor = "Test Vendor";
            DriverVersion = "1.0.0";
            TotalMemory = 1024 * 1024 * 1024; // 1 GB
            AvailableMemory = 1024 * 1024 * 1024;
            MaxSharedMemoryPerBlock = 48 * 1024; // 48 KB
            MaxMemoryAllocationSize = 1024 * 1024 * 1024;
            LocalMemorySize = 64 * 1024; // 64 KB
            IsUnifiedMemory = false;
            ComputeUnits = 8;
            MaxClockFrequency = 1500;
            MaxThreadsPerBlock = 1024;
            ComputeCapability = new Version(7, 5);

            // Initialize new properties with sensible defaults

            MaxComputeUnits = ComputeUnits; // Use existing ComputeUnits as default
            GlobalMemorySize = TotalMemory; // Use existing TotalMemory as default
            SupportsFloat64 = true;         // Most modern accelerators support double precision
            SupportsInt64 = true;           // Most modern accelerators support 64-bit integers
        }

        /// <summary>
        /// Gets or sets the maximum work group size (alias for MaxThreadsPerBlock).
        /// </summary>
        public int MaxWorkGroupSize
        {
            get => MaxThreadsPerBlock;
            init => MaxThreadsPerBlock = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of compute units available on this accelerator.
        /// For GPUs, this typically represents streaming multiprocessors or compute units.
        /// For CPUs, this represents the number of CPU cores or logical processors.
        /// </summary>
        public int MaxComputeUnits { get; init; }

        /// <summary>
        /// Gets or sets the total global memory size in bytes.
        /// This represents the total amount of memory accessible by compute kernels.
        /// </summary>
        public long GlobalMemorySize { get; init; }

        /// <summary>
        /// Gets or sets whether this accelerator supports double-precision (64-bit) floating-point operations.
        /// This is important for scientific computing applications requiring high precision.
        /// </summary>
        public bool SupportsFloat64 { get; init; }

        /// <summary>
        /// Gets or sets whether this accelerator supports 64-bit integer operations.
        /// This affects the availability of long and ulong data types in compute kernels.
        /// </summary>
        public bool SupportsInt64 { get; init; }

        // Metal-specific properties (populated from Capabilities dictionary for Metal backend)

        /// <summary>
        /// Gets the maximum thread execution width for Metal GPUs.
        /// This represents the SIMD width of the GPU (typically 32 for Apple GPUs).
        /// </summary>
        public int MaxThreadExecutionWidth => Capabilities?.TryGetValue("MaxThreadExecutionWidth", out var value) == true && value is int intValue ? intValue : MaxThreadsPerBlock;

        /// <summary>
        /// Gets the maximum threads per threadgroup for Metal GPUs.
        /// This is equivalent to MaxThreadsPerBlock but uses Metal terminology.
        /// </summary>
        public int MaxThreadsPerThreadgroup => Capabilities?.TryGetValue("MaxThreadsPerThreadgroup", out var value) == true && value is int intValue ? intValue : MaxThreadsPerBlock;

        /// <summary>
        /// Gets the recommended maximum working set size for Metal unified memory.
        /// This represents the optimal amount of memory to use for best performance.
        /// </summary>
        public ulong RecommendedMaxWorkingSetSize => Capabilities?.TryGetValue("RecommendedMaxWorkingSetSize", out var value) == true && value is ulong ulongValue ? ulongValue : (ulong)TotalMemory;

        /// <summary>
        /// Gets the Metal Shading Language version supported by this device.
        /// Example: "2.4", "3.0", "3.1"
        /// </summary>
        public string LanguageVersion => Capabilities?.TryGetValue("LanguageVersion", out var value) == true && value is string strValue ? strValue : "1.0";

        // Convenience properties for common use cases (added for compatibility)

        /// <summary>
        /// Gets the GPU architecture name (e.g., "Ampere", "RDNA2", "Xe", "Apple M1").
        /// This is derived from the Capabilities dictionary or vendor-specific information.
        /// </summary>
        public string Architecture
        {
            get => Capabilities?.TryGetValue("Architecture", out var value) == true && value is string strValue
                ? strValue
                : "Unknown";
            init => Capabilities ??= new Dictionary<string, object> { ["Architecture"] = value };
        }

        /// <summary>
        /// Gets the compute capability major version.
        /// For CUDA: The major version of compute capability (e.g., 8 for CC 8.6).
        /// For other backends: Derived from ComputeCapability or default to 0.
        /// </summary>
        public int MajorVersion => ComputeCapability?.Major ?? 0;

        /// <summary>
        /// Gets the compute capability minor version.
        /// For CUDA: The minor version of compute capability (e.g., 6 for CC 8.6).
        /// For other backends: Derived from ComputeCapability or default to 0.
        /// </summary>
        public int MinorVersion => ComputeCapability?.Minor ?? 0;

        /// <summary>
        /// Gets the collection of hardware features supported by this accelerator.
        /// This provides a strongly-typed view of accelerator capabilities.
        /// </summary>
        public IReadOnlyCollection<AcceleratorFeature> Features
        {
            get
            {
                if (Capabilities?.TryGetValue("Features", out var value) == true && value is IReadOnlyCollection<AcceleratorFeature> features)
                {
                    return features;
                }
                return Array.Empty<AcceleratorFeature>();
            }
            init => Capabilities ??= new Dictionary<string, object> { ["Features"] = value };
        }

        /// <summary>
        /// Gets the collection of backend-specific extensions supported by this accelerator.
        /// Examples: OpenCL extensions, CUDA PTX features, Metal feature sets.
        /// </summary>
        public IReadOnlyCollection<string> Extensions
        {
            get
            {
                if (Capabilities?.TryGetValue("Extensions", out var value) == true && value is IReadOnlyCollection<string> extensions)
                {
                    return extensions;
                }
                return Array.Empty<string>();
            }
            init => Capabilities ??= new Dictionary<string, object> { ["Extensions"] = value };
        }

        /// <summary>
        /// Gets the warp/wavefront size for this accelerator.
        /// - NVIDIA CUDA: 32 threads
        /// - AMD ROCm: 64 threads (wavefront)
        /// - Intel: 8-32 threads (SIMD width)
        /// - CPU: 1 (no warp concept)
        /// </summary>
        /// <remarks>
        /// This is critical for optimizing memory access patterns and thread synchronization.
        /// Applications should align thread block sizes to multiples of WarpSize for optimal performance.
        /// </remarks>
        public int WarpSize
        {
            get
            {
                if (Capabilities?.TryGetValue("WarpSize", out var value) == true && value is int warpSize)
                {
                    return warpSize;
                }
                // Default based on device type
                return DeviceType switch
                {
                    "GPU" when Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) => 32,
                    "GPU" when Vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) => 64,
                    "GPU" => 32, // Default for unknown GPUs
                    "CPU" => 1,  // CPUs don't have warps
                    _ => 32      // Safe default
                };
            }
            init => Capabilities ??= new Dictionary<string, object> { ["WarpSize"] = value };
        }

        /// <summary>
        /// Gets the maximum work-item dimensions supported by this accelerator.
        /// This is typically 3 (x, y, z) for most modern GPUs.
        /// </summary>
        /// <remarks>
        /// This determines how many dimensions you can use when launching kernels.
        /// Most GPU compute APIs support 3D work-item grids (x, y, z).
        /// </remarks>
        public int MaxWorkItemDimensions
        {
            get
            {
                if (Capabilities?.TryGetValue("MaxWorkItemDimensions", out var value) == true && value is int dims)
                {
                    return dims;
                }
                return 3; // Standard for OpenCL, CUDA, and most compute APIs
            }
            init => Capabilities ??= new Dictionary<string, object> { ["MaxWorkItemDimensions"] = value };
        }

        /// <summary>
        /// Gets the NUMA node affinity for this accelerator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NUMA (Non-Uniform Memory Access) nodes represent memory domains in multi-socket systems.
        /// Devices and memory within the same NUMA node have lower latency and higher bandwidth
        /// for memory transfers.
        /// </para>
        /// <para>
        /// <b>Values:</b>
        /// <list type="bullet">
        /// <item><description>-1: NUMA node not available or not applicable (default)</description></item>
        /// <item><description>0+: The NUMA node index this accelerator is attached to</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Work-Stealing Optimization:</b> The <c>WorkStealingCoordinator</c> uses this property
        /// to prioritize stealing work from devices on the same NUMA node, reducing memory
        /// transfer overhead.
        /// </para>
        /// </remarks>
        public int NumaNode
        {
            get
            {
                if (Capabilities?.TryGetValue("NumaNode", out var value) == true && value is int node)
                {
                    return node;
                }
                return -1; // NUMA not available or not applicable
            }
            init => Capabilities ??= new Dictionary<string, object> { ["NumaNode"] = value };
        }

        /// <summary>
        /// Gets the PCI bus ID for this accelerator (for GPUs).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The PCI bus ID uniquely identifies the physical location of the device in the system.
        /// Format is typically "domain:bus:device.function" (e.g., "0000:03:00.0").
        /// </para>
        /// <para>
        /// <b>Work-Stealing Optimization:</b> Devices with similar PCI bus IDs are typically
        /// on the same physical bus and may have better P2P transfer performance.
        /// </para>
        /// </remarks>
        public string? PciBusId
        {
            get
            {
                if (Capabilities?.TryGetValue("PciBusId", out var value) == true && value is string busId)
                {
                    return busId;
                }
                return null;
            }
            init => Capabilities ??= new Dictionary<string, object> { ["PciBusId"] = value! };
        }
    }

    /// <summary>
    /// Represents a compiled kernel ready for execution.
    /// </summary>
    public interface ICompiledKernel : IAsyncDisposable, IDisposable
    {
        /// <summary>Gets the kernel unique identifier.</summary>
        public Guid Id { get; }

        /// <summary>Gets the kernel name.</summary>
        public string Name { get; }

        /// <summary>Executes the kernel with given arguments.</summary>
        public ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default);
    }
}
