using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Optimization;
using ComputeBackend = DotCompute.Linq.CodeGeneration.ComputeBackend;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub GPU kernel generator for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Replaced by real implementations in Phase 5 Task 4:
/// - CudaKernelGenerator
/// - OpenCLKernelGenerator
/// - MetalKernelGenerator
/// </remarks>
public class GpuKernelGeneratorStub : IGpuKernelGenerator
{
    /// <inheritdoc/>
    public string GenerateCudaKernel(OperationGraph graph, TypeMetadata metadata)
        => throw new NotImplementedException("Phase 5: Use CudaKernelGenerator instead");

    /// <inheritdoc/>
    public string GenerateOpenCLKernel(OperationGraph graph, TypeMetadata metadata)
        => throw new NotImplementedException("Phase 5: Use OpenCLKernelGenerator instead");

    /// <inheritdoc/>
    public string GenerateMetalKernel(OperationGraph graph, TypeMetadata metadata)
        => throw new NotImplementedException("Phase 5: Use MetalKernelGenerator instead");

    /// <inheritdoc/>
    public GpuCompilationOptions GetCompilationOptions(ComputeBackend backend)
        => new GpuCompilationOptions();
}
