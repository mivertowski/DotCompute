// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Analysis;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Compilation.Analysis;
using CompilationOperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;
using PipelineOperatorInfo = DotCompute.Linq.Pipelines.Analysis.OperatorInfo;

namespace DotCompute.Linq.Analysis;
{

/// <summary>
/// Analyzes conditional operators in expression trees.
/// </summary>
public class ConditionalOperatorAnalyzer : DotCompute.Linq.Analysis.IOperatorAnalyzer
{
    /// <inheritdoc />
    public OperatorAnalysisResult AnalyzeOperator(Expression expression)
    {
        if (expression is not ConditionalExpression conditional)
        {
            return new OperatorAnalysisResult
            {
                OperatorType = expression.NodeType,
                IsComputeFriendly = false,
                SupportsVectorization = false
            };
        }

        return new OperatorAnalysisResult
        {
            OperatorType = ExpressionType.Conditional,
            OperandTypes = [conditional.Test.Type, conditional.IfTrue.Type, conditional.IfFalse.Type],
            IsComputeFriendly = IsConditionalComputeFriendly(conditional),
            SupportsVectorization = CanVectorizeConditional(conditional),
            OptimalVectorWidth = GetOptimalConditionalVectorWidth(conditional),
            BackendCompatibility = AnalyzeConditionalBackendCompatibility(conditional),
            OptimizationHints = GenerateConditionalOptimizationHints(conditional),
            Complexity = DetermineConditionalComplexity(conditional),
            FusionOpportunities = FindConditionalFusionOpportunities(conditional)
        };
    }

    /// <inheritdoc />
    public DotCompute.Linq.Analysis.OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        return backend == BackendType.CUDA

            ? DotCompute.Linq.Analysis.OperatorInfo.ForCUDA(operatorType, operandTypes, operandTypes?.LastOrDefault() ?? typeof(object))
            : DotCompute.Linq.Analysis.OperatorInfo.ForCPU(operatorType, operandTypes, operandTypes?.LastOrDefault() ?? typeof(object));
    }


    /// <inheritdoc />
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes)
    {
        // Default analysis for method calls - generally not vectorizable without more context
        return new VectorizationAnalysis
        {
            IsVectorizable = false,
            OptimalWidth = 1,
            SupportedWidths = [1],
            ExpectedSpeedup = 1.0,
            Constraints = ["Method calls require specific analysis per method"]
        };
    }

    /// <inheritdoc />
    public FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators)
    {
        var methodCalls = operators.OfType<MethodCallExpression>().ToList();


        var opportunities = new List<FusionOpportunity>();

        // Look for chainable LINQ operations

        for (var i = 0; i < methodCalls.Count - 1; i++)
        {
            var current = methodCalls[i];
            var next = methodCalls[i + 1];


            if (CanFuseMethods(current, next))
            {
                opportunities.Add(new FusionOpportunity
                {
                    Operators = [current.NodeType, next.NodeType],
                    Pattern = DetermineFusionPattern(current, next),
                    PerformanceBenefit = EstimatePerformanceBenefit(current, next),
                    MemorySavings = EstimateMemorySavings(current, next),
                    Complexity = EstimateFusionComplexity(current, next)
                });
            }
        }

        return new FusionAnalysisResult
        {
            IsFusionBeneficial = opportunities.Count > 0,
            Opportunities = opportunities,
            EstimatedImprovement = opportunities.Sum(o => o.PerformanceBenefit),
            Constraints = ["Method fusion depends on specific method implementations"]
        };
    }

    /// <inheritdoc />
    public double EstimateComputationalCost(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        // Method calls generally have moderate to high cost due to overhead
        return backend switch
        {
            BackendType.CPU => 5.0,
            BackendType.CUDA => 10.0, // Higher cost due to kernel launch overhead
            _ => 7.5
        };
    }

    /// <inheritdoc />
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes)
    {
        return new PrecisionAnalysisResult
        {
            Precision = NumericalPrecision.Standard,
            IsNumericallyStable = true,
            ErrorPropagation = ErrorPropagation.Linear,
            MitigationStrategies = ["Use appropriate numeric types", "Consider precision requirements"]
        };
    }

    /// <inheritdoc />
    public OperatorAnalysisResult Analyze(Expression expression, object? context = null)
    {
        if (expression is ConditionalExpression conditional)
        {
            return AnalyzeOperator(conditional);
        }

        // Create a basic analysis result for non-conditional expressions

        return new OperatorAnalysisResult
        {
            OperatorType = expression.NodeType,
            OperandTypes = [],
            IsComputeFriendly = false,
            SupportsVectorization = false,
            OptimalVectorWidth = 1,
            BackendCompatibility = [],
            OptimizationHints = [],
            Complexity = ComputationalComplexity.Linear,
            FusionOpportunities = []
        };
    }

    /// <summary>
    /// Determines if a conditional is compute-friendly.
    /// </summary>
    private static bool IsConditionalComputeFriendly(ConditionalExpression conditional)
    {
        // Conditional expressions are generally compute-friendly if their branches are simple
        return IsExpressionSimple(conditional.IfTrue) && IsExpressionSimple(conditional.IfFalse);
    }

    /// <summary>
    /// Checks if an expression is simple (compute-friendly).
    /// </summary>
    private static bool IsExpressionSimple(Expression expression)
    {
        return expression.NodeType switch
        {
            ExpressionType.Constant => true,
            ExpressionType.Parameter => true,
            ExpressionType.MemberAccess => true,
            ExpressionType.Add or ExpressionType.Subtract or

            ExpressionType.Multiply or ExpressionType.Divide => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a conditional can be vectorized.
    /// </summary>
    private static bool CanVectorizeConditional(ConditionalExpression conditional)
    {
        // Conditionals can be vectorized if both branches are vectorizable and the test is simple
        return IsVectorizableExpression(conditional.Test) &&
               IsVectorizableExpression(conditional.IfTrue) &&
               IsVectorizableExpression(conditional.IfFalse);
    }

    /// <summary>
    /// Checks if an expression can be vectorized.
    /// </summary>
    private static bool IsVectorizableExpression(Expression expression)
    {
        return expression.NodeType switch
        {
            ExpressionType.Constant => true,
            ExpressionType.Parameter => true,
            ExpressionType.Add or ExpressionType.Subtract or

            ExpressionType.Multiply or ExpressionType.Divide => true,
            ExpressionType.GreaterThan or ExpressionType.LessThan or
            ExpressionType.GreaterThanOrEqual or ExpressionType.LessThanOrEqual => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the optimal vector width for a conditional.
    /// </summary>
    private static int GetOptimalConditionalVectorWidth(ConditionalExpression conditional)
    {
        if (!CanVectorizeConditional(conditional))
        {
            return 1;
        }

        // For conditionals, use conservative vector width due to branching overhead

        return 4;
    }

    /// <summary>
    /// Analyzes backend compatibility for a conditional.
    /// </summary>
    private static Dictionary<BackendType, OperatorCompatibility> AnalyzeConditionalBackendCompatibility(ConditionalExpression conditional)
    {
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>();


        var isComputeFriendly = IsConditionalComputeFriendly(conditional);

        // CPU supports all conditionals

        compatibility[BackendType.CPU] = new OperatorCompatibility
        {
            IsSupported = true,
            SupportLevel = SupportLevel.Full,
            Alternatives = [],
            Performance = new PerformanceCharacteristics
            {
                Throughput = 800.0, // Lower than pure arithmetic due to branching
                Latency = 0.002,
                MemoryBandwidth = 80.0,
                ComputeIntensity = 8.0
            }
        };

        // GPU has challenges with branching but can handle simple conditionals

        compatibility[BackendType.CUDA] = new OperatorCompatibility
        {
            IsSupported = isComputeFriendly,
            SupportLevel = isComputeFriendly ? SupportLevel.Partial : SupportLevel.None,
            Alternatives = isComputeFriendly ? [] : ["CPU fallback", "Predicated execution"],
            Performance = new PerformanceCharacteristics
            {
                Throughput = isComputeFriendly ? 5000.0 : 0.0,
                Latency = isComputeFriendly ? 0.0005 : 0.0,
                MemoryBandwidth = isComputeFriendly ? 400.0 : 0.0,
                ComputeIntensity = isComputeFriendly ? 50.0 : 0.0
            }
        };


        return compatibility;
    }

    /// <summary>
    /// Generates optimization hints for a conditional.
    /// </summary>
    private static List<DotCompute.Linq.Types.OptimizationHint> GenerateConditionalOptimizationHints(ConditionalExpression conditional)
    {
        var hints = new List<DotCompute.Linq.Types.OptimizationHint>();


        if (CanVectorizeConditional(conditional))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Vectorization,
                Description = "Conditional can be vectorized using predicated execution",
                Priority = DotCompute.Linq.Types.OptimizationPriority.Medium,
                EstimatedBenefit = 1.5
            });
        }


        if (HasComplexBranches(conditional))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.BranchPrediction,
                Description = "Consider branch prediction optimization or predicated execution",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = 2.5
            });
        }


        return hints;
    }

    /// <summary>
    /// Checks if conditional has complex branches.
    /// </summary>
    private static bool HasComplexBranches(ConditionalExpression conditional)
    {
        return !IsExpressionSimple(conditional.IfTrue) || !IsExpressionSimple(conditional.IfFalse);
    }

    /// <summary>
    /// Determines computational complexity for a conditional.
    /// </summary>
    private static ComputationalComplexity DetermineConditionalComplexity(ConditionalExpression conditional)
    {
        // Conditionals are generally constant time, but can be linear if branches have linear complexity
        var testComplexity = GetExpressionComplexity(conditional.Test);
        var trueComplexity = GetExpressionComplexity(conditional.IfTrue);
        var falseComplexity = GetExpressionComplexity(conditional.IfFalse);


        return new[] { testComplexity, trueComplexity, falseComplexity }.Max();
    }

    /// <summary>
    /// Gets computational complexity of an expression.
    /// </summary>
    private static ComputationalComplexity GetExpressionComplexity(Expression expression)
    {
        return expression.NodeType switch
        {
            ExpressionType.Constant or ExpressionType.Parameter => ComputationalComplexity.Constant,
            ExpressionType.Add or ExpressionType.Subtract or

            ExpressionType.Multiply or ExpressionType.Divide => ComputationalComplexity.Constant,
            ExpressionType.Call => ComputationalComplexity.Linear,
            _ => ComputationalComplexity.Linear
        };
    }

    /// <summary>
    /// Finds fusion opportunities for a conditional.
    /// </summary>
    private static List<FusionOpportunity> FindConditionalFusionOpportunities(ConditionalExpression conditional)
    {
        var opportunities = new List<FusionOpportunity>();

        // Conditionals can be fused with element-wise operations

        if (IsConditionalComputeFriendly(conditional))
        {
            opportunities.Add(new FusionOpportunity
            {
                Operators = [ExpressionType.Conditional],
                Pattern = FusionPattern.ElementWise,
                PerformanceBenefit = 1.2,
                MemorySavings = 0.3,
                Complexity = FusionComplexity.Moderate
            });
        }


        return opportunities;
    }

    /// <summary>
    /// Determines if two methods can be fused.
    /// </summary>
    private static bool CanFuseMethods(MethodCallExpression first, MethodCallExpression second)
    {
        // Simple heuristic: LINQ operations on the same type can often be fused
        return first.Method.DeclaringType == typeof(Enumerable) &&
               second.Method.DeclaringType == typeof(Enumerable) &&
               first.Type.IsAssignableFrom(second.Arguments.FirstOrDefault()?.Type ?? typeof(object));
    }

    /// <summary>
    /// Determines the fusion pattern for two methods.
    /// </summary>
    private static FusionPattern DetermineFusionPattern(MethodCallExpression first, MethodCallExpression second)
    {
        var firstName = first.Method.Name;
        var secondName = second.Method.Name;


        return (firstName, secondName) switch
        {
            ("Where", "Select") or ("Select", "Where") => FusionPattern.ElementWise,
            ("Select", "Sum") or ("Select", "Count") => FusionPattern.Reduction,
            _ => FusionPattern.Custom
        };
    }

    /// <summary>
    /// Estimates performance benefit from fusing two methods.
    /// </summary>
    private static double EstimatePerformanceBenefit(MethodCallExpression first, MethodCallExpression second)
    {
        // Simple estimation based on eliminating intermediate collections
        return 1.5; // 50% improvement from avoiding intermediate allocation
    }

    /// <summary>
    /// Estimates memory savings from fusing two methods.
    /// </summary>
    private static double EstimateMemorySavings(MethodCallExpression first, MethodCallExpression second)
    {
        // Estimate based on avoiding intermediate storage
        return 0.8; // 80% memory reduction from eliminating intermediate collection
    }

    /// <summary>
    /// Estimates complexity of fusing two methods.
    /// </summary>
    private static FusionComplexity EstimateFusionComplexity(MethodCallExpression first, MethodCallExpression second)
    {
        var firstName = first.Method.Name;
        var secondName = second.Method.Name;


        return (firstName, secondName) switch
        {
            ("Where", "Select") or ("Select", "Where") => FusionComplexity.Simple,
            ("Select", "Sum") or ("Select", "Count") => FusionComplexity.Moderate,
            _ => FusionComplexity.Complex
        };
    }


    /// <inheritdoc />
    public ParallelizationOpportunity AnalyzeParallelization(Expression expression)
    {
        if (expression is not ConditionalExpression conditional)
        {
            return new ParallelizationOpportunity
            {
                VectorizationSuitable = false,
                SupportsParallelExecution = false,
                RecommendedParallelism = 1,
                DataDependencies = ["Non-conditional expression"],
                EstimatedSpeedup = 1.0
            };
        }

        var hasConstantCondition = HasConstantCondition(conditional);
        var branchDivergence = GetBranchDivergence(conditional);


        return new ParallelizationOpportunity
        {
            VectorizationSuitable = hasConstantCondition || branchDivergence < 0.3,
            SupportsParallelExecution = true,
            RecommendedParallelism = hasConstantCondition ? Environment.ProcessorCount : Math.Max(1, Environment.ProcessorCount / 2),
            DataDependencies = hasConstantCondition ? [] : ["Branch-dependent data access"],
            EstimatedSpeedup = hasConstantCondition ? 2.0 : (1.0 + (1.0 - branchDivergence))
        };
    }

    /// <inheritdoc />
    public double EstimateExecutionCost(Expression expression)
    {
        if (expression == null)
        {
            return 0.0;
        }

        if (expression is not ConditionalExpression conditional)
        {

            return 2.0; // Basic cost for non-conditional
        }

        var baseCost = 3.0; // Base cost for conditional
        var branchPenalty = GetBranchDivergence(conditional) * 2.0;
        var complexityPenalty = EstimateExpressionComplexity(conditional.Test) +

                               Math.Max(EstimateExpressionComplexity(conditional.IfTrue),

                                       EstimateExpressionComplexity(conditional.IfFalse));


        return baseCost + branchPenalty + complexityPenalty * 0.5;
    }

    /// <inheritdoc />
    public DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, DotCompute.Linq.Pipelines.Analysis.AnalysisContext context)
    {
        if (expression is not ConditionalExpression conditional)
        {
            return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
            {
                OperatorType = UnifiedOperatorType.Unknown,
                Name = "UnknownConditional",
                InputTypes = [],
                OutputType = expression.Type,
                ComplexityScore = 1,
                SupportsGpu = false,
                SupportsCpu = true
            };
        }

        var inputTypes = new[] { conditional.Test.Type, conditional.IfTrue.Type, conditional.IfFalse.Type };

        return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            OperatorType = UnifiedOperatorType.Logical,
            Name = "Conditional",
            InputTypes = inputTypes.ToList(),
            OutputType = conditional.Type,
            ComplexityScore = EstimateComplexity(conditional),
            SupportsGpu = false, // Conditionals are challenging for GPU execution
            SupportsCpu = true,
            Properties = new Dictionary<string, object>
            {
                ["BranchDivergence"] = GetBranchDivergence(conditional),
                ["HasConstantCondition"] = HasConstantCondition(conditional),
                ["IsVectorizable"] = false // Generally not vectorizable due to branching
            }
        };
    }

    private static int EstimateComplexity(ConditionalExpression conditional)
    {
        // Base complexity for the conditional operation
        var complexity = 2;

        // Add complexity for condition evaluation
        complexity += EstimateExpressionComplexity(conditional.Test);

        // Add complexity for branch expressions
        complexity += Math.Max(EstimateExpressionComplexity(conditional.IfTrue),

                              EstimateExpressionComplexity(conditional.IfFalse));

        return complexity;
    }

    private static double GetBranchDivergence(ConditionalExpression conditional)
    {
        // Estimate branch divergence based on condition complexity
        if (HasConstantCondition(conditional))
        {
            return 0.0; // No divergence if condition is constant
        }


        if (HasSimpleCondition(conditional))
        {

            return 0.5; // Moderate divergence for simple conditions
        }


        return 0.8; // High divergence for complex conditions
    }

    private static bool HasConstantCondition(ConditionalExpression conditional)
    {
        return conditional.Test is ConstantExpression;
    }

    private static bool HasSimpleCondition(ConditionalExpression conditional)
    {
        return conditional.Test switch
        {
            BinaryExpression binary when IsComparisonOrLogical(binary.NodeType) => true,
            UnaryExpression unary when unary.NodeType == ExpressionType.Not => true,
            MemberExpression => true,
            ParameterExpression => true,
            ConstantExpression => true,
            _ => false
        };
    private static bool IsComparisonOrLogical(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal or
        ExpressionType.NotEqual or
        ExpressionType.LessThan or
        ExpressionType.LessThanOrEqual or
        ExpressionType.GreaterThan or
        ExpressionType.GreaterThanOrEqual or
        ExpressionType.AndAlso or
        ExpressionType.OrElse or
        ExpressionType.And or
        ExpressionType.Or => true,
        _ => false
    };

    private static int EstimateExpressionComplexity(Expression expression) => expression switch
    {
        ConstantExpression => 0,
        ParameterExpression => 0,
        MemberExpression => 1,
        BinaryExpression => 1,
        UnaryExpression => 1,
        ConditionalExpression => 3,
        MethodCallExpression => 5,
        _ => 2
    };
}
