using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Linq.CodeGeneration;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Interface for compiling GPU kernels at runtime from source code.
/// </summary>
/// <remarks>
/// Implementations provide backend-specific compilation using:
/// - CUDA: NVRTC (NVIDIA Runtime Compilation) to generate PTX/CUBIN
/// - OpenCL: clBuildProgram to compile OpenCL C kernels
/// - Metal: MTLLibrary to compile Metal Shading Language
/// </remarks>
public interface IGpuKernelCompiler
{
    /// <summary>
    /// Gets the compute backend this compiler targets.
    /// </summary>
    ComputeBackend TargetBackend { get; }

    /// <summary>
    /// Compiles GPU kernel source code into an executable kernel.
    /// </summary>
    /// <param name="sourceCode">The GPU kernel source code (CUDA C, OpenCL C, or MSL).</param>
    /// <param name="metadata">Type metadata for kernel parameter marshaling.</param>
    /// <param name="options">Compilation options (optimization level, debug info, etc.).</param>
    /// <param name="cancellationToken">Cancellation token for compilation.</param>
    /// <returns>Compiled kernel ready for execution, or null if compilation fails.</returns>
    /// <remarks>
    /// Compilation may fail due to:
    /// - Syntax errors in generated source code
    /// - Unsupported GPU features or compute capabilities
    /// - Missing runtime libraries (NVRTC, OpenCL ICD, Metal framework)
    ///
    /// Callers should handle null returns by falling back to CPU execution.
    /// </remarks>
    Task<CompiledKernel?> CompileAsync(
        string sourceCode,
        TypeMetadata metadata,
        CompilationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the compilation environment is available and configured.
    /// </summary>
    /// <returns>True if the compiler can compile kernels; false otherwise.</returns>
    /// <remarks>
    /// This checks for:
    /// - CUDA: NVRTC library availability, CUDA driver version
    /// - OpenCL: OpenCL runtime, available devices
    /// - Metal: Metal framework on macOS, GPU availability
    /// </remarks>
    bool IsAvailable();

    /// <summary>
    /// Gets diagnostic information about the compilation environment.
    /// </summary>
    /// <returns>Human-readable diagnostic string (versions, capabilities, etc.).</returns>
    string GetDiagnostics();
}
