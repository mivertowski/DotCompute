using System.Collections.Generic;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Options for compiling LINQ expressions to compute kernels.
/// </summary>
/// <remarks>
/// Phase 6: Extended with GPU runtime compilation options for NVRTC, OpenCL, and Metal.
/// </remarks>
public class CompilationOptions
{
    /// <summary>
    /// Gets or sets the target compute backend.
    /// </summary>
    public ComputeBackend TargetBackend { get; set; } = ComputeBackend.Auto;

    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets whether to enable kernel fusion optimization.
    /// </summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }

    /// <summary>
    /// Gets or sets the cache time-to-live duration.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(30);

    // ========================================================================================
    // Phase 6: GPU Runtime Compilation Options
    // ========================================================================================

    /// <summary>
    /// Whether to generate line number information for profiling (GPU kernels).
    /// </summary>
    public bool GenerateLineInfo { get; set; }

    /// <summary>
    /// Whether to use fast math optimizations (may reduce precision, GPU only).
    /// </summary>
    public bool UseFastMath { get; set; }

    /// <summary>
    /// Target GPU compute capability (CUDA) or architecture (OpenCL/Metal).
    /// </summary>
    /// <remarks>
    /// - CUDA: "sm_75" (RTX 2000, 3000), "sm_80" (A100), "sm_89" (RTX 4000)
    /// - OpenCL: "1.2", "2.0", "3.0"
    /// - Metal: "macos-1.2", "ios-1.0"
    /// </remarks>
    public string? TargetArchitecture { get; set; }

    /// <summary>
    /// Additional compiler flags specific to the backend.
    /// </summary>
    /// <remarks>
    /// - CUDA: "--use_fast_math", "--maxrregcount=32"
    /// - OpenCL: "-cl-mad-enable", "-cl-no-signed-zeros"
    /// - Metal: "-ffast-math"
    /// </remarks>
    public List<string> AdditionalFlags { get; set; } = new();

    /// <summary>
    /// Include directories for header files (CUDA/OpenCL).
    /// </summary>
    public List<string> IncludePaths { get; set; } = new();

    /// <summary>
    /// Preprocessor definitions (e.g., "DEBUG=1", "BLOCK_SIZE=256").
    /// </summary>
    public Dictionary<string, string> Defines { get; set; } = new();

    /// <summary>
    /// Maximum number of registers per thread (CUDA only).
    /// </summary>
    public int? MaxRegistersPerThread { get; set; }

    /// <summary>
    /// Whether to compile to PTX (portable) or CUBIN (binary) for CUDA.
    /// </summary>
    public bool GeneratePTX { get; set; } = true;

    /// <summary>
    /// Creates default compilation options for the specified backend.
    /// </summary>
    public static CompilationOptions ForBackend(ComputeBackend backend) => new()
    {
        TargetBackend = backend,
        OptimizationLevel = OptimizationLevel.Balanced,
        EnableKernelFusion = true,
        GenerateDebugInfo = false,
        UseFastMath = false
    };

    /// <summary>
    /// Creates debug compilation options (unoptimized with debug info).
    /// </summary>
    public static CompilationOptions CreateDebug(ComputeBackend backend) => new()
    {
        TargetBackend = backend,
        OptimizationLevel = OptimizationLevel.None,
        EnableKernelFusion = false,
        GenerateDebugInfo = true,
        GenerateLineInfo = true,
        UseFastMath = false
    };

    /// <summary>
    /// Creates release compilation options (aggressive optimization).
    /// </summary>
    public static CompilationOptions CreateRelease(ComputeBackend backend) => new()
    {
        TargetBackend = backend,
        OptimizationLevel = OptimizationLevel.Aggressive,
        EnableKernelFusion = true,
        GenerateDebugInfo = false,
        UseFastMath = true
    };
}

/// <summary>
/// Compute backend enumeration.
/// </summary>
public enum ComputeBackend
{
    /// <summary>
    /// Automatic backend selection based on workload.
    /// </summary>
    Auto,

    /// <summary>
    /// CPU backend with SIMD vectorization.
    /// </summary>
    Cpu,

    /// <summary>
    /// CUDA GPU backend (NVIDIA).
    /// </summary>
    Cuda,

    /// <summary>
    /// OpenCL GPU backend (cross-platform).
    /// </summary>
    OpenCL,

    /// <summary>
    /// Metal GPU backend (Apple).
    /// </summary>
    Metal
}

/// <summary>
/// Optimization level enumeration.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization.
    /// </summary>
    None,

    /// <summary>
    /// Conservative optimization (fast compilation).
    /// </summary>
    Conservative,

    /// <summary>
    /// Balanced optimization (default).
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive optimization (slower compilation, faster execution).
    /// </summary>
    Aggressive,

    /// <summary>
    /// ML-optimized (learns from execution patterns).
    /// </summary>
    MLOptimized
}
