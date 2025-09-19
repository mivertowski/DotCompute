// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;

namespace DotCompute.Linq.Analysis
{
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
        OperatorAnalysisResult AnalyzeOperator(Expression expression);

        /// <summary>
        /// Gets information about a specific operator for a given backend.
        /// </summary>
        /// <param name="operatorType">The type of operator.</param>
        /// <param name="operandTypes">The types of the operands.</param>
        /// <param name="backend">The target backend.</param>
        /// <returns>Operator information for the backend.</returns>
        OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

        /// <summary>
        /// Determines if an operator can be vectorized.
        /// </summary>
        /// <param name="operatorType">The operator type.</param>
        /// <param name="operandTypes">The operand types.</param>
        /// <returns>Vectorization analysis for the operator.</returns>
        VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes);

        /// <summary>
        /// Gets fusion opportunities for combining multiple operators.
        /// </summary>
        /// <param name="operators">The sequence of operators to analyze.</param>
        /// <returns>Fusion analysis result.</returns>
        FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators);

        /// <summary>
        /// Estimates the computational cost of an operator.
        /// </summary>
        /// <param name="operatorType">The operator type.</param>
        /// <param name="operandTypes">The operand types.</param>
        /// <param name="backend">The target backend.</param>
        /// <returns>Estimated computational cost.</returns>
        double EstimateComputationalCost(ExpressionType operatorType, Type[] operandTypes, BackendType backend);

        /// <summary>
        /// Analyzes operator precision and numerical stability.
        /// </summary>
        /// <param name="operatorType">The operator type.</param>
        /// <param name="operandTypes">The operand types.</param>
        /// <returns>Precision analysis result.</returns>
        PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes);

        /// <summary>
        /// Analyzes an expression containing an operator for optimization opportunities.
        /// </summary>
        /// <param name="expression">The expression to analyze.</param>
        /// <param name="context">The analysis context.</param>
        /// <returns>Analysis result containing optimization information.</returns>
        OperatorAnalysisResult Analyze(Expression expression, object? context = null);
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
        public Type[] OperandTypes { get; set; } = [];

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
        public Dictionary<BackendType, OperatorCompatibility> BackendCompatibility { get; set; } = [];

        /// <summary>
        /// Gets or sets optimization hints for this operator.
        /// </summary>
        public List<OptimizationHint> OptimizationHints { get; set; } = [];

        /// <summary>
        /// Gets or sets the estimated computational complexity.
        /// </summary>
        public ComputationalComplexity Complexity { get; set; }

        /// <summary>
        /// Gets or sets fusion opportunities with other operators.
        /// </summary>
        public List<FusionOpportunity> FusionOpportunities { get; set; } = [];

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
    /// Represents vectorization analysis for an operator.
    /// </summary>
    public sealed class VectorizationAnalysis
    {
        /// <summary>
        /// Gets or sets whether vectorization is possible.
        /// </summary>
        public bool IsVectorizable { get; set; }

        /// <summary>
        /// Gets or sets the optimal vector width.
        /// </summary>
        public int OptimalWidth { get; set; }

        /// <summary>
        /// Gets or sets supported vector widths.
        /// </summary>
        public int[] SupportedWidths { get; set; } = [];

        /// <summary>
        /// Gets or sets the expected speedup from vectorization.
        /// </summary>
        public double ExpectedSpeedup { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets vectorization constraints.
        /// </summary>
        public List<string> Constraints { get; set; } = [];

        /// <summary>
        /// Gets or sets the vector instruction to use.
        /// </summary>
        public string? VectorInstruction { get; set; }
    }

    /// <summary>
    /// Represents the result of fusion analysis.
    /// </summary>
    public sealed class FusionAnalysisResult
    {
        /// <summary>
        /// Gets or sets whether fusion is beneficial.
        /// </summary>
        public bool IsFusionBeneficial { get; set; }

        /// <summary>
        /// Gets or sets fusion opportunities found.
        /// </summary>
        public List<FusionOpportunity> Opportunities { get; set; } = [];

        /// <summary>
        /// Gets or sets the estimated performance improvement from fusion.
        /// </summary>
        public double EstimatedImprovement { get; set; }

        /// <summary>
        /// Gets or sets fusion constraints or limitations.
        /// </summary>
        public List<string> Constraints { get; set; } = [];
    }

    /// <summary>
    /// Represents a fusion opportunity between operators.
    /// </summary>
    public sealed class FusionOpportunity
    {
        /// <summary>
        /// Gets or sets the operators that can be fused.
        /// </summary>
        public List<ExpressionType> Operators { get; set; } = [];

        /// <summary>
        /// Gets or sets the fusion pattern type.
        /// </summary>
        public FusionPattern Pattern { get; set; }

        /// <summary>
        /// Gets or sets the expected performance benefit.
        /// </summary>
        public double PerformanceBenefit { get; set; }

        /// <summary>
        /// Gets or sets memory bandwidth savings.
        /// </summary>
        public double MemorySavings { get; set; }

        /// <summary>
        /// Gets or sets the complexity of implementing this fusion.
        /// </summary>
        public FusionComplexity Complexity { get; set; }
    }

    /// <summary>
    /// Represents precision analysis for an operator.
    /// </summary>
    public sealed class PrecisionAnalysisResult
    {
        /// <summary>
        /// Gets or sets the numerical precision characteristics.
        /// </summary>
        public NumericalPrecision Precision { get; set; }

        /// <summary>
        /// Gets or sets whether the operator is numerically stable.
        /// </summary>
        public bool IsNumericallyStable { get; set; }

        /// <summary>
        /// Gets or sets the error propagation characteristics.
        /// </summary>
        public ErrorPropagation ErrorPropagation { get; set; }

        /// <summary>
        /// Gets or sets recommended mitigation strategies.
        /// </summary>
        public List<string> MitigationStrategies { get; set; } = [];
    }

    /// <summary>
    /// Represents operator compatibility with a backend.
    /// </summary>
    public sealed class OperatorCompatibility
    {
        /// <summary>
        /// Gets or sets whether the operator is supported.
        /// </summary>
        public bool IsSupported { get; set; }

        /// <summary>
        /// Gets or sets the support level.
        /// </summary>
        public SupportLevel SupportLevel { get; set; }

        /// <summary>
        /// Gets or sets alternative implementations if not natively supported.
        /// </summary>
        public List<string> Alternatives { get; set; } = [];

        /// <summary>
        /// Gets or sets performance characteristics on this backend.
        /// </summary>
        public PerformanceCharacteristics Performance { get; set; } = new();
    }

    /// <summary>
    /// Represents accuracy information for an operator.
    /// </summary>
    public sealed class AccuracyInfo
    {
        /// <summary>
        /// Gets or sets the typical relative error.
        /// </summary>
        public double RelativeError { get; set; }

        /// <summary>
        /// Gets or sets the typical absolute error.
        /// </summary>
        public double AbsoluteError { get; set; }

        /// <summary>
        /// Gets or sets whether the implementation is IEEE 754 compliant.
        /// </summary>
        public bool IsIEEE754Compliant { get; set; }

        /// <summary>
        /// Gets or sets special cases or limitations.
        /// </summary>
        public List<string> SpecialCases { get; set; } = [];

        /// <summary>
        /// Gets or sets the overall accuracy level of the operator.
        /// </summary>
        public AccuracyLevel AccuracyLevel { get; set; } = AccuracyLevel.High;

        /// <summary>
        /// Gets or sets the numerical stability measure.
        /// </summary>
        public double NumericalStability { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the error bounds information.
        /// </summary>
        public double ErrorBounds { get; set; } = 0.0;
    }

    /// <summary>
    /// Represents performance characteristics of an operator.
    /// </summary>
    public sealed class PerformanceCharacteristics
    {
        /// <summary>
        /// Gets or sets the throughput in operations per second.
        /// </summary>
        public double Throughput { get; set; }

        /// <summary>
        /// Gets or sets the latency per operation.
        /// </summary>
        public double Latency { get; set; }

        /// <summary>
        /// Gets or sets the memory bandwidth requirement.
        /// </summary>
        public double MemoryBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the compute intensity (ops per byte).
        /// </summary>
        public double ComputeIntensity { get; set; }

        /// <summary>
        /// Gets or sets the cache efficiency (0.0 to 1.0).
        /// </summary>
        public double CacheEfficiency { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the parallel efficiency (0.0 to 1.0).
        /// </summary>
        public double ParallelEfficiency { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets the scalability factor.
        /// </summary>
        public double ScalabilityFactor { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the power efficiency.
        /// </summary>
        public double PowerEfficiency { get; set; } = 1.0;
    }

    /// <summary>
    /// Defines computational complexity levels.
    /// </summary>
    public enum ComputationalComplexity
    {
        /// <summary>
        /// Constant time complexity.
        /// </summary>
        Constant,

        /// <summary>
        /// Logarithmic complexity.
        /// </summary>
        Logarithmic,

        /// <summary>
        /// Linear complexity.
        /// </summary>
        Linear,

        /// <summary>
        /// Quadratic complexity.
        /// </summary>
        Quadratic,

        /// <summary>
        /// Linearithmic complexity (O(n log n)).
        /// </summary>
        Linearithmic,

        /// <summary>
        /// Cubic complexity (O(n³)).
        /// </summary>
        Cubic,

        /// <summary>
        /// Exponential complexity.
        /// </summary>
        Exponential,

        /// <summary>
        /// Unknown or variable complexity.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Defines operator implementation methods.
    /// </summary>
    public enum ImplementationMethod
    {
        /// <summary>
        /// Hardware-native implementation.
        /// </summary>
        Native,

        /// <summary>
        /// Intrinsic function call.
        /// </summary>
        Intrinsic,

        /// <summary>
        /// Library function call.
        /// </summary>
        Library,

        /// <summary>
        /// Software emulation.
        /// </summary>
        Emulated,

        /// <summary>
        /// Custom implementation required.
        /// </summary>
        Custom,

        /// <summary>
        /// Not supported.
        /// </summary>
        NotSupported
    }

    /// <summary>
    /// Defines fusion patterns.
    /// </summary>
    public enum FusionPattern
    {
        /// <summary>
        /// Element-wise operations on the same data.
        /// </summary>
        ElementWise,

        /// <summary>
        /// Multiply-add operations.
        /// </summary>
        MultiplyAdd,

        /// <summary>
        /// Reduction operations.
        /// </summary>
        Reduction,

        /// <summary>
        /// Matrix operations.
        /// </summary>
        Matrix,

        /// <summary>
        /// Conditional operations.
        /// </summary>
        Conditional,

        /// <summary>
        /// Custom fusion pattern.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Defines fusion complexity levels.
    /// </summary>
    public enum FusionComplexity
    {
        /// <summary>
        /// Simple fusion with minimal overhead.
        /// </summary>
        Simple,

        /// <summary>
        /// Moderate complexity requiring some optimization.
        /// </summary>
        Moderate,

        /// <summary>
        /// Complex fusion requiring significant optimization effort.
        /// </summary>
        Complex,

        /// <summary>
        /// Very complex, may not be worth implementing.
        /// </summary>
        VeryComplex
    }

    /// <summary>
    /// Defines support levels for operators.
    /// </summary>
    public enum SupportLevel
    {
        /// <summary>
        /// Full native support with optimal performance.
        /// </summary>
        Full,

        /// <summary>
        /// Partial support, may have limitations.
        /// </summary>
        Partial,

        /// <summary>
        /// Basic support through emulation.
        /// </summary>
        Basic,

        /// <summary>
        /// No support available.
        /// </summary>
        None
    }

    /// <summary>
    /// Defines numerical precision characteristics.
    /// </summary>
    public enum NumericalPrecision
    {
        /// <summary>
        /// Exact computation.
        /// </summary>
        Exact,

        /// <summary>
        /// High precision with minimal error.
        /// </summary>
        High,

        /// <summary>
        /// Standard floating-point precision.
        /// </summary>
        Standard,

        /// <summary>
        /// Reduced precision for performance.
        /// </summary>
        Reduced,

        /// <summary>
        /// Low precision, significant errors possible.
        /// </summary>
        Low
    }

    /// <summary>
    /// Defines error propagation characteristics.
    /// </summary>
    public enum ErrorPropagation
    {
        /// <summary>
        /// Errors do not accumulate.
        /// </summary>
        None,

        /// <summary>
        /// Linear error propagation.
        /// </summary>
        Linear,

        /// <summary>
        /// Quadratic error growth.
        /// </summary>
        Quadratic,

        /// <summary>
        /// Exponential error growth.
        /// </summary>
        Exponential,

        /// <summary>
        /// Unstable error propagation.
        /// </summary>
        Unstable
    }

    /// <summary>
    /// Defines accuracy levels for operator implementations.
    /// </summary>
    public enum AccuracyLevel
    {
        /// <summary>
        /// Exact computation with no loss of precision.
        /// </summary>
        Exact,

        /// <summary>
        /// High accuracy with minimal error.
        /// </summary>
        High,

        /// <summary>
        /// Standard accuracy suitable for most applications.
        /// </summary>
        Standard,

        /// <summary>
        /// Reduced accuracy for performance optimization.
        /// </summary>
        Reduced,

        /// <summary>
        /// Low accuracy with potential significant errors.
        /// </summary>
        Low,

        /// <summary>
        /// Unknown or variable accuracy.
        /// </summary>
        Unknown
    }
}