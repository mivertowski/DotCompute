// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.Types;
using DotCompute.Linq.Analysis;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Analyzer for memory access patterns in expressions.
/// </summary>
public class MemoryAccessAnalyzer
{
    /// <summary>
    /// Analyzes memory access patterns in the given expression.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>Memory access pattern information</returns>
    public GlobalMemoryAccessPattern AnalyzeAccessPattern(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var pattern = new GlobalMemoryAccessPattern
        {
            PredominantPattern = MemoryAccessPattern.Sequential,
            HasCoalescingOpportunities = true,
            LocalityFactor = 0.8
        };

        // Analyze expression tree for memory access patterns
        var visitor = new MemoryAccessVisitor();
        visitor.Visit(expression);

        // Note: These would be set via pattern with { ... } syntax since they're init-only
        // For now, using defaults from the ComplexityMetrics

        return pattern;
    }

    /// <summary>
    /// Analyzes a specific operator for memory access patterns.
    /// </summary>
    /// <param name="operatorInfo">Operator information to analyze</param>
    /// <returns>Memory access analysis for the operator</returns>
    public MemoryAccessAnalysisResult AnalyzeOperator(OperatorInfo operatorInfo)
    {
        ArgumentNullException.ThrowIfNull(operatorInfo);

        return new MemoryAccessAnalysisResult
        {
            AccessPattern = MemoryAccessPattern.Sequential,
            AccessFrequency = 1,
            MemoryBandwidthUsage = 0.8,
            CoalescingOpportunities = true,
            Bottlenecks = new List<string>()
        };
    }

    /// <summary>
    /// Detects global memory access patterns across multiple operations.
    /// </summary>
    /// <param name="operations">List of operations to analyze</param>
    /// <returns>Global memory access pattern</returns>
    public GlobalMemoryAccessPattern DetectGlobalPattern(IEnumerable<OperatorInfo> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return new GlobalMemoryAccessPattern
        {
            PredominantPattern = MemoryAccessPattern.Sequential,
            HasCoalescingOpportunities = true,
            BenefitsFromPrefetching = false,
            EstimatedCacheHitRatio = 0.8,
            LocalityFactor = 0.8,
            TotalMemoryFootprint = 1024,
            WorkingSetSize = 512,
            BandwidthUtilization = 0.7
        };
    }

    /// <summary>
    /// Expression visitor for memory access pattern analysis.
    /// </summary>
    private class MemoryAccessVisitor : ExpressionVisitor
    {
        public List<string> AccessedLocations { get; } = new(); // Simplified as strings for now
        public MemoryAccessPattern DominantAccessType { get; private set; } = MemoryAccessPattern.Sequential;
        public bool IsCoalesced { get; private set; } = true;

        protected override Expression VisitMember(MemberExpression node)
        {
            // Track member access as potential memory location
            AccessedLocations.Add($"{node.Member.Name}@{AccessedLocations.Count * 8}"); // Simplified representation

            return base.VisitMember(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            // Array/indexer access - analyze for patterns
            if (node.Arguments.Count > 0 && IsSequentialAccess(node.Arguments[0]))
            {
                DominantAccessType = MemoryAccessPattern.Sequential;
            }
            else
            {
                DominantAccessType = MemoryAccessPattern.Random;
                IsCoalesced = false;
            }

            return base.VisitIndex(node);
        }

        private static bool IsSequentialAccess(Expression indexExpression)
        {
            // Simple heuristic: if index is a parameter or simple increment, it's likely sequential
            return indexExpression is ParameterExpression ||
                   (indexExpression is BinaryExpression binary && binary.NodeType == ExpressionType.Add);
        }
    }
}

/// <summary>
/// Calculator for expression complexity metrics.
/// </summary>
public class ComplexityCalculator
{
    /// <summary>
    /// Calculates complexity metrics for the given expression.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>Complexity metrics</returns>
    public PipelineComplexityMetrics CalculateComplexity(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var metrics = new PipelineComplexityMetrics();
        var visitor = new ComplexityVisitor();


        visitor.Visit(expression);

        metrics.TotalComplexity = visitor.TotalComplexity;
        metrics.OperationCount = visitor.OperationCount;
        metrics.EstimatedMemoryUsage = visitor.EstimatedMemoryUsage;
        metrics.ParallelizationPotential = visitor.ParallelizationPotential;
        metrics.GpuRecommended = visitor.TotalComplexity > 100; // Simple heuristic
        metrics.ComplexityByCategory = visitor.ComplexityByCategory;

        return metrics;
    }

    /// <summary>
    /// Calculates complexity metrics for operator chain, type usage, and data flow.
    /// </summary>
    /// <param name="operatorChain">Operator chain to analyze</param>
    /// <param name="typeUsage">Type usage information</param>
    /// <param name="dataFlowGraph">Data flow graph</param>
    /// <returns>Pipeline complexity metrics</returns>
    public PipelineComplexityMetrics Calculate(
        IEnumerable<OperatorInfo> operatorChain,
        IEnumerable<TypeUsageInfo> typeUsage,
        object dataFlowGraph)
    {
        ArgumentNullException.ThrowIfNull(operatorChain);
        ArgumentNullException.ThrowIfNull(typeUsage);

        var metrics = new PipelineComplexityMetrics();

        // Calculate complexity based on operator chain

        var operators = operatorChain.ToList();
        metrics.OperationCount = operators.Count;
        metrics.TotalComplexity = operators.Sum(op => GetOperatorComplexity(op.Operator.ToString()));

        // Calculate memory usage based on type usage

        var types = typeUsage.ToList();
        metrics.EstimatedMemoryUsage = types.Sum(t => EstimateTypeMemoryUsage(t));

        // Set parallelization potential based on complexity

        metrics.ParallelizationPotential = metrics.TotalComplexity > 10 ? 0.8 : 0.4;
        metrics.GpuRecommended = metrics.TotalComplexity > 50;

        // Set complexity scores

        metrics.ComputeComplexity = metrics.TotalComplexity;
        metrics.MemoryComplexity = (int)(metrics.EstimatedMemoryUsage / 1024);
        metrics.ComputationalComplexity = metrics.TotalComplexity;

        return metrics;
    }

    private static int GetOperatorComplexity(string operatorType)
    {
        return operatorType switch
        {
            "Select" => 1,
            "Where" => 2,
            "Sum" => 3,
            "Average" => 4,
            "OrderBy" => 10,
            "GroupBy" => 15,
            "Join" => 20,
            _ => 2
        };
    }

    private static long EstimateTypeMemoryUsage(TypeUsageInfo typeUsage)
    {
        return typeUsage.RequiresSpecialization ? 64 : 32; // Basic estimation
    }

    /// <summary>
    /// Expression visitor for complexity calculation.
    /// </summary>
    private class ComplexityVisitor : ExpressionVisitor
    {
        public int TotalComplexity { get; private set; }
        public int OperationCount { get; private set; }
        public long EstimatedMemoryUsage { get; private set; }
        public double ParallelizationPotential { get; private set; } = 0.7;
        public Dictionary<string, int> ComplexityByCategory { get; } = new();

        protected override Expression VisitBinary(BinaryExpression node)
        {
            OperationCount++;
            var complexity = GetBinaryOperationComplexity(node.NodeType);
            TotalComplexity += complexity;


            AddComplexity("Arithmetic", complexity);
            EstimatedMemoryUsage += 16; // Approximate memory for binary operation

            return base.VisitBinary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            OperationCount++;
            var complexity = GetMethodComplexity(node.Method.Name);
            TotalComplexity += complexity;


            AddComplexity("Method", complexity);
            EstimatedMemoryUsage += 32; // Approximate memory for method call

            // Some methods have good parallelization potential
            if (IsParallelizableMethod(node.Method.Name))
            {
                ParallelizationPotential = Math.Min(1.0, ParallelizationPotential + 0.1);
            }

            return base.VisitMethodCall(node);
        }

        private static int GetBinaryOperationComplexity(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Add => 1,
                ExpressionType.Subtract => 1,
                ExpressionType.Multiply => 2,
                ExpressionType.Divide => 4,
                ExpressionType.Modulo => 4,
                ExpressionType.Power => 8,
                _ => 1
            };
        }

        private static int GetMethodComplexity(string methodName)
        {
            return methodName switch
            {
                "Sin" or "Cos" or "Tan" => 10,
                "Sqrt" => 5,
                "Log" => 8,
                "Exp" => 8,
                _ => 3
            };
        }

        private static bool IsParallelizableMethod(string methodName)
        {
            return methodName switch
            {
                "Select" or "Where" or "Sum" or "Average" => true,
                _ => false
            };
        }

        private void AddComplexity(string category, int complexity)
        {
            ComplexityByCategory[category] = ComplexityByCategory.GetValueOrDefault(category, 0) + complexity;
        }
    }
}

/// <summary>
/// Result of memory access analysis for a specific operator.
/// </summary>
public class MemoryAccessAnalysisResult
{
    /// <summary>Gets or sets the memory access pattern.</summary>
    public MemoryAccessPattern AccessPattern { get; set; }

    /// <summary>Gets or sets the access frequency.</summary>
    public int AccessFrequency { get; set; }

    /// <summary>Gets or sets the memory bandwidth usage (0.0 to 1.0).</summary>
    public double MemoryBandwidthUsage { get; set; }

    /// <summary>Gets or sets whether coalescing opportunities exist.</summary>
    public bool CoalescingOpportunities { get; set; }

    /// <summary>Gets or sets the identified bottlenecks.</summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>Gets or sets the cache hit ratio estimate.</summary>
    public double CacheHitRatio { get; set; } = 0.8;

    /// <summary>Gets or sets the memory locality factor.</summary>
    public double LocalityFactor { get; set; } = 0.8;
}

/// <summary>
/// Type analyzer for decimal types in expressions.
/// </summary>
public class DecimalTypeAnalyzer : ITypeAnalyzer
{
    /// <inheritdoc />
    public Type TargetType => typeof(decimal);


    /// <inheritdoc />
    public TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context)
    {
        return AnalyzeDecimalUsage(expression);
    }


    /// <inheritdoc />
    public bool SupportsVectorization() => false;


    /// <inheritdoc />
    public int GetOptimalAlignment() => 16;


    /// <inheritdoc />
    public double EstimateOperationComplexity(ExpressionType operation) => 5.0;


    /// <inheritdoc />
    public IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression)
    {
        yield return new OptimizationHint(OptimizationHintType.TypeSpecialization, "Decimal not GPU compatible", OptimizationImpact.High);
    }


    /// <inheritdoc />
    public TypeUsageInfo Analyze(Expression expression, object? context = null)
    {
        var analysisContext = context as AnalysisContext ?? new AnalysisContext();
        return AnalyzeUsage(expression, analysisContext);
    }

    /// <summary>
    /// Analyzes decimal type usage in expressions.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>Decimal type analysis result</returns>
    public TypeUsageInfo AnalyzeDecimalUsage(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var info = new TypeUsageInfo
        {
            Type = typeof(decimal),
            UsageFrequency = 1,
            UsageCount = 1,
            RequiresSpecialization = true,
            MemoryPattern = MemoryAccessPattern.Sequential,
            SupportsSimd = false,
            EstimatedSize = 16,
            Hints = new List<string>()
        };

        var visitor = new DecimalUsageVisitor();
        visitor.Visit(expression);

        // Update the usage count (cannot use 'with' on mutable class)
        info.UsageCount = visitor.DecimalUsageCount;
        // ConversionRequirements not part of TypeUsageInfo

        return info;
    }

    private class DecimalUsageVisitor : ExpressionVisitor
    {
        public int DecimalUsageCount { get; private set; }
        public List<string> ConversionRequirements { get; } = new();

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(decimal))
            {
                DecimalUsageCount++;
                ConversionRequirements.Add("Convert decimal to double for GPU execution");
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Type == typeof(decimal))
            {
                DecimalUsageCount++;
                ConversionRequirements.Add("Convert decimal parameter to double");
            }
            return base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Type analyzer for string types in expressions.
/// </summary>
public class StringTypeAnalyzer : ITypeAnalyzer
{
    /// <inheritdoc />
    public Type TargetType => typeof(string);


    /// <inheritdoc />
    public TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context)
    {
        return AnalyzeStringUsage(expression);
    }


    /// <inheritdoc />
    public bool SupportsVectorization() => false;


    /// <inheritdoc />
    public int GetOptimalAlignment() => IntPtr.Size;


    /// <inheritdoc />
    public double EstimateOperationComplexity(ExpressionType operation) => 5.0;


    /// <inheritdoc />
    public IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression)
    {
        yield return new OptimizationHint(OptimizationHintType.TypeSpecialization, "String not GPU compatible", OptimizationImpact.High);
    }


    /// <inheritdoc />
    public TypeUsageInfo Analyze(Expression expression, object? context = null)
    {
        var analysisContext = context as AnalysisContext ?? new AnalysisContext();
        return AnalyzeUsage(expression, analysisContext);
    }

    /// <summary>
    /// Analyzes string type usage in expressions.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>String type analysis result</returns>
    public TypeUsageInfo AnalyzeStringUsage(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var info = new TypeUsageInfo
        {
            Type = typeof(string),
            UsageFrequency = 1,
            UsageCount = 1,
            RequiresSpecialization = true,
            MemoryPattern = MemoryAccessPattern.Random,
            SupportsSimd = false,
            EstimatedSize = IntPtr.Size,
            Hints = new List<string>()
        };

        var visitor = new StringUsageVisitor();
        visitor.Visit(expression);

        // Update the usage count (cannot use 'with' on mutable class)
        info.UsageCount = visitor.StringUsageCount;
        // ConversionRequirements not part of TypeUsageInfo

        return info;
    }

    private class StringUsageVisitor : ExpressionVisitor
    {
        public int StringUsageCount { get; private set; }
        public List<string> ConversionRequirements { get; } = new();

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(string))
            {
                StringUsageCount++;
                ConversionRequirements.Add("String operations not supported on GPU");
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string))
            {
                StringUsageCount++;
                ConversionRequirements.Add($"String method {node.Method.Name} requires CPU execution");
            }
            return base.VisitMethodCall(node);
        }
    }
}

/// <summary>
/// Type analyzer for boolean types in expressions.
/// </summary>
public class BooleanTypeAnalyzer : ITypeAnalyzer
{
    /// <inheritdoc />
    public Type TargetType => typeof(bool);


    /// <inheritdoc />
    public TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context)
    {
        return AnalyzeBooleanUsage(expression);
    }


    /// <inheritdoc />
    public bool SupportsVectorization() => true;


    /// <inheritdoc />
    public int GetOptimalAlignment() => 16;


    /// <inheritdoc />
    public double EstimateOperationComplexity(ExpressionType operation) => 1.0;


    /// <inheritdoc />
    public IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression)
    {
        yield return new OptimizationHint(OptimizationHintType.Vectorization, "Boolean operations vectorizable", OptimizationImpact.Medium);
    }


    /// <inheritdoc />
    public TypeUsageInfo Analyze(Expression expression, object? context = null)
    {
        var analysisContext = context as AnalysisContext ?? new AnalysisContext();
        return AnalyzeUsage(expression, analysisContext);
    }

    /// <summary>
    /// Analyzes boolean type usage in expressions.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>Boolean type analysis result</returns>
    public TypeUsageInfo AnalyzeBooleanUsage(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var info = new TypeUsageInfo
        {
            Type = typeof(bool),
            UsageFrequency = 1,
            UsageCount = 1,
            RequiresSpecialization = false,
            MemoryPattern = MemoryAccessPattern.Sequential,
            SupportsSimd = true,
            EstimatedSize = 1,
            Hints = new List<string>()
        };

        var visitor = new BooleanUsageVisitor();
        visitor.Visit(expression);

        // Update the usage count (cannot use 'with' on mutable class)
        info.UsageCount = visitor.BooleanUsageCount;

        return info;
    }

    private class BooleanUsageVisitor : ExpressionVisitor
    {
        public int BooleanUsageCount { get; private set; }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Type == typeof(bool))
            {
                BooleanUsageCount++;
            }
            return base.VisitBinary(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                BooleanUsageCount++;
            }
            return base.VisitUnary(node);
        }
    }
}