// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Tests.Common;

namespace DotCompute.Linq.Tests.Analysis;

/// <summary>
/// Comprehensive tests for pipeline expression analysis and optimization.
/// Tests expression tree parsing, performance analysis, and backend selection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ExpressionAnalysis")]
public class PipelineExpressionTests : PipelineTestBase
{
    private readonly MockPipelineExpressionVisitor _expressionVisitor;
    private readonly MockPerformanceAnalyzer _performanceAnalyzer;
    private readonly MockBackendSelector _backendSelector;

    public PipelineExpressionTests()
    {
        _expressionVisitor = new MockPipelineExpressionVisitor();
        _performanceAnalyzer = new MockPerformanceAnalyzer();
        _backendSelector = new MockBackendSelector();
    }

    [Fact]
    public void ExpressionVisitor_SimpleSelect_IdentifiesOperation()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var queryable = data.AsQueryable();
        Expression<Func<float, float>> selector = x => x * 2.0f;
        var query = queryable.Select(selector);

        // Act
        var result = _expressionVisitor.Visit(query.Expression);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Select", _expressionVisitor.IdentifiedOperations);
        Assert.Single(_expressionVisitor.IdentifiedOperations);
        Assert.Equal(1, _expressionVisitor.ComplexityScore);
    }

    [Fact]
    public void ExpressionVisitor_ComplexQuery_ParsesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var queryable = data.AsQueryable();
        
        var complexQuery = queryable
            .Where(x => x > 0.5f)
            .Select(x => x * x)
            .GroupBy(x => (int)(x * 10))
            .Select(g => g.Sum())
            .OrderByDescending(x => x)
            .Take(100);

        // Act
        var result = _expressionVisitor.Visit(complexQuery.Expression);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Where", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("Select", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("GroupBy", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("OrderByDescending", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("Take", _expressionVisitor.IdentifiedOperations);
        
        // Complex query should have higher complexity score
        Assert.True(_expressionVisitor.ComplexityScore >= 5);
    }

    [Fact]
    public void ExpressionVisitor_UnsupportedExpression_ThrowsException()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var queryable = data.AsQueryable();
        
        // Create an unsupported query with string operations
        var unsupportedQuery = queryable
            .Select(x => x.ToString()) // String conversion not supported
            .Where(s => s.Length > 3);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
        {
            _expressionVisitor.Visit(unsupportedQuery.Expression);
        });
    }

    [Fact]
    public void PerformanceAnalyzer_EstimatesMemoryCorrectly()
    {
        // Arrange
        var dataSize = 100000;
        var data = GenerateTestData<float>(dataSize);
        var queryable = data.AsQueryable();
        
        var query = queryable
            .Where(x => x > 0.5f)
            .Select(x => x * 2.0f);

        // Act
        var memoryEstimate = _performanceAnalyzer.EstimateMemoryUsage(query.Expression, dataSize);

        // Assert
        Assert.True(memoryEstimate > 0);
        Assert.True(memoryEstimate < dataSize * sizeof(float) * 3); // Should be reasonable
        
        // Memory estimate should account for intermediate results
        var expectedBaseMemory = dataSize * sizeof(float);
        Assert.True(memoryEstimate >= expectedBaseMemory);
    }

    [Theory]
    [InlineData(1000, "CPU")]
    [InlineData(10000, "CPU")]
    [InlineData(100000, "CUDA")]
    [InlineData(1000000, "CUDA")]
    public void BackendSelector_RecommendsOptimal(int dataSize, string expectedBackend)
    {
        // Arrange
        var data = GenerateTestData<float>(dataSize);
        var queryable = data.AsQueryable();
        var query = queryable.Select(x => x * 2.0f);

        // Act
        var recommendation = _backendSelector.RecommendOptimalBackend(query.Expression, dataSize);

        // Assert
        Assert.Equal(expectedBackend, recommendation.Backend);
        Assert.True(recommendation.ConfidenceScore > 0.0);
        Assert.True(recommendation.ConfidenceScore <= 1.0);
        Assert.NotNull(recommendation.Reasoning);
    }

    [Theory]
    [InlineData("x => x * 2", 1)]
    [InlineData("x => x * x + x", 2)]
    [InlineData("x => Math.Sin(x) + Math.Cos(x)", 3)]
    [InlineData("x => (x > 0) ? x * 2 : x / 2", 4)]
    public void ExpressionComplexity_AnalyzesCorrectly(string expressionString, int expectedComplexity)
    {
        // Arrange
        var parameter = Expression.Parameter(typeof(float), "x");
        var expression = CreateExpressionFromString(expressionString, parameter);

        // Act
        var complexity = _expressionVisitor.AnalyzeComplexity(expression);

        // Assert
        Assert.Equal(expectedComplexity, complexity);
    }

    [Fact]
    public void ExpressionVisitor_NestedLambdas_HandlesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var queryable = data.AsQueryable();
        
        var nestedQuery = queryable
            .GroupBy(x => (int)x)
            .Select(g => g.Where(x => x > g.Key).Sum());

        // Act
        var result = _expressionVisitor.Visit(nestedQuery.Expression);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("GroupBy", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("Select", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("Where", _expressionVisitor.IdentifiedOperations);
        Assert.Contains("Sum", _expressionVisitor.IdentifiedOperations);
        
        // Nested queries should have higher complexity
        Assert.True(_expressionVisitor.ComplexityScore >= 4);
    }

    [Fact]
    public void PerformanceAnalyzer_PredicateSelectivity_CalculatesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(10000);
        var queryable = data.AsQueryable();
        
        // Create queries with different selectivity
        var highSelectivity = queryable.Where(x => x > 0.1f); // ~90% of data
        var lowSelectivity = queryable.Where(x => x > 0.9f);  // ~10% of data

        // Act
        var highSelectivityEstimate = _performanceAnalyzer.EstimateSelectivity(highSelectivity.Expression);
        var lowSelectivityEstimate = _performanceAnalyzer.EstimateSelectivity(lowSelectivity.Expression);

        // Assert
        Assert.True(highSelectivityEstimate > lowSelectivityEstimate);
        Assert.True(highSelectivityEstimate >= 0.8); // Should be around 90%
        Assert.True(lowSelectivityEstimate <= 0.2);  // Should be around 10%
    }

    [Fact]
    public void ExpressionVisitor_StaticMethodCalls_IdentifiesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var queryable = data.AsQueryable();
        
        var mathQuery = queryable
            .Select(x => MathF.Sqrt(x))
            .Where(x => MathF.Abs(x) > 0.5f)
            .Select(x => MathF.Sin(x));

        // Act
        var result = _expressionVisitor.Visit(mathQuery.Expression);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("MathF.Sqrt", _expressionVisitor.IdentifiedMethodCalls);
        Assert.Contains("MathF.Abs", _expressionVisitor.IdentifiedMethodCalls);
        Assert.Contains("MathF.Sin", _expressionVisitor.IdentifiedMethodCalls);
        
        // Math operations should increase complexity
        Assert.True(_expressionVisitor.ComplexityScore >= 5);
    }

    [Fact]
    public void BackendSelector_MemoryConstraints_ConsidersCorrectly()
    {
        // Arrange - Large dataset that might not fit in GPU memory
        var largeDataSize = 100_000_000; // 100M elements
        var data = GenerateTestData<float>(1000); // Small sample for testing
        var queryable = data.AsQueryable();
        var query = queryable.Select(x => x * 2.0f);

        // Act
        var recommendation = _backendSelector.RecommendOptimalBackend(query.Expression, largeDataSize);

        // Assert
        // For very large datasets, should consider memory constraints
        Assert.NotNull(recommendation.MemoryConstraints);
        Assert.True(recommendation.EstimatedMemoryUsage > 0);
        
        if (recommendation.Backend == "CUDA")
        {
            Assert.True(recommendation.EstimatedMemoryUsage <= recommendation.MemoryConstraints.GpuMemoryLimit);
        }
    }

    [Fact]
    public void ExpressionOptimizer_FusesOperations_CorrectlyIdentifies()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var queryable = data.AsQueryable();
        
        // Query that can be fused: select -> select
        var fusableQuery = queryable
            .Select(x => x * 2.0f)
            .Select(x => x + 1.0f);

        // Act
        var optimizations = _expressionVisitor.IdentifyOptimizations(fusableQuery.Expression);

        // Assert
        Assert.NotNull(optimizations);
        Assert.Contains("KernelFusion", optimizations.PossibleOptimizations);
        Assert.True(optimizations.FusionOpportunities > 0);
    }

    [Fact]
    public void PerformanceAnalyzer_EstimatesExecutionTime_Reasonably()
    {
        // Arrange
        var dataSize = 1000000;
        var data = GenerateTestData<float>(1000); // Sample for testing
        var queryable = data.AsQueryable();
        
        var simpleQuery = queryable.Select(x => x * 2.0f);
        var complexQuery = queryable
            .Where(x => x > 0.1f)
            .Select(x => MathF.Sin(x))
            .GroupBy(x => (int)(x * 10))
            .Select(g => g.Sum());

        // Act
        var simpleTime = _performanceAnalyzer.EstimateExecutionTime(simpleQuery.Expression, dataSize, "CPU");
        var complexTime = _performanceAnalyzer.EstimateExecutionTime(complexQuery.Expression, dataSize, "CPU");

        // Assert
        Assert.True(simpleTime > TimeSpan.Zero);
        Assert.True(complexTime > simpleTime); // Complex query should take longer
        Assert.True(complexTime < TimeSpan.FromSeconds(10)); // Should be reasonable
    }

    [Fact]
    public void ExpressionVisitor_ConditionalExpressions_HandlesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var queryable = data.AsQueryable();
        
        var conditionalQuery = queryable.Select(x => x > 0 ? x * 2 : x / 2);

        // Act
        var result = _expressionVisitor.Visit(conditionalQuery.Expression);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Conditional", _expressionVisitor.IdentifiedExpressionTypes);
        Assert.True(_expressionVisitor.ComplexityScore >= 2); // Conditionals add complexity
    }

    [Theory]
    [InlineData(1000, 1000)]    // Small data, quick processing
    [InlineData(100000, 5000)]  // Medium data, moderate processing
    [InlineData(1000000, 50000)] // Large data, longer processing
    public void PerformanceAnalyzer_ScalesWithDataSize_Correctly(int dataSize, int expectedTimeMs)
    {
        // Arrange
        var data = GenerateTestData<float>(Math.Min(dataSize, 1000));
        var queryable = data.AsQueryable();
        var query = queryable.Select(x => x * 2.0f).Where(x => x > 1.0f);

        // Act
        var estimatedTime = _performanceAnalyzer.EstimateExecutionTime(query.Expression, dataSize, "CPU");

        // Assert
        // Should scale roughly linearly with data size
        var tolerance = TimeSpan.FromMilliseconds(expectedTimeMs * 0.5); // 50% tolerance
        Assert.True(estimatedTime >= TimeSpan.FromMilliseconds(expectedTimeMs * 0.5));
        Assert.True(estimatedTime <= TimeSpan.FromMilliseconds(expectedTimeMs * 1.5));
    }

    private Expression CreateExpressionFromString(string expressionString, ParameterExpression parameter)
    {
        // Simplified expression creation for testing
        // In a real implementation, this would parse the string into an expression tree
        return expressionString switch
        {
            "x => x * 2" => Expression.Multiply(parameter, Expression.Constant(2.0f)),
            "x => x * x + x" => Expression.Add(
                Expression.Multiply(parameter, parameter), 
                parameter),
            "x => Math.Sin(x) + Math.Cos(x)" => Expression.Add(
                Expression.Call(typeof(MathF), "Sin", null, parameter),
                Expression.Call(typeof(MathF), "Cos", null, parameter)),
            "x => (x > 0) ? x * 2 : x / 2" => Expression.Condition(
                Expression.GreaterThan(parameter, Expression.Constant(0.0f)),
                Expression.Multiply(parameter, Expression.Constant(2.0f)),
                Expression.Divide(parameter, Expression.Constant(2.0f))),
            _ => parameter
        };
    }
}

/// <summary>
/// Mock pipeline expression visitor for testing expression analysis.
/// </summary>
public class MockPipelineExpressionVisitor
{
    public List<string> IdentifiedOperations { get; } = new();
    public List<string> IdentifiedMethodCalls { get; } = new();
    public List<string> IdentifiedExpressionTypes { get; } = new();
    public int ComplexityScore { get; private set; }

    public Expression Visit(Expression expression)
    {
        ComplexityScore = 0;
        IdentifiedOperations.Clear();
        IdentifiedMethodCalls.Clear();
        IdentifiedExpressionTypes.Clear();
        
        return VisitInternal(expression);
    }

    public int AnalyzeComplexity(Expression expression)
    {
        return expression.NodeType switch
        {
            ExpressionType.Multiply => 1,
            ExpressionType.Add => 1,
            ExpressionType.Call => 2,
            ExpressionType.Conditional => 3,
            _ => 1
        };
    }

    public OptimizationOpportunities IdentifyOptimizations(Expression expression)
    {
        var opportunities = new OptimizationOpportunities();
        
        // Simple fusion detection: consecutive Select operations
        if (IsConsecutiveSelectOperations(expression))
        {
            opportunities.PossibleOptimizations.Add("KernelFusion");
            opportunities.FusionOpportunities = 1;
        }
        
        return opportunities;
    }

    private Expression VisitInternal(Expression expression)
    {
        ComplexityScore++;
        
        switch (expression.NodeType)
        {
            case ExpressionType.Call:
                var methodCall = (MethodCallExpression)expression;
                var methodName = $"{methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}";
                
                if (methodCall.Method.DeclaringType == typeof(Queryable))
                {
                    IdentifiedOperations.Add(methodCall.Method.Name);
                }
                else if (methodCall.Method.DeclaringType == typeof(MathF))
                {
                    IdentifiedMethodCalls.Add(methodName);
                }
                
                // Check for unsupported operations
                if (methodCall.Method.Name == "ToString" && 
                    methodCall.Method.DeclaringType != typeof(Queryable))
                {
                    throw new NotSupportedException("String operations not supported in GPU kernels");
                }
                
                foreach (var arg in methodCall.Arguments)
                {
                    VisitInternal(arg);
                }
                break;
                
            case ExpressionType.Conditional:
                IdentifiedExpressionTypes.Add("Conditional");
                var conditional = (ConditionalExpression)expression;
                VisitInternal(conditional.Test);
                VisitInternal(conditional.IfTrue);
                VisitInternal(conditional.IfFalse);
                ComplexityScore += 2; // Conditionals add extra complexity
                break;
                
            case ExpressionType.Lambda:
                var lambda = (LambdaExpression)expression;
                VisitInternal(lambda.Body);
                break;
                
            case ExpressionType.Quote:
                var quote = (UnaryExpression)expression;
                VisitInternal(quote.Operand);
                break;
                
            default:
                // Handle other expression types
                if (expression is BinaryExpression binary)
                {
                    VisitInternal(binary.Left);
                    VisitInternal(binary.Right);
                }
                break;
        }
        
        return expression;
    }

    private bool IsConsecutiveSelectOperations(Expression expression)
    {
        // Simplified detection logic
        return IdentifiedOperations.Count(op => op == "Select") >= 2;
    }
}

/// <summary>
/// Mock performance analyzer for testing performance estimation.
/// </summary>
public class MockPerformanceAnalyzer
{
    public long EstimateMemoryUsage(Expression expression, int dataSize)
    {
        // Simple estimation based on data size and operation count
        var baseMemory = dataSize * sizeof(float);
        var operationMultiplier = CountOperations(expression);
        
        return baseMemory * operationMultiplier;
    }

    public double EstimateSelectivity(Expression expression)
    {
        // Simple selectivity estimation based on threshold values
        if (TryExtractThreshold(expression, out var threshold))
        {
            // Assume uniform distribution from 0 to 1
            return threshold switch
            {
                <= 0.1 => 0.9,
                <= 0.5 => 0.5,
                <= 0.9 => 0.1,
                _ => 0.05
            };
        }
        
        return 0.5; // Default 50% selectivity
    }

    public TimeSpan EstimateExecutionTime(Expression expression, int dataSize, string backend)
    {
        var complexity = CountOperations(expression);
        var baseTimePerElement = backend == "CUDA" ? 0.001 : 0.01; // microseconds
        
        var totalMicroseconds = dataSize * complexity * baseTimePerElement;
        return TimeSpan.FromMilliseconds(totalMicroseconds / 1000);
    }

    private int CountOperations(Expression expression)
    {
        // Simplified operation counting
        if (expression is MethodCallExpression methodCall)
        {
            return methodCall.Method.Name switch
            {
                "Select" => 1,
                "Where" => 2,
                "GroupBy" => 5,
                "Sum" => 3,
                "OrderBy" or "OrderByDescending" => 10,
                _ => 1
            };
        }
        
        return 1;
    }

    private bool TryExtractThreshold(Expression expression, out double threshold)
    {
        threshold = 0.5;
        
        if (expression is MethodCallExpression methodCall && methodCall.Method.Name == "Where")
        {
            // Try to extract threshold from lambda: x => x > threshold
            if (methodCall.Arguments.Count > 1 && 
                methodCall.Arguments[1] is UnaryExpression quote &&
                quote.Operand is LambdaExpression lambda &&
                lambda.Body is BinaryExpression comparison &&
                comparison.Right is ConstantExpression constant)
            {
                if (constant.Value is float floatValue)
                {
                    threshold = floatValue;
                    return true;
                }
            }
        }
        
        return false;
    }
}

/// <summary>
/// Mock backend selector for testing backend recommendation.
/// </summary>
public class MockBackendSelector
{
    public BackendRecommendation RecommendOptimalBackend(Expression expression, int dataSize)
    {
        var complexity = CountComplexity(expression);
        var memoryUsage = EstimateMemoryUsage(dataSize);
        
        // Simple recommendation logic
        var backend = dataSize >= 50000 && memoryUsage <= 1024 * 1024 * 1024 ? "CUDA" : "CPU"; // 1GB GPU limit
        var confidence = dataSize >= 100000 ? 0.9 : 0.6;
        
        return new BackendRecommendation
        {
            Backend = backend,
            ConfidenceScore = confidence,
            EstimatedMemoryUsage = memoryUsage,
            Reasoning = $"Data size: {dataSize}, Complexity: {complexity}",
            MemoryConstraints = new MemoryConstraints
            {
                GpuMemoryLimit = 1024 * 1024 * 1024, // 1GB
                CpuMemoryLimit = 8L * 1024 * 1024 * 1024 // 8GB
            }
        };
    }

    private int CountComplexity(Expression expression)
    {
        // Simplified complexity counting
        return 1;
    }

    private long EstimateMemoryUsage(int dataSize)
    {
        return dataSize * sizeof(float) * 2; // Input + output
    }
}

/// <summary>
/// Represents optimization opportunities identified in an expression.
/// </summary>
public class OptimizationOpportunities
{
    public List<string> PossibleOptimizations { get; } = new();
    public int FusionOpportunities { get; set; }
    public int VectorizationOpportunities { get; set; }
    public int MemoryOptimizations { get; set; }
}

/// <summary>
/// Represents a backend recommendation with confidence and constraints.
/// </summary>
public class BackendRecommendation
{
    public required string Backend { get; init; }
    public required double ConfidenceScore { get; init; }
    public required long EstimatedMemoryUsage { get; init; }
    public required string Reasoning { get; init; }
    public MemoryConstraints? MemoryConstraints { get; init; }
}

/// <summary>
/// Memory constraints for different backends.
/// </summary>
public class MemoryConstraints
{
    public long GpuMemoryLimit { get; set; }
    public long CpuMemoryLimit { get; set; }
}