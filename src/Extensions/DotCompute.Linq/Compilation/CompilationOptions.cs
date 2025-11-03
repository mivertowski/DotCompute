namespace DotCompute.Linq.Compilation;

/// <summary>
/// Options for compiling LINQ expressions to compute kernels.
/// </summary>
/// <remarks>
/// ⚠️ STUB IMPLEMENTATION - Phase 2 (Test Infrastructure)
/// Full implementation planned for Phase 3-5.
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
