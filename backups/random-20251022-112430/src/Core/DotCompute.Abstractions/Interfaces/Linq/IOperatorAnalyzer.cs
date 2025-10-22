// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;

namespace DotCompute.Abstractions.Interfaces.Linq;

/// <summary>
/// Defines the interface for analyzing operators in expressions for compute optimization.
/// </summary>
/// <remarks>
/// Operator analysis examines mathematical and logical operations to determine
/// optimization opportunities, vectorization potential, and backend-specific
/// implementation strategies for maximum performance.
/// </remarks>
public interface IOperatorAnalyzer
{
    /// <summary>
    /// Analyzes an operator expression for optimization opportunities.
    /// </summary>
    /// <param name="expression">The operator expression to analyze.</param>
    /// <returns>Analysis result containing optimization information.</returns>
    public OperatorAnalysisResult AnalyzeOperator(Expression expression);

    /// <summary>
    /// Gets information about a specific operator for a given backend.
    /// </summary>
    /// <param name="operatorType">The type of operator.</param>
    /// <param name="operandTypes">The types of the operands.</param>
    /// <param name="backend">The target backend.</param>
    /// <returns>Operator information for the backend.</returns>
    public OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

    /// <summary>
    /// Determines if an operator can be vectorized.
    /// </summary>
    /// <param name="operatorType">The operator type.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <returns>Vectorization analysis for the operator.</returns>
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes);

    /// <summary>
    /// Gets fusion opportunities for combining multiple operators.
    /// </summary>
    /// <param name="operators">The sequence of operators to analyze.</param>
    /// <returns>Fusion analysis result.</returns>
    public FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators);

    /// <summary>
    /// Estimates the computational cost of an operator.
    /// </summary>
    /// <param name="operatorType">The operator type.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <param name="backend">The target backend.</param>
    /// <returns>Estimated computational cost.</returns>
    public double EstimateComputationalCost(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

    /// <summary>
    /// Analyzes operator precision and numerical stability.
    /// </summary>
    /// <param name="operatorType">The operator type.</param>
    /// <param name="operandTypes">The operand types.</param>
    /// <returns>Precision analysis result.</returns>
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes);

    /// <summary>
    /// Analyzes an expression containing an operator for optimization opportunities.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>Analysis result containing optimization information.</returns>
    public OperatorAnalysisResult Analyze(Expression expression, object? context = null);
}

/// <summary>
/// Represents the result of operator analysis.
/// </summary>
public sealed class OperatorAnalysisResult
{
    /// <summary>
    /// Gets or sets the analyzed operator type.
    /// </summary>
    public ExpressionType OperatorType { get; set; }

    /// <summary>
    /// Gets or sets the operand types.
    /// </summary>
    public IReadOnlyList<Type> OperandTypes { get; } = Array.Empty<Type>();

    /// <summary>
    /// Gets or sets a value indicating whether the operator is compute-friendly.
    /// </summary>
    public bool IsComputeFriendly { get; set; }

    /// <summary>
    /// Gets or sets whether the operator supports vectorization.
    /// </summary>
    public bool SupportsVectorization { get; set; }

    /// <summary>
    /// Gets or sets the optimal vector width for this operator.
    /// </summary>
    public int OptimalVectorWidth { get; set; }

    /// <summary>
    /// Gets or sets backend compatibility information.
    /// </summary>
    public Dictionary<BackendType, OperatorCompatibility> BackendCompatibility { get; } = [];

    /// <summary>
    /// Gets or sets optimization hints for this operator.
    /// </summary>
    public IList<string> OptimizationHints { get; } = [];

    /// <summary>
    /// Gets or sets the estimated computational complexity.
    /// </summary>
    public ComputationalComplexity Complexity { get; set; }

    /// <summary>
    /// Gets or sets fusion opportunities with other operators.
    /// </summary>
    public IList<FusionOpportunity> FusionOpportunities { get; } = [];

    /// <summary>
    /// Gets or sets the implementation method for this operator.
    /// </summary>
    public ImplementationMethod Implementation { get; set; } = ImplementationMethod.Library;

    /// <summary>
    /// Gets or sets the accuracy information for this operator.
    /// </summary>
    public AccuracyInfo Accuracy { get; set; } = new();
}

/// <summary>
/// Supporting types for operator analysis.
/// </summary>
public class OperatorInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public double PerformanceScore { get; set; }
}

public class VectorizationAnalysis
{
    public bool IsVectorizable { get; set; }
    public int OptimalWidth { get; set; }
    public double ExpectedSpeedup { get; set; }
}

public class FusionAnalysisResult
{
    public bool IsFusionBeneficial { get; set; }
    public IList<FusionOpportunity> Opportunities { get; } = [];
    public double EstimatedImprovement { get; set; }
}

public class FusionOpportunity
{
    public IList<ExpressionType> Operators { get; } = [];
    public FusionPattern Pattern { get; set; }
    public double PerformanceBenefit { get; set; }
}

public class PrecisionAnalysisResult
{
    public NumericalPrecision Precision { get; set; }
    public bool IsNumericallyStable { get; set; }
    public IList<string> MitigationStrategies { get; } = [];
}

public class OperatorCompatibility
{
    public bool IsSupported { get; set; }
    public SupportLevel SupportLevel { get; set; }
    public IList<string> Alternatives { get; } = [];
}

public class AccuracyInfo
{
    public double RelativeError { get; set; }
    public double AbsoluteError { get; set; }
    public bool IsIEEE754Compliant { get; set; }
    public AccuracyLevel AccuracyLevel { get; set; } = AccuracyLevel.High;
}

// Enums
public enum BackendType
{
    CPU, CUDA, Metal, OpenCL, Vulkan
}

public enum ComputationalComplexity
{
    Constant, Linear, Quadratic, Logarithmic, Exponential
}

public enum ImplementationMethod
{
    Native, Intrinsic, Library, Emulated, Custom
}

public enum FusionPattern
{
    ElementWise, MultiplyAdd, Reduction, Matrix, Conditional
}

public enum NumericalPrecision
{
    Exact, High, Standard, Reduced, Low
}

public enum SupportLevel
{
    Full, Partial, Basic, None
}

public enum AccuracyLevel
{
    Exact, High, Standard, Reduced, Low
}
