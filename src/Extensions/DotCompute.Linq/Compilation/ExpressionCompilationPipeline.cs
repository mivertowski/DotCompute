using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Compilation.Stages;
using DotCompute.Linq.Expressions;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Main orchestrator for expression-to-kernel compilation pipeline.
/// Coordinates analysis, code generation, optimization, and caching.
/// </summary>
public sealed class ExpressionCompilationPipeline : IDisposable
{
    private readonly ILogger<ExpressionCompilationPipeline> _logger;
    private readonly ExpressionAnalyzer _analyzer;
    private readonly KernelCodeGenerator _codeGenerator;
    private readonly KernelOptimizer _optimizer;
    private readonly ConcurrentDictionary<string, CachedKernel> _kernelCache;
    private readonly SemaphoreSlim _compilationSemaphore;
    private readonly CompilationMetrics _metrics;
    private bool _disposed;

    public ExpressionCompilationPipeline(
        ILogger<ExpressionCompilationPipeline> logger,
        ExpressionAnalyzer analyzer,
        KernelCodeGenerator codeGenerator,
        KernelOptimizer optimizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        
        _kernelCache = new ConcurrentDictionary<string, CachedKernel>();
        _compilationSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        _metrics = new CompilationMetrics();
    }

    /// <summary>
    /// Compiles an expression tree into executable kernels for specified backends.
    /// </summary>
    /// <param name="expression">The LINQ expression tree to compile</param>
    /// <param name="targetBackends">Target compute backends (CPU, CUDA, etc.)</param>
    /// <param name="options">Compilation options and hints</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compiled kernel package with all target implementations</returns>
    public async Task<CompiledKernelPackage> CompileAsync(
        Expression expression,
        IEnumerable<BackendType> targetBackends,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = CompilationActivity.Start(nameof(CompileAsync));
        
        try
        {
            var cacheKey = GenerateCacheKey(expression, targetBackends, options);
            
            // Check cache first
            if (_kernelCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                _logger.LogDebug("Using cached kernel for expression: {CacheKey}", cacheKey);
                _metrics.RecordCacheHit();
                return cached.Package;
            }

            await _compilationSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check cache after acquiring semaphore
                if (_kernelCache.TryGetValue(cacheKey, out cached) && !cached.IsExpired)
                {
                    _metrics.RecordCacheHit();
                    return cached.Package;
                }

                _logger.LogInformation("Starting compilation for expression: {ExpressionType}", expression.Type);
                var startTime = DateTimeOffset.UtcNow;

                // Stage 1: Expression Analysis
                var analysisResult = await AnalyzeExpressionAsync(expression, options, cancellationToken);
                
                // Stage 2: Code Generation
                var generatedKernels = await GenerateKernelCodeAsync(
                    analysisResult, targetBackends, options, cancellationToken);
                
                // Stage 3: Optimization
                var optimizedKernels = await OptimizeKernelsAsync(
                    generatedKernels, analysisResult, options, cancellationToken);
                
                // Stage 4: Package Creation
                var package = CreateKernelPackage(optimizedKernels, analysisResult);
                
                // Cache the result
                var compilationTime = DateTimeOffset.UtcNow - startTime;
                var cachedKernel = new CachedKernel(package, DateTimeOffset.UtcNow.AddHours(1));
                _kernelCache.TryAdd(cacheKey, cachedKernel);
                
                _metrics.RecordCompilation(compilationTime);
                _logger.LogInformation("Compilation completed in {Duration}ms", compilationTime.TotalMilliseconds);
                
                return package;
            }
            finally
            {
                _compilationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compilation failed for expression: {ExpressionType}", expression.Type);
            _metrics.RecordError();
            throw new CompilationException("Failed to compile expression tree", ex);
        }
    }

    /// <summary>
    /// Compiles a batch of expressions for improved efficiency.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, CompiledKernelPackage>> CompileBatchAsync(
        IEnumerable<(string Key, Expression Expression)> expressions,
        IEnumerable<BackendType> targetBackends,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = expressions.Select(async expr =>
        {
            var package = await CompileAsync(expr.Expression, targetBackends, options, cancellationToken);
            return new KeyValuePair<string, CompiledKernelPackage>(expr.Key, package);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private async Task<ExpressionAnalysisResult> AnalyzeExpressionAsync(
        Expression expression,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        using var activity = CompilationActivity.Start(nameof(AnalyzeExpressionAsync));
        
        _logger.LogDebug("Analyzing expression tree structure");
        
        var result = await _analyzer.AnalyzeAsync(expression, options, cancellationToken);
        
        _logger.LogDebug("Expression analysis completed: {OperatorCount} operators, {Complexity} complexity",
            result.OperatorChain.Count, result.ComplexityMetrics.OverallComplexity);
            
        return result;
    }

    private async Task<IReadOnlyDictionary<BackendType, GeneratedKernel>> GenerateKernelCodeAsync(
        ExpressionAnalysisResult analysisResult,
        IEnumerable<BackendType> targetBackends,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        using var activity = CompilationActivity.Start(nameof(GenerateKernelCodeAsync));
        
        _logger.LogDebug("Generating kernel code for backends: {Backends}", 
            string.Join(", ", targetBackends));

        var tasks = targetBackends.Select(async backend =>
        {
            var kernel = await _codeGenerator.GenerateAsync(
                analysisResult, backend, options, cancellationToken);
            return new KeyValuePair<BackendType, GeneratedKernel>(backend, kernel);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private async Task<IReadOnlyDictionary<BackendType, OptimizedKernel>> OptimizeKernelsAsync(
        IReadOnlyDictionary<BackendType, GeneratedKernel> generatedKernels,
        ExpressionAnalysisResult analysisResult,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        using var activity = CompilationActivity.Start(nameof(OptimizeKernelsAsync));
        
        _logger.LogDebug("Optimizing kernels for {Count} backends", generatedKernels.Count);

        var tasks = generatedKernels.Select(async kvp =>
        {
            var optimized = await _optimizer.OptimizeAsync(
                kvp.Value, analysisResult, options, cancellationToken);
            return new KeyValuePair<BackendType, OptimizedKernel>(kvp.Key, optimized);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static CompiledKernelPackage CreateKernelPackage(
        IReadOnlyDictionary<BackendType, OptimizedKernel> optimizedKernels,
        ExpressionAnalysisResult analysisResult)
    {
        var kernelDefinitions = optimizedKernels.ToDictionary(
            kvp => kvp.Key,
            kvp => new KernelDefinition(
                kvp.Value.Name,
                kvp.Value.SourceCode,
                kvp.Value.Parameters,
                kvp.Value.EntryPoint));

        return new CompiledKernelPackage(
            analysisResult.OperationSignature,
            kernelDefinitions,
            analysisResult.ComplexityMetrics,
            analysisResult.MemoryAccessPattern);
    }

    private static string GenerateCacheKey(
        Expression expression,
        IEnumerable<BackendType> targetBackends,
        CompilationOptions? options)
    {
        var expressionHash = ExpressionHasher.ComputeHash(expression);
        var backendsStr = string.Join(",", targetBackends.OrderBy(b => b.ToString()));
        var optionsHash = options?.GetHashCode() ?? 0;
        
        return $"{expressionHash}_{backendsStr}_{optionsHash:X8}";
    }

    /// <summary>
    /// Clears the compilation cache.
    /// </summary>
    public void ClearCache()
    {
        _kernelCache.Clear();
        _logger.LogInformation("Kernel compilation cache cleared");
    }

    /// <summary>
    /// Gets compilation metrics and statistics.
    /// </summary>
    public CompilationStatistics GetStatistics()
    {
        return new CompilationStatistics(
            _metrics.CompilationCount,
            _metrics.CacheHitCount,
            _metrics.ErrorCount,
            _metrics.AverageCompilationTime,
            _kernelCache.Count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _compilationSemaphore?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Represents a cached compiled kernel with expiration.
/// </summary>
internal record CachedKernel(CompiledKernelPackage Package, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
}

/// <summary>
/// Thread-safe compilation metrics collector.
/// </summary>
internal class CompilationMetrics
{
    private long _compilationCount;
    private long _cacheHitCount;
    private long _errorCount;
    private long _totalCompilationTimeMs;

    public long CompilationCount => Interlocked.Read(ref _compilationCount);
    public long CacheHitCount => Interlocked.Read(ref _cacheHitCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);
    
    public double AverageCompilationTime
    {
        get
        {
            var count = CompilationCount;
            return count > 0 ? Interlocked.Read(ref _totalCompilationTimeMs) / (double)count : 0;
        }
    }

    public void RecordCompilation(TimeSpan duration)
    {
        Interlocked.Increment(ref _compilationCount);
        Interlocked.Add(ref _totalCompilationTimeMs, (long)duration.TotalMilliseconds);
    }

    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHitCount);
    public void RecordError() => Interlocked.Increment(ref _errorCount);
}

/// <summary>
/// Compilation statistics snapshot.
/// </summary>
public record CompilationStatistics(
    long CompilationCount,
    long CacheHitCount,
    long ErrorCount,
    double AverageCompilationTimeMs,
    int CachedKernelCount);

/// <summary>
/// Exception thrown during expression compilation.
/// </summary>
public class CompilationException : Exception
{
    public CompilationException(string message) : base(message) { }
    public CompilationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Activity tracking for compilation stages.
/// </summary>
internal static class CompilationActivity
{
    public static IDisposable Start(string operationName)
    {
        // In a real implementation, this would integrate with System.Diagnostics.Activity
        // or OpenTelemetry for distributed tracing
        return new NoOpDisposable();
    }
    
    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}