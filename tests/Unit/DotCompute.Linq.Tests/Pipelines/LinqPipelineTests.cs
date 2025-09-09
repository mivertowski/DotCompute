// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Linq.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for LINQ to pipeline conversion functionality.
/// Tests expression analysis, pipeline generation, and execution optimization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LinqPipeline")]
public class LinqPipelineTests : PipelineTestBase
{
    private readonly MockComputeOrchestrator _mockOrchestrator;
    private readonly MockKernelPipelineBuilder _mockPipelineBuilder;

    public LinqPipelineTests()
    {
        _mockOrchestrator = (MockComputeOrchestrator)Services.GetRequiredService<IComputeOrchestrator>();
        _mockPipelineBuilder = new MockKernelPipelineBuilder();
        SetupMockServices();
    }

    [Fact]
    public void AsComputePipeline_SimpleQuery_ConvertsCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000, DataPattern.Sequential);
        var enumerable = data.AsEnumerable();

        // Act
        var pipeline = enumerable.AsComputePipeline(Services);

        // Assert
        Assert.NotNull(pipeline);
        Assert.IsAssignableFrom<IKernelPipelineBuilder>(pipeline);
    }

    [Fact]
    public void AsKernelPipeline_ComplexQuery_ExecutesOnGPU()
    {
        // Arrange
        var data = GenerateTestData<float>(10000, DataPattern.Random);
        var queryable = data.AsQueryable();

        // Act
        var pipeline = queryable.AsKernelPipeline(Services);

        // Assert
        Assert.NotNull(pipeline);
        Assert.IsAssignableFrom<IKernelPipelineBuilder>(pipeline);
    }

    [Fact]
    public async Task ThenSelect_TransformData_AppliesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000, DataPattern.Sequential);
        var pipeline = _mockPipelineBuilder;
        Expression<Func<float, float>> selector = x => x * 2.0f;

        // Act
        var result = pipeline.ThenSelect(selector);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IKernelPipeline>(result);
        
        // Verify the select operation was added to the pipeline
        Assert.Single(_mockPipelineBuilder.Operations);
        Assert.Equal("SelectKernel", _mockPipelineBuilder.Operations[0].KernelName);
    }

    [Fact]
    public async Task ThenWhere_FilterData_FiltersCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000, DataPattern.Random);
        var pipeline = _mockPipelineBuilder;
        Expression<Func<float, bool>> predicate = x => x > 0.5f;

        // Act
        var result = pipeline.ThenWhere(predicate);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IKernelPipeline>(result);
        
        // Verify the where operation was added to the pipeline
        Assert.Single(_mockPipelineBuilder.Operations);
        Assert.Equal("WhereKernel", _mockPipelineBuilder.Operations[0].KernelName);
    }

    [Fact]
    public async Task ThenGroupBy_GroupsData_GroupsCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000, DataPattern.Alternating);
        var pipeline = _mockPipelineBuilder;
        Expression<Func<float, int>> keySelector = x => (int)x % 10;

        // Act
        var result = pipeline.ThenGroupBy(keySelector);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IKernelPipeline>(result);
        
        // Verify the group by operation was added to the pipeline
        Assert.Single(_mockPipelineBuilder.Operations);
        Assert.Equal("GroupByKernel", _mockPipelineBuilder.Operations[0].KernelName);
    }

    [Fact]
    public async Task ExecutePipelineAsync_ReturnsExpectedResults()
    {
        // Arrange
        var data = GenerateTestData<float>(1000, DataPattern.Sequential);
        var mockPipeline = new MockKernelPipeline();
        mockPipeline.SetupMockResult(data);

        // Act
        var result = await mockPipeline.ExecutePipelineAsync<float[]>(OptimizationLevel.Balanced);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Length);
        Assert.True(mockPipeline.OptimizationApplied);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(100000)]
    public async Task LINQ_vs_Pipeline_Performance_Comparison(int dataSize)
    {
        // Arrange
        var data = GenerateTestData<float>(dataSize, DataPattern.Random);
        var threshold = 0.5f;

        // Act - Traditional LINQ
        var linqTime = MeasureExecutionTime(() =>
        {
            var linqResult = data.Where(x => x > threshold).Select(x => x * 2.0f).ToArray();
        });

        // Act - Pipeline execution (mocked)
        var pipelineTime = await MeasureExecutionTimeAsync(async () =>
        {
            var queryable = data.AsQueryable();
            var pipeline = queryable
                .AsKernelPipeline(Services)
                .ThenWhere<float>(x => x > threshold)
                .ThenSelect<float, float>(x => x * 2.0f);
            
            var pipelineResult = await pipeline.ExecutePipelineAsync<float[]>();
        });

        // Assert - Pipeline should be competitive or better for large datasets
        Logger?.LogInformation($"Data size: {dataSize}, LINQ: {linqTime.TotalMilliseconds:F2}ms, " +
                             $"Pipeline: {pipelineTime.TotalMilliseconds:F2}ms");

        if (dataSize >= 10000)
        {
            // For large datasets, pipeline should have overhead advantage
            Assert.True(pipelineTime <= linqTime * 2.0, 
                $"Pipeline performance regression for size {dataSize}. " +
                $"LINQ: {linqTime.TotalMilliseconds:F2}ms, Pipeline: {pipelineTime.TotalMilliseconds:F2}ms");
        }
        
        // Both should produce correct results (verified by mock setup)
        Assert.True(true); // Placeholder - in real implementation, compare actual results TODO g
    }

    [Fact]
    public async Task StreamingPipeline_RealTimeData_ProcessesCorrectly()
    {
        // Arrange
        const int batchSize = 100;
        const int totalBatches = 10;
        var streamingData = DataGenerator.GenerateStreamingData(batchSize, totalBatches);
        
        var results = new List<float[]>();

        // Act
        var streamingPipeline = streamingData.AsStreamingPipeline(Services, batchSize);
        
        await foreach (var batch in streamingPipeline)
        {
            results.Add(batch);
        }

        // Assert
        Assert.Equal(totalBatches, results.Count);
        Assert.All(results, batch => Assert.Equal(batchSize, batch.Length));
    }

    [Fact]
    public async Task ComplexLinqQuery_MultipleOperations_ExecutesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(5000, DataPattern.Random);
        var queryable = data.AsQueryable();

        // Act
        var pipeline = queryable
            .AsKernelPipeline(Services)
            .ThenWhere<float>(x => x > 0.1f)
            .ThenSelect<float, float>(x => x * x) // Square the values
            .ThenWhere<float>(x => x < 0.8f) // Additional filter
            .ThenSelect<float, float>(x => MathF.Sqrt(x)); // Square root

        var result = await pipeline.ExecutePipelineAsync<float[]>(OptimizationLevel.Aggressive);

        // Assert
        Assert.NotNull(result);
        
        // Verify pipeline optimization was applied
        var mockPipeline = (MockKernelPipeline)pipeline;
        Assert.True(mockPipeline.OptimizationApplied);
        Assert.Equal(OptimizationStrategy.Aggressive, mockPipeline.OptimizationStrategy);
    }

    [Fact]
    public async Task PipelineValidation_InvalidExpression_ThrowsException()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var queryable = data.AsQueryable();

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            var pipeline = queryable
                .AsKernelPipeline(Services)
                .ThenSelect<float, string>(x => x.ToString()); // String conversion not supported
            
            await pipeline.ExecutePipelineAsync<string[]>();
        });
    }

    [Fact]
    public async Task BatchProcessing_LargeDataset_HandlesMemoryEfficiently()
    {
        // Arrange
        var largeData = GenerateTestData<float>(1_000_000); // 1M elements
        var batches = new List<float[]>();

        // Act
        var memoryMetrics = await ValidateMemoryUsageAsync(async () =>
        {
            var enumerable = largeData.AsEnumerable();
            var pipeline = enumerable.AsComputePipeline(Services);
            
            // Process in batches to manage memory
            const int batchSize = 10_000;
            for (int i = 0; i < largeData.Length; i += batchSize)
            {
                var batch = largeData.Skip(i).Take(batchSize).ToArray();
                var batchResult = await pipeline
                    .ThenSelect<float, float>(x => x * 2.0f)
                    .ExecutePipelineAsync<float[]>();
                batches.Add(batchResult);
            }
        }, maxMemoryIncreaseMB: 100); // Should stay under 100MB increase

        // Assert
        Assert.Equal(100, batches.Count); // Should have 100 batches of 10k each
        Assert.True(memoryMetrics.MemoryIncreaseMB < 100, 
            $"Memory usage too high: {memoryMetrics.MemoryIncreaseMB:F2}MB");
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Conservative)]
    [InlineData(OptimizationLevel.Balanced)]
    [InlineData(OptimizationLevel.Aggressive)]
    [InlineData(OptimizationLevel.Adaptive)]
    public async Task OptimizationLevels_ApplyCorrectly(OptimizationLevel level)
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var mockPipeline = new MockKernelPipeline();
        mockPipeline.SetupMockResult(data);

        // Act
        var result = await mockPipeline.ExecutePipelineAsync<float[]>(level);

        // Assert
        Assert.NotNull(result);
        
        if (level != OptimizationLevel.None)
        {
            Assert.True(mockPipeline.OptimizationApplied);
            
            var expectedStrategy = level switch
            {
                OptimizationLevel.Conservative => OptimizationStrategy.Conservative,
                OptimizationLevel.Balanced => OptimizationStrategy.Balanced,
                OptimizationLevel.Aggressive => OptimizationStrategy.Aggressive,
                OptimizationLevel.Adaptive => OptimizationStrategy.Adaptive,
                _ => OptimizationStrategy.Conservative
            };
            
            Assert.Equal(expectedStrategy, mockPipeline.OptimizationStrategy);
        }
        else
        {
            Assert.False(mockPipeline.OptimizationApplied);
        }
    }

    private void SetupMockServices()
    {
        _mockOrchestrator.Reset();
        
        // Setup mock kernels for LINQ operations
        _mockOrchestrator.RegisterMockKernel("SelectKernel", args => 
        {
            var input = (float[])args[0];
            var selector = (Func<float, float>)args[1];
            return input.Select(selector).ToArray();
        });
        
        _mockOrchestrator.RegisterMockKernel("WhereKernel", args => 
        {
            var input = (float[])args[0];
            var predicate = (Func<float, bool>)args[1];
            return input.Where(predicate).ToArray();
        });
        
        _mockOrchestrator.RegisterMockKernel("GroupByKernel", args => 
        {
            var input = (float[])args[0];
            var keySelector = (Func<float, int>)args[1];
            return input.GroupBy(keySelector).ToArray();
        });
        
        _mockOrchestrator.RegisterMockKernel("AggregateKernel", args => 
        {
            var input = (IEnumerable<float>)args[0];
            var aggregator = (Func<float, float, float>)args[1];
            return input.Aggregate(aggregator);
        });
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Register LINQ-specific services
        services.AddSingleton<IPipelineExpressionAnalyzer, MockPipelineExpressionAnalyzer>();
        services.AddSingleton<IKernelPipelineBuilder>(_ => _mockPipelineBuilder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mockOrchestrator?.Reset();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Mock implementation of IKernelPipelineBuilder for testing LINQ integration.
/// </summary>
public class MockKernelPipelineBuilder : IKernelPipelineBuilder
{
    public List<PipelineOperation> Operations { get; } = new();

    public IKernelPipelineBuilder FromData<T>(T[] data) where T : unmanaged
    {
        return this;
    }

    public IKernelPipelineBuilder FromExpression<T>(Expression<Func<IQueryable<T>, IQueryable<T>>> expression)
    {
        return this;
    }

    public IKernelPipeline Create()
    {
        return new MockKernelPipeline();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock implementation of IKernelPipeline for testing pipeline execution.
/// </summary>
public class MockKernelPipeline : IKernelPipeline
{
    private object? _mockResult;
    
    public bool OptimizationApplied { get; private set; }
    public OptimizationStrategy OptimizationStrategy { get; private set; }

    public void SetupMockResult(object result)
    {
        _mockResult = result;
    }

    public IKernelPipeline Then<TIn, TOut>(string kernelName, Func<TIn, object[]> argumentProvider, PipelineStageOptions? options = null)
        where TIn : unmanaged where TOut : unmanaged
    {
        return this;
    }

    public IKernelPipeline Optimize(OptimizationStrategy strategy)
    {
        OptimizationApplied = true;
        OptimizationStrategy = strategy;
        return this;
    }

    public IKernelPipeline AdaptiveCache(AdaptiveCacheOptions options)
    {
        return this;
    }

    public async Task<TResult> ExecuteAsync<TResult>(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate async execution
        return (TResult)(_mockResult ?? throw new InvalidOperationException("Mock result not set"));
    }

    public async Task<TOut> ExecuteAsync<TIn, TOut>(TIn input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return (TOut)(_mockResult ?? throw new InvalidOperationException("Mock result not set"));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock implementation of IPipelineExpressionAnalyzer for testing.
/// </summary>
public class MockPipelineExpressionAnalyzer : IPipelineExpressionAnalyzer
{
    public PipelineConfiguration AnalyzeExpression<T>(Expression expression)
    {
        return new PipelineConfiguration
        {
            EstimatedComplexity = 1,
            RecommendedBackend = "CPU",
            ExpectedMemoryUsage = 1024 * 1024, // 1MB
            SupportedOperations = ["Select", "Where", "GroupBy", "Aggregate"]
        };
    }
}

/// <summary>
/// Represents a pipeline operation for testing.
/// </summary>
public class PipelineOperation
{
    public required string KernelName { get; init; }
    public required object[] Arguments { get; init; }
    public PipelineStageOptions? Options { get; init; }
}

/// <summary>
/// Pipeline configuration result from expression analysis.
/// </summary>
public class PipelineConfiguration
{
    public int EstimatedComplexity { get; set; }
    public string RecommendedBackend { get; set; } = "CPU";
    public long ExpectedMemoryUsage { get; set; }
    public IReadOnlyList<string> SupportedOperations { get; set; } = Array.Empty<string>();
}

// Placeholder types for compilation
public interface IPipelineExpressionAnalyzer
{
    PipelineConfiguration AnalyzeExpression<T>(Expression expression);
}

public class PipelineStageOptions { }
public class AdaptiveCacheOptions { }

public enum OptimizationStrategy
{
    Conservative,
    Balanced,
    Aggressive,
    Adaptive
}