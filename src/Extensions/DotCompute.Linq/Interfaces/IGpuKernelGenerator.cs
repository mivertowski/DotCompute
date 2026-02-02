using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using ComputeBackend = DotCompute.Linq.CodeGeneration.ComputeBackend;

namespace DotCompute.Linq.Interfaces;

/// <summary>
/// Interface for GPU kernel code generation from operation graphs.
/// </summary>
/// <remarks>
/// Phase 5 Task 4: GPU Kernel Generation Implementation.
/// Generates optimized GPU kernels for CUDA, OpenCL, and Metal backends.
/// Converts LINQ OperationGraph to native GPU kernel source code.
/// </remarks>
public interface IGpuKernelGenerator
{
    /// <summary>
    /// Generates CUDA C kernel source code from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for input/output types.</param>
    /// <returns>CUDA C source code for the kernel.</returns>
    public string GenerateCudaKernel(OperationGraph graph, TypeMetadata metadata);

    /// <summary>
    /// Generates OpenCL C kernel source code from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for input/output types.</param>
    /// <returns>OpenCL C source code for the kernel.</returns>
    public string GenerateOpenCLKernel(OperationGraph graph, TypeMetadata metadata);

    /// <summary>
    /// Generates Metal Shading Language kernel source code from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for input/output types.</param>
    /// <returns>Metal Shading Language source code for the kernel.</returns>
    public string GenerateMetalKernel(OperationGraph graph, TypeMetadata metadata);

    /// <summary>
    /// Gets GPU-specific compilation options for the target backend.
    /// </summary>
    /// <param name="backend">The compute backend to target.</param>
    /// <returns>GPU compilation options.</returns>
    public GpuCompilationOptions GetCompilationOptions(ComputeBackend backend);
}

/// <summary>
/// Contains GPU-specific compilation options for kernel generation and execution.
/// </summary>
/// <remarks>
/// <para>
/// Provides comprehensive configuration for GPU kernel compilation across all supported backends.
/// Default values are optimized for production use on modern GPUs.
/// </para>
/// <para>
/// <b>Backend-Specific Defaults:</b>
/// </para>
/// <list type="bullet">
/// <item><description><b>CUDA</b>: sm_50+ (Maxwell and newer), 256 threads/block, 64 registers</description></item>
/// <item><description><b>OpenCL</b>: 256 work items, local memory enabled</description></item>
/// <item><description><b>Metal</b>: 256 threads/threadgroup, threadgroup memory enabled</description></item>
/// </list>
/// </remarks>
public class GpuCompilationOptions
{
    /// <summary>
    /// Gets the thread block size for kernel launches.
    /// </summary>
    /// <remarks>
    /// Optimal values are typically 128-512 for modern GPUs.
    /// 256 provides good occupancy across most GPU architectures.
    /// </remarks>
    public int ThreadBlockSize { get; init; } = 256;

    /// <summary>
    /// Gets the maximum number of registers per thread.
    /// </summary>
    /// <remarks>
    /// Lower values increase occupancy but may cause register spilling.
    /// Default of 64 balances occupancy with performance.
    /// </remarks>
    public int MaxRegisters { get; init; } = 64;

    /// <summary>
    /// Gets a value indicating whether to use shared memory optimization.
    /// </summary>
    /// <remarks>
    /// Shared memory provides low-latency communication within a thread block.
    /// Disable for kernels that don't benefit from data sharing.
    /// </remarks>
    public bool UseSharedMemory { get; init; } = true;

    /// <summary>
    /// Gets the target GPU architecture (e.g., "sm_50", "sm_80", "gfx900", "applegpu").
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>CUDA: sm_50 (Maxwell), sm_60 (Pascal), sm_70 (Volta), sm_80 (Ampere), sm_89 (Ada)</description></item>
    /// <item><description>OpenCL: cl1.2, cl2.0, cl3.0</description></item>
    /// <item><description>Metal: applegpu (unified), metal2.0, metal3.0</description></item>
    /// </list>
    /// </remarks>
    public string TargetArchitecture { get; init; } = "sm_50";

    /// <summary>
    /// Gets a value indicating whether to enable fast math optimizations.
    /// </summary>
    /// <remarks>
    /// Enables -use_fast_math (CUDA), -cl-fast-relaxed-math (OpenCL), and fastmath (Metal).
    /// May reduce precision but significantly improves performance.
    /// </remarks>
    public bool EnableFastMath { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; init; } = 1024;

    /// <summary>
    /// Gets the amount of shared/local memory per block in bytes.
    /// </summary>
    /// <remarks>
    /// Default of 48KB works on most GPUs. Modern GPUs may support up to 164KB.
    /// </remarks>
    public int SharedMemoryPerBlock { get; init; } = 48 * 1024; // 48 KB

    /// <summary>
    /// Gets a value indicating whether to generate debug information.
    /// </summary>
    /// <remarks>
    /// Enables -G (CUDA), -g (OpenCL) for debugging support.
    /// Significantly increases compilation time and reduces performance.
    /// </remarks>
    public bool GenerateDebugInfo { get; init; }

    /// <summary>
    /// Gets the optimization level (0-3).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>0: No optimization (fastest compilation, slowest execution)</description></item>
    /// <item><description>1: Basic optimization</description></item>
    /// <item><description>2: Standard optimization (default)</description></item>
    /// <item><description>3: Aggressive optimization (may increase compilation time)</description></item>
    /// </list>
    /// </remarks>
    public int OptimizationLevel { get; init; } = 2;

    /// <summary>
    /// Gets a value indicating whether to use FP16 (half precision) where possible.
    /// </summary>
    /// <remarks>
    /// Tensor cores on NVIDIA GPUs (Volta+) and Apple Neural Engine can accelerate FP16.
    /// Reduces memory bandwidth but may affect precision.
    /// </remarks>
    public bool UseHalfPrecision { get; init; }

    /// <summary>
    /// Gets a value indicating whether to prefer L1 cache over shared memory.
    /// </summary>
    /// <remarks>
    /// CUDA allows configuring L1/shared memory split. When true, maximizes L1 cache.
    /// </remarks>
    public bool PreferL1Cache { get; init; }

    /// <summary>
    /// Gets the number of grid dimensions to use (1, 2, or 3).
    /// </summary>
    /// <remarks>
    /// 1D grids are simplest and sufficient for most LINQ operations.
    /// 2D/3D grids are useful for matrix and tensor operations.
    /// </remarks>
    public int GridDimensions { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether to enable cooperative groups (CUDA) or barriers.
    /// </summary>
    /// <remarks>
    /// Cooperative groups allow grid-wide synchronization but require special launch.
    /// </remarks>
    public bool EnableCooperativeGroups { get; init; }

    /// <summary>
    /// Gets the maximum constant memory usage in bytes.
    /// </summary>
    /// <remarks>
    /// Constant memory is cached and optimized for broadcast patterns.
    /// Default of 64KB is the hardware limit on most GPUs.
    /// </remarks>
    public int MaxConstantMemory { get; init; } = 64 * 1024; // 64 KB
}
