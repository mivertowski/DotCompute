// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Analysis;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Compilation.Analysis;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Analyzes logical operators in expression trees.
/// </summary>
public class LogicalOperatorAnalyzer : DotCompute.Linq.Analysis.IOperatorAnalyzer
{
    /// <inheritdoc />
    public OperatorAnalysisResult AnalyzeOperator(Expression expression)
    {
        if (expression is not BinaryExpression && expression is not UnaryExpression)
        {
            return new OperatorAnalysisResult
            {
                OperatorType = expression.NodeType,
                IsComputeFriendly = false,
                SupportsVectorization = false,
                OptimalVectorWidth = 1,
                BackendCompatibility = new Dictionary<BackendType, OperatorCompatibility>(),
                OptimizationHints = [],
                Complexity = ComputationalComplexity.Linear,
                FusionOpportunities = []
            };
        }

        var operandTypes = GetInputTypes(expression).ToArray();
        var isVectorizable = IsVectorizable(expression.NodeType);


        return new OperatorAnalysisResult
        {
            OperatorType = expression.NodeType,
            OperandTypes = operandTypes,
            IsComputeFriendly = true,
            SupportsVectorization = isVectorizable,
            OptimalVectorWidth = GetOptimalVectorWidth(expression.NodeType, operandTypes),
            BackendCompatibility = AnalyzeBackendCompatibility(expression.NodeType, operandTypes),
            OptimizationHints = GenerateOptimizationHints(expression),
            Complexity = ComputationalComplexity.Constant,
            FusionOpportunities = FindFusionOpportunities(expression)
        };
    }

    /// <inheritdoc />
    public OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        var isSupported = IsSupportedOperator(operatorType);
        var performance = GetPerformanceCharacteristics(operatorType, backend);


        return new OperatorInfo
        {
            OperatorType = operatorType,
            OperandTypes = operandTypes,
            ResultType = typeof(bool), // Logical operators return bool
            Backend = backend,
            Implementation = GetImplementationMethod(operatorType, backend),
            IsNativelySupported = isSupported,
            SupportsVectorization = IsVectorizable(operatorType),
            OptimalVectorWidth = GetOptimalVectorWidth(operatorType, operandTypes),
            Performance = performance,
            Accuracy = new AccuracyInfo
            {
                RelativeError = 0.0, // Logical operations are exact
                AbsoluteError = 0.0,
                IsIEEE754Compliant = true,
                SpecialCases = GetSpecialCases(operatorType)
            },
            Complexity = ComputationalComplexity.Constant,
            IsCommutative = IsCommutative(operatorType),
            IsAssociative = IsAssociative(operatorType),
            IsDistributive = IsDistributive(operatorType),
            MemoryAccessPattern = DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential
        };
    }

    /// <inheritdoc />
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes)
    {
        var isVectorizable = IsVectorizable(operatorType);


        return new VectorizationAnalysis
        {
            IsVectorizable = isVectorizable,
            OptimalWidth = isVectorizable ? GetOptimalVectorWidth(operatorType, operandTypes) : 1,
            SupportedWidths = isVectorizable ? GetSupportedWidths(operandTypes.FirstOrDefault() ?? typeof(bool)) : [1],
            ExpectedSpeedup = isVectorizable ? EstimateVectorizationSpeedup(operatorType) : 1.0,
            Constraints = GetVectorizationConstraints(operatorType),
            VectorInstruction = GetVectorInstruction(operatorType, operandTypes.FirstOrDefault() ?? typeof(bool))
        };
    }

    /// <inheritdoc />
    public FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators)
    {
        var operatorList = operators.ToList();
        var opportunities = new List<FusionOpportunity>();

        // Look for logical operation chains
        for (int i = 0; i < operatorList.Count - 1; i++)
        {
            var current = operatorList[i];
            var next = operatorList[i + 1];

            if (CanFuseOperations(current.NodeType, next.NodeType))
            {
                opportunities.Add(new FusionOpportunity
                {
                    Operators = [current.NodeType, next.NodeType],
                    Pattern = FusionPattern.ElementWise,
                    PerformanceBenefit = 1.4,
                    MemorySavings = 0.3,
                    Complexity = IsShortCircuiting(current.NodeType) || IsShortCircuiting(next.NodeType) ?

                        FusionComplexity.Complex : FusionComplexity.Simple
                });
            }
        }

        return new FusionAnalysisResult
        {
            IsFusionBeneficial = opportunities.Count > 0,
            Opportunities = opportunities,
            EstimatedImprovement = opportunities.Sum(o => o.PerformanceBenefit),
            Constraints = GetFusionConstraints(operatorList)
        };
    }

    /// <inheritdoc />
    public double EstimateComputationalCost(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        var baseCost = GetBaseCost(operatorType);
        var backendMultiplier = backend switch
        {
            BackendType.CPU => 1.0,
            BackendType.CUDA => IsShortCircuiting(operatorType) ? 2.0 : 0.2, // Short-circuiting is problematic on GPU
            _ => 1.2
        };


        return baseCost * backendMultiplier;
    }

    /// <inheritdoc />
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes)
    {
        return new PrecisionAnalysisResult
        {
            Precision = NumericalPrecision.Exact, // Logical operations are exact
            IsNumericallyStable = true,
            ErrorPropagation = ErrorPropagation.None,
            MitigationStrategies = ["No precision issues with boolean operations"]
        };
    }

    /// <inheritdoc />
    public OperatorAnalysisResult Analyze(Expression expression, object? context = null)
    {
        return AnalyzeOperator(expression);
    }

    /// <inheritdoc />
    public ParallelizationOpportunity AnalyzeParallelization(Expression expression)
    {
        var isShortCircuiting = IsShortCircuiting(expression.NodeType);


        return new ParallelizationOpportunity
        {
            VectorizationSuitable = !isShortCircuiting,
            SupportsParallelExecution = !isShortCircuiting,
            RecommendedParallelism = isShortCircuiting ? 1 : Environment.ProcessorCount,
            DataDependencies = isShortCircuiting ? ["Short-circuiting evaluation order"] : [],
            EstimatedSpeedup = isShortCircuiting ? 1.0 : 3.0
        };
    }

    /// <inheritdoc />
    public double EstimateExecutionCost(Expression expression)
    {
        if (expression == null)
            return 0.0;


        return GetBaseCost(expression.NodeType);
    }

    /// <inheritdoc />
    public DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, DotCompute.Linq.Pipelines.Analysis.AnalysisContext context)
    {
        var operatorName = GetOperatorName(expression.NodeType);
        var inputTypes = GetInputTypes(expression);
        var outputType = typeof(bool);

        return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            OperatorType = UnifiedOperatorType.Logical,
            Name = operatorName,
            InputTypes = inputTypes.ToList(),
            OutputType = outputType,
            ComplexityScore = 1,
            SupportsGpu = IsVectorizable(expression.NodeType),
            SupportsCpu = true,
            Properties = new Dictionary<string, object>
            {
                ["IsVectorizable"] = IsVectorizable(expression.NodeType),
                ["SupportsFusion"] = true,
                ["ShortCircuiting"] = IsShortCircuiting(expression.NodeType)
            }
        };
    }


    private static string GetOperatorName(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.AndAlso => "AndAlso",
        ExpressionType.OrElse => "OrElse",
        ExpressionType.And => "And",
        ExpressionType.Or => "Or",
        ExpressionType.ExclusiveOr => "ExclusiveOr",
        ExpressionType.Not => "Not",
        _ => nodeType.ToString()
    };

    private static IReadOnlyList<Type> GetInputTypes(Expression expression) => expression switch
    {
        BinaryExpression binary => [binary.Left.Type, binary.Right.Type],
        UnaryExpression unary => [unary.Operand.Type],
        _ => Array.Empty<Type>()
    };

    private static bool IsVectorizable(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.And => true,
        ExpressionType.Or => true,
        ExpressionType.ExclusiveOr => true,
        ExpressionType.Not => true,
        ExpressionType.AndAlso => false, // Short-circuiting makes vectorization complex
        ExpressionType.OrElse => false,  // Short-circuiting makes vectorization complex
        _ => false
    };

    private static bool IsShortCircuiting(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.AndAlso => true,
        ExpressionType.OrElse => true,
        _ => false
    };

    private static bool IsSupportedOperator(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.AndAlso or ExpressionType.OrElse or
        ExpressionType.And or ExpressionType.Or or
        ExpressionType.ExclusiveOr or ExpressionType.Not => true,
        _ => false
    };

    private static int GetOptimalVectorWidth(ExpressionType operatorType, Type[] operandTypes)
    {
        if (!IsVectorizable(operatorType))
            return 1;


        return operandTypes.FirstOrDefault() switch
        {
            var t when t == typeof(int) => 8,
            var t when t == typeof(byte) => 32,
            var t when t == typeof(bool) => 32,
            _ => 8
        };
    }

    private static int[] GetSupportedWidths(Type type) => type switch
    {
        var t when t == typeof(bool) => [8, 16, 32, 64],
        var t when t == typeof(byte) => [16, 32, 64],
        var t when t == typeof(int) => [4, 8, 16],
        _ => [1]
    };

    private static PerformanceCharacteristics GetPerformanceCharacteristics(ExpressionType operatorType, BackendType backend)
    {
        var baseThroughput = 2e9; // Logical operations are very fast
        var backendMultiplier = backend switch
        {
            BackendType.CUDA => IsShortCircuiting(operatorType) ? 0.1 : 300.0, // Short-circuiting is bad on GPU
            BackendType.CPU => 1.0,
            _ => 1.0
        };

        return new PerformanceCharacteristics
        {
            Throughput = baseThroughput * backendMultiplier,
            Latency = 1.0 / (baseThroughput * backendMultiplier),
            MemoryBandwidth = backend == BackendType.CUDA ? 900e9 : 50e9,
            ComputeIntensity = 0.5 // Very low compute intensity
        };
    }

    private static ImplementationMethod GetImplementationMethod(ExpressionType operatorType, BackendType backend)
    {
        return backend switch
        {
            BackendType.CPU => ImplementationMethod.Intrinsic,
            BackendType.CUDA => IsShortCircuiting(operatorType) ? ImplementationMethod.Emulated : ImplementationMethod.Native,
            _ => ImplementationMethod.Library
        };
    }

    private static List<string> GetSpecialCases(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.AndAlso => ["Short-circuit evaluation"],
        ExpressionType.OrElse => ["Short-circuit evaluation"],
        _ => []
    };

    private static bool IsCommutative(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => true,
        _ => false
    };

    private static bool IsAssociative(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => true,
        _ => false
    };

    private static bool IsDistributive(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.And => true, // AND distributes over OR
        ExpressionType.Or => true,  // OR distributes over AND
        _ => false
    };

    private static Dictionary<BackendType, OperatorCompatibility> AnalyzeBackendCompatibility(ExpressionType operatorType, Type[] operandTypes)
    {
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>();
        var isSupported = IsSupportedOperator(operatorType);
        var isShortCircuiting = IsShortCircuiting(operatorType);


        compatibility[BackendType.CPU] = new OperatorCompatibility
        {
            IsSupported = isSupported,
            SupportLevel = isSupported ? SupportLevel.Full : SupportLevel.None,
            Alternatives = isSupported ? [] : ["Software emulation"],
            Performance = GetPerformanceCharacteristics(operatorType, BackendType.CPU)
        };


        compatibility[BackendType.CUDA] = new OperatorCompatibility
        {
            IsSupported = isSupported && !isShortCircuiting,
            SupportLevel = isSupported ? (isShortCircuiting ? SupportLevel.Partial : SupportLevel.Full) : SupportLevel.None,
            Alternatives = isSupported ? (isShortCircuiting ? ["CPU fallback", "Predicated execution"] : []) : ["CPU fallback"],
            Performance = GetPerformanceCharacteristics(operatorType, BackendType.CUDA)
        };


        return compatibility;
    }

    private static List<DotCompute.Linq.Types.OptimizationHint> GenerateOptimizationHints(Expression expression)
    {
        var hints = new List<DotCompute.Linq.Types.OptimizationHint>();


        if (IsVectorizable(expression.NodeType))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Vectorization,
                Description = "Logical operation can be vectorized using bitwise SIMD instructions",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = 4.0
            });
        }


        if (IsShortCircuiting(expression.NodeType))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.BranchPrediction,
                Description = "Short-circuiting may cause divergent branching on GPU",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = -2.0 // Negative for GPU
            });
        }


        return hints;
    }

    private static List<FusionOpportunity> FindFusionOpportunities(Expression expression)
    {
        var opportunities = new List<FusionOpportunity>();


        if (IsVectorizable(expression.NodeType))
        {
            opportunities.Add(new FusionOpportunity
            {
                Operators = [expression.NodeType],
                Pattern = FusionPattern.ElementWise,
                PerformanceBenefit = 1.3,
                MemorySavings = 0.2,
                Complexity = FusionComplexity.Simple
            });
        }


        return opportunities;
    }

    private static double EstimateVectorizationSpeedup(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => 8.0, // Very good vectorization
        ExpressionType.Not => 16.0, // Excellent vectorization
        _ => 1.0
    };

    private static List<string> GetVectorizationConstraints(ExpressionType operatorType)
    {
        var constraints = new List<string>();


        if (IsShortCircuiting(operatorType))
        {
            constraints.Add("Short-circuiting prevents straightforward vectorization");
            constraints.Add("Requires predicated execution or masking");
        }


        return constraints;
    }

    private static string? GetVectorInstruction(ExpressionType operatorType, Type type)
    {
        return (operatorType, type) switch
        {
            (ExpressionType.And, _) => "vpand",
            (ExpressionType.Or, _) => "vpor",
            (ExpressionType.ExclusiveOr, _) => "vpxor",
            (ExpressionType.Not, _) => "vpxor", // XOR with all 1s
            _ => null
        };
    }

    private static bool CanFuseOperations(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.And, ExpressionType.And) or
            (ExpressionType.Or, ExpressionType.Or) or
            (ExpressionType.ExclusiveOr, ExpressionType.ExclusiveOr) => true,
            (ExpressionType.And, ExpressionType.Or) or
            (ExpressionType.Or, ExpressionType.And) => true, // Can form compound expressions
            _ => false
        };
    }

    private static List<string> GetFusionConstraints(List<Expression> operators)
    {
        var constraints = new List<string>();


        if (operators.Any(op => IsShortCircuiting(op.NodeType)))
        {
            constraints.Add("Short-circuiting operators complicate fusion");
        }


        return constraints;
    }

    private static double GetBaseCost(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.And or ExpressionType.Or or

        ExpressionType.ExclusiveOr or ExpressionType.Not => 0.5, // Very fast
        ExpressionType.AndAlso or ExpressionType.OrElse => 1.5, // Slower due to branching
        _ => 1.0
    };
}