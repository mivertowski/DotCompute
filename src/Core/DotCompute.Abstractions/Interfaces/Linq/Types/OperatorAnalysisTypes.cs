// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;

namespace DotCompute.Abstractions.Interfaces.Linq.Types;

/// <summary>
/// Represents the comprehensive result of operator analysis.
/// </summary>
/// <remarks>
/// <para>
/// Aggregates all analysis dimensions including compute friendliness, vectorization support,
/// backend compatibility, complexity estimation, fusion opportunities, and numerical accuracy.
/// </para>
/// <para>
/// Used by query optimizer to select optimal execution strategy for operators.
/// </para>
/// </remarks>
public sealed class OperatorAnalysisResult
{
    /// <summary>Gets or sets the analyzed operator type.</summary>
    public ExpressionType OperatorType { get; set; }

    /// <summary>Gets or sets the operand types.</summary>
    public IReadOnlyList<Type> OperandTypes { get; } = Array.Empty<Type>();

    /// <summary>Gets or sets whether the operator is compute-friendly (GPU-suitable).</summary>
    /// <remarks>
    /// Compute-friendly operators have regular memory access patterns and minimal branching.
    /// </remarks>
    public bool IsComputeFriendly { get; set; }

    /// <summary>Gets or sets whether the operator supports SIMD vectorization.</summary>
    public bool SupportsVectorization { get; set; }

    /// <summary>Gets or sets the optimal vector width for this operator.</summary>
    /// <remarks>
    /// Vector width (e.g., 128, 256, 512 bits) for SIMD execution.
    /// </remarks>
    public int OptimalVectorWidth { get; set; }

    /// <summary>Gets or sets backend compatibility information.</summary>
    /// <remarks>
    /// Maps backend types to their support level and alternatives.
    /// </remarks>
    public Dictionary<BackendType, OperatorCompatibility> BackendCompatibility { get; } = [];

    /// <summary>Gets or sets optimization hints for this operator.</summary>
    /// <remarks>
    /// Human-readable suggestions for improving operator performance.
    /// </remarks>
    public IList<string> OptimizationHints { get; } = [];

    /// <summary>Gets or sets the estimated computational complexity.</summary>
    public ComputationalComplexity Complexity { get; set; }

    /// <summary>Gets or sets fusion opportunities with other operators.</summary>
    /// <remarks>
    /// Identifies combinations of operators that can be fused into single kernels
    /// for improved performance.
    /// </remarks>
    public IList<FusionOpportunity> FusionOpportunities { get; } = [];

    /// <summary>Gets or sets the implementation method for this operator.</summary>
    public ImplementationMethod Implementation { get; set; } = ImplementationMethod.Library;

    /// <summary>Gets or sets the numerical accuracy information.</summary>
    public AccuracyInfo Accuracy { get; set; } = new();
}

/// <summary>
/// Information about an operator for a specific backend.
/// </summary>
/// <remarks>
/// Provides operator metadata including support status and performance characteristics.
/// </remarks>
public class OperatorInfo
{
    /// <summary>Gets or sets the operator name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the operator is supported on this backend.</summary>
    public bool IsSupported { get; set; }

    /// <summary>Gets or sets the performance score (0.0 = poor, 1.0 = excellent).</summary>
    /// <remarks>
    /// Relative performance compared to other backends and implementations.
    /// </remarks>
    public double PerformanceScore { get; set; }
}

/// <summary>
/// Analysis of operator vectorization potential.
/// </summary>
/// <remarks>
/// <para>
/// Vectorization analysis determines if an operator can benefit from SIMD execution
/// and estimates the expected performance improvement.
/// </para>
/// </remarks>
public class VectorizationAnalysis
{
    /// <summary>Gets or sets whether the operator can be vectorized.</summary>
    public bool IsVectorizable { get; set; }

    /// <summary>Gets or sets the optimal SIMD vector width in bits.</summary>
    /// <remarks>
    /// Common values: 128 (SSE), 256 (AVX2), 512 (AVX-512), 1024+ (GPU).
    /// </remarks>
    public int OptimalWidth { get; set; }

    /// <summary>Gets or sets the expected speedup from vectorization.</summary>
    /// <remarks>
    /// Multiplier relative to scalar execution (e.g., 4.0x for AVX2 float operations).
    /// </remarks>
    public double ExpectedSpeedup { get; set; }
}

/// <summary>
/// Result of analyzing operator fusion opportunities.
/// </summary>
/// <remarks>
/// <para>
/// Operator fusion combines multiple operations to reduce memory traffic and
/// improve cache locality. This analysis identifies beneficial fusion patterns.
/// </para>
/// </remarks>
public class FusionAnalysisResult
{
    /// <summary>Gets or sets whether fusion would improve performance.</summary>
    public bool IsFusionBeneficial { get; set; }

    /// <summary>Gets or sets the identified fusion opportunities.</summary>
    public IList<FusionOpportunity> Opportunities { get; } = [];

    /// <summary>Gets or sets the estimated performance improvement from fusion.</summary>
    /// <remarks>
    /// Percentage improvement (e.g., 0.25 = 25% faster with fusion).
    /// </remarks>
    public double EstimatedImprovement { get; set; }
}

/// <summary>
/// Represents a specific opportunity to fuse multiple operators.
/// </summary>
/// <remarks>
/// Describes which operators can be combined and the expected benefit.
/// </remarks>
public class FusionOpportunity
{
    /// <summary>Gets or sets the operators that can be fused together.</summary>
    public IList<ExpressionType> Operators { get; } = [];

    /// <summary>Gets or sets the fusion pattern classification.</summary>
    public FusionPattern Pattern { get; set; }

    /// <summary>Gets or sets the performance benefit of this fusion.</summary>
    /// <remarks>
    /// Percentage speedup (e.g., 0.30 = 30% faster when fused).
    /// </remarks>
    public double PerformanceBenefit { get; set; }
}

/// <summary>
/// Analysis of numerical precision and stability for an operator.
/// </summary>
/// <remarks>
/// <para>
/// Precision analysis examines floating-point behavior to identify potential
/// numerical issues and suggest mitigation strategies.
/// </para>
/// <para>
/// Critical for scientific computing where accumulated errors can invalidate results.
/// </para>
/// </remarks>
public class PrecisionAnalysisResult
{
    /// <summary>Gets or sets the precision level of the operator.</summary>
    public NumericalPrecision Precision { get; set; }

    /// <summary>Gets or sets whether the operator is numerically stable.</summary>
    /// <remarks>
    /// Stable operators don't amplify rounding errors significantly.
    /// </remarks>
    public bool IsNumericallyStable { get; set; }

    /// <summary>Gets or sets strategies to improve numerical stability.</summary>
    /// <remarks>
    /// Human-readable suggestions (e.g., "Use Kahan summation for accumulation").
    /// </remarks>
    public IList<string> MitigationStrategies { get; } = [];
}

/// <summary>
/// Operator compatibility information for a specific backend.
/// </summary>
/// <remarks>
/// Describes support level and alternative implementations if primary operator
/// is not available.
/// </remarks>
public class OperatorCompatibility
{
    /// <summary>Gets or sets whether the operator is supported.</summary>
    public bool IsSupported { get; set; }

    /// <summary>Gets or sets the level of support provided.</summary>
    public SupportLevel SupportLevel { get; set; }

    /// <summary>Gets or sets alternative operators that could be used instead.</summary>
    /// <remarks>
    /// Fallback options when native support is unavailable.
    /// </remarks>
    public IList<string> Alternatives { get; } = [];
}

/// <summary>
/// Information about operator numerical accuracy.
/// </summary>
/// <remarks>
/// <para>
/// Quantifies expected errors in computed results and indicates compliance
/// with IEEE 754 floating-point standard.
/// </para>
/// </remarks>
public class AccuracyInfo
{
    /// <summary>Gets or sets the relative error magnitude.</summary>
    /// <remarks>
    /// Relative error = |computed - exact| / |exact|
    /// </remarks>
    public double RelativeError { get; set; }

    /// <summary>Gets or sets the absolute error magnitude.</summary>
    /// <remarks>
    /// Absolute error = |computed - exact|
    /// </remarks>
    public double AbsoluteError { get; set; }

    /// <summary>Gets or sets whether the implementation follows IEEE 754 standard.</summary>
    /// <remarks>
    /// IEEE 754 compliance ensures consistent rounding and special value handling.
    /// </remarks>
    public bool IsIEEE754Compliant { get; set; }

    /// <summary>Gets or sets the overall accuracy level classification.</summary>
    public AccuracyLevel AccuracyLevel { get; set; } = AccuracyLevel.High;
}
