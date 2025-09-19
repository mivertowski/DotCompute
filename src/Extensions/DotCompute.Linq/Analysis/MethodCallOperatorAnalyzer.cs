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

/// <summary>
/// Analyzes method call operators in expression trees.
/// </summary>
public class MethodCallOperatorAnalyzer : IOperatorAnalyzer
{
    /// <inheritdoc />
    public OperatorAnalysisResult AnalyzeOperator(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
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
            OperatorType = ExpressionType.Call,
            OperandTypes = [.. methodCall.Arguments.Select(arg => arg.Type)],
            IsComputeFriendly = IsMethodComputeFriendly(methodCall.Method),
            SupportsVectorization = CanVectorizeMethod(methodCall.Method),
            OptimalVectorWidth = GetOptimalVectorWidth(methodCall.Method),
            BackendCompatibility = AnalyzeBackendCompatibility(methodCall.Method),
            OptimizationHints = GenerateOptimizationHints(methodCall),
            Complexity = DetermineComplexity(methodCall.Method),
            FusionOpportunities = FindFusionOpportunities(methodCall)
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
        if (expression is MethodCallExpression methodCall)
        {
            return AnalyzeOperator(methodCall);
        }

        // Create a basic analysis result for non-method-call expressions

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
    /// Determines if a method is compute-friendly.
    /// </summary>
    private static bool IsMethodComputeFriendly(System.Reflection.MethodInfo method)
    {
        // Check if it's a LINQ method or mathematical operation
        if (method.DeclaringType == typeof(Enumerable) || method.DeclaringType == typeof(Queryable))
        {
            return method.Name switch
            {
                "Where" or "Select" or "Sum" or "Average" or "Min" or "Max" or "Count" => true,
                _ => false
            };
        }


        if (method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF))
        {
            return true;
        }


        return false;
    }

    /// <summary>
    /// Determines if a method can be vectorized.
    /// </summary>
    private static bool CanVectorizeMethod(System.Reflection.MethodInfo method)
    {
        // Mathematical operations are generally vectorizable
        if (method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF))
        {
            return method.Name switch
            {
                "Abs" or "Min" or "Max" or "Sqrt" or "Pow" => true,
                _ => false
            };
        }

        // Some LINQ operations can be vectorized

        if (method.DeclaringType == typeof(Enumerable))
        {
            return method.Name switch
            {
                "Select" or "Where" => true,
                _ => false
            };
        }


        return false;
    }

    /// <summary>
    /// Gets the optimal vector width for a method.
    /// </summary>
    private static int GetOptimalVectorWidth(System.Reflection.MethodInfo method)
    {
        if (!CanVectorizeMethod(method))
        {
            return 1;
        }

        // Return optimal width based on method and typical SIMD capabilities

        return method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF) ? 8 : 4;
    }

    /// <summary>
    /// Analyzes backend compatibility for a method.
    /// </summary>
    private static Dictionary<BackendType, OperatorCompatibility> AnalyzeBackendCompatibility(System.Reflection.MethodInfo method)
    {
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>
        {
            // CPU is generally compatible with most methods

            [BackendType.CPU] = new OperatorCompatibility
            {
                IsSupported = true,
                SupportLevel = SupportLevel.Full,
                Alternatives = [],
                Performance = new PerformanceCharacteristics
                {
                    Throughput = 1000.0,
                    Latency = 0.001,
                    MemoryBandwidth = 100.0,
                    ComputeIntensity = 10.0
                }
            }
        };

        // GPU compatibility depends on the method

        var gpuSupported = IsMethodComputeFriendly(method);
        compatibility[BackendType.CUDA] = new OperatorCompatibility
        {
            IsSupported = gpuSupported,
            SupportLevel = gpuSupported ? SupportLevel.Full : SupportLevel.None,
            Alternatives = gpuSupported ? [] : ["CPU fallback"],
            Performance = new PerformanceCharacteristics
            {
                Throughput = gpuSupported ? 10000.0 : 0.0,
                Latency = gpuSupported ? 0.0001 : 0.0,
                MemoryBandwidth = gpuSupported ? 500.0 : 0.0,
                ComputeIntensity = gpuSupported ? 100.0 : 0.0
            }
        };


        return compatibility;
    }

    /// <summary>
    /// Generates optimization hints for a method call.
    /// </summary>
    private static List<DotCompute.Linq.Types.OptimizationHint> GenerateOptimizationHints(MethodCallExpression methodCall)
    {
        var hints = new List<DotCompute.Linq.Types.OptimizationHint>();


        if (methodCall.Method.DeclaringType == typeof(Enumerable))
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Vectorization,
                Description = "Consider vectorizing LINQ operation",
                Priority = DotCompute.Linq.Types.OptimizationPriority.High,
                EstimatedBenefit = 2.0
            });
        }


        if (methodCall.Arguments.Count > 2)
        {
            hints.Add(new DotCompute.Linq.Types.OptimizationHint
            {
                Type = DotCompute.Linq.Types.OptimizationHintType.Parallelization,
                Description = "Method may benefit from parallelization",
                Priority = DotCompute.Linq.Types.OptimizationPriority.Medium,
                EstimatedBenefit = 1.5
            });
        }


        return hints;
    }

    /// <summary>
    /// Determines computational complexity for a method.
    /// </summary>
    private static ComputationalComplexity DetermineComplexity(System.Reflection.MethodInfo method)
    {
        return method.Name switch
        {
            "Where" or "Select" => ComputationalComplexity.Linear,
            "OrderBy" or "Sort" => ComputationalComplexity.Quadratic,
            "Sum" or "Count" or "Min" or "Max" => ComputationalComplexity.Linear,
            _ => ComputationalComplexity.Linear
        };
    }

    /// <summary>
    /// Finds fusion opportunities for a method call.
    /// </summary>
    private static List<FusionOpportunity> FindFusionOpportunities(MethodCallExpression methodCall)
    {
        var opportunities = new List<FusionOpportunity>();

        // LINQ operations often have good fusion potential

        if (methodCall.Method.DeclaringType == typeof(Enumerable))
        {
            opportunities.Add(new FusionOpportunity
            {
                Operators = [methodCall.NodeType],
                Pattern = FusionPattern.ElementWise,
                PerformanceBenefit = 1.3,
                MemorySavings = 0.5,
                Complexity = FusionComplexity.Simple
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

    /// <summary>
    /// Converts an ExpressionType to an OperatorType.
    /// </summary>
    /// <param name="expressionType">The expression type to convert.</param>
    /// <returns>The corresponding operator type.</returns>
    private static DotCompute.Core.Analysis.UnifiedOperatorType ConvertExpressionTypeToOperatorType(ExpressionType expressionType)
    {
        return expressionType switch
        {
            ExpressionType.Add => DotCompute.Core.Analysis.UnifiedOperatorType.Add,
            ExpressionType.Subtract => DotCompute.Core.Analysis.UnifiedOperatorType.Subtract,
            ExpressionType.Multiply => DotCompute.Core.Analysis.UnifiedOperatorType.Multiply,
            ExpressionType.Divide => DotCompute.Core.Analysis.UnifiedOperatorType.Divide,
            ExpressionType.Modulo => DotCompute.Core.Analysis.UnifiedOperatorType.Modulo,
            ExpressionType.Power => DotCompute.Core.Analysis.UnifiedOperatorType.Power,
            ExpressionType.UnaryPlus => DotCompute.Core.Analysis.UnifiedOperatorType.Mathematical,
            ExpressionType.Negate => DotCompute.Core.Analysis.UnifiedOperatorType.Mathematical,


            ExpressionType.Equal => DotCompute.Core.Analysis.UnifiedOperatorType.Equal,
            ExpressionType.NotEqual => UnifiedOperatorType.NotEqual,
            ExpressionType.GreaterThan => UnifiedOperatorType.GreaterThan,
            ExpressionType.GreaterThanOrEqual => UnifiedOperatorType.GreaterThanOrEqual,
            ExpressionType.LessThan => UnifiedOperatorType.LessThan,
            ExpressionType.LessThanOrEqual => UnifiedOperatorType.LessThanOrEqual,


            ExpressionType.AndAlso => UnifiedOperatorType.LogicalAnd,
            ExpressionType.OrElse => UnifiedOperatorType.LogicalOr,
            ExpressionType.Not => UnifiedOperatorType.LogicalNot,
            ExpressionType.And => UnifiedOperatorType.BitwiseAnd,
            ExpressionType.Or => UnifiedOperatorType.BitwiseOr,
            ExpressionType.ExclusiveOr => UnifiedOperatorType.BitwiseXor,


            ExpressionType.Convert => UnifiedOperatorType.Conversion,
            ExpressionType.ConvertChecked => UnifiedOperatorType.Conversion,


            ExpressionType.Call => UnifiedOperatorType.MethodCall,
            ExpressionType.Lambda => UnifiedOperatorType.Custom,
            ExpressionType.Invoke => UnifiedOperatorType.MethodCall,


            _ => UnifiedOperatorType.Unknown
        };
    }

    /// <inheritdoc />
    public DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, DotCompute.Linq.Pipelines.Analysis.AnalysisContext context)
    {
        if (expression is not MethodCallExpression methodCall)
        {
            return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
            {
                OperatorType = UnifiedOperatorType.Unknown,
                Name = "UnknownCall",
                InputTypes = [],
                OutputType = expression.Type,
                ComplexityScore = 1,
                SupportsGpu = false,
                SupportsCpu = true
            };
        }

        var methodName = GetMethodName(methodCall);
        var operatorType = DetermineOperatorType(methodCall);
        var inputTypes = GetInputTypes(methodCall);

        return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            OperatorType = operatorType,
            Name = methodName,
            InputTypes = [.. inputTypes],
            OutputType = methodCall.Type,
            ComplexityScore = GetMethodComplexity(methodCall),
            SupportsGpu = IsVectorizable(methodCall),
            SupportsCpu = true,
            Properties = new Dictionary<string, object>
            {
                ["MethodName"] = methodName,
                ["DeclaringType"] = methodCall.Method.DeclaringType?.FullName ?? "Unknown",
                ["IsStatic"] = methodCall.Method.IsStatic,
                ["IsLinqMethod"] = IsLinqMethod(methodCall)
            }
        };
    }

    private static string GetMethodName(MethodCallExpression methodCall)
    {
        var declaringType = methodCall.Method.DeclaringType?.Name ?? "Unknown";
        return $"{declaringType}.{methodCall.Method.Name}";
    }

    private static DotCompute.Core.Analysis.UnifiedOperatorType DetermineOperatorType(MethodCallExpression methodCall)
    {
        var methodName = methodCall.Method.Name;

        if (IsLinqMethod(methodCall))
        {
            return methodName switch
            {
                "Where" => DotCompute.Core.Analysis.UnifiedOperatorType.Where,
                "Select" => DotCompute.Core.Analysis.UnifiedOperatorType.Select,
                "Sum" => DotCompute.Core.Analysis.UnifiedOperatorType.Sum,
                "Count" => DotCompute.Core.Analysis.UnifiedOperatorType.Count,
                "Average" => DotCompute.Core.Analysis.UnifiedOperatorType.Average,
                "Max" => DotCompute.Core.Analysis.UnifiedOperatorType.Max,
                "Min" => DotCompute.Core.Analysis.UnifiedOperatorType.Min,
                "OrderBy" => DotCompute.Core.Analysis.UnifiedOperatorType.OrderBy,
                "OrderByDescending" => DotCompute.Core.Analysis.UnifiedOperatorType.OrderByDescending,
                "GroupBy" => DotCompute.Core.Analysis.UnifiedOperatorType.GroupBy,
                _ => DotCompute.Core.Analysis.UnifiedOperatorType.Transformation
            };
        }

        if (IsMathMethod(methodCall))
        {
            return DotCompute.Core.Analysis.UnifiedOperatorType.Mathematical;
        }

        return DotCompute.Core.Analysis.UnifiedOperatorType.Custom;
    }

    private static IReadOnlyList<Type> GetInputTypes(MethodCallExpression methodCall)
    {
        var types = new List<Type>();

        // Add instance type if not static
        if (!methodCall.Method.IsStatic && methodCall.Object != null)
        {
            types.Add(methodCall.Object.Type);
        }

        // Add argument types
        types.AddRange(methodCall.Arguments.Select(arg => arg.Type));

        return types;
    }

    private static bool IsLinqMethod(MethodCallExpression methodCall)
    {
        var declaringType = methodCall.Method.DeclaringType;
        return declaringType?.Name == "Enumerable" || declaringType?.Name == "Queryable";
    }

    private static bool IsMathMethod(MethodCallExpression methodCall)
    {
        var declaringType = methodCall.Method.DeclaringType;
        return declaringType == typeof(Math) || declaringType == typeof(MathF);
    }

    private static int GetMethodComplexity(MethodCallExpression methodCall)
    {
        if (IsMathMethod(methodCall))
        {
            var methodName = methodCall.Method.Name;
            return methodName switch
            {
                "Abs" or "Min" or "Max" => 1,
                "Sqrt" => 3,
                "Sin" or "Cos" or "Tan" => 8,
                "Pow" => 10,
                _ => 5
            };
        }

        if (IsLinqMethod(methodCall))
        {
            var methodName = methodCall.Method.Name;
            return methodName switch
            {
                "Select" or "Where" => 1,
                "Sum" or "Count" => 2,
                "OrderBy" => 20,
                _ => 5
            };
        }

        return 5;
    }


    /// <summary>
    /// Determines if a method is vectorizable on GPU.
    /// </summary>
    private static bool IsVectorizable(MethodCallExpression methodCall)
    {
        if (IsMathMethod(methodCall))
        {
            var methodName = methodCall.Method.Name;
            return methodName switch
            {
                "Add" or "Subtract" or "Multiply" or "Divide" or
                "Abs" or "Min" or "Max" or "Sqrt" => true,
                _ => false
            };
        }

        if (IsLinqMethod(methodCall))
        {
            var methodName = methodCall.Method.Name;
            return methodName switch
            {
                "Select" or "Where" or "Sum" or "Count" => true,
                _ => false
            };
        }

        return false;
    }

    /// <summary>
    /// Determines if a method is GPU compatible.
    /// </summary>
    private static bool IsGpuCompatibleMethod(ExpressionType operatorType, Type[] operandTypes)
    {
        // Most mathematical operations are GPU compatible
        return operatorType == ExpressionType.Call &&

               operandTypes != null &&

               operandTypes.All(t => t.IsPrimitive || t == typeof(float) || t == typeof(double));
    }
}