// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces.Linq.Types;

namespace DotCompute.Abstractions.Interfaces.Linq;

/// <summary>
/// Defines the interface for analyzing operators in expressions for compute optimization.
/// </summary>
/// <remarks>
/// <para>
/// Operator analysis examines mathematical and logical operations to determine
/// optimization opportunities, vectorization potential, and backend-specific
/// implementation strategies for maximum performance.
/// </para>
/// <para>
/// The analyzer provides multiple analysis modes:
/// - Single operator analysis for optimization decisions
/// - Vectorization analysis for SIMD opportunities
/// - Fusion analysis for combining multiple operators
/// - Precision analysis for numerical stability
/// - Cost estimation for backend selection
/// </para>
/// <example>
/// <code>
/// var analyzer = serviceProvider.GetRequiredService&lt;IOperatorAnalyzer&gt;();
/// var result = analyzer.AnalyzeOperator(addExpression);
///
/// if (result.SupportsVectorization)
/// {
///     Console.WriteLine($"Can vectorize with width {result.OptimalVectorWidth}");
/// }
///
/// foreach (var hint in result.OptimizationHints)
/// {
///     Console.WriteLine($"Hint: {hint}");
/// }
/// </code>
/// </example>
/// </remarks>
public interface IOperatorAnalyzer
{
    /// <summary>
    /// Analyzes an operator expression for optimization opportunities.
    /// </summary>
    /// <param name="expression">The operator expression to analyze.</param>
    /// <returns>Comprehensive analysis result with optimization information.</returns>
    /// <remarks>
    /// Primary analysis method that examines all aspects of operator execution.
    /// </remarks>
    public OperatorAnalysisResult AnalyzeOperator(Expression expression);

    /// <summary>
    /// Gets information about a specific operator for a given backend.
    /// </summary>
    /// <param name="operatorType">The type of operator (Add, Multiply, etc.).</param>
    /// <param name="operandTypes">The types of the operands (int, float, etc.).</param>
    /// <param name="backend">The target compute backend.</param>
    /// <returns>Operator information including support status and performance score.</returns>
    /// <remarks>
    /// Use this for backend-specific operator availability checks.
    /// </remarks>
    public OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

    /// <summary>
    /// Determines if an operator can be vectorized for SIMD execution.
    /// </summary>
    /// <param name="operatorType">The operator type to analyze.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <returns>Vectorization analysis with optimal width and expected speedup.</returns>
    /// <remarks>
    /// Critical for CPU backend optimization where SIMD can provide 4-8x speedups.
    /// </remarks>
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes);

    /// <summary>
    /// Identifies fusion opportunities for combining multiple operators into single kernels.
    /// </summary>
    /// <param name="operators">The sequence of operators to analyze for fusion.</param>
    /// <returns>Fusion analysis result with identified opportunities and estimated benefits.</returns>
    /// <remarks>
    /// <para>
    /// Fusion reduces memory bandwidth by eliminating intermediate results.
    /// Particularly effective for GPU execution.
    /// </para>
    /// <para>
    /// Example: Fusing `x.Select(a => a * 2).Select(b => b + 1)` into single kernel.
    /// </para>
    /// </remarks>
    public FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators);

    /// <summary>
    /// Estimates the computational cost of an operator on a specific backend.
    /// </summary>
    /// <param name="operatorType">The operator type.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <param name="backend">The target backend.</param>
    /// <returns>Estimated cost in arbitrary units (lower is better).</returns>
    /// <remarks>
    /// Cost estimation guides backend selection and operator ordering decisions.
    /// </remarks>
    public double EstimateComputationalCost(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

    /// <summary>
    /// Analyzes operator precision and numerical stability characteristics.
    /// </summary>
    /// <param name="operatorType">The operator type.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <returns>Precision analysis with stability assessment and mitigation strategies.</returns>
    /// <remarks>
    /// <para>
    /// Critical for scientific computing where numerical errors can accumulate.
    /// Identifies operators prone to catastrophic cancellation or overflow.
    /// </para>
    /// </remarks>
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes);

    /// <summary>
    /// Analyzes an expression containing an operator with additional context.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">Optional analysis context for customization.</param>
    /// <returns>Comprehensive analysis result.</returns>
    /// <remarks>
    /// Extended analysis method allowing custom analysis parameters via context object.
    /// </remarks>
    public OperatorAnalysisResult Analyze(Expression expression, object? context = null);
}
