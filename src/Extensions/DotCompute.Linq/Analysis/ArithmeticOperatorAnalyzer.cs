// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Compilation.Analysis;
using CompilationOperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;
using PipelineOperatorInfo = DotCompute.Linq.Pipelines.Analysis.OperatorInfo;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Analyzes arithmetic operators in expression trees.
/// </summary>
public class ArithmeticOperatorAnalyzer : DotCompute.Linq.Analysis.IOperatorAnalyzer
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
        var isVectorizable = IsVectorizable(expression.Type);


        return new OperatorAnalysisResult
        {
            OperatorType = expression.NodeType,
            OperandTypes = operandTypes,
            IsComputeFriendly = true,
            SupportsVectorization = isVectorizable,
            OptimalVectorWidth = GetOptimalVectorWidth(expression.Type),
            BackendCompatibility = AnalyzeBackendCompatibility(expression.NodeType, operandTypes),
            OptimizationHints = GenerateOptimizationHints(expression),
            Complexity = DetermineComplexity(expression.NodeType),
            FusionOpportunities = FindFusionOpportunities(expression)
        };
    }

    /// <inheritdoc />
    public DotCompute.Linq.Analysis.OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        var isSupported = IsSupportedOperator(operatorType);
        var performance = GetPerformanceCharacteristics(operatorType, backend);


        return backend == BackendType.CUDA

            ? DotCompute.Linq.Analysis.OperatorInfo.ForCUDA(operatorType, operandTypes, DetermineResultType(operatorType, operandTypes))
            : DotCompute.Linq.Analysis.OperatorInfo.ForCPU(operatorType, operandTypes, DetermineResultType(operatorType, operandTypes));
    }

    /// <inheritdoc />
    public VectorizationAnalysis AnalyzeVectorization(ExpressionType operatorType, Type[] operandTypes)
    {
        var isVectorizable = IsVectorizableOperator(operatorType) &&

                           operandTypes.All(IsVectorizable);


        return new VectorizationAnalysis
        {
            IsVectorizable = isVectorizable,
            OptimalWidth = isVectorizable ? GetOptimalVectorWidth(operandTypes.FirstOrDefault() ?? typeof(object)) : 1,
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

        // Look for arithmetic operation chains
        for (int i = 0; i < operatorList.Count - 1; i++)
        {
            var current = operatorList[i];
            var next = operatorList[i + 1];

            if (CanFuseOperations(current.NodeType, next.NodeType))
            {
                opportunities.Add(new FusionOpportunity
                {
                    Operators = [current.NodeType, next.NodeType],
                    Pattern = DetermineFusionPattern(current.NodeType, next.NodeType),
                    PerformanceBenefit = EstimateFusionBenefit(current.NodeType, next.NodeType),
                    MemorySavings = EstimateMemorySavings(current.NodeType, next.NodeType),
                    Complexity = EstimateFusionComplexity(current.NodeType, next.NodeType)
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
            BackendType.CUDA => 0.1, // GPU operations are much faster for parallel workloads
            _ => 1.5
        };


        return baseCost * backendMultiplier;
    }

    /// <inheritdoc />
    public PrecisionAnalysisResult AnalyzePrecision(ExpressionType operatorType, Type[] operandTypes)
    {
        return new PrecisionAnalysisResult
        {
            Precision = DetermineNumericalPrecision(operatorType, operandTypes),
            IsNumericallyStable = IsNumericallyStable(operatorType),
            ErrorPropagation = DetermineErrorPropagation(operatorType),
            MitigationStrategies = GetPrecisionMitigationStrategies(operatorType, operandTypes)
        };
    }

    /// <inheritdoc />
    public OperatorAnalysisResult Analyze(Expression expression, object? context = null)
    {
        return AnalyzeOperator(expression);
    }

    /// <inheritdoc />
    public DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, DotCompute.Linq.Pipelines.Analysis.AnalysisContext context)
    {
        var operatorName = GetOperatorName(expression.NodeType);
        var inputTypes = GetInputTypes(expression);
        var outputType = expression.Type;

        return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            OperatorType = DotCompute.Linq.Pipelines.Analysis.OperatorType.Mathematical,
            Name = operatorName,
            InputTypes = inputTypes.ToList(),
            OutputType = outputType,
            ComplexityScore = EstimateComplexity(expression.NodeType),
            SupportsGpu = true,
            SupportsCpu = true,
            Properties = new Dictionary<string, object>
            {
                ["IsVectorizable"] = IsVectorizable(expression.Type),
                ["SupportsFusion"] = true,
                ["MemoryBound"] = false
            }
        };
    }

    /// <inheritdoc />
    public ParallelizationOpportunity AnalyzeParallelization(Expression expression)
    {
        return new ParallelizationOpportunity
        {
            VectorizationSuitable = IsVectorizableOperator(expression.NodeType),
            SupportsParallelExecution = true,
            RecommendedParallelism = Environment.ProcessorCount,
            DataDependencies = [],
            EstimatedSpeedup = IsVectorizableOperator(expression.NodeType) ? 4.0 : 2.0
        };
    }

    /// <inheritdoc />
    public double EstimateExecutionCost(Expression expression)
    {
        if (expression == null)
            return 0.0;


        var baseCost = GetBaseCost(expression.NodeType);
        var typePenalty = GetTypePenalty(expression.Type);
        return baseCost * typePenalty;
    }

    private static string GetOperatorName(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add => "Add",
        ExpressionType.Subtract => "Subtract",
        ExpressionType.Multiply => "Multiply",
        ExpressionType.Divide => "Divide",
        ExpressionType.Modulo => "Modulo",
        ExpressionType.Power => "Power",
        ExpressionType.Negate => "Negate",
        ExpressionType.UnaryPlus => "UnaryPlus",
        _ => nodeType.ToString()
    };

    private static IReadOnlyList<Type> GetInputTypes(Expression expression) => expression switch
    {
        BinaryExpression binary => [binary.Left.Type, binary.Right.Type],
        UnaryExpression unary => [unary.Operand.Type],
        _ => Array.Empty<Type>()
    };

    private static int EstimateComplexity(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add or ExpressionType.Subtract => 1,
        ExpressionType.Multiply => 2,
        ExpressionType.Divide => 4,
        ExpressionType.Modulo => 6,
        ExpressionType.Power => 10,
        _ => 1
    };

    private static bool IsVectorizable(Type type) => type switch
    {
        var t when t == typeof(int) => true,
        var t when t == typeof(float) => true,
        var t when t == typeof(double) => true,
        var t when t == typeof(long) => true,
        var t when t == typeof(short) => true,
        var t when t == typeof(byte) => true,
        _ => false
    };

    private static bool IsVectorizableOperator(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract or
        ExpressionType.Multiply or ExpressionType.Divide or
        ExpressionType.Negate or ExpressionType.UnaryPlus => true,
        _ => false
    };

    private static bool IsSupportedOperator(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract or
        ExpressionType.Multiply or ExpressionType.Divide or
        ExpressionType.Modulo or ExpressionType.Power or
        ExpressionType.Negate or ExpressionType.UnaryPlus => true,
        _ => false
    };

    private static int GetOptimalVectorWidth(Type type) => type switch
    {
        var t when t == typeof(float) => 8,  // 256-bit SIMD
        var t when t == typeof(double) => 4, // 256-bit SIMD
        var t when t == typeof(int) => 8,    // 256-bit SIMD
        var t when t == typeof(long) => 4,   // 256-bit SIMD
        var t when t == typeof(byte) => 32,  // 256-bit SIMD
        var t when t == typeof(short) => 16, // 256-bit SIMD
        _ => 1
    };

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
        var baseThroughput = operatorType switch
        {
            ExpressionType.Add or ExpressionType.Subtract => 1e9,
            ExpressionType.Multiply => 8e8,
            ExpressionType.Divide => 2e8,
            _ => 5e8
        };

        var backendMultiplier = backend switch
        {
            BackendType.CUDA => 100.0,
            BackendType.CPU => 1.0,
            _ => 1.0
        };

        return new PerformanceCharacteristics
        {
            Throughput = baseThroughput * backendMultiplier,
            Latency = 1.0 / (baseThroughput * backendMultiplier),
            MemoryBandwidth = backend == BackendType.CUDA ? 900e9 : 50e9,
            ComputeIntensity = operatorType switch
            {
                ExpressionType.Add or ExpressionType.Subtract => 1.0,
                ExpressionType.Multiply => 2.0,
                ExpressionType.Divide => 4.0,
                _ => 2.0
            }
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

    private static Type DetermineResultType(ExpressionType operatorType, Type[] operandTypes)
    {
        if (operandTypes.Length == 0) return typeof(object);


        return operatorType switch
        {
            ExpressionType.Add or ExpressionType.Subtract or
            ExpressionType.Multiply or ExpressionType.Divide or
            ExpressionType.Modulo or ExpressionType.Power => operandTypes[0],
            ExpressionType.Negate or ExpressionType.UnaryPlus => operandTypes[0],
            _ => operandTypes[0]
        };
    }

    private static double GetRelativeError(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract => 1e-15,
        ExpressionType.Multiply => 2e-15,
        ExpressionType.Divide => 1e-14,
        _ => 1e-13
    };

    private static List<string> GetSpecialCases(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Divide => ["Division by zero", "Infinity handling"],
        ExpressionType.Modulo => ["Modulo by zero"],
        ExpressionType.Power => ["Negative base with fractional exponent", "0^0 case"],
        _ => []
    };

    private static ComputationalComplexity DetermineComplexity(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract or
        ExpressionType.Negate or ExpressionType.UnaryPlus => ComputationalComplexity.Constant,
        ExpressionType.Multiply => ComputationalComplexity.Constant,
        ExpressionType.Divide or ExpressionType.Modulo => ComputationalComplexity.Constant,
        ExpressionType.Power => ComputationalComplexity.Linear,
        _ => ComputationalComplexity.Constant
    };

    private static bool IsCommutative(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Multiply => true,
        _ => false
    };

    private static bool IsAssociative(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Multiply => true,
        _ => false
    };

    private static bool IsDistributive(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Multiply => true, // Multiplication distributes over addition
        _ => false
    };

    private static Dictionary<BackendType, OperatorCompatibility> AnalyzeBackendCompatibility(ExpressionType operatorType, Type[] operandTypes)
    {
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>();
        var isSupported = IsSupportedOperator(operatorType);

        // CPU compatibility

        compatibility[BackendType.CPU] = new OperatorCompatibility
        {
            IsSupported = isSupported,
            SupportLevel = isSupported ? SupportLevel.Full : SupportLevel.None,
            Alternatives = isSupported ? [] : ["Software emulation"],
            Performance = GetPerformanceCharacteristics(operatorType, BackendType.CPU)
        };

        // GPU compatibility

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


        if (IsVectorizableOperator(expression.NodeType))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Vectorization,
                Description = "Operation can be vectorized using SIMD instructions",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = 4.0
            });
        }


        if (IsCommutative(expression.NodeType) || IsAssociative(expression.NodeType))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Parallelization,
                Description = "Operation order can be optimized for better performance",
                Priority = DotCompute.Linq.Types.OptimizationPriority.Medium,
                EstimatedBenefit = 1.2
            });
        }


        return hints;
    }

    private static List<FusionOpportunity> FindFusionOpportunities(Expression expression)
    {
        var opportunities = new List<FusionOpportunity>();


        if (IsVectorizableOperator(expression.NodeType))
        {
            opportunities.Add(new FusionOpportunity
            {
                Operators = [expression.NodeType],
                Pattern = FusionPattern.ElementWise,
                PerformanceBenefit = 1.5,
                MemorySavings = 0.3,
                Complexity = FusionComplexity.Simple
            });
        }


        return opportunities;
    }

    private static double EstimateVectorizationSpeedup(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract => 7.0,
        ExpressionType.Multiply => 6.0,
        ExpressionType.Divide => 3.0,
        _ => 4.0
    };

    private static List<string> GetVectorizationConstraints(ExpressionType operatorType, Type[] operandTypes)
    {
        var constraints = new List<string>();


        if (!operandTypes.All(IsVectorizable))
        {
            constraints.Add("Not all operand types support vectorization");
        }


        if (operatorType == ExpressionType.Divide)
        {
            constraints.Add("Division may have precision limitations in vectorized form");
        }


        return constraints;
    }

    private static string? GetVectorInstruction(ExpressionType operatorType, Type type)
    {
        return (operatorType, type) switch
        {
            (ExpressionType.Add, var t) when t == typeof(float) => "vaddps",
            (ExpressionType.Add, var t) when t == typeof(double) => "vaddpd",
            (ExpressionType.Multiply, var t) when t == typeof(float) => "vmulps",
            (ExpressionType.Multiply, var t) when t == typeof(double) => "vmulpd",
            _ => null
        };
    }

    private static bool CanFuseOperations(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.Multiply, ExpressionType.Add) => true, // FMA
            (ExpressionType.Add, ExpressionType.Multiply) => true,
            (ExpressionType.Add, ExpressionType.Add) => true,
            (ExpressionType.Multiply, ExpressionType.Multiply) => true,
            _ => false
        };
    }

    private static FusionPattern DetermineFusionPattern(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.Multiply, ExpressionType.Add) or
            (ExpressionType.Add, ExpressionType.Multiply) => FusionPattern.MultiplyAdd,
            _ => FusionPattern.ElementWise
        };
    }

    private static double EstimateFusionBenefit(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.Multiply, ExpressionType.Add) => 2.0, // FMA is very beneficial
            _ => 1.3
        };
    }

    private static double EstimateMemorySavings(ExpressionType first, ExpressionType second)
    {
        return 0.5; // Approximate 50% reduction in intermediate storage
    }

    private static FusionComplexity EstimateFusionComplexity(ExpressionType first, ExpressionType second)
    {
        return (first, second) switch
        {
            (ExpressionType.Multiply, ExpressionType.Add) => FusionComplexity.Simple,
            _ => FusionComplexity.Moderate
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

    private static double GetBaseCost(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract => 1.0,
        ExpressionType.Multiply => 2.0,
        ExpressionType.Divide => 8.0,
        ExpressionType.Modulo => 10.0,
        ExpressionType.Power => 20.0,
        _ => 5.0
    };

    private static NumericalPrecision DetermineNumericalPrecision(ExpressionType operatorType, Type[] operandTypes)
    {
        if (operandTypes.Any(t => t == typeof(decimal)))
            return NumericalPrecision.High;


        if (operandTypes.Any(t => t == typeof(double)))
            return NumericalPrecision.Standard;


        return operatorType switch
        {
            ExpressionType.Divide or ExpressionType.Power => NumericalPrecision.Standard,
            _ => NumericalPrecision.High
        };
    }

    private static bool IsNumericallyStable(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract or
        ExpressionType.Multiply => true,
        ExpressionType.Divide => false, // Can be unstable near zero
        ExpressionType.Power => false,  // Can be unstable
        _ => true
    };

    private static ErrorPropagation DetermineErrorPropagation(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add or ExpressionType.Subtract => ErrorPropagation.Linear,
        ExpressionType.Multiply => ErrorPropagation.Linear,
        ExpressionType.Divide => ErrorPropagation.Linear,
        ExpressionType.Power => ErrorPropagation.Exponential,
        _ => ErrorPropagation.Linear
    };

    private static List<string> GetPrecisionMitigationStrategies(ExpressionType operatorType, Type[] operandTypes)
    {
        var strategies = new List<string>();


        if (operatorType == ExpressionType.Divide)
        {
            strategies.Add("Check for division by values close to zero");
            strategies.Add("Consider using higher precision types for intermediate calculations");
        }


        if (operatorType == ExpressionType.Power)
        {
            strategies.Add("Use logarithmic transformations where possible");
            strategies.Add("Check for overflow/underflow conditions");
        }


        if (operandTypes.Any(t => t == typeof(float)))
        {
            strategies.Add("Consider using double precision for better accuracy");
        }


        return strategies;
    }

    private static double GetTypePenalty(Type type) => type switch
    {
        var t when t == typeof(double) => 1.5, // Double precision is slower
        var t when t == typeof(decimal) => 10.0, // Decimal is much slower
        _ => 1.0
    };
}