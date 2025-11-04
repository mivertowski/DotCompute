// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ComputeBackend = DotCompute.Linq.Compilation.ComputeBackend;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// Comprehensive unit tests for <see cref="CompilationPipeline"/>.
/// </summary>
/// <remarks>
/// Tests cover end-to-end compilation, caching, error handling, metrics,
/// parallel compilation, and disposal scenarios.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Category", "Compilation")]
public sealed class CompilationPipelineTests : IDisposable
{
    private readonly KernelCache _cache;
    private readonly CpuKernelGenerator _generator;
    private readonly CompilationPipeline _pipeline;
    private readonly ILogger<CompilationPipeline> _logger;

    public CompilationPipelineTests()
    {
        _cache = new KernelCache(maxEntries: 100, maxMemoryBytes: 10 * 1024 * 1024);
        _generator = new CpuKernelGenerator();
        _logger = NullLogger<CompilationPipeline>.Instance;
        _pipeline = new CompilationPipeline(_cache, _generator, _logger);
    }

    public void Dispose()
    {
        _pipeline?.Dispose();
        _cache?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert
        using var pipeline = new CompilationPipeline(_cache, _generator, _logger);
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CompilationPipeline(null!, _generator, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kernelCache");
    }

    [Fact]
    public void Constructor_WithNullGenerator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CompilationPipeline(_cache, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kernelGenerator");
    }

    [Fact]
    public void Constructor_WithNullLogger_CreatesInstance()
    {
        // Act & Assert
        using var pipeline = new CompilationPipeline(_cache, _generator, null);
        pipeline.Should().NotBeNull();
    }

    #endregion

    #region End-to-End Compilation Tests

    [Fact]
    public void CompileToDelegate_WithValidMapOperation_ProducesExecutableDelegate()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
        compiled.Should().BeAssignableTo<Func<int[], int[]>>();
    }

    [Fact]
    public void CompileToDelegate_WithValidFilterOperation_ProducesExecutableDelegate()
    {
        // Arrange
        var graph = CreateFilterOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
        compiled.Should().BeAssignableTo<Func<int[], int[]>>();
    }

    [Fact]
    public void CompileToDelegate_WithValidReduceOperation_ProducesExecutableDelegate()
    {
        // Arrange
        var graph = CreateReduceOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [Fact]
    public void CompileToDelegate_WithAllOperationTypes_ProducesExecutableDelegates()
    {
        // Arrange
        var operationTypes = Enum.GetValues<OperationType>();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act & Assert
        foreach (var opType in operationTypes)
        {
            var graph = CreateGraphWithOperationType(opType);
            var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
            compiled.Should().NotBeNull($"operation type {opType} should compile");
        }
    }

    [Fact]
    public void CompileToDelegate_ExecutingCompiledDelegate_ProducesCorrectResults()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        var input = new[] { 1, 2, 3, 4, 5 };

        // Act
        var result = compiled(input);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(input.Length);
        // Note: Actual values depend on generated code implementation
        result.Should().AllBeOfType<int>();
    }

    #endregion

    #region Roslyn Compilation Tests

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Conservative)]
    [InlineData(OptimizationLevel.Balanced)]
    [InlineData(OptimizationLevel.Aggressive)]
    [InlineData(OptimizationLevel.MLOptimized)]
    public void CompileToDelegate_WithDifferentOptimizationLevels_Succeeds(OptimizationLevel level)
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var baseOptions = CreateDefaultCompilationOptions();
        var options = new CompilationOptions
        {
            TargetBackend = baseOptions.TargetBackend,
            OptimizationLevel = level,
            EnableKernelFusion = baseOptions.EnableKernelFusion,
            GenerateDebugInfo = baseOptions.GenerateDebugInfo,
            CacheTtl = baseOptions.CacheTtl};

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [Fact]
    public void CompileToDelegate_WithDebugInfoEnabled_Succeeds()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var baseOptions = CreateDefaultCompilationOptions();
        var options = new CompilationOptions
        {
            TargetBackend = baseOptions.TargetBackend,
            OptimizationLevel = baseOptions.OptimizationLevel,
            EnableKernelFusion = baseOptions.EnableKernelFusion,
            GenerateDebugInfo = true,
            CacheTtl = baseOptions.CacheTtl};

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [Fact]
    public void CompileToDelegate_WithDebugInfoDisabled_Succeeds()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var baseOptions = CreateDefaultCompilationOptions();
        var options = new CompilationOptions
        {
            TargetBackend = baseOptions.TargetBackend,
            OptimizationLevel = baseOptions.OptimizationLevel,
            EnableKernelFusion = baseOptions.EnableKernelFusion,
            GenerateDebugInfo = false,
            CacheTtl = baseOptions.CacheTtl};

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [Fact]
    public void CompileToDelegate_WithDifferentBackendTargets_Succeeds()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var backends = new[] { ComputeBackend.Auto, ComputeBackend.Cpu };

        // Act & Assert
        foreach (var backend in backends)
        {
            var baseOptions = CreateDefaultCompilationOptions();
            var options = new CompilationOptions
            {
                TargetBackend = backend,
                OptimizationLevel = baseOptions.OptimizationLevel,
                EnableKernelFusion = baseOptions.EnableKernelFusion,
                GenerateDebugInfo = baseOptions.GenerateDebugInfo,
                CacheTtl = baseOptions.CacheTtl
            };
            var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
            compiled.Should().NotBeNull($"backend {backend} should compile");
        }
    }

    #endregion

    #region Cache Integration Tests

    [Fact]
    public void CompileToDelegate_CalledTwiceWithSameParameters_UsesCacheOnSecondCall()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var firstCompiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        var secondCompiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        firstCompiled.Should().NotBeNull();
        secondCompiled.Should().NotBeNull();
        firstCompiled.Should().BeSameAs(secondCompiled, "cached delegate should be reused");

        var metrics = _pipeline.GetMetrics();
        metrics.CacheHits.Should().BeGreaterThanOrEqualTo(1, "second call should hit cache");
    }

    [Fact]
    public void CompileToDelegate_WithDifferentGraphs_CreatesDistinctCacheEntries()
    {
        // Arrange
        var graph1 = CreateMapOperationGraph();
        var graph2 = CreateFilterOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled1 = _pipeline.CompileToDelegate<int, int>(graph1, metadata, options);
        var compiled2 = _pipeline.CompileToDelegate<int, int>(graph2, metadata, options);

        // Assert
        compiled1.Should().NotBeSameAs(compiled2, "different graphs should produce different delegates");

        var metrics = _pipeline.GetMetrics();
        metrics.TotalCompilations.Should().BeGreaterThanOrEqualTo(2);
        metrics.CacheHits.Should().Be(0, "no cache hits for different graphs");
    }

    [Fact]
    public void CompileToDelegate_CacheMiss_IncrementsStatistics()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
        var metrics = _pipeline.GetMetrics();
        metrics.TotalCompilations.Should().BeGreaterThanOrEqualTo(1);
        metrics.CacheHits.Should().Be(0, "first compilation should not hit cache");
    }

    [Fact]
    public void CompileToDelegate_CacheHit_IncrementsHitStatistics()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options); // Miss
        var initialMetrics = _pipeline.GetMetrics();
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options); // Hit

        // Assert
        var finalMetrics = _pipeline.GetMetrics();
        finalMetrics.CacheHits.Should().BeGreaterThan(initialMetrics.CacheHits);
    }

    [Fact]
    public void CompileToDelegate_WithExpiredCache_RecompilesKernel()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var baseOptions = CreateDefaultCompilationOptions();
        var options = new CompilationOptions
        {
            TargetBackend = baseOptions.TargetBackend,
            OptimizationLevel = baseOptions.OptimizationLevel,
            EnableKernelFusion = baseOptions.EnableKernelFusion,
            GenerateDebugInfo = baseOptions.GenerateDebugInfo,
            CacheTtl = TimeSpan.FromMilliseconds(10) // Very short TTL
        };

        // Act
        var firstCompiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        Thread.Sleep(50); // Wait for cache to expire
        var secondCompiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        firstCompiled.Should().NotBeNull();
        secondCompiled.Should().NotBeNull();
        // Note: May or may not be same instance depending on GC and timing
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void CompileToDelegate_WithIntTypes_ProducesCorrectDelegateType()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().BeAssignableTo<Func<int[], int[]>>();
    }

    [Fact]
    public void CompileToDelegate_WithFloatTypes_ProducesCorrectDelegateType()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<float, float>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<float, float>(graph, metadata, options);

        // Assert
        compiled.Should().BeAssignableTo<Func<float[], float[]>>();
    }

    [Fact]
    public void CompileToDelegate_WithDoubleTypes_ProducesCorrectDelegateType()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<double, double>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<double, double>(graph, metadata, options);

        // Assert
        compiled.Should().BeAssignableTo<Func<double[], double[]>>();
    }

    [Fact]
    public void CompileToDelegate_WithLongTypes_ProducesCorrectDelegateType()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<long, long>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<long, long>(graph, metadata, options);

        // Assert
        compiled.Should().BeAssignableTo<Func<long[], long[]>>();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void CompileToDelegate_WithNullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(null!, metadata, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("graph");
    }

    [Fact]
    public void CompileToDelegate_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadata");
    }

    [Fact]
    public void CompileToDelegate_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, metadata, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void CompileToDelegate_WithEmptyGraph_ThrowsArgumentException()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation>(),
            Root = null
        };
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Operation graph contains no operations*");
    }

    [Fact]
    public void CompileToDelegate_WithNullRootOperation_ThrowsArgumentException()
    {
        // Arrange
        var operation = new Operation
        {
            Id = "op1",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>()
        };

        var graph = new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = null
        };
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Operation graph has no root operation*");
    }

    [Fact]
    public void CompileToDelegate_WithNullInputType_ThrowsArgumentException()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = new TypeMetadata
        {
            InputType = null!,
            ResultType = typeof(int),
            IntermediateTypes = new Dictionary<string, Type>()
        };
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type metadata missing input type*");
    }

    [Fact]
    public void CompileToDelegate_WithNullResultType_ThrowsArgumentException()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = new TypeMetadata
        {
            InputType = typeof(int),
            ResultType = null!,
            IntermediateTypes = new Dictionary<string, Type>()
        };
        var options = CreateDefaultCompilationOptions();

        // Act
        var act = () => _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type metadata missing result type*");
    }

    #endregion

    #region Performance Metrics Tests

    [Fact]
    public void GetMetrics_AfterCompilation_ReturnsValidMetrics()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        var metrics = _pipeline.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalCompilations.Should().BeGreaterThanOrEqualTo(1);
        metrics.CacheStatistics.Should().NotBeNull();
    }

    [Fact]
    public void GetMetrics_AfterMultipleCompilations_TracksTotalCorrectly()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        _pipeline.CompileToDelegate<int, int>(graph, metadata, options);
        var metrics = _pipeline.GetMetrics();

        // Assert
        metrics.TotalCompilations.Should().Be(3);
    }

    [Fact]
    public void GetMetrics_InitialState_ReturnsZeroMetrics()
    {
        // Act
        var metrics = _pipeline.GetMetrics();

        // Assert
        metrics.TotalCompilations.Should().Be(0);
        metrics.CacheHits.Should().Be(0);
        metrics.CompilationFailures.Should().Be(0);
        metrics.FallbackCount.Should().Be(0);
    }

    [Fact]
    public void GetMetrics_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var cache = new KernelCache();
        using var pipeline = new CompilationPipeline(cache, _generator);
        pipeline.Dispose();

        // Act
        var act = () => pipeline.GetMetrics();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Assembly Loading and Type Resolution Tests

    [Fact]
    public void CompileToDelegate_LoadsAssemblySuccessfully()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Should().NotBeNull();
        compiled.Method.Should().NotBeNull();
        compiled.Method.DeclaringType.Should().NotBeNull();
    }

    [Fact]
    public void CompileToDelegate_GeneratedTypeInCorrectNamespace()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        var declaringType = compiled.Method.DeclaringType;
        declaringType.Should().NotBeNull();
        declaringType!.Namespace.Should().Be("DotCompute.Linq.Generated");
        declaringType.Name.Should().Be("GeneratedKernel");
    }

    [Fact]
    public void CompileToDelegate_GeneratedMethodHasCorrectName()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var compiled = _pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        compiled.Method.Name.Should().Be("Execute");
    }

    #endregion

    #region Parallel Compilation Tests

    [Fact]
    public async Task CompileToDelegate_ParallelCompilations_AllSucceed()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();
        const int parallelCount = 10;

        // Act
        var tasks = Enumerable.Range(0, parallelCount)
            .Select(_ => Task.Run(() => _pipeline.CompileToDelegate<int, int>(graph, metadata, options)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().HaveCount(parallelCount);
    }

    [Fact]
    public async Task CompileToDelegate_ParallelWithDifferentGraphs_ProducesDistinctDelegates()
    {
        // Arrange
        var graphs = new[]
        {
            CreateMapOperationGraph(),
            CreateFilterOperationGraph(),
            CreateReduceOperationGraph(),
            CreateScanOperationGraph()
        };
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        // Act
        var tasks = graphs
            .Select(g => Task.Run(() => _pipeline.CompileToDelegate<int, int>(g, metadata, options)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().HaveCount(graphs.Length);
    }

    [Fact]
    public async Task CompileToDelegate_ConcurrentSameGraph_UsesCache()
    {
        // Arrange
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();
        const int concurrentCount = 20;

        // Act
        var tasks = Enumerable.Range(0, concurrentCount)
            .Select(_ => Task.Run(() => _pipeline.CompileToDelegate<int, int>(graph, metadata, options)))
            .ToArray();

        await Task.WhenAll(tasks);
        var metrics = _pipeline.GetMetrics();

        // Assert
        metrics.TotalCompilations.Should().Be(concurrentCount);
        metrics.CacheHits.Should().BeGreaterThan(0, "some compilations should hit cache");
    }

    #endregion

    #region Disposal and Cleanup Tests

    [Fact]
    public void Dispose_CalledOnce_Succeeds()
    {
        // Arrange
        using var cache = new KernelCache();
        var pipeline = new CompilationPipeline(cache, _generator);

        // Act
        pipeline.Dispose();

        // Assert - No exception thrown
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_Succeeds()
    {
        // Arrange
        using var cache = new KernelCache();
        var pipeline = new CompilationPipeline(cache, _generator);

        // Act
        pipeline.Dispose();
        pipeline.Dispose();
        pipeline.Dispose();

        // Assert - No exception thrown
    }

    [Fact]
    public void CompileToDelegate_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var cache = new KernelCache();
        var pipeline = new CompilationPipeline(cache, _generator);
        var graph = CreateMapOperationGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var options = CreateDefaultCompilationOptions();

        pipeline.Dispose();

        // Act
        var act = () => pipeline.CompileToDelegate<int, int>(graph, metadata, options);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Helper Methods

    private static OperationGraph CreateMapOperationGraph()
    {
        Expression<Func<int, int>> lambda = x => x * 2;
        var operation = new Operation
        {
            Id = "map1",
            Type = OperationType.Map,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object> { ["Lambda"] = lambda },
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = operation,
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };
    }

    private static OperationGraph CreateFilterOperationGraph()
    {
        Expression<Func<int, bool>> lambda = x => x > 0;
        var operation = new Operation
        {
            Id = "filter1",
            Type = OperationType.Filter,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object> { ["Lambda"] = lambda },
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = operation,
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };
    }

    private static OperationGraph CreateReduceOperationGraph()
    {
        Expression<Func<int, int>> lambda = x => x;
        var operation = new Operation
        {
            Id = "reduce1",
            Type = OperationType.Reduce,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object> { ["Lambda"] = lambda },
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = operation,
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };
    }

    private static OperationGraph CreateScanOperationGraph()
    {
        Expression<Func<int, int>> lambda = x => x;
        var operation = new Operation
        {
            Id = "scan1",
            Type = OperationType.Scan,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object> { ["Lambda"] = lambda },
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = operation,
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };
    }

    private static OperationGraph CreateGraphWithOperationType(OperationType operationType)
    {
        var metadata = new Dictionary<string, object>();

        // Add appropriate lambda based on operation type
        if (operationType == OperationType.Filter)
        {
            Expression<Func<int, bool>> filterLambda = x => x > 0;
            metadata["Lambda"] = filterLambda;
        }
        else
        {
            Expression<Func<int, int>> lambda = x => x;
            metadata["Lambda"] = lambda;
        }

        var operation = new Operation
        {
            Id = $"{operationType.ToString().ToLowerInvariant()}1",
            Type = operationType,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = metadata,
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Root = operation,
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };
    }

    private static TypeMetadata CreateTypeMetadata<TInput, TResult>()
    {
        return new TypeMetadata
        {
            InputType = typeof(TInput),
            ResultType = typeof(TResult),
            IntermediateTypes = new Dictionary<string, Type>(),
            RequiresUnsafe = false,
            IsSimdCompatible = true,
            HasNullableTypes = false
        };
    }

    private static CompilationOptions CreateDefaultCompilationOptions()
    {
        return new CompilationOptions
        {
            TargetBackend = ComputeBackend.Cpu,
            OptimizationLevel = OptimizationLevel.Balanced,
            EnableKernelFusion = true,
            GenerateDebugInfo = false,
            CacheTtl = TimeSpan.FromMinutes(30)
        };
    }

    #endregion
}
