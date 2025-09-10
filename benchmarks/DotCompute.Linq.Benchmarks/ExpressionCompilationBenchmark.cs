// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Optimization;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for expression compilation performance in DotCompute LINQ system.
/// Tests compilation time, caching effectiveness, memory allocations, and complexity impact.
/// </summary>
[Config(typeof(ExpressionCompilationConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[EtwProfiler]
public class ExpressionCompilationBenchmark
{
    private IComputeLinqProvider _provider = null!;
    private IComputeLinqProvider _cachedProvider = null!;
    private IComputeLinqProvider _nonCachedProvider = null!;
    private LinqToGpuKernelCompiler _compiler = null!;
    private LinqToGpuKernelCompiler _cachedCompiler = null!;
    private LinqToGpuKernelCompiler _nonCachedCompiler = null!;
    private IMemoryCache _cache = null!;
    private ILogger<LinqToGpuKernelCompiler> _logger = null!;
    
    // Test expressions of varying complexity
    private Expression<Func<int[], int[]>> _simpleExpression = null!;
    private Expression<Func<float[], float[]>> _mediumExpression = null!;
    private Expression<Func<double[], double[]>> _complexExpression = null!;
    private Expression<Func<int[], int>> _aggregateExpression = null!;
    private Expression<Func<float[], bool[]>> _conditionalExpression = null!;
    
    // Test data
    private int[] _intData = null!;
    private float[] _floatData = null!;
    private double[] _doubleData = null!;
    
    [Params(1000, 10000, 100000)] 
    public int DataSize { get; set; }
    
    [Params(ExpressionComplexity.Simple, ExpressionComplexity.Medium, ExpressionComplexity.Complex)]
    public ExpressionComplexity Complexity { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup logging
        _logger = NullLogger<LinqToGpuKernelCompiler>.Instance;
        
        // Setup memory cache
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000,
            CompactionPercentage = 0.2
        });
        
        // Setup service collection for dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(_cache);
        services.AddDotComputeRuntime();
        services.AddDotComputeLinq();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Setup providers with and without caching
        _provider = serviceProvider.GetRequiredService<IComputeLinqProvider>();
        
        // Setup compilers
        _cachedCompiler = new LinqToGpuKernelCompiler(_cache, _logger);
        _nonCachedCompiler = new LinqToGpuKernelCompiler(null, _logger);
        _compiler = _cachedCompiler;
        
        SetupTestData();
        SetupTestExpressions();
        
        // Warm up caches
        WarmupCaches();
    }
    
    private void SetupTestData()
    {
        var random = new Random(42); // Deterministic seed
        
        _intData = Enumerable.Range(0, DataSize)
            .Select(_ => random.Next(1, 1000))
            .ToArray();
            
        _floatData = Enumerable.Range(0, DataSize)
            .Select(_ => (float)random.NextDouble() * 1000f)
            .ToArray();
            
        _doubleData = Enumerable.Range(0, DataSize)
            .Select(_ => random.NextDouble() * 1000.0)
            .ToArray();
    }
    
    private void SetupTestExpressions()
    {
        // Simple expression: x => x * 2
        _simpleExpression = x => x.Select(i => i * 2).ToArray();
        
        // Medium expression: x => x.Where(i => i > 100).Select(i => i * 3.14f + 1).ToArray()
        _mediumExpression = x => x.Where(f => f > 100f).Select(f => f * 3.14f + 1f).ToArray();
        
        // Complex expression: Multiple operations with math functions
        _complexExpression = x => x
            .Where(d => d > 50.0)
            .Select(d => Math.Sqrt(d * d + 100.0))
            .Where(d => d < 500.0)
            .Select(d => Math.Sin(d) * Math.Cos(d))
            .ToArray();
            
        // Aggregate expression: Sum with filtering
        _aggregateExpression = x => x.Where(i => i > 50).Sum();
        
        // Conditional expression: Complex boolean logic
        _conditionalExpression = x => x.Select(f => f > 100f && f < 900f && (f % 2 == 0)).ToArray();
    }
    
    private void WarmupCaches()
    {
        // Warm up compilation caches with all expression types
        try
        {
            _cachedCompiler.CompileExpression(_simpleExpression);
            _cachedCompiler.CompileExpression(_mediumExpression);
            _cachedCompiler.CompileExpression(_complexExpression);
            _cachedCompiler.CompileExpression(_aggregateExpression);
            _cachedCompiler.CompileExpression(_conditionalExpression);
        }
        catch (Exception)
        {
            // Ignore warmup failures for benchmarking
        }
    }

    #region Compilation Time Benchmarks
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compilation", "Cached")]
    public object CachedCompilation()
    {
        return GetExpressionByComplexity() switch
        {
            var expr when expr == _simpleExpression => _cachedCompiler.CompileExpression(_simpleExpression),
            var expr when expr == _mediumExpression => _cachedCompiler.CompileExpression(_mediumExpression),
            var expr when expr == _complexExpression => _cachedCompiler.CompileExpression(_complexExpression),
            var expr when expr == _aggregateExpression => _cachedCompiler.CompileExpression(_aggregateExpression),
            _ => _cachedCompiler.CompileExpression(_conditionalExpression)
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("Compilation", "NonCached")]
    public object NonCachedCompilation()
    {
        return GetExpressionByComplexity() switch
        {
            var expr when expr == _simpleExpression => _nonCachedCompiler.CompileExpression(_simpleExpression),
            var expr when expr == _mediumExpression => _nonCachedCompiler.CompileExpression(_mediumExpression),
            var expr when expr == _complexExpression => _nonCachedCompiler.CompileExpression(_complexExpression),
            var expr when expr == _aggregateExpression => _nonCachedCompiler.CompileExpression(_aggregateExpression),
            _ => _nonCachedCompiler.CompileExpression(_conditionalExpression)
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("Compilation", "FirstTime")]
    public object FirstTimeCompilation()
    {
        // Create fresh compiler for each iteration to simulate first-time compilation
        var freshCompiler = new LinqToGpuKernelCompiler(_cache, _logger);
        
        return GetExpressionByComplexity() switch
        {
            var expr when expr == _simpleExpression => freshCompiler.CompileExpression(_simpleExpression),
            var expr when expr == _mediumExpression => freshCompiler.CompileExpression(_mediumExpression), 
            var expr when expr == _complexExpression => freshCompiler.CompileExpression(_complexExpression),
            var expr when expr == _aggregateExpression => freshCompiler.CompileExpression(_aggregateExpression),
            _ => freshCompiler.CompileExpression(_conditionalExpression)
        };
    }
    
    #endregion
    
    #region Memory Allocation Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Memory", "Allocation")]
    public void CompilationMemoryPressure()
    {
        // Force garbage collection before measurement
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        
        var compiler = new LinqToGpuKernelCompiler(null, _logger);
        
        // Compile multiple expressions to measure total allocation
        for (int i = 0; i < 10; i++)
        {
            switch (Complexity)
            {
                case ExpressionComplexity.Simple:
                    compiler.CompileExpression(_simpleExpression);
                    break;
                case ExpressionComplexity.Medium:
                    compiler.CompileExpression(_mediumExpression);
                    break;
                case ExpressionComplexity.Complex:
                    compiler.CompileExpression(_complexExpression);
                    break;
            }
        }
    }
    
    [Benchmark]
    [BenchmarkCategory("Memory", "CacheEfficiency")]
    public object CacheHitRatio()
    {
        // Measure cache effectiveness by repeatedly compiling same expression
        object result = null!;
        
        for (int i = 0; i < 100; i++)
        {
            result = _cachedCompiler.CompileExpression(_simpleExpression);
        }
        
        return result;
    }
    
    #endregion
    
    #region Expression Complexity Impact
    
    [Benchmark]
    [BenchmarkCategory("Complexity", "Simple")]
    public object SimpleExpressionCompilation()
    {
        return _compiler.CompileExpression(_simpleExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Complexity", "Medium")]
    public object MediumExpressionCompilation()
    {
        return _compiler.CompileExpression(_mediumExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Complexity", "Complex")]
    public object ComplexExpressionCompilation()
    {
        return _compiler.CompileExpression(_complexExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Complexity", "Aggregate")]
    public object AggregateExpressionCompilation()
    {
        return _compiler.CompileExpression(_aggregateExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Complexity", "Conditional")]
    public object ConditionalExpressionCompilation()
    {
        return _compiler.CompileExpression(_conditionalExpression);
    }
    
    #endregion
    
    #region Compilation Pipeline Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Pipeline", "FullStack")]
    public async Task FullCompilationPipeline()
    {
        var expressions = new[]
        {
            (Expression)_simpleExpression,
            _mediumExpression,
            _complexExpression,
            _aggregateExpression,
            _conditionalExpression
        };
        
        // Measure full compilation pipeline including precompilation
        await _provider.PrecompileExpressionsAsync(expressions);
    }
    
    [Benchmark]
    [BenchmarkCategory("Pipeline", "Parallel")]
    public void ParallelCompilation()
    {
        var expressions = new[]
        {
            (Expression)_simpleExpression,
            _mediumExpression,
            _complexExpression,
            _aggregateExpression,
            _conditionalExpression
        };
        
        // Test parallel compilation performance
        Parallel.ForEach(expressions, expr =>
        {
            var compiler = new LinqToGpuKernelCompiler(_cache, _logger);
            compiler.CompileExpression(expr);
        });
    }
    
    #endregion
    
    #region Cache Benchmark Scenarios
    
    [Benchmark]
    [BenchmarkCategory("Cache", "WarmCache")]
    public object WarmCacheScenario()
    {
        // Simulate warm cache scenario - expression already compiled
        return _cachedCompiler.CompileExpression(_simpleExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Cache", "ColdCache")]
    public object ColdCacheScenario()
    {
        // Simulate cold cache scenario - clear cache and compile
        _cache.Remove(_simpleExpression.ToString());
        return _cachedCompiler.CompileExpression(_simpleExpression);
    }
    
    [Benchmark]
    [BenchmarkCategory("Cache", "MixedAccess")]
    public object MixedCacheAccessPattern()
    {
        // Simulate realistic mixed access pattern
        var expressions = new[] { _simpleExpression, _mediumExpression, _complexExpression };
        var random = new Random(42);
        
        object result = null!;
        for (int i = 0; i < 10; i++)
        {
            var expr = expressions[random.Next(expressions.Length)];
            result = _cachedCompiler.CompileExpression(expr);
        }
        
        return result;
    }
    
    #endregion
    
    private Expression GetExpressionByComplexity()
    {
        return Complexity switch
        {
            ExpressionComplexity.Simple => _simpleExpression,
            ExpressionComplexity.Medium => _mediumExpression,
            ExpressionComplexity.Complex => _complexExpression,
            _ => _simpleExpression
        };
    }
    
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cache?.Dispose();
    }
}

/// <summary>
/// Expression complexity levels for benchmarking.
/// </summary>
public enum ExpressionComplexity
{
    Simple,
    Medium,
    Complex
}

/// <summary>
/// Custom benchmark configuration for expression compilation tests.
/// </summary>
public class ExpressionCompilationConfig : ManualConfig
{
    public ExpressionCompilationConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithJit(Jit.RyuJit)
            .WithPlatform(Platform.X64)
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithInvocationCount(100)
            .WithStrategy(RunStrategy.Throughput));
            
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        
        // Add custom columns for cache hit ratios and compilation metrics
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Min);
        AddColumn(StatisticColumn.Max);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);
        
        AddOrderer(DefaultOrderer.Instance);
        
        // Group by categories
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}