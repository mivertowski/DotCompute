using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Interfaces;

/// <summary>
/// Interface for GPU kernel code generation from operation graphs.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 5: CUDA Kernel Generation.
/// Generates optimized GPU kernels for CUDA and Metal backends.
/// </remarks>
public interface IGpuKernelGenerator
{
    /// <summary>
    /// Generates CUDA kernel source code from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <returns>CUDA C source code for the kernel.</returns>
    public string GenerateCudaKernel(OperationGraph graph);

    /// <summary>
    /// Generates Metal shader source code from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <returns>Metal Shading Language source code for the kernel.</returns>
    public string GenerateMetalShader(OperationGraph graph);

    /// <summary>
    /// Gets GPU-specific compilation options for the target backend.
    /// </summary>
    /// <param name="backend">The compute backend to target.</param>
    /// <returns>GPU compilation options.</returns>
    public GpuCompilationOptions GetCompilationOptions(ComputeBackend backend);
}

/// <summary>
/// Contains GPU-specific compilation options.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 5: CUDA Kernel Generation.
/// </remarks>
public class GpuCompilationOptions
{
    /// <summary>
    /// Gets the thread block size for kernel launches.
    /// </summary>
    /// <remarks>
    /// Optimal values are typically 128-512 for modern GPUs.
    /// </remarks>
    public int ThreadBlockSize { get; init; } = 256;

    /// <summary>
    /// Gets the maximum number of registers per thread.
    /// </summary>
    public int MaxRegisters { get; init; } = 64;

    /// <summary>
    /// Gets a value indicating whether to use shared memory optimization.
    /// </summary>
    public bool UseSharedMemory { get; init; } = true;

    /// <summary>
    /// Gets the target GPU architecture (e.g., "sm_50", "sm_80").
    /// </summary>
    public string TargetArchitecture { get; init; } = "sm_50";

    /// <summary>
    /// Gets a value indicating whether to enable fast math optimizations.
    /// </summary>
    public bool EnableFastMath { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; init; } = 1024;
}
