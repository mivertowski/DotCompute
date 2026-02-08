// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Linq.Types;

/// <summary>
/// Backend type enumeration for operator analysis.
/// </summary>
/// <remarks>
/// Identifies the compute backend for which operator analysis is being performed.
/// Different backends have different operator support and optimization characteristics.
/// </remarks>
public enum BackendType
{
    /// <summary>CPU backend with SIMD vectorization.</summary>
    CPU,

    /// <summary>NVIDIA CUDA backend for GPU acceleration.</summary>
    CUDA,

    /// <summary>Apple Metal backend for GPU acceleration.</summary>
    Metal,

    /// <summary>OpenCL backend for cross-platform GPU acceleration.</summary>
    OpenCL,

    /// <summary>Vulkan backend for GPU compute.</summary>
    Vulkan
}

/// <summary>
/// Computational complexity classification for operators.
/// </summary>
/// <remarks>
/// <para>
/// Describes the algorithmic complexity (Big-O notation) of operator execution
/// to guide optimization decisions and cost estimation.
/// </para>
/// <para>
/// Lower complexity operators are generally preferred for GPU execution where
/// kernel launch overhead can dominate simple operations.
/// </para>
/// </remarks>
public enum ComputationalComplexity
{
    /// <summary>O(1) - Constant time complexity.</summary>
    /// <remarks>Examples: assignment, basic arithmetic, comparison.</remarks>
    Constant,

    /// <summary>O(n) - Linear time complexity.</summary>
    /// <remarks>Examples: array traversal, element-wise operations.</remarks>
    Linear,

    /// <summary>O(n²) - Quadratic time complexity.</summary>
    /// <remarks>Examples: nested loops, simple matrix multiplication.</remarks>
    Quadratic,

    /// <summary>O(log n) - Logarithmic time complexity.</summary>
    /// <remarks>Examples: binary search, tree operations.</remarks>
    Logarithmic,

    /// <summary>O(2ⁿ) - Exponential time complexity.</summary>
    /// <remarks>Examples: recursive algorithms without memoization.</remarks>
    Exponential
}

/// <summary>
/// Implementation method used for operator execution.
/// </summary>
/// <remarks>
/// Different implementation methods have different performance characteristics
/// and portability tradeoffs. Native and intrinsic methods are fastest but
/// platform-specific, while library methods offer portability.
/// </remarks>
public enum ImplementationMethod
{
    /// <summary>Native hardware instruction.</summary>
    /// <remarks>Fastest execution, platform-specific.</remarks>
    Native,

    /// <summary>Compiler intrinsic function.</summary>
    /// <remarks>Near-native performance with better portability.</remarks>
    Intrinsic,

    /// <summary>Standard library implementation.</summary>
    /// <remarks>Good performance, highly portable.</remarks>
    Library,

    /// <summary>Emulated in software.</summary>
    /// <remarks>Slowest, but works everywhere.</remarks>
    Emulated,

    /// <summary>Custom optimized implementation.</summary>
    /// <remarks>Tailored for specific use cases.</remarks>
    Custom
}

/// <summary>
/// Pattern for operator fusion optimization.
/// </summary>
/// <remarks>
/// <para>
/// Operator fusion combines multiple operations into a single kernel to reduce
/// memory bandwidth requirements and improve cache locality.
/// </para>
/// <para>
/// Different patterns enable different optimizations and have different
/// applicability constraints.
/// </para>
/// </remarks>
public enum FusionPattern
{
    /// <summary>Element-wise operations (map pattern).</summary>
    /// <remarks>Independent operations on each element. Easiest to fuse.</remarks>
    ElementWise,

    /// <summary>Fused multiply-add operations.</summary>
    /// <remarks>Common in linear algebra and signal processing.</remarks>
    MultiplyAdd,

    /// <summary>Reduction operations (fold pattern).</summary>
    /// <remarks>Sum, product, min, max aggregations.</remarks>
    Reduction,

    /// <summary>Matrix operations.</summary>
    /// <remarks>Matrix multiplication, transpose, etc.</remarks>
    Matrix,

    /// <summary>Conditional operations.</summary>
    /// <remarks>Branching and predication. Harder to fuse efficiently.</remarks>
    Conditional
}

/// <summary>
/// Numerical precision level for floating-point operations.
/// </summary>
/// <remarks>
/// <para>
/// Precision affects both accuracy and performance. Lower precision enables
/// faster execution but may accumulate errors in iterative algorithms.
/// </para>
/// <para>
/// GPU hardware often has dedicated accelerators for reduced precision operations.
/// </para>
/// </remarks>
public enum NumericalPrecision
{
    /// <summary>Exact arithmetic (no floating-point).</summary>
    /// <remarks>Integer or rational arithmetic. Slowest but exact.</remarks>
    Exact,

    /// <summary>High precision (double or higher).</summary>
    /// <remarks>IEEE 754 double precision (64-bit).</remarks>
    High,

    /// <summary>Standard precision (single float).</summary>
    /// <remarks>IEEE 754 single precision (32-bit). Good balance.</remarks>
    Standard,

    /// <summary>Reduced precision (half float).</summary>
    /// <remarks>16-bit floating point. Faster on modern GPUs.</remarks>
    Reduced,

    /// <summary>Low precision (bfloat16 or less).</summary>
    /// <remarks>Optimized for ML workloads. Use carefully.</remarks>
    Low
}

/// <summary>
/// Support level for operator on a backend.
/// </summary>
/// <remarks>
/// Indicates the completeness of operator implementation and optimization
/// on a particular backend.
/// </remarks>
public enum SupportLevel
{
    /// <summary>Full native support with optimizations.</summary>
    /// <remarks>Best performance, all features available.</remarks>
    Full,

    /// <summary>Partial support with some limitations.</summary>
    /// <remarks>Core functionality works, some edge cases may be unoptimized.</remarks>
    Partial,

    /// <summary>Basic support via fallback implementation.</summary>
    /// <remarks>Functional but not optimized.</remarks>
    Basic,

    /// <summary>No support on this backend.</summary>
    /// <remarks>Operation cannot be executed.</remarks>
    None
}

/// <summary>
/// Accuracy level for numerical computations.
/// </summary>
/// <remarks>
/// Quantifies the expected accuracy of computed results relative to
/// mathematically exact values. Helps users select appropriate precision
/// for their use case.
/// </remarks>
public enum AccuracyLevel
{
    /// <summary>Mathematically exact (no rounding errors).</summary>
    Exact,

    /// <summary>High accuracy (relative error &lt; 10⁻¹²).</summary>
    High,

    /// <summary>Standard accuracy (relative error &lt; 10⁻⁶).</summary>
    Standard,

    /// <summary>Reduced accuracy (relative error &lt; 10⁻³).</summary>
    Reduced,

    /// <summary>Low accuracy (relative error &gt; 10⁻³).</summary>
    Low
}
