using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation.Stages;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Pipelines.Analysis;
using CompilationAnalysisResult = DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult;
using PipelineAnalysisResult = DotCompute.Linq.Compilation.Analysis.PipelineAnalysisResult;
using PipelineOperatorInfo = DotCompute.Linq.Compilation.Analysis.PipelineOperatorInfo;
using PipelineComplexityMetrics = DotCompute.Linq.Compilation.Analysis.PipelineComplexityMetrics;
using DotCompute.Linq.Operators.Types;
using DotCompute.Linq.Operators.Execution;

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
                var pipelineAnalysisResult = new PipelineAnalysisResult
                    OperatorInfo = [],
                    TypeUsage = analysisResult.TypeUsage.Select(kvp => new DotCompute.Linq.Compilation.Analysis.TypeUsageInfo
                    {
                        Type = kvp.Key,
                        UsageCount = kvp.Value.UsageCount,
                        IsGpuCompatible = kvp.Value.IsGpuCompatible,
                        TypeSize = kvp.Value.TypeSize,
                        ConversionRequirements = kvp.Value.ConversionRequirements,
                        RequiresSpecialization = kvp.Value.RequiresSpecialization
                    }).ToList(),
                    Dependencies = analysisResult.Dependencies.Select(d => new DotCompute.Linq.Compilation.Analysis.DependencyInfo
                        DependentOperation = d.DependentOperation ?? string.Empty,
                        Dependencies = d.Dependencies,
                        Type = d.Type,
                        AllowsParallelization = d.AllowsParallelization
                    ComplexityMetrics = new PipelineComplexityMetrics
                        OverallComplexity = (int)analysisResult.ComplexityScore,
                        MemoryComplexity = (int)(analysisResult.MemoryRequirement / 1024),
                        ComputeComplexity = (int)analysisResult.ParallelizationPotential
                    },
                    ParallelizationInfo = new DotCompute.Linq.Compilation.Analysis.ParallelizationInfo
                        CanParallelize = analysisResult.IsGpuCompatible,
                        MaxParallelism = Environment.ProcessorCount,
                        ParallelizationMethod = analysisResult.IsGpuCompatible ? "GPU" : "CPU"
                    MemoryAccessPattern = new DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern(),
                    AnalysisTimestamp = DateTimeOffset.UtcNow
                };
                var generatedKernels = await GenerateKernelCodeAsync(
                    pipelineAnalysisResult, targetBackends, options, cancellationToken);
                // Stage 3: Optimization
                var optimizedKernels = await OptimizeKernelsAsync(
                    generatedKernels, ConvertAnalysisResult(analysisResult), options, cancellationToken);
                // Stage 4: Package Creation
                var package = CreateKernelPackage(optimizedKernels, ConvertAnalysisResult(analysisResult));
                // Cache the result
                var compilationTime = DateTimeOffset.UtcNow - startTime;
                var cachedKernel = new CachedKernel(package, DateTimeOffset.UtcNow.AddHours(1));
                _kernelCache.TryAdd(cacheKey, cachedKernel);
                _metrics.RecordCompilation(compilationTime);
                _logger.LogInformation("Compilation completed in {Duration}ms", compilationTime.TotalMilliseconds);
                return package;
            finally
                _compilationSemaphore.Release();
        }
        catch (Exception ex)
            _logger.LogError(ex, "Compilation failed for expression: {ExpressionType}", expression.Type);
            _metrics.RecordError();
            throw new CompilationException("Failed to compile expression tree", ex);
    /// Compiles a batch of expressions for improved efficiency.
    public async Task<IReadOnlyDictionary<string, CompiledKernelPackage>> CompileBatchAsync(
        IEnumerable<(string Key, Expression Expression)> expressions,
        var tasks = expressions.Select(async expr =>
            var package = await CompileAsync(expr.Expression, targetBackends, options, cancellationToken);
            return new KeyValuePair<string, CompiledKernelPackage>(expr.Key, package);
        });
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    private async Task<Compilation.Analysis.ExpressionAnalysisResult> AnalyzeExpressionAsync(
        CompilationOptions? options,
        CancellationToken cancellationToken)
        using var activity = CompilationActivity.Start(nameof(AnalyzeExpressionAsync));
        _logger.LogDebug("Analyzing expression tree structure");
        var result = await _analyzer.AnalyzeAsync(expression, options, cancellationToken);
        _logger.LogDebug("Expression analysis completed: {OperatorCount} operators, {Complexity} complexity",
            result.OperatorChain.Count, result.ComplexityMetrics.OverallComplexity);
        // Return the compilation analysis result directly
        return result;
    private async Task<IReadOnlyDictionary<BackendType, DotCompute.Linq.Operators.Generation.GeneratedKernel>> GenerateKernelCodeAsync(
        PipelineAnalysisResult analysisResult,
        using var activity = CompilationActivity.Start(nameof(GenerateKernelCodeAsync));
        _logger.LogDebug("Generating kernel code for backends: {Backends}",
            string.Join(", ", targetBackends));
        // Convert pipeline analysis result to compilation analysis result
        var compilationAnalysisResult = ConvertToCompilationAnalysisResult(analysisResult);
        var tasks = targetBackends.Select(async backend =>
            var kernel = await _codeGenerator.GenerateAsync(
                compilationAnalysisResult, backend, options, cancellationToken);
            return new KeyValuePair<BackendType, DotCompute.Linq.Operators.Generation.GeneratedKernel>(backend, kernel);
    private async Task<IReadOnlyDictionary<BackendType, OptimizedKernel>> OptimizeKernelsAsync(
        IReadOnlyDictionary<BackendType, DotCompute.Linq.Operators.Generation.GeneratedKernel> generatedKernels,
        ExpressionAnalysisResult analysisResult,
        using var activity = CompilationActivity.Start(nameof(OptimizeKernelsAsync));
        _logger.LogDebug("Optimizing kernels for {Count} backends", generatedKernels.Count);
        var tasks = generatedKernels.Select(async kvp =>
            var optimized = await _optimizer.OptimizeAsync(
                kvp.Value, analysisResult, options, cancellationToken);
            return new KeyValuePair<BackendType, OptimizedKernel>(kvp.Key, optimized);
    private static CompiledKernelPackage CreateKernelPackage(
        IReadOnlyDictionary<BackendType, OptimizedKernel> optimizedKernels,
        ExpressionAnalysisResult analysisResult)
        var kernelDefinitions = optimizedKernels.ToDictionary(
            kvp => kvp.Key,
            kvp => new DotCompute.Abstractions.Kernels.KernelDefinition(
                kvp.Value.CompiledKernel.Name,
                kvp.Value.CompiledKernel.SourceCode,
                kvp.Value.CompiledKernel.EntryPoint));
        // For now, return the first compiled kernel as the package
        // This is a simplified approach - in production, you'd want to handle multiple kernels
        var firstKernel = optimizedKernels.First();
        return new CompiledKernelPackage
            CompiledKernel = CreateKernelAdapter(firstKernel.Value.CompiledKernel),
            Backend = DotCompute.Core.Compute.Enums.ComputeBackendType.CPU, // Default to CPU
            Definition = new DotCompute.Linq.Operators.Types.KernelDefinition
                Name = kernelDefinitions.First().Value.Name,
                Source = kernelDefinitions.First().Value.Source ?? string.Empty
            },
            OptimizationHints = ConvertOptimizationHints(analysisResult.OptimizationHints),
            EstimatedCost = analysisResult.ComplexityScore
        };
    private static string GenerateCacheKey(
        CompilationOptions? options)
        var expressionHash = ExpressionHasher.ComputeHash(expression);
        var backendsStr = string.Join(",", targetBackends.OrderBy(b => b.ToString()));
        var optionsHash = options?.GetHashCode() ?? 0;
        return $"{expressionHash}_{backendsStr}_{optionsHash:X8}";
    /// Clears the compilation cache.
    public void ClearCache()
        _kernelCache.Clear();
        _logger.LogInformation("Kernel compilation cache cleared");
    /// Gets compilation metrics and statistics.
    public CompilationStatistics GetStatistics()
        return new CompilationStatistics(
            _metrics.CompilationCount,
            _metrics.CacheHitCount,
            _metrics.ErrorCount,
            _metrics.AverageCompilationTime,
            _kernelCache.Count);
    /// Converts from Compilation.Analysis.ExpressionAnalysisResult to Pipelines.Analysis.ExpressionAnalysisResult.
    private static Pipelines.Analysis.ExpressionAnalysisResult ConvertAnalysisResult(
        Compilation.Analysis.ExpressionAnalysisResult source)
        return new Pipelines.Analysis.ExpressionAnalysisResult
            OperationSignature = source.OperationSignature,
            IsCompilable = source.IsCompilable,
            IsGpuCompatible = source.IsGpuCompatible,
            IsCpuCompatible = source.IsCpuCompatible,
            ComplexityScore = source.ComplexityScore,
            MemoryRequirement = source.MemoryRequirement,
            ParallelizationPotential = source.ParallelizationPotential,
            Recommendations = source.Recommendations.ToList(),
            Bottlenecks = source.Bottlenecks.ToList(),
            OptimizationHints = source.OptimizationHints?.Select(h => h.Description).ToList() ?? []
    /// Converts a list of optimization hint strings to OptimizationHint objects.
    private static ImmutableArray<OptimizationHint> ConvertOptimizationHints(List<string> hintStrings)
        if (hintStrings == null || hintStrings.Count == 0)
            return ImmutableArray<OptimizationHint>.Empty;
        var hints = hintStrings.Select(hint => new OptimizationHint
            Type = ParseOptimizationHintType(hint),
            Description = hint,
            Priority = OptimizationPriority.Medium
        return hints.ToImmutableArray();
    /// Parses a hint string to determine the optimization hint type.
    private static OptimizationHintType ParseOptimizationHintType(string hint)
        return hint.ToLowerInvariant() switch
            var s when s.Contains("vector") => OptimizationHintType.Vectorization,
            var s when s.Contains("memory") || s.Contains("coalesce") => OptimizationHintType.MemoryCoalescing,
            var s when s.Contains("loop") || s.Contains("unroll") => OptimizationHintType.LoopUnrolling,
            var s when s.Contains("workgroup") || s.Contains("block") => OptimizationHintType.WorkgroupSize,
            var s when s.Contains("cache") => OptimizationHintType.CacheOptimization,
            var s when s.Contains("parallel") => OptimizationHintType.Parallelization,
            var s when s.Contains("gpu") => OptimizationHintType.GpuExecution,
            var s when s.Contains("shared") => OptimizationHintType.SharedMemoryOptimization,
            _ => OptimizationHintType.Custom
    /// Converts an expression analysis result to a pipeline analysis result.
    private Analysis.PipelineAnalysisResult ConvertToAnalysisResult(Compilation.Analysis.ExpressionAnalysisResult analysisResult)
        return new Analysis.PipelineAnalysisResult
            ComplexityMetrics = new Analysis.PipelineComplexityMetrics
                TotalComplexity = analysisResult.ComplexityMetrics.OverallComplexity,
                OperationCount = analysisResult.OperatorChain.Count,
                EstimatedMemoryUsage = analysisResult.EstimatedMemoryUsage,
                ParallelizationPotential = analysisResult.IsParallelizable ? 0.8 : 0.2,
                GpuRecommended = analysisResult.IsGpuSuitable
            TypeUsage = analysisResult.TypeUsage.Select(kvp => new Analysis.TypeUsageInfo
                Type = kvp.Key,
                UsageCount = kvp.Value.UsageCount,
                IsGpuCompatible = kvp.Value.IsGpuCompatible,
                TypeSize = (int)kvp.Value.TypeSize
            }).ToList(),
            Dependencies = analysisResult.Dependencies.Select(d => new Analysis.DependencyInfo
                DependentOperation = d.DependentOperation ?? string.Empty,
                Dependencies = d.Dependencies?.ToList() ?? [],
                Type = Analysis.DependencyType.Data,
                AllowsParallelization = d.AllowsParallelization
            MemoryAccessPattern = new Analysis.GlobalMemoryAccessPattern
                AccessType = Analysis.MemoryAccessType.Sequential,
                IsCoalesced = true,
                CacheEfficiency = 0.8
    /// Converts a pipeline analysis result to a compilation analysis result.
    private CompilationAnalysisResult ConvertToCompilationAnalysisResult(PipelineAnalysisResult pipelineResult)
        // Create a minimal compilation analysis result from pipeline result
        // This conversion bridges the gap between pipeline and compilation analysis
        return new CompilationAnalysisResult(
            "Pipeline Operation", // operationSignature
            new List<DotCompute.Linq.Compilation.Analysis.PipelineOperatorInfo>(), // operatorChain
            new Dictionary<Type, DotCompute.Linq.Compilation.Analysis.TypeUsageInfo>().AsReadOnly(), // typeUsage
            new List<DotCompute.Linq.Compilation.Analysis.DependencyInfo>(), // dependencies
            new DotCompute.Linq.Compilation.Analysis.PipelineComplexityMetrics(), // complexityMetrics
            new DotCompute.Linq.Compilation.Analysis.ParallelizationInfo(), // parallelizationInfo
            new DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern(), // memoryAccessPattern
            new List<DotCompute.Linq.Compilation.Analysis.OptimizationHint>() // optimizationHints
        );
    /// Converts memory access patterns between different namespace types.
    private static DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern ConvertMemoryAccessPattern(
        object sourcePattern)
        if (sourcePattern is DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern compilationPattern)
            return compilationPattern;
        // Create a new pattern with default values
        return new DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern();
    /// Creates an adapter to convert from LINQ ICompiledKernel to Abstractions ICompiledKernel.
    private static DotCompute.Abstractions.ICompiledKernel CreateKernelAdapter(
        DotCompute.Linq.Operators.Execution.ICompiledKernel linqKernel)
        return new LinqToAbstractionsKernelAdapter(linqKernel);
    public void Dispose()
        if (_disposed)
            return;
        _compilationSemaphore?.Dispose();
        _disposed = true;
}
/// Represents a cached compiled kernel with expiration.
internal record CachedKernel(CompiledKernelPackage Package, DateTimeOffset ExpiresAt)
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
/// Thread-safe compilation metrics collector.
internal class CompilationMetrics
    private long _compilationCount;
    private long _cacheHitCount;
    private long _errorCount;
    private long _totalCompilationTimeMs;
    public long CompilationCount => Interlocked.Read(ref _compilationCount);
    public long CacheHitCount => Interlocked.Read(ref _cacheHitCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);
    public double AverageCompilationTime
        get
            var count = CompilationCount;
            return count > 0 ? Interlocked.Read(ref _totalCompilationTimeMs) / (double)count : 0;
    public void RecordCompilation(TimeSpan duration)
        Interlocked.Increment(ref _compilationCount);
        Interlocked.Add(ref _totalCompilationTimeMs, (long)duration.TotalMilliseconds);
    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHitCount);
    public void RecordError() => Interlocked.Increment(ref _errorCount);
/// Adapter that converts from LINQ ICompiledKernel to Abstractions ICompiledKernel.
internal sealed class LinqToAbstractionsKernelAdapter : DotCompute.Abstractions.ICompiledKernel
    private readonly DotCompute.Linq.Operators.Execution.ICompiledKernel _linqKernel;
    public LinqToAbstractionsKernelAdapter(DotCompute.Linq.Operators.Execution.ICompiledKernel linqKernel)
        _linqKernel = linqKernel ?? throw new ArgumentNullException(nameof(linqKernel));
    public Guid Id { get; } = Guid.NewGuid();
    public string Name => _linqKernel.Name;
    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        // Convert KernelArguments to the LINQ kernel format
        var linqParams = new DotCompute.Linq.Operators.Models.KernelExecutionParameters();
        await _linqKernel.ExecuteAsync(linqParams, cancellationToken);
        if (!_disposed)
            _linqKernel?.Dispose();
            _disposed = true;
    public async ValueTask DisposeAsync()
        await ValueTask.CompletedTask;
    /// Converts Compilation ExpressionAnalysisResult to PipelineAnalysisResult.
    private static PipelineAnalysisResult ConvertAnalysisResultToPipeline(Compilation.Analysis.ExpressionAnalysisResult analysisResult)
        return new PipelineAnalysisResult
            OperatorInfo = [],
            TypeUsage = analysisResult.TypeUsage.Select(kvp => new DotCompute.Linq.Compilation.Analysis.TypeUsageInfo
                TypeSize = kvp.Value.TypeSize,
                ConversionRequirements = kvp.Value.ConversionRequirements,
                RequiresSpecialization = kvp.Value.RequiresSpecialization
            Dependencies = analysisResult.Dependencies.Select(d => new DotCompute.Linq.Compilation.Analysis.DependencyInfo
                Dependencies = d.Dependencies,
                Type = d.Type,
            ComplexityMetrics = new PipelineComplexityMetrics
                OverallComplexity = (int)analysisResult.ComplexityScore,
                MemoryComplexity = (int)(analysisResult.MemoryRequirement / 1024),
                ComputeComplexity = (int)analysisResult.ParallelizationPotential
            ParallelizationInfo = new DotCompute.Linq.Compilation.Analysis.ParallelizationInfo
                CanParallelize = analysisResult.IsGpuCompatible,
                MaxParallelism = Environment.ProcessorCount,
                ParallelizationMethod = analysisResult.IsGpuCompatible ? "GPU" : "CPU"
            MemoryAccessPattern = new DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern(),
            AnalysisTimestamp = DateTimeOffset.UtcNow
    /// Converts memory access pattern for pipeline analysis.
    private static Compilation.Analysis.GlobalMemoryAccessPattern ConvertMemoryAccessPattern(Compilation.Analysis.GlobalMemoryAccessPattern pattern)
        // Return a copy or the same pattern
        return pattern;
    /// Converts Pipeline ExpressionAnalysisResult to PipelineAnalysisResult.
    private static PipelineAnalysisResult ConvertFromPipelineAnalysisResult(Pipelines.Analysis.ExpressionAnalysisResult analysisResult)
            TypeUsage = [],
            Dependencies = [],
                MemoryComplexity = (int)(analysisResult.MemoryRequirement / 1024), // Convert to MB
/// Compilation statistics snapshot.
public record CompilationStatistics(
    long CompilationCount,
    long CacheHitCount,
    long ErrorCount,
    double AverageCompilationTimeMs,
    int CachedKernelCount);
/// Exception thrown during expression compilation.
public class CompilationException : Exception
    public CompilationException(string message) : base(message) { }
    public CompilationException(string message, Exception innerException) : base(message, innerException) { }
/// Activity tracking for compilation stages.
internal static class CompilationActivity
    public static IDisposable Start(string operationName)
        // In a real implementation, this would integrate with System.Diagnostics.Activity
        // or OpenTelemetry for distributed tracing
        return new NoOpDisposable();
    private class NoOpDisposable : IDisposable
        public void Dispose() { }
