// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration;

/// <summary>
/// Centralized constants for code generation, providing configurable thresholds,
/// sizes, and other magic numbers used throughout the generator pipeline.
/// </summary>
/// <remarks>
/// This class consolidates all constants to improve maintainability and allow
/// easy tuning of performance characteristics without modifying generator logic.
/// </remarks>
public static class GeneratorConstants
{
    #region Array Size Thresholds
    
    /// <summary>
    /// Threshold below which scalar implementation is preferred over vectorized.
    /// Arrays smaller than this use scalar for better cache efficiency.
    /// </summary>
    public const int SmallArrayThreshold = 32;
    
    /// <summary>
    /// Minimum array size for using vectorized operations.
    /// </summary>
    public const int MinVectorSize = 32;
    
    /// <summary>
    /// Minimum array size for using AVX-512 instructions.
    /// Larger threshold due to higher setup overhead.
    /// </summary>
    public const int MinAvx512Size = 64;
    
    /// <summary>
    /// Default chunk size for parallel partitioning.
    /// Balances work distribution with scheduling overhead.
    /// </summary>
    public const int DefaultChunkSize = 1024;
    
    #endregion
    
    #region SIMD Vector Sizes
    
    /// <summary>
    /// Number of float elements in an AVX2 vector (256-bit / 32-bit).
    /// </summary>
    public const int Avx2VectorSize = 8;
    
    /// <summary>
    /// Number of float elements in an AVX-512 vector (512-bit / 32-bit).
    /// </summary>
    public const int Avx512VectorSize = 16;
    
    /// <summary>
    /// Number of double elements in an AVX2 vector (256-bit / 64-bit).
    /// </summary>
    public const int Avx2DoubleVectorSize = 4;
    
    /// <summary>
    /// Number of double elements in an AVX-512 vector (512-bit / 64-bit).
    /// </summary>
    public const int Avx512DoubleVectorSize = 8;
    
    #endregion
    
    #region Code Formatting
    
    /// <summary>
    /// Base indentation level for namespace content.
    /// </summary>
    public const int BaseIndentLevel = 2;
    
    /// <summary>
    /// Indentation level for method body content.
    /// </summary>
    public const int MethodBodyIndentLevel = 3;
    
    /// <summary>
    /// Indentation level for loop body content.
    /// </summary>
    public const int LoopBodyIndentLevel = 4;
    
    /// <summary>
    /// Number of spaces per indentation level.
    /// </summary>
    public const int SpacesPerIndent = 4;
    
    #endregion
    
    #region Performance Tuning
    
    /// <summary>
    /// Minimum number of iterations to justify parallel execution.
    /// </summary>
    public const int MinParallelIterations = 1000;
    
    /// <summary>
    /// Maximum degree of parallelism as a multiplier of processor count.
    /// </summary>
    public const double MaxParallelismMultiplier = 2.0;
    
    /// <summary>
    /// Cache line size in bytes for alignment optimization.
    /// </summary>
    public const int CacheLineSize = 64;
    
    #endregion
    
    #region Method Complexity Limits
    
    /// <summary>
    /// Maximum allowed cyclomatic complexity for generated methods.
    /// </summary>
    public const int MaxCyclomaticComplexity = 10;
    
    /// <summary>
    /// Maximum number of lines per generated method.
    /// </summary>
    public const int MaxMethodLines = 50;
    
    /// <summary>
    /// Maximum nesting depth for control structures.
    /// </summary>
    public const int MaxNestingDepth = 4;
    
    #endregion
    
    #region Kernel Configuration
    
    /// <summary>
    /// Default vector size for kernels when not specified.
    /// </summary>
    public const int DefaultKernelVectorSize = 8;
    
    /// <summary>
    /// Maximum supported vector size for any kernel.
    /// </summary>
    public const int MaxKernelVectorSize = 64;
    
    /// <summary>
    /// Default block size for GPU kernels.
    /// </summary>
    public const int DefaultGpuBlockSize = 256;
    
    #endregion
}