using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub GPU kernel generator for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 5.
/// </remarks>
public class GpuKernelGeneratorStub : IGpuKernelGenerator
{
    /// <inheritdoc/>
    public string GenerateCudaKernel(OperationGraph graph)
        => throw new NotImplementedException("Phase 5: CUDA Kernel Generation");

    /// <inheritdoc/>
    public string GenerateMetalShader(OperationGraph graph)
        => throw new NotImplementedException("Metal backend support - future phase");

    /// <inheritdoc/>
    public GpuCompilationOptions GetCompilationOptions(ComputeBackend backend)
        => new GpuCompilationOptions();
}
