// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Optimization;

/// <summary>
/// Constraints for CUDA kernel launch configuration.
/// </summary>
/// <remarks>
/// <para>
/// Provides fine-grained control over kernel launch parameter calculation.
/// All constraints are optional - the occupancy calculator will use sensible
/// defaults based on device capabilities when constraints are not specified.
/// </para>
/// <para><b>Thread Safety</b>: This class is immutable after construction (init-only properties).</para>
/// </remarks>
internal class LaunchConstraints
{
    /// <summary>
    /// Gets or sets the minimum block size in threads.
    /// </summary>
    /// <remarks>
    /// Useful for ensuring minimum occupancy or meeting algorithm requirements.
    /// Must be less than or equal to device's MaxThreadsPerBlock.
    /// </remarks>
    public int? MinBlockSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum block size in threads.
    /// </summary>
    /// <remarks>
    /// Useful for limiting register pressure or meeting hardware constraints.
    /// Defaults to device's MaxThreadsPerBlock if not specified.
    /// </remarks>
    public int? MaxBlockSize { get; set; }

    /// <summary>
    /// Gets or sets the total problem size (number of work items).
    /// </summary>
    /// <remarks>
    /// Used to calculate optimal grid dimensions. If not specified, uses
    /// device's maximum grid size.
    /// </remarks>
    public int? ProblemSize { get; set; }

    /// <summary>
    /// Gets or sets whether block size must be a multiple of warp size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default</b>: true (highly recommended for optimal performance).
    /// </para>
    /// <para>
    /// Non-warp-aligned block sizes result in partial warps which reduce occupancy
    /// and performance. Only disable for specialized use cases.
    /// </para>
    /// </remarks>
    public bool RequireWarpMultiple { get; set; } = true;

    /// <summary>
    /// Gets or sets whether block size must be a power of two.
    /// </summary>
    /// <remarks>
    /// Power-of-two block sizes can simplify indexing arithmetic in kernels
    /// and may improve performance on some architectures.
    /// </remarks>
    public bool RequirePowerOfTwo { get; set; }

    /// <summary>
    /// Gets or sets the optimization hint for launch configuration.
    /// </summary>
    /// <remarks>
    /// Guides the occupancy calculator to optimize for specific performance
    /// characteristics. Defaults to Balanced optimization.
    /// </remarks>
    public OptimizationHint OptimizationHint { get; set; } = OptimizationHint.Balanced;

    /// <summary>
    /// Gets the default launch constraints with balanced optimization.
    /// </summary>
    public static LaunchConstraints Default => new();
}

/// <summary>
/// Result of CUDA occupancy calculation for a specific block size.
/// </summary>
/// <remarks>
/// Contains theoretical occupancy metrics and limiting factors identified by the
/// CUDA occupancy API. Used to analyze and optimize kernel launch configurations.
/// </remarks>
internal class OccupancyResult
{
    /// <summary>
    /// Gets or sets the theoretical occupancy as a percentage (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Occupancy is the ratio of active warps to maximum warps per SM. Higher occupancy
    /// generally improves performance by hiding latency, but 100% occupancy is not
    /// always optimal - often 50-75% provides best performance.
    /// </para>
    /// <para><b>Range</b>: 0.0 (0%) to 1.0 (100% occupancy)</para>
    /// </remarks>
    public double Percentage { get; set; }

    /// <summary>
    /// Gets or sets the number of active warps per SM.
    /// </summary>
    /// <remarks>
    /// Indicates how many warps can execute concurrently on each SM with the
    /// given configuration. Higher values improve latency hiding.
    /// </remarks>
    public int ActiveWarps { get; set; }

    /// <summary>
    /// Gets or sets the number of active blocks per SM.
    /// </summary>
    /// <remarks>
    /// Number of thread blocks that can reside concurrently on each SM.
    /// Limited by hardware resources (registers, shared memory, block slots).
    /// </remarks>
    public int ActiveBlocks { get; set; }

    /// <summary>
    /// Gets or sets the primary limiting factor for occupancy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identifies which resource constraint is limiting occupancy:
    /// </para>
    /// <list type="bullet">
    /// <item><b>"Warps"</b>: Limited by maximum warps per SM</item>
    /// <item><b>"Registers"</b>: Limited by register file capacity</item>
    /// <item><b>"Shared Memory"</b>: Limited by shared memory capacity</item>
    /// <item><b>"Blocks"</b>: Limited by maximum resident blocks per SM</item>
    /// </list>
    /// <para>
    /// Use this information to guide optimization efforts (e.g., reduce register
    /// usage if "Registers" is the limiting factor).
    /// </para>
    /// </remarks>
    public string LimitingFactor { get; set; } = "";
}

/// <summary>
/// Occupancy curve showing performance across different block sizes.
/// </summary>
/// <remarks>
/// <para>
/// Contains occupancy data for multiple block sizes, allowing analysis of how
/// occupancy varies with block size. Useful for understanding performance
/// characteristics and identifying optimal configurations.
/// </para>
/// <para>
/// Generated by <see cref="CudaOccupancyCalculator.CalculateOccupancyCurveAsync"/>.
/// </para>
/// </remarks>
internal class OccupancyCurve
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = "";

    /// <summary>
    /// Gets or sets the device ID this curve was generated for.
    /// </summary>
    public int DeviceId { get; set; }

    /// <summary>
    /// Gets the collection of occupancy data points.
    /// </summary>
    /// <remarks>
    /// Each data point represents occupancy metrics for a specific block size.
    /// Points are typically generated in increments of 32 threads (warp size).
    /// </remarks>
    public IList<OccupancyDataPoint> DataPoints { get; } = [];

    /// <summary>
    /// Gets or sets the optimal block size that achieves maximum occupancy.
    /// </summary>
    /// <remarks>
    /// If multiple block sizes achieve the same maximum occupancy, the smallest
    /// block size is selected to minimize resource usage.
    /// </remarks>
    public int OptimalBlockSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum achievable occupancy across all tested block sizes.
    /// </summary>
    /// <remarks>
    /// Theoretical maximum occupancy (0.0-1.0) for this kernel on the target device.
    /// Actual runtime performance may vary based on memory access patterns and other factors.
    /// </remarks>
    public double MaxOccupancy { get; set; }
}

/// <summary>
/// Single data point in an occupancy curve.
/// </summary>
/// <remarks>
/// Represents occupancy metrics for a specific block size configuration.
/// Multiple data points form an occupancy curve for analysis.
/// </remarks>
internal class OccupancyDataPoint
{
    /// <summary>
    /// Gets or sets the block size (threads per block).
    /// </summary>
    public int BlockSize { get; set; }

    /// <summary>
    /// Gets or sets the theoretical occupancy (0.0-1.0).
    /// </summary>
    public double Occupancy { get; set; }

    /// <summary>
    /// Gets or sets the number of active warps per SM.
    /// </summary>
    public int ActiveWarps { get; set; }

    /// <summary>
    /// Gets or sets the number of active blocks per SM.
    /// </summary>
    public int ActiveBlocks { get; set; }

    /// <summary>
    /// Gets or sets the limiting factor for this configuration.
    /// </summary>
    /// <remarks>
    /// One of: "Warps", "Registers", "Shared Memory", or "Blocks".
    /// </remarks>
    public string LimitingFactor { get; set; } = "";
}

/// <summary>
/// Analysis of register pressure and optimization suggestions.
/// </summary>
/// <remarks>
/// <para>
/// Provides detailed analysis of register usage and its impact on occupancy.
/// Includes actionable suggestions for reducing register pressure when it
/// becomes the limiting factor.
/// </para>
/// <para>
/// Generated by <see cref="CudaOccupancyCalculator.AnalyzeRegisterPressureAsync"/>.
/// </para>
/// </remarks>
internal class RegisterAnalysis
{
    /// <summary>
    /// Gets or sets the number of registers used per thread.
    /// </summary>
    /// <remarks>
    /// Reported by CUDA compiler. Typical values range from 16-64 registers,
    /// but can exceed 100 for complex kernels.
    /// </remarks>
    public int RegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets the total registers used per block.
    /// </summary>
    /// <remarks>
    /// Calculated as RegistersPerThread × BlockSize. This determines how many
    /// blocks can fit on an SM given register file constraints.
    /// </remarks>
    public int RegistersUsedPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the maximum registers available per block.
    /// </summary>
    /// <remarks>
    /// Hardware limit varies by architecture (e.g., 65536 on Ampere/Ada).
    /// </remarks>
    public int MaxRegistersPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the maximum registers available per SM.
    /// </summary>
    /// <remarks>
    /// Total register file size per SM. Shared among all resident blocks.
    /// </remarks>
    public int MaxRegistersPerSM { get; set; }

    /// <summary>
    /// Gets or sets the maximum blocks per SM limited by register availability.
    /// </summary>
    /// <remarks>
    /// Calculated as MaxRegistersPerSM / RegistersUsedPerBlock. Lower values
    /// indicate higher register pressure.
    /// </remarks>
    public int MaxBlocksPerSmRegisters { get; set; }

    /// <summary>
    /// Gets or sets the warp occupancy percentage (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Ratio of active warps to maximum warps per SM, considering register constraints.
    /// Values below 0.5 indicate significant register pressure.
    /// </remarks>
    public double WarpOccupancy { get; set; }

    /// <summary>
    /// Gets or sets whether register pressure is the limiting factor for occupancy.
    /// </summary>
    /// <remarks>
    /// True if register usage prevents achieving maximum occupancy. When true,
    /// review Suggestions for optimization strategies.
    /// </remarks>
    public bool IsRegisterLimited { get; set; }

    /// <summary>
    /// Gets the list of optimization suggestions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains actionable recommendations for reducing register pressure:
    /// </para>
    /// <list type="bullet">
    /// <item>Target register count for full occupancy</item>
    /// <item>Compiler flags (--maxrregcount)</item>
    /// <item>__launch_bounds__ directive usage</item>
    /// <item>Code refactoring suggestions</item>
    /// </list>
    /// </remarks>
    public IList<string> Suggestions { get; } = [];
}

/// <summary>
/// Internal cache of CUDA device properties for occupancy calculations.
/// </summary>
/// <remarks>
/// Stores frequently accessed device attributes to avoid repeated CUDA API calls.
/// Properties are queried once per device and cached for the lifetime of the
/// CudaOccupancyCalculator instance.
/// </remarks>
internal class DeviceProperties
{
    /// <summary>Gets or sets the device ID.</summary>
    public int DeviceId { get; set; }

    /// <summary>Gets or sets the maximum number of threads per block.</summary>
    public int MaxThreadsPerBlock { get; set; }

    /// <summary>Gets or sets the maximum X dimension of a thread block.</summary>
    public int MaxBlockDimX { get; set; }

    /// <summary>Gets or sets the maximum Y dimension of a thread block.</summary>
    public int MaxBlockDimY { get; set; }

    /// <summary>Gets or sets the maximum Z dimension of a thread block.</summary>
    public int MaxBlockDimZ { get; set; }

    /// <summary>Gets or sets the maximum X dimension of a grid.</summary>
    public int MaxGridSize { get; set; }

    /// <summary>Gets or sets the warp size (typically 32 threads).</summary>
    public int WarpSize { get; set; }

    /// <summary>Gets or sets the maximum 32-bit registers available per block.</summary>
    public int RegistersPerBlock { get; set; }

    /// <summary>Gets or sets the total 32-bit registers per multiprocessor.</summary>
    public int RegistersPerMultiprocessor { get; set; }

    /// <summary>Gets or sets the maximum shared memory per block in bytes.</summary>
    public nuint SharedMemoryPerBlock { get; set; }

    /// <summary>Gets or sets the total shared memory per multiprocessor in bytes.</summary>
    public nuint SharedMemoryPerMultiprocessor { get; set; }

    /// <summary>Gets or sets the number of multiprocessors on the device.</summary>
    public int MultiprocessorCount { get; set; }

    /// <summary>Gets or sets the maximum resident blocks per multiprocessor.</summary>
    public int MaxBlocksPerMultiprocessor { get; set; }

    /// <summary>Gets or sets the maximum resident warps per multiprocessor.</summary>
    public int MaxWarpsPerMultiprocessor { get; set; }

    /// <summary>
    /// Gets or sets the compute capability as integer (e.g., 89 for CC 8.9).
    /// </summary>
    /// <remarks>
    /// Calculated as (major × 10) + minor. Used to check feature support
    /// (e.g., dynamic parallelism requires CC ≥ 3.5).
    /// </remarks>
    public int ComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth of nested kernel launches (dynamic parallelism).
    /// </summary>
    /// <remarks>
    /// Only applicable for devices with compute capability ≥ 3.5. Value of 0
    /// indicates dynamic parallelism is not supported.
    /// </remarks>
    public int MaxDeviceDepth { get; set; }
}

/// <summary>
/// Internal exception type for occupancy calculation errors.
/// </summary>
/// <remarks>
/// Thrown when CUDA occupancy API calls fail or when invalid configurations
/// are detected. Contains diagnostic information for troubleshooting.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public",
    Justification = "Internal exception type used only within CudaOccupancyCalculator for occupancy calculation errors")]
internal class OccupancyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the OccupancyException class.
    /// </summary>
    public OccupancyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the OccupancyException class with a message.
    /// </summary>
    /// <param name="message">The error message describing the occupancy calculation failure.</param>
    public OccupancyException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the OccupancyException class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public OccupancyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
