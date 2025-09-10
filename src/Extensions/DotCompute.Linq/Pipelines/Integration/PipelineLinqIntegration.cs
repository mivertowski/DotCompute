// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Optimization;
using DotCompute.Linq.Pipelines.Providers;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AdvancedPipelineOptimizer = DotCompute.Linq.Pipelines.Optimization.IAdvancedPipelineOptimizer;

namespace DotCompute.Linq.Pipelines.Integration;

/// <summary>
/// Comprehensive integration layer for LINQ-to-Pipeline functionality with DotCompute infrastructure.
/// Provides seamless integration with orchestrators, backend selectors, and memory managers.
/// </summary>
public static class PipelineLinqIntegration
{
    /// <summary>
    /// Registers all pipeline LINQ services with the dependency injection container.
    /// </summary>
    /// <param name="services">Service collection to register services with</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddPipelineLinqServices(this IServiceCollection services)
    {
        // Core pipeline services
        services.AddSingleton<IPipelineExpressionAnalyzer, PipelineExpressionAnalyzer>();
        services.AddSingleton<IPipelinePerformanceAnalyzer, PipelinePerformanceAnalyzer>();
        services.AddSingleton<AdvancedPipelineOptimizer, Optimization.AdvancedPipelineOptimizer>();
        
        // LINQ provider services
        services.AddScoped<PipelineOptimizedProvider>();
        services.AddScoped<IComputeLinqProvider, IntegratedPipelineLinqProvider>();
        
        // Integration services
        services.AddScoped<IPipelineOrchestrationService, PipelineOrchestrationService>();
        services.AddScoped<IPipelineMemoryIntegration, PipelineMemoryIntegration>();
        services.AddScoped<IPipelineBackendIntegration, PipelineBackendIntegration>();
        
        return services;
    }

    /// <summary>
    /// Configures pipeline LINQ with existing DotCompute orchestrator integration.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection ConfigurePipelineLinq(
        this IServiceCollection services,
        Action<PipelineLinqOptions>? configureOptions = null)
    {
        var options = new PipelineLinqOptions();
        configureOptions?.Invoke(options);
        
        services.AddSingleton(options);
        services.AddPipelineLinqServices();
        
        return services;
    }
}

/// <summary>
/// Configuration options for pipeline LINQ integration.
/// </summary>
public class PipelineLinqOptions
{
    /// <summary>Whether to enable automatic backend selection.</summary>
    public bool EnableAutomaticBackendSelection { get; set; } = true;

    /// <summary>Whether to enable performance profiling.</summary>
    public bool EnablePerformanceProfiling { get; set; } = false;

    /// <summary>Whether to enable aggressive caching.</summary>
    public bool EnableAggressiveCaching { get; set; } = false;

    /// <summary>Default optimization level for pipeline operations.</summary>
    public OptimizationLevel DefaultOptimizationLevel { get; set; } = OptimizationLevel.Balanced;

    /// <summary>Maximum memory usage before spilling to disk.</summary>
    public long MaxMemoryUsageBytes { get; set; } = 1024L * 1024 * 1024; // 1GB

    /// <summary>Default timeout for pipeline operations.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Whether to enable kernel fusion optimizations.</summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>Whether to enable streaming optimizations.</summary>
    public bool EnableStreamingOptimizations { get; set; } = true;
}

/// <summary>
/// Service for orchestrating pipeline execution with DotCompute integration.
/// </summary>
public interface IPipelineOrchestrationService
{
    /// <summary>
    /// Executes a LINQ query using the integrated pipeline orchestration.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="queryable">LINQ queryable to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    Task<T> ExecuteWithOrchestrationAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a query and provides execution recommendations.
    /// </summary>
    /// <param name="queryable">Query to analyze</param>
    /// <returns>Execution recommendations</returns>
    Task<ExecutionRecommendation> AnalyzeQueryAsync(IQueryable queryable);
}

/// <summary>
/// Implementation of pipeline orchestration service with full DotCompute integration.
/// </summary>
public class PipelineOrchestrationService : IPipelineOrchestrationService
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IPipelinePerformanceAnalyzer _performanceAnalyzer;
    private readonly AdvancedPipelineOptimizer _optimizer;
    private readonly ILogger<PipelineOrchestrationService> _logger;
    private readonly PipelineLinqOptions _options;

    /// <summary>
    /// Initializes a new instance of the PipelineOrchestrationService class.
    /// </summary>
    public PipelineOrchestrationService(
        IComputeOrchestrator orchestrator,
        IPipelinePerformanceAnalyzer performanceAnalyzer,
        AdvancedPipelineOptimizer optimizer,
        ILogger<PipelineOrchestrationService> logger,
        PipelineLinqOptions options)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _performanceAnalyzer = performanceAnalyzer ?? throw new ArgumentNullException(nameof(performanceAnalyzer));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithOrchestrationAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting orchestrated pipeline execution for query");

        try
        {
            // Analyze query for optimization opportunities
            var recommendation = await AnalyzeQueryAsync(queryable);
            
            // Create optimized execution context
            var executionContext = CreateExecutionContext(recommendation);
            
            // Execute through orchestrator
            var result = await _orchestrator.ExecuteKernelAsync<T>(
                recommendation.RecommendedKernel,
                recommendation.Parameters.ToArray(),
                executionContext,
                cancellationToken);

            _logger.LogInformation("Orchestrated pipeline execution completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestrated pipeline execution failed");
            throw new PipelineOrchestrationException("Failed to execute query through orchestration", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ExecutionRecommendation> AnalyzeQueryAsync(IQueryable queryable)
    {
        _logger.LogDebug("Analyzing query for execution recommendations");

        // Get performance analysis
        var performanceReport = await _performanceAnalyzer.AnalyzePipelineAsync(queryable.Expression);
        
        // Get backend recommendation
        var backendRecommendation = await _performanceAnalyzer.RecommendOptimalBackendAsync(queryable);
        
        // Get memory estimate
        var memoryEstimate = await _performanceAnalyzer.EstimateMemoryUsageAsync(queryable);

        var recommendation = new ExecutionRecommendation
        {
            RecommendedBackend = backendRecommendation.RecommendedBackend,
            RecommendedKernel = "OptimizedLinqPipelineKernel",
            EstimatedExecutionTime = performanceReport.EstimatedExecutionTime,
            EstimatedMemoryUsage = memoryEstimate.PeakMemoryUsage,
            OptimizationRecommendations = performanceReport.Recommendations,
            Parameters = new List<object> { queryable.Expression },
            Confidence = performanceReport.ConfidenceLevel
        };

        _logger.LogDebug("Query analysis completed - Backend: {Backend}, Memory: {Memory}MB", 
            recommendation.RecommendedBackend, 
            recommendation.EstimatedMemoryUsage / (1024.0 * 1024.0));

        return recommendation;
    }

    private IKernelExecutionContext CreateExecutionContext(ExecutionRecommendation recommendation)
    {
        return new KernelExecutionContext
        {
            PreferredBackend = recommendation.RecommendedBackend,
            TimeoutMs = (int)_options.DefaultTimeout.TotalMilliseconds,
            EnableProfiling = _options.EnablePerformanceProfiling,
            MaxMemoryUsage = Math.Min(recommendation.EstimatedMemoryUsage * 2, _options.MaxMemoryUsageBytes),
            OptimizationLevel = _options.DefaultOptimizationLevel
        };
    }
}

/// <summary>
/// Service for integrating pipeline operations with DotCompute memory management.
/// </summary>
public interface IPipelineMemoryIntegration
{
    /// <summary>
    /// Creates unified memory buffers for pipeline operations.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="data">Source data</param>
    /// <returns>Unified memory buffer</returns>
    Task<IUnifiedMemoryBuffer<T>> CreateUnifiedBufferAsync<T>(IEnumerable<T> data) where T : unmanaged;

    /// <summary>
    /// Optimizes memory layout for pipeline operations.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="buffer">Buffer to optimize</param>
    /// <returns>Optimized buffer</returns>
    Task<IUnifiedMemoryBuffer<T>> OptimizeMemoryLayoutAsync<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged;
}

/// <summary>
/// Implementation of pipeline memory integration.
/// </summary>
public class PipelineMemoryIntegration : IPipelineMemoryIntegration
{
    private readonly IUnifiedMemoryManager _memoryManager;
    private readonly ILogger<PipelineMemoryIntegration> _logger;

    /// <summary>
    /// Initializes a new instance of the PipelineMemoryIntegration class.
    /// </summary>
    public PipelineMemoryIntegration(
        IUnifiedMemoryManager memoryManager,
        ILogger<PipelineMemoryIntegration> logger)
    {
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IUnifiedMemoryBuffer<T>> CreateUnifiedBufferAsync<T>(IEnumerable<T> data) where T : unmanaged
    {
        _logger.LogDebug("Creating unified memory buffer for {Type}", typeof(T));

        var array = data.ToArray();
        var buffer = _memoryManager.AllocateBuffer<T>(array.Length);
        
        await buffer.CopyFromAsync(array);
        
        return buffer;
    }

    /// <inheritdoc />
    public Task<IUnifiedMemoryBuffer<T>> OptimizeMemoryLayoutAsync<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
    {
        _logger.LogDebug("Optimizing memory layout for buffer");

        // Apply memory layout optimizations
        // This would involve reordering data for better cache locality
        // For now, return the same buffer (placeholder)
        return Task.FromResult(buffer);
    }
}

/// <summary>
/// Service for integrating pipeline operations with backend selection.
/// </summary>
public interface IPipelineBackendIntegration
{
    /// <summary>
    /// Selects optimal backend for a pipeline operation.
    /// </summary>
    /// <param name="queryable">Query to analyze</param>
    /// <returns>Selected backend</returns>
    Task<string> SelectOptimalBackendAsync(IQueryable queryable);

    /// <summary>
    /// Validates backend compatibility for a query.
    /// </summary>
    /// <param name="queryable">Query to validate</param>
    /// <param name="backend">Backend to validate against</param>
    /// <returns>Compatibility validation result</returns>
    Task<BackendCompatibilityResult> ValidateBackendCompatibilityAsync(IQueryable queryable, string backend);
}

/// <summary>
/// Implementation of pipeline backend integration.
/// </summary>
public class PipelineBackendIntegration : IPipelineBackendIntegration
{
    private readonly IAdaptiveBackendSelector _backendSelector;
    private readonly IPipelinePerformanceAnalyzer _performanceAnalyzer;
    private readonly ILogger<PipelineBackendIntegration> _logger;

    /// <summary>
    /// Initializes a new instance of the PipelineBackendIntegration class.
    /// </summary>
    public PipelineBackendIntegration(
        IAdaptiveBackendSelector backendSelector,
        IPipelinePerformanceAnalyzer performanceAnalyzer,
        ILogger<PipelineBackendIntegration> logger)
    {
        _backendSelector = backendSelector ?? throw new ArgumentNullException(nameof(backendSelector));
        _performanceAnalyzer = performanceAnalyzer ?? throw new ArgumentNullException(nameof(performanceAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> SelectOptimalBackendAsync(IQueryable queryable)
    {
        _logger.LogDebug("Selecting optimal backend for query");

        var recommendation = await _performanceAnalyzer.RecommendOptimalBackendAsync(queryable);
        
        // Use adaptive backend selector for final decision
        var workloadCharacteristics = CreateWorkloadCharacteristics(recommendation);
        var selectedBackend = await _backendSelector.SelectBackendAsync(workloadCharacteristics);

        _logger.LogDebug("Selected backend: {Backend} (confidence: {Confidence:P})", 
            selectedBackend, recommendation.Confidence);

        return selectedBackend;
    }

    /// <inheritdoc />
    public async Task<BackendCompatibilityResult> ValidateBackendCompatibilityAsync(IQueryable queryable, string backend)
    {
        _logger.LogDebug("Validating backend compatibility for {Backend}", backend);

        var analysisResult = await _performanceAnalyzer.AnalyzePipelineAsync(queryable.Expression);
        
        var isCompatible = backend switch
        {
            "CUDA" => analysisResult.Bottlenecks.All(b => b.Type != BottleneckType.ComputeThroughput),
            "CPU" => true, // CPU is always compatible
            _ => false
        };

        return new BackendCompatibilityResult
        {
            IsCompatible = isCompatible,
            Backend = backend,
            CompatibilityScore = isCompatible ? 0.9 : 0.1,
            Limitations = analysisResult.Bottlenecks.Select(b => b.Description).ToList(),
            Recommendations = isCompatible ? new List<string>() : new List<string> { $"Consider using CPU backend instead of {backend}" }
        };
    }

    private WorkloadCharacteristics CreateWorkloadCharacteristics(BackendRecommendation recommendation)
    {
        return new WorkloadCharacteristics
        {
            ComputeIntensity = recommendation.BackendEstimates.ContainsKey("CUDA") ? 0.8 : 0.3,
            MemoryIntensity = 0.5,
            ParallelismDegree = Environment.ProcessorCount,
            DataSize = recommendation.BackendEstimates.Values.FirstOrDefault()?.EstimatedMemory ?? 1024,
            WorkloadType = "LINQ Pipeline"
        };
    }
}

/// <summary>
/// Integrated LINQ provider that combines all pipeline optimizations.
/// </summary>
public class IntegratedPipelineLinqProvider : IComputeLinqProvider
{
    private readonly PipelineOptimizedProvider _pipelineProvider;
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly ILogger<IntegratedPipelineLinqProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the IntegratedPipelineLinqProvider class.
    /// </summary>
    public IntegratedPipelineLinqProvider(
        PipelineOptimizedProvider pipelineProvider,
        IPipelineOrchestrationService orchestrationService,
        ILogger<IntegratedPipelineLinqProvider> logger)
    {
        _pipelineProvider = pipelineProvider ?? throw new ArgumentNullException(nameof(pipelineProvider));
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IQueryable<T> CreateQueryable<T>(IEnumerable<T> source, IAccelerator? accelerator = null)
    {
        _logger.LogDebug("Creating integrated pipeline queryable for {Type}", typeof(T));

        // Use the pipeline optimized provider with orchestration integration
        var queryable = source.AsQueryable();
        return new IntegratedComputeQueryable<T>(_pipelineProvider, queryable.Expression);
    }

    /// <inheritdoc />
    public IQueryable<T> CreateQueryable<T>(T[] source, IAccelerator? accelerator = null)
    {
        _logger.LogDebug("Creating integrated pipeline queryable for array of {Type}", typeof(T));

        // Use the pipeline optimized provider with orchestration integration
        var queryable = source.AsQueryable();
        return new IntegratedComputeQueryable<T>(_pipelineProvider, queryable.Expression);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression)
    {
        return await ExecuteAsync<T>(expression, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing expression asynchronously for {Type}", typeof(T));

        if (_pipelineProvider is PipelineOptimizedProvider optimizedProvider)
        {
            return await optimizedProvider.ExecuteAsync<T>(expression);
        }

        return _pipelineProvider.Execute<T>(expression);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator)
    {
        return await ExecuteAsync<T>(expression, preferredAccelerator, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing expression asynchronously for {Type} with accelerator {Accelerator}", 
            typeof(T), preferredAccelerator.GetType().Name);

        // For now, ignore the preferred accelerator and use the standard execution
        return await ExecuteAsync<T>(expression, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PrecompileExpressionsAsync(IEnumerable<Expression> expressions)
    {
        await PrecompileExpressionsAsync(expressions, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task PrecompileExpressionsAsync(IEnumerable<Expression> expressions, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Precompiling {ExpressionCount} expressions", expressions.Count());

        foreach (var expression in expressions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var queryable = Expression.Lambda(expression).Compile().DynamicInvoke() as IQueryable;
                if (queryable != null)
                {
                    await _orchestrationService.AnalyzeQueryAsync(queryable);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to precompile expression: {Expression}", expression);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<OptimizationSuggestion> GetOptimizationSuggestions(Expression expression)
    {
        // Convert performance recommendations to optimization suggestions
        try
        {
            var queryable = Expression.Lambda(expression).Compile().DynamicInvoke() as IQueryable;
            if (queryable != null)
            {
                var recommendation = _orchestrationService.AnalyzeQueryAsync(queryable).Result;
                return recommendation.OptimizationRecommendations.Select(r => new OptimizationSuggestion
                {
                    Category = "Performance",
                    Message = r,
                    Severity = SuggestionSeverity.Info,
                    EstimatedImpact = 0.5
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate optimization suggestions");
        }

        return Enumerable.Empty<OptimizationSuggestion>();
    }

    /// <inheritdoc />
    public bool IsGpuCompatible(Expression expression)
    {
        try
        {
            var queryable = Expression.Lambda(expression).Compile().DynamicInvoke() as IQueryable;
            if (queryable != null)
            {
                var recommendation = _orchestrationService.AnalyzeQueryAsync(queryable).Result;
                return recommendation.RecommendedBackend == "CUDA";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check GPU compatibility");
        }

        return false;
    }
}

#region Supporting Types

/// <summary>
/// Execution recommendation from query analysis.
/// </summary>
public class ExecutionRecommendation
{
    /// <summary>Recommended backend for execution.</summary>
    public string RecommendedBackend { get; set; } = "CPU";

    /// <summary>Recommended kernel for execution.</summary>
    public string RecommendedKernel { get; set; } = string.Empty;

    /// <summary>Estimated execution time.</summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>Estimated memory usage.</summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>Optimization recommendations.</summary>
    public List<string> OptimizationRecommendations { get; set; } = new();

    /// <summary>Execution parameters.</summary>
    public List<object> Parameters { get; set; } = new();

    /// <summary>Confidence in the recommendation.</summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Backend compatibility validation result.
/// </summary>
public class BackendCompatibilityResult
{
    /// <summary>Whether the backend is compatible.</summary>
    public bool IsCompatible { get; set; }

    /// <summary>Backend name.</summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>Compatibility score (0-1).</summary>
    public double CompatibilityScore { get; set; }

    /// <summary>Known limitations with this backend.</summary>
    public List<string> Limitations { get; set; } = new();

    /// <summary>Recommendations for better compatibility.</summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Kernel execution context for pipeline operations.
/// </summary>
public class KernelExecutionContext : IKernelExecutionContext
{
    /// <summary>Preferred backend for execution.</summary>
    public string PreferredBackend { get; set; } = "CPU";

    /// <summary>Timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>Whether to enable profiling.</summary>
    public bool EnableProfiling { get; set; } = false;

    /// <summary>Maximum memory usage.</summary>
    public long MaxMemoryUsage { get; set; }

    /// <summary>Optimization level.</summary>
    public OptimizationLevel OptimizationLevel { get; set; }
}

/// <summary>
/// Exception thrown when pipeline orchestration fails.
/// </summary>
public class PipelineOrchestrationException : Exception
{
    /// <summary>Initializes a new PipelineOrchestrationException.</summary>
    public PipelineOrchestrationException(string message) : base(message) { }

    /// <summary>Initializes a new PipelineOrchestrationException.</summary>
    public PipelineOrchestrationException(string message, Exception innerException) : base(message, innerException) { }
}

#endregion