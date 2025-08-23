// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Settings.Features;

/// <summary>
/// Feature flags for selective code generation, controlling which advanced
/// features and optimizations are enabled in the generated code.
/// </summary>
/// <remarks>
/// This class provides fine-grained control over code generation features,
/// allowing developers to enable or disable specific optimizations based
/// on their target environment, performance requirements, and compatibility
/// constraints. Feature flags help balance performance gains against
/// code complexity and platform compatibility.
/// </remarks>
public class FeatureFlags
{
    /// <summary>
    /// Gets or sets a value indicating whether to generate SIMD (Single Instruction, Multiple Data) code.
    /// </summary>
    /// <value>
    /// <c>true</c> if SIMD optimizations should be applied to vectorizable operations;
    /// otherwise, <c>false</c>. SIMD can significantly improve performance for
    /// mathematical operations on arrays and vectors.
    /// </value>
    /// <remarks>
    /// SIMD optimizations can provide substantial performance improvements for
    /// data-parallel operations but require hardware support and may increase
    /// code complexity. When enabled, the generator will automatically
    /// vectorize suitable operations using the appropriate instruction sets.
    /// </remarks>
    public bool EnableSimd { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate AVX2 (Advanced Vector Extensions 2) code.
    /// </summary>
    /// <value>
    /// <c>true</c> if AVX2 instructions should be used for vectorized operations
    /// when available; otherwise, <c>false</c>. AVX2 provides 256-bit vector
    /// operations for enhanced SIMD performance.
    /// </value>
    /// <remarks>
    /// AVX2 support requires compatible x86-64 processors (Intel Haswell+ or AMD
    /// Excavator+). When enabled, operations can process twice as much data per
    /// instruction compared to SSE, but may not be available on all target machines.
    /// </remarks>
    public bool EnableAvx2 { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate AVX-512 code.
    /// </summary>
    /// <value>
    /// <c>true</c> if AVX-512 instructions should be used for vectorized operations
    /// when available; otherwise, <c>false</c>. AVX-512 provides 512-bit vector
    /// operations for maximum SIMD throughput.
    /// </value>
    /// <remarks>
    /// AVX-512 is available on high-end Intel processors (Skylake-X+) and some
    /// AMD processors. While it offers the highest SIMD performance, it has
    /// limited availability and may cause CPU frequency throttling on some systems.
    /// </remarks>
    public bool EnableAvx512 { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate parallel code using multiple threads.
    /// </summary>
    /// <value>
    /// <c>true</c> if operations should be parallelized across multiple CPU cores
    /// when beneficial; otherwise, <c>false</c>. Parallel execution can significantly
    /// improve performance for large datasets.
    /// </value>
    /// <remarks>
    /// Parallel code generation uses .NET's parallel processing capabilities to
    /// distribute work across available CPU cores. This is most effective for
    /// CPU-bound operations on large data sets but adds complexity and overhead
    /// for smaller operations.
    /// </remarks>
    public bool EnableParallel { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate ARM NEON code.
    /// </summary>
    /// <value>
    /// <c>true</c> if ARM NEON SIMD instructions should be used on ARM processors;
    /// otherwise, <c>false</c>. NEON provides vectorized operations on ARM
    /// architectures including mobile and Apple Silicon processors.
    /// </value>
    /// <remarks>
    /// ARM NEON is the SIMD instruction set for ARM processors, providing
    /// similar vectorization benefits to x86 SSE/AVX on ARM-based systems.
    /// This is particularly relevant for mobile applications and Apple Silicon Macs.
    /// </remarks>
    public bool EnableArmNeon { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether to allow unsafe code generation.
    /// </summary>
    /// <value>
    /// <c>true</c> if unsafe code blocks and pointer operations are permitted
    /// in generated code; otherwise, <c>false</c>. Unsafe code can provide
    /// performance benefits but bypasses .NET's memory safety guarantees.
    /// </value>
    /// <remarks>
    /// Unsafe code allows direct memory manipulation and can provide significant
    /// performance improvements, especially for low-level operations. However,
    /// it requires the project to be compiled with unsafe code enabled and
    /// bypasses .NET's built-in memory safety protections.
    /// </remarks>
    public bool AllowUnsafe { get; set; } = true;
}