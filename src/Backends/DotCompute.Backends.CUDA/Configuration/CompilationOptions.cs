using System.Collections.Generic;

namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// CUDA-specific compilation options extending the base compilation options.
    /// </summary>
    public class CudaCompilationOptions : DotCompute.Abstractions.CompilationOptions
    {
        /// <summary>
        /// Gets or sets the CUDA target architecture (e.g., "sm_70", "sm_80", "sm_90").
        /// </summary>
        public string CudaArchitecture { get; set; } = "sm_60";

        /// <summary>
        /// Gets or sets whether to generate position-independent code.
        /// </summary>
        public bool GeneratePositionIndependentCode { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum register count per thread (CUDA-specific).
        /// </summary>
        public int? MaxRegistersPerThread 
        { 
            get => MaxRegisters;
            set => MaxRegisters = value;
        }

        /// <summary>
        /// Gets or sets include directories for CUDA compilation.
        /// </summary>
        public List<string> IncludeDirectories 
        { 
            get => IncludePaths;
            set => IncludePaths = value;
        }

        /// <summary>
        /// Gets or sets whether to use CUDA fast math operations.
        /// </summary>
        public bool UseFastMath
        {
            get => EnableFastMath;
            set => EnableFastMath = value;
        }

        /// <summary>
        /// Gets or sets whether to enable CUDA Cooperative Groups.
        /// </summary>
        public bool EnableCooperativeGroups { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable CUDA Dynamic Parallelism.
        /// </summary>
        public bool EnableDynamicParallelism { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable CUDA Unified Memory.
        /// </summary>
        public bool EnableUnifiedMemory { get; set; } = false;

        /// <summary>
        /// Gets or sets the CUDA compute mode.
        /// </summary>
        public string ComputeMode { get; set; } = "Default";

        /// <summary>
        /// Gets a default set of CUDA compilation options.
        /// </summary>
        public new static CudaCompilationOptions Default => new()
        {
            CudaArchitecture = "sm_60",
            OptimizationLevel = DotCompute.Abstractions.Enums.OptimizationLevel.Default
        };

        /// <summary>
        /// Gets CUDA options optimized for performance.
        /// </summary>
        public static CudaCompilationOptions Performance => new()
        {
            CudaArchitecture = "sm_80",
            OptimizationLevel = DotCompute.Abstractions.Enums.OptimizationLevel.Aggressive,
            UseFastMath = true,
            EnableDebugInfo = false,
            EnableLoopUnrolling = true,
            EnableVectorization = true,
            EnableInlining = true,
            AggressiveOptimizations = true
        };

        /// <summary>
        /// Gets CUDA options for debugging.
        /// </summary>
        public new static CudaCompilationOptions Debug => new()
        {
            CudaArchitecture = "sm_60",
            OptimizationLevel = DotCompute.Abstractions.Enums.OptimizationLevel.Minimal,
            UseFastMath = false,
            EnableDebugInfo = true,
            GeneratePositionIndependentCode = false
        };

        /// <summary>
        /// Gets CUDA options optimized for Ada Lovelace architecture (RTX 40 series).
        /// </summary>
        public static CudaCompilationOptions ForAda => new()
        {
            CudaArchitecture = "sm_89",
            OptimizationLevel = DotCompute.Abstractions.Enums.OptimizationLevel.Aggressive,
            UseFastMath = true,
            EnableDebugInfo = false,
            EnableLoopUnrolling = true,
            EnableVectorization = true,
            MaxRegistersPerThread = 255,
            AggressiveOptimizations = true
        };
    }

    /// <summary>
    /// Alias for backward compatibility.
    /// </summary>
    [Obsolete("Use CudaCompilationOptions instead")]
    public class CompilationOptions : CudaCompilationOptions
    {
    }
}