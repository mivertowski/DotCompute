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
