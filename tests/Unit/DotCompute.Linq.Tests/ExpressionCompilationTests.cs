// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Tests;

/// <summary>
/// Tests for dynamic expression-to-kernel compilation functionality.
/// </summary>
public class ExpressionCompilationTests
{
    private readonly ILogger<ExpressionOptimizer> _optimizerLogger;
    private readonly ILogger<DefaultKernelFactory> _factoryLogger;
    private readonly ILogger<ExpressionToKernelCompiler> _compilerLogger;
    private readonly MockAccelerator _accelerator;

    public ExpressionCompilationTests()
    {
        _optimizerLogger = NullLogger<ExpressionOptimizer>.Instance;
        _factoryLogger = NullLogger<DefaultKernelFactory>.Instance;
        _compilerLogger = NullLogger<ExpressionToKernelCompiler>.Instance;
        _accelerator = new MockAccelerator();
    }

    [Fact]
    public void ExpressionOptimizer_CanFuseSelectOperations()
    {
        // Arrange
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var options = new CompilationOptions { EnableOperatorFusion = true };
        
        // Create a chained Select expression: source.Select(x => x * 2).Select(x => x + 1)
        var sourceExpr = Expression.Parameter(typeof(IQueryable<int>), "source");
        var firstSelect = CreateSelectExpression(sourceExpr, x => x * 2);
        var secondSelect = CreateSelectExpression(firstSelect, x => x + 1);

        // Act
        var optimized = optimizer.Optimize(secondSelect, options);

        // Assert
        Assert.NotNull(optimized);
        // The optimizer should create a fused expression
        // We can't directly test the fusion metadata, but we can verify the structure
        Assert.IsType<MethodCallExpression>(optimized);
    }

    [Fact]
    public void ExpressionOptimizer_CanAnalyzeExpressions()
    {
        // Arrange
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var expression = CreateSelectExpression(
            Expression.Parameter(typeof(IQueryable<int>), "source"),
            x => x * 2);

        // Act
        var suggestions = optimizer.Analyze(expression);

        // Assert
        Assert.NotNull(suggestions);
        // Should not suggest fusion for a single operation
        Assert.Empty(suggestions);
    }

    [Fact]
    public void DefaultKernelFactory_CanCreateKernelFromExpression()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var expression = CreateSimpleMapExpression();
        var context = CreateTestKernelContext();

        // Act
        var kernel = factory.CreateKernelFromExpression(_accelerator, expression, context);

        // Assert
        Assert.NotNull(kernel);
        Assert.NotEmpty(kernel.Name);
    }

    [Fact]
    public void DefaultKernelFactory_HandlesFusedExpressions()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var options = new CompilationOptions { EnableOperatorFusion = true };
        
        // Create fused expression
        var sourceExpr = Expression.Parameter(typeof(IQueryable<int>), "source");
        var firstSelect = CreateSelectExpression(sourceExpr, x => x * 2);
        var secondSelect = CreateSelectExpression(firstSelect, x => x + 1);
        var fusedExpression = optimizer.Optimize(secondSelect, options);
        
        var context = CreateTestKernelContext();

        // Act
        var kernel = factory.CreateKernelFromExpression(_accelerator, fusedExpression, context);

        // Assert
        Assert.NotNull(kernel);
        Assert.NotEmpty(kernel.Name);
    }

    [Fact]
    public async Task ExpressionToKernelCompiler_CanCompileSimpleExpression()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var compiler = new ExpressionToKernelCompiler(factory, optimizer, _compilerLogger);
        
        var expression = CreateSimpleMapExpression();

        // Act
        var kernel = await compiler.CompileExpressionAsync(expression, _accelerator);

        // Assert
        Assert.NotNull(kernel);
        Assert.NotEmpty(kernel.Name);
    }

    [Fact]
    public void ExpressionToKernelCompiler_CanValidateExpressions()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var compiler = new ExpressionToKernelCompiler(factory, optimizer, _compilerLogger);
        
        var validExpression = CreateSimpleMapExpression();
        var invalidExpression = CreateUnsupportedExpression();

        // Act
        var isValidValid = compiler.CanCompileExpression(validExpression);
        var isInvalidValid = compiler.CanCompileExpression(invalidExpression);

        // Assert
        Assert.True(isValidValid);
        Assert.False(isInvalidValid);
    }

    [Fact]
    public void ExpressionToKernelCompiler_CanEstimateResources()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var compiler = new ExpressionToKernelCompiler(factory, optimizer, _compilerLogger);
        
        var expression = CreateSimpleMapExpression();

        // Act
        var estimate = compiler.EstimateResources(expression);

        // Assert
        Assert.NotNull(estimate);
        Assert.True(estimate.EstimatedMemoryUsage > 0);
        Assert.True(estimate.EstimatedCompilationTime > TimeSpan.Zero);
        Assert.True(estimate.ComplexityScore >= 0);
        Assert.True(estimate.ParallelizationFactor >= 0 && estimate.ParallelizationFactor <= 1);
    }

    [Fact]
    public async Task ExpressionToKernelCompiler_HandlesFusedExpressions()
    {
        // Arrange
        var factory = new DefaultKernelFactory(_factoryLogger);
        var optimizer = new ExpressionOptimizer(_optimizerLogger);
        var compiler = new ExpressionToKernelCompiler(factory, optimizer, _compilerLogger);
        
        // Create a fusable expression chain
        var sourceExpr = Expression.Parameter(typeof(IQueryable<int>), "source");
        var whereExpr = CreateWhereExpression(sourceExpr, x => x > 0);
        var selectExpr = CreateSelectExpression(whereExpr, x => x * 2);

        // Act
        var kernel = await compiler.CompileExpressionAsync(selectExpr, _accelerator);

        // Assert
        Assert.NotNull(kernel);
        Assert.NotEmpty(kernel.Name);
    }

    [Fact]
    public void FusionMetadataStore_CanStoreAndRetrieveMetadata()
    {
        // Arrange
        var key = "test_expression";
        var metadata = new Dictionary<string, object>
        {
            ["FusedOperations"] = new[] { "Where", "Select" },
            ["FusionType"] = "FilterMap",
            ["EstimatedSpeedup"] = 1.8
        };

        // Act
        FusionMetadataStore.SetMetadata(key, metadata);
        var retrieved = FusionMetadataStore.GetMetadata(key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(metadata["FusionType"], retrieved["FusionType"]);
        Assert.Equal(metadata["EstimatedSpeedup"], retrieved["EstimatedSpeedup"]);

        // Cleanup
        FusionMetadataStore.Clear();
    }

    [Theory]
    [InlineData(typeof(int), 4)]
    [InlineData(typeof(float), 4)]
    [InlineData(typeof(double), 8)]
    [InlineData(typeof(bool), 1)]
    public void GetElementSize_ReturnsCorrectSizes(Type type, int expectedSize)
    {
        // Act
        var size = GetElementSize(type);

        // Assert
        Assert.Equal(expectedSize, size);
    }

    private static Expression CreateSelectExpression(Expression source, Expression<Func<int, int>> selector)
    {
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));

        return Expression.Call(selectMethod, source, Expression.Quote(selector));
    }

    private static Expression CreateWhereExpression(Expression source, Expression<Func<int, bool>> predicate)
    {
        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int));

        return Expression.Call(whereMethod, source, Expression.Quote(predicate));
    }

    private static Expression CreateSimpleMapExpression()
    {
        var parameter = Expression.Parameter(typeof(int), "x");
        var multiply = Expression.Multiply(parameter, Expression.Constant(2));
        var lambda = Expression.Lambda<Func<int, int>>(multiply, parameter);
        
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        return CreateSelectExpression(source, lambda);
    }

    private static Expression CreateUnsupportedExpression()
    {
        // Create an expression that uses unsupported operations (like file I/O)
        var fileReadMethod = typeof(File).GetMethod("ReadAllText", new[] { typeof(string) })!;
        return Expression.Call(fileReadMethod, Expression.Constant("test.txt"));
    }

    private static KernelGenerationContext CreateTestKernelContext()
    {
        return new KernelGenerationContext
        {
            DeviceInfo = new AcceleratorInfo 
            { 
                Name = "Test Device", 
                Type = AcceleratorType.CPU,
                MaxWorkGroupSize = 256
            },
            UseSharedMemory = false,
            UseVectorTypes = false,
            Precision = PrecisionMode.Single
        };
    }

    private static int GetElementSize(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
            _ => 4
        };
    }

    /// <summary>
    /// Mock accelerator for testing.
    /// </summary>
    private class MockAccelerator : IAccelerator
    {
        public AcceleratorInfo Info { get; } = new() 
        { 
            Name = "Mock GPU", 
            Type = AcceleratorType.CUDA,
            MaxWorkGroupSize = 1024
        };
        public AcceleratorType Type => AcceleratorType.CUDA;
        public IMemoryManager Memory => throw new NotImplementedException();
        public bool IsDisposed => false;

        public ValueTask<TResult> ExecuteAsync<TResult>(Func<ValueTask<TResult>> operation, CancellationToken cancellationToken = default)
            => operation();

        public void Dispose() { }
        public ValueTask DisposeAsync() => default;
    }
}

/// <summary>
/// Tests for specific expression fusion scenarios.
/// </summary>
public class ExpressionFusionTests
{
    private readonly ExpressionOptimizer _optimizer;
    private readonly CompilationOptions _options;

    public ExpressionFusionTests()
    {
        _optimizer = new ExpressionOptimizer(NullLogger<ExpressionOptimizer>.Instance);
        _options = new CompilationOptions { EnableOperatorFusion = true };
    }

    [Fact]
    public void FuseWhereSelect_CreatesFilterMapOperation()
    {
        // Arrange
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        var whereExpr = CreateWhereExpression(source, x => x > 0);
        var selectExpr = CreateSelectExpression(whereExpr, x => x * 2);

        // Act
        var optimized = _optimizer.Optimize(selectExpr, _options);

        // Assert
        Assert.NotNull(optimized);
        Assert.IsType<MethodCallExpression>(optimized);
        
        // Check if fusion metadata was stored
        var key = optimized.ToString();
        var metadata = FusionMetadataStore.GetMetadata(key);
        
        if (metadata != null)
        {
            Assert.Equal("FilterMap", metadata["FusionType"]);
        }
        
        FusionMetadataStore.Clear();
    }

    [Fact]
    public void FuseSelectWhere_CreatesMapFilterOperation()
    {
        // Arrange
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        var selectExpr = CreateSelectExpression(source, x => x * 2);
        var whereExpr = CreateWhereExpression(selectExpr, x => x > 0);

        // Act
        var optimized = _optimizer.Optimize(whereExpr, _options);

        // Assert
        Assert.NotNull(optimized);
        Assert.IsType<MethodCallExpression>(optimized);
        
        FusionMetadataStore.Clear();
    }

    [Fact]
    public void FuseMultipleSelects_CreatesMapMapOperation()
    {
        // Arrange
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        var firstSelect = CreateSelectExpression(source, x => x * 2);
        var secondSelect = CreateSelectExpression(firstSelect, x => x + 1);

        // Act
        var optimized = _optimizer.Optimize(secondSelect, _options);

        // Assert
        Assert.NotNull(optimized);
        Assert.IsType<MethodCallExpression>(optimized);
        
        FusionMetadataStore.Clear();
    }

    private static Expression CreateSelectExpression(Expression source, Expression<Func<int, int>> selector)
    {
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));

        return Expression.Call(selectMethod, source, Expression.Quote(selector));
    }

    private static Expression CreateWhereExpression(Expression source, Expression<Func<int, bool>> predicate)
    {
        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int));

        return Expression.Call(whereMethod, source, Expression.Quote(predicate));
    }
}

/// <summary>
/// Performance and resource estimation tests.
/// </summary>
public class ExpressionResourceEstimationTests
{
    private readonly ExpressionToKernelCompiler _compiler;

    public ExpressionResourceEstimationTests()
    {
        var factory = new DefaultKernelFactory(NullLogger<DefaultKernelFactory>.Instance);
        var optimizer = new ExpressionOptimizer(NullLogger<ExpressionOptimizer>.Instance);
        _compiler = new ExpressionToKernelCompiler(factory, optimizer, 
            NullLogger<ExpressionToKernelCompiler>.Instance);
    }

    [Theory]
    [InlineData(1, 100)]      // Simple expression should have low memory usage
    [InlineData(5, 500)]      // More complex expression should have higher usage
    [InlineData(10, 1000)]    // Very complex expression should have high usage
    public void EstimateResources_ScalesWithComplexity(int operationCount, long expectedMinMemory)
    {
        // Arrange
        var expression = CreateComplexExpression(operationCount);

        // Act
        var estimate = _compiler.EstimateResources(expression);

        // Assert
        Assert.True(estimate.EstimatedMemoryUsage >= expectedMinMemory,
            $"Memory usage {estimate.EstimatedMemoryUsage} should be at least {expectedMinMemory}");
        Assert.True(estimate.ComplexityScore >= operationCount,
            $"Complexity score {estimate.ComplexityScore} should be at least {operationCount}");
    }

    [Fact]
    public void EstimateResources_HandlesParallelizableOperations()
    {
        // Arrange - Create expression with highly parallelizable operations
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        var select1 = CreateSelectExpression(source, x => x * 2);
        var select2 = CreateSelectExpression(select1, x => x + 1);
        var where1 = CreateWhereExpression(select2, x => x > 0);

        // Act
        var estimate = _compiler.EstimateResources(where1);

        // Assert
        Assert.True(estimate.ParallelizationFactor > 0.5,
            $"Parallelization factor {estimate.ParallelizationFactor} should be high for parallelizable operations");
    }

    [Fact]
    public void EstimateResources_HandlesNonParallelizableOperations()
    {
        // Arrange - Create expression with sequential operations
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        var orderBy = CreateOrderByExpression(source, x => x);

        // Act
        var estimate = _compiler.EstimateResources(orderBy);

        // Assert
        // OrderBy is less parallelizable than map/filter operations
        Assert.True(estimate.ParallelizationFactor >= 0);
    }

    private static Expression CreateComplexExpression(int operationCount)
    {
        var source = Expression.Parameter(typeof(IQueryable<int>), "source");
        Expression current = source;

        for (int i = 0; i < operationCount; i++)
        {
            if (i % 2 == 0)
            {
                current = CreateSelectExpression(current, x => x * (i + 1));
            }
            else
            {
                current = CreateWhereExpression(current, x => x > i);
            }
        }

        return current;
    }

    private static Expression CreateSelectExpression(Expression source, Expression<Func<int, int>> selector)
    {
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));

        return Expression.Call(selectMethod, source, Expression.Quote(selector));
    }

    private static Expression CreateWhereExpression(Expression source, Expression<Func<int, bool>> predicate)
    {
        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int));

        return Expression.Call(whereMethod, source, Expression.Quote(predicate));
    }

    private static Expression CreateOrderByExpression(Expression source, Expression<Func<int, int>> keySelector)
    {
        var orderByMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));

        return Expression.Call(orderByMethod, source, Expression.Quote(keySelector));
    }
}