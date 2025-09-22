// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Analysis;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Compilation.Analysis;

namespace DotCompute.Linq.Analysis;
{

/// <summary>
/// Analyzes comparison operators in expression trees.
/// </summary>
public class ComparisonOperatorAnalyzer : DotCompute.Linq.Analysis.IOperatorAnalyzer
{
    /// <inheritdoc />
    public OperatorAnalysisResult AnalyzeOperator(Expression expression)
    {
        if (expression is not BinaryExpression binaryExpr)
        {
            return new OperatorAnalysisResult
            {
                OperatorType = expression.NodeType,
                IsComputeFriendly = false,
                SupportsVectorization = false,
                OptimalVectorWidth = 1,
                BackendCompatibility = [],
                OptimizationHints = [],
                Complexity = ComputationalComplexity.Linear,
                FusionOpportunities = []
            };
        }

        var operandTypes = GetInputTypes(expression).ToArray();
        var isVectorizable = IsVectorizable(operandTypes);


        return new OperatorAnalysisResult
        {
            OperatorType = expression.NodeType,
            OperandTypes = operandTypes,
            IsComputeFriendly = true,
            SupportsVectorization = isVectorizable,
            OptimalVectorWidth = GetOptimalVectorWidth(operandTypes),
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
            ResultType = typeof(bool), // Comparison operators return bool
            Backend = backend,
            Implementation = GetImplementationMethod(operatorType, backend),
            IsNativelySupported = isSupported,
            SupportsVectorization = IsVectorizable(operandTypes),
            OptimalVectorWidth = GetOptimalVectorWidth(operandTypes),
            Performance = performance,
            Accuracy = new AccuracyInfo
            {
                RelativeError = 0.0, // Comparisons are exact
                AbsoluteError = 0.0,
                IsIEEE754Compliant = true,
                SpecialCases = GetSpecialCases(operatorType)
            },
            Complexity = ComputationalComplexity.Constant,
            IsCommutative = IsCommutative(operatorType),
            IsAssociative = false, // Comparisons are not associative
            IsDistributive = false,
            MemoryAccessPattern = DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential
        };
    }

    /// <inheritdoc />
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes)
    {
        var isVectorizable = IsSupportedOperator(operatorType) && IsVectorizable(operandTypes);


        return new VectorizationAnalysis
        {
            IsVectorizable = isVectorizable,
            OptimalWidth = isVectorizable ? GetOptimalVectorWidth(operandTypes) : 1,
            SupportedWidths = isVectorizable ? GetSupportedWidths(operandTypes.FirstOrDefault() ?? typeof(object)) : [1],
            ExpectedSpeedup = isVectorizable ? EstimateVectorizationSpeedup(operatorType) : 1.0,
            Constraints = GetVectorizationConstraints(operatorType, operandTypes),
            VectorInstruction = GetVectorInstruction(operatorType, operandTypes.FirstOrDefault() ?? typeof(object))
        };
    }

    /// <inheritdoc />
    public FusionAnalysisResult AnalyzeFusion(IEnumerable<Expression> operators)
    {
        var operatorList = operators.ToList();
        var opportunities = new List<FusionOpportunity>();

        // Look for comparison chains that can be optimized
        for (var i = 0; i < operatorList.Count - 1; i++)
        {
            var current = operatorList[i];
            var next = operatorList[i + 1];

            if (CanFuseOperations(current.NodeType, next.NodeType))
            {
                opportunities.Add(new FusionOpportunity
                {
                    Operators = [current.NodeType, next.NodeType],
                    Pattern = FusionPattern.Conditional,
                    PerformanceBenefit = 1.3,
                    MemorySavings = 0.2,
                    Complexity = FusionComplexity.Moderate
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
            BackendType.CUDA => 0.2, // Very fast on GPU
            _ => 1.2
        };


        return baseCost * backendMultiplier;
    }

    /// <inheritdoc />
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes)
    {
        return new PrecisionAnalysisResult
        {
            Precision = NumericalPrecision.Exact, // Comparisons are exact
            IsNumericallyStable = true,
            ErrorPropagation = ErrorPropagation.None,
            MitigationStrategies = GetPrecisionMitigationStrategies(operatorType, operandTypes)
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
        return new ParallelizationOpportunity
        {
            VectorizationSuitable = IsSupportedOperator(expression.NodeType),
            SupportsParallelExecution = true,
            RecommendedParallelism = Environment.ProcessorCount,
            DataDependencies = [],
            EstimatedSpeedup = IsSupportedOperator(expression.NodeType) ? 3.0 : 1.0
        };
    }

    /// <inheritdoc />
    public double EstimateExecutionCost(Expression expression)
    {
        if (expression == null)
        {

            return 0.0;
        }

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
            OperatorType = UnifiedOperatorType.Comparison,
            Name = operatorName,
            InputTypes = inputTypes.ToList(),
            OutputType = outputType,
            ComplexityScore = 1,
            SupportsGpu = true,
            SupportsCpu = true,
            Properties = new Dictionary<string, object>
            {
                ["IsVectorizable"] = IsVectorizable(inputTypes),
                ["SupportsFusion"] = true,
                ["MemoryBound"] = false
            }
        };
    }


    private static string GetOperatorName(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => "Equal",
        ExpressionType.NotEqual => "NotEqual",
        ExpressionType.LessThan => "LessThan",
        ExpressionType.LessThanOrEqual => "LessThanOrEqual",
        ExpressionType.GreaterThan => "GreaterThan",
        ExpressionType.GreaterThanOrEqual => "GreaterThanOrEqual",
        _ => nodeType.ToString()
    };

    private static IReadOnlyList<Type> GetInputTypes(Expression expression) => expression switch
    {
        BinaryExpression binary => [binary.Left.Type, binary.Right.Type],
        _ => Array.Empty<Type>()
    };

    private static bool IsVectorizable(IReadOnlyList<Type> inputTypes)
        {
        => inputTypes.All(type => type switch
        {
            var t when t == typeof(int) => true,
            var t when t == typeof(float) => true,
            var t when t == typeof(double) => true,
            var t when t == typeof(long) => true,
            var t when t == typeof(short) => true,
            var t when t == typeof(byte) => true,
            _ => false
        });


    private static bool IsVectorizable(Type[] operandTypes)
        => operandTypes.All(type => type switch
        {
            var t when t == typeof(int) => true,
            var t when t == typeof(float) => true,
            var t when t == typeof(double) => true,
            var t when t == typeof(long) => true,
            var t when t == typeof(short) => true,
            var t when t == typeof(byte) => true,
            _ => false
        });

    private static bool IsSupportedOperator(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual or
        ExpressionType.LessThan or ExpressionType.LessThanOrEqual or
        ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual => true,
        _ => false
    };

    private static int GetOptimalVectorWidth(Type[] operandTypes)
    {
        if (operandTypes.Length == 0)
        {
            return 1;
        }


        var primaryType = operandTypes[0];
        return primaryType switch
        {
            var t when t == typeof(float) => 8,
            var t when t == typeof(double) => 4,
            var t when t == typeof(int) => 8,
            var t when t == typeof(long) => 4,
            var t when t == typeof(byte) => 32,
            var t when t == typeof(short) => 16,
            _ => 1
        };
    }

    private static int[] GetSupportedWidths(Type type) => type switch
    {
        var t when t == typeof(float) => [4, 8, 16],
        var t when t == typeof(double) => [2, 4, 8],
        var t when t == typeof(int) => [4, 8, 16],
        var t when t == typeof(long) => [2, 4, 8],
        var t when t == typeof(byte) => [16, 32, 64],
        var t when t == typeof(short) => [8, 16, 32],
        _ => [1]
    };

    private static PerformanceCharacteristics GetPerformanceCharacteristics(ExpressionType operatorType, BackendType backend)
    {
        var baseThroughput = 1.5e9; // Comparisons are generally fast
        var backendMultiplier = backend switch
        {
            BackendType.CUDA => 200.0,
            BackendType.CPU => 1.0,
            _ => 1.0
        };

        return new PerformanceCharacteristics
        {
            Throughput = baseThroughput * backendMultiplier,
            Latency = 1.0 / (baseThroughput * backendMultiplier),
            MemoryBandwidth = backend == BackendType.CUDA ? 900e9 : 50e9,
            ComputeIntensity = 1.0 // Low compute intensity
        };
    }

    private static ImplementationMethod GetImplementationMethod(ExpressionType operatorType, BackendType backend)
    {
        return backend switch
        {
            BackendType.CPU => ImplementationMethod.Intrinsic,
            BackendType.CUDA => ImplementationMethod.Native,
            _ => ImplementationMethod.Library
        };
    }

    private static List<string> GetSpecialCases(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual => ["NaN comparisons", "Floating point precision issues"],
        _ => ["Signed/unsigned comparison edge cases"]
    };

    private static bool IsCommutative(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual => true,
        _ => false
    };

    private static Dictionary<BackendType, OperatorCompatibility> AnalyzeBackendCompatibility(ExpressionType operatorType, Type[] operandTypes)
    {
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>();
        var isSupported = IsSupportedOperator(operatorType);


        compatibility[BackendType.CPU] = new OperatorCompatibility
        {
            IsSupported = isSupported,
            SupportLevel = isSupported ? SupportLevel.Full : SupportLevel.None,
            Alternatives = isSupported ? [] : ["Software emulation"],
            Performance = GetPerformanceCharacteristics(operatorType, BackendType.CPU)
        };


        compatibility[BackendType.CUDA] = new OperatorCompatibility
        {
            IsSupported = isSupported,
            SupportLevel = isSupported ? SupportLevel.Full : SupportLevel.None,
            Alternatives = isSupported ? [] : ["CPU fallback"],
            Performance = GetPerformanceCharacteristics(operatorType, BackendType.CUDA)
        };


        return compatibility;
    }

    private static List<DotCompute.Linq.Types.OptimizationHint> GenerateOptimizationHints(Expression expression)
    {
        var hints = new List<DotCompute.Linq.Types.OptimizationHint>();


        if (IsSupportedOperator(expression.NodeType))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Vectorization,
                Description = "Comparison can be vectorized using SIMD instructions",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = 3.0
            });
        }


        return hints;
    }

    private static List<FusionOpportunity> FindFusionOpportunities(Expression expression)
    {
        var opportunities = new List<FusionOpportunity>();


        if (IsSupportedOperator(expression.NodeType))
        {
            opportunities.Add(new FusionOpportunity
            {
                Operators = [expression.NodeType],
                Pattern = FusionPattern.Conditional,
                PerformanceBenefit = 1.2,
                MemorySavings = 0.1,
                Complexity = FusionComplexity.Simple
            });
        }


        return opportunities;
    }

    private static double EstimateVectorizationSpeedup(ExpressionType operatorType) => 5.0; // Comparisons vectorize well

    private static List<string> GetVectorizationConstraints(ExpressionType operatorType, Type[] operandTypes)
    {
        var constraints = new List<string>();


        if (!IsVectorizable(operandTypes))
        {
            constraints.Add("Not all operand types support vectorization");
        }


        if (operandTypes.Any(t => t == typeof(decimal)))
        {
            constraints.Add("Decimal comparisons cannot be vectorized");
        }


        return constraints;
    }

    private static string? GetVectorInstruction(ExpressionType operatorType, Type type)
    {
        return (operatorType, type) switch
        {
            (ExpressionType.Equal, var t) when t == typeof(float) => "vcmpeqps",
            (ExpressionType.Equal, var t) when t == typeof(double) => "vcmpeqpd",
            (ExpressionType.LessThan, var t) when t == typeof(float) => "vcmpltps",
            (ExpressionType.LessThan, var t) when t == typeof(double) => "vcmpltpd",
            _ => null
        };
    }

    private static bool CanFuseOperations(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.Equal, ExpressionType.Equal) => true,
            (ExpressionType.LessThan, ExpressionType.LessThanOrEqual) => true,
            (ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual) => true,
            _ => false
        };
    }

    private static List<string> GetFusionConstraints(List<Expression> operators)
    {
        var constraints = new List<string>();


        if (operators.Count < 2)
        {
            constraints.Add("Insufficient operations for fusion");
        }


        return constraints;
    }

    private static double GetBaseCost(ExpressionType operatorType) => 1.0; // All comparisons have similar cost

    private static List<string> GetPrecisionMitigationStrategies(ExpressionType operatorType, Type[] operandTypes)
    {
        var strategies = new List<string>();


        if (operandTypes.Any(t => t == typeof(float) || t == typeof(double)))
        {
            strategies.Add("Be aware of floating-point precision issues in equality comparisons");
            strategies.Add("Consider using epsilon-based comparisons for floating-point values");
        }


        return strategies;
    }
}