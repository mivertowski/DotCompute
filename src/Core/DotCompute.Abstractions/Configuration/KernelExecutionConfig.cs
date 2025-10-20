// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Enums;

namespace DotCompute.Abstractions.Configuration;

/// <summary>
/// Configuration settings for kernel execution.
/// </summary>
/// <remarks>
/// This class encapsulates all configuration parameters that control how kernels
/// are executed across different compute backends (CPU, CUDA, Metal, etc.).
/// Settings include precision mode, thread/block dimensions, and debugging options.
/// </remarks>
public sealed class KernelExecutionConfig
{
    /// <summary>
    /// Gets or initializes the precision mode for kernel computations.
    /// </summary>
    /// <value>
    /// The precision mode. Default is <see cref="PrecisionMode.Single"/>.
    /// </value>
    public PrecisionMode Precision { get; init; } = PrecisionMode.Single;

    /// <summary>
    /// Gets or initializes the maximum number of threads per block for GPU kernels.
    /// </summary>
    /// <value>
    /// The maximum threads per block. Default is 256.
    /// </value>
    /// <remarks>
    /// This value must not exceed the hardware limits of the target GPU.
    /// Common values: 128, 256, 512, 1024.
    /// Higher values may improve occupancy but can increase register pressure.
    /// </remarks>
    public int MaxThreadsPerBlock { get; init; } = 256;

    /// <summary>
    /// Gets or initializes the maximum number of blocks per grid dimension.
    /// </summary>
    /// <value>
    /// The maximum blocks per grid. Default is 65535.
    /// </value>
    /// <remarks>
    /// This represents the maximum grid dimension size.
    /// For CUDA, this is typically 2^16-1 (65535) for compute capability 2.x and above.
    /// For larger workloads, use 2D or 3D grids.
    /// </remarks>
    public int MaxBlocksPerGrid { get; init; } = 65535;

    /// <summary>
    /// Gets or initializes a value indicating whether debug information should be generated.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable debug information; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, kernels are compiled with debug symbols and additional validation.
    /// This may significantly impact performance and should only be used during development.
    /// </remarks>
    public bool EnableDebug { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether performance profiling should be enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable profiling; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, runtime collects detailed performance metrics including
    /// kernel execution time, memory transfer time, and hardware utilization.
    /// Minimal performance overhead (~1-3%).
    /// </remarks>
    public bool EnableProfiling { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelExecutionConfig"/> class with default values.
    /// </summary>
    public KernelExecutionConfig()
    {
    }

    /// <summary>
    /// Creates a configuration optimized for debugging.
    /// </summary>
    /// <returns>A configuration with debugging enabled and reduced thread counts.</returns>
    public static KernelExecutionConfig CreateDebugConfig() => new()
    {
        EnableDebug = true,
        EnableProfiling = true,
        MaxThreadsPerBlock = 64, // Reduced for easier debugging
        Precision = PrecisionMode.Single
    };

    /// <summary>
    /// Creates a configuration optimized for maximum performance.
    /// </summary>
    /// <returns>A configuration with high thread counts and profiling disabled.</returns>
    public static KernelExecutionConfig CreatePerformanceConfig() => new()
    {
        EnableDebug = false,
        EnableProfiling = false,
        MaxThreadsPerBlock = 1024,
        MaxBlocksPerGrid = 65535,
        Precision = PrecisionMode.Single
    };

    /// <summary>
    /// Creates a configuration for high-precision scientific computing.
    /// </summary>
    /// <returns>A configuration with double precision enabled.</returns>
    public static KernelExecutionConfig CreateHighPrecisionConfig() => new()
    {
        Precision = PrecisionMode.Double,
        MaxThreadsPerBlock = 256,
        EnableDebug = false,
        EnableProfiling = false
    };
}
