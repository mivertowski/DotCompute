// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;

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
            throw new ArgumentException("Expression must be a MethodCallExpression", nameof(expression));
        }

        return new OperatorAnalysisResult
        {
            OperatorType = ExpressionType.Call,
            OperandTypes = methodCall.Arguments.Select(arg => arg.Type).ToArray(),
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
    public OperatorInfo GetOperatorInfo(ExpressionType operatorType, Type[] operandTypes, BackendType backend)
    {
        // For method calls, we need more context than just the operator type
        return new OperatorInfo
        {
            OperatorType = ConvertExpressionTypeToOperatorType(operatorType),
            Name = "MethodCall",
            IsNativelySupported = false,
            Implementation = ImplementationMethod.Library,
            PerformanceCost = 2.0, // Method calls generally have higher overhead
            Accuracy = new AccuracyInfo
            {
                RelativeError = 0.0,
                AbsoluteError = 0.0,
                IsIEEE754Compliant = true
            }
        };
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
        for (int i = 0; i < methodCalls.Count - 1; i++)
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
            BackendCompatibility = new Dictionary<BackendType, OperatorCompatibility>(),
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
        var compatibility = new Dictionary<BackendType, OperatorCompatibility>();
        
        // CPU is generally compatible with most methods
        compatibility[BackendType.CPU] = new OperatorCompatibility
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
    private static List<OptimizationHint> GenerateOptimizationHints(MethodCallExpression methodCall)
    {
        var hints = new List<OptimizationHint>();
        
        if (methodCall.Method.DeclaringType == typeof(Enumerable))
        {
            hints.Add(new OptimizationHint
            {
                Type = OptimizationHintType.Vectorization,
                Description = "Consider vectorizing LINQ operation",
                Priority = OptimizationPriority.High,
                EstimatedBenefit = 2.0
            });
        }
        
        if (methodCall.Arguments.Count > 2)
        {
            hints.Add(new OptimizationHint
            {
                Type = OptimizationHintType.Parallelization,
                Description = "Method may benefit from parallelization",
                Priority = OptimizationPriority.Medium,
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
    private static OperatorType ConvertExpressionTypeToOperatorType(ExpressionType expressionType)
    {
        return expressionType switch
        {
            ExpressionType.Add => OperatorType.Mathematical,
            ExpressionType.Subtract => OperatorType.Mathematical,
            ExpressionType.Multiply => OperatorType.Mathematical,
            ExpressionType.Divide => OperatorType.Mathematical,
            ExpressionType.Modulo => OperatorType.Mathematical,
            ExpressionType.Power => OperatorType.Mathematical,
            ExpressionType.UnaryPlus => OperatorType.Mathematical,
            ExpressionType.UnaryMinus => OperatorType.Mathematical,
            
            ExpressionType.Equal => OperatorType.Comparison,
            ExpressionType.NotEqual => OperatorType.Comparison,
            ExpressionType.GreaterThan => OperatorType.Comparison,
            ExpressionType.GreaterThanOrEqual => OperatorType.Comparison,
            ExpressionType.LessThan => OperatorType.Comparison,
            ExpressionType.LessThanOrEqual => OperatorType.Comparison,
            
            ExpressionType.AndAlso => OperatorType.Logical,
            ExpressionType.OrElse => OperatorType.Logical,
            ExpressionType.Not => OperatorType.Logical,
            ExpressionType.And => OperatorType.Logical,
            ExpressionType.Or => OperatorType.Logical,
            ExpressionType.ExclusiveOr => OperatorType.Logical,
            
            ExpressionType.Convert => OperatorType.Conversion,
            ExpressionType.ConvertChecked => OperatorType.Conversion,
            
            ExpressionType.Call => OperatorType.Custom,
            ExpressionType.Lambda => OperatorType.Custom,
            ExpressionType.Invoke => OperatorType.Custom,
            
            _ => OperatorType.Unknown
        };
    }
}

/// <summary>
/// Represents an optimization hint.
/// </summary>
public class OptimizationHint
{
    /// <summary>
    /// Gets or sets the type of optimization.
    /// </summary>
    public OptimizationHintType Type { get; set; }

    /// <summary>
    /// Gets or sets the description of the optimization.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority of this optimization.
    /// </summary>
    public OptimizationPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the estimated benefit multiplier (1.0 = no benefit, 2.0 = 2x speedup).
    /// </summary>
    public double EstimatedBenefit { get; set; } = 1.0;
}

/// <summary>
/// Types of optimization hints.
/// </summary>
public enum OptimizationHintType
{
    /// <summary>
    /// Vectorization optimization.
    /// </summary>
    Vectorization,

    /// <summary>
    /// Parallelization optimization.
    /// </summary>
    Parallelization,

    /// <summary>
    /// Memory access optimization.
    /// </summary>
    MemoryAccess,

    /// <summary>
    /// Loop optimization.
    /// </summary>
    Loop,

    /// <summary>
    /// Fusion optimization.
    /// </summary>
    Fusion,

    /// <summary>
    /// Backend-specific optimization.
    /// </summary>
    BackendSpecific
}

/// <summary>
/// Priority levels for optimization hints.
/// </summary>
public enum OptimizationPriority
{
    /// <summary>
    /// Low priority optimization.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority optimization.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority optimization.
    /// </summary>
    High,

    /// <summary>
    /// Critical optimization that should always be applied.
    /// </summary>
    Critical
}