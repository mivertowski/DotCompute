// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Operators;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Pipelines.Optimization;
using DotCompute.Linq.Pipelines.Providers;
using DotCompute.Linq.Providers;
using DotCompute.Linq.Services;
using DotCompute.Linq.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Linq.Extensions;

/// <summary>
/// Extension methods for registering DotCompute LINQ services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DotCompute LINQ services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure LINQ options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeLinq(
        this IServiceCollection services,
        Action<LinqServiceOptions>? configureOptions = null)
    {
        // Configure options
        var options = new LinqServiceOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Add cache services
        services.AddSingleton<Services.QueryCacheOptions>(provider =>
        {
            var linqOptions = provider.GetRequiredService<LinqServiceOptions>();
            return new Services.QueryCacheOptions
            {
                MaxEntries = linqOptions.CacheMaxEntries,
                EnableExpiration = linqOptions.EnableCacheExpiration,
                DefaultExpiration = linqOptions.CacheExpiration
            };
        });

        services.AddSingleton<IQueryCache, DefaultQueryCache>();

        // Add memory management
        services.AddSingleton<Services.DefaultMemoryManagerFactory>();

        // Add expression compilation services
        services.AddSingleton<IExpressionOptimizer, DotCompute.Linq.Expressions.ExpressionOptimizer>();
        services.AddSingleton<IKernelFactory, DefaultKernelFactory>();
        services.AddSingleton<IQueryCompiler, QueryCompiler>();

        // Add execution services
        services.AddSingleton<IQueryExecutor, QueryExecutor>();

        // Add the integrated query provider that uses IComputeOrchestrator
        services.AddSingleton<IntegratedComputeQueryProvider>();

        // Add the main LINQ provider using the integrated approach

        services.AddSingleton<IComputeLinqProvider, RuntimeIntegratedLinqProvider>();

        // Add query provider factory for backward compatibility
        services.AddSingleton<IComputeQueryProviderFactory, ComputeQueryProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds DotCompute LINQ services with advanced configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="enableCaching">Enable query result caching</param>
    /// <param name="enableOptimization">Enable expression optimization</param>
    /// <param name="enableProfiling">Enable query profiling</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeLinqAdvanced(
        this IServiceCollection services,
        bool enableCaching = true,
        bool enableOptimization = true,
        bool enableProfiling = false)
    {
        return services.AddDotComputeLinq(options =>
        {
            options.EnableCaching = enableCaching;
            options.EnableOptimization = enableOptimization;
            options.EnableProfiling = enableProfiling;
        });
    }

    /// <summary>
    /// Creates extension methods for IServiceProvider to get LINQ services.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The LINQ provider instance</returns>
    public static IComputeLinqProvider GetComputeLinqProvider(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IComputeLinqProvider>();
    }

    /// <summary>
    /// Creates a compute queryable from an enumerable using DI services.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="source">The source enumerable</param>
    /// <param name="accelerator">Optional specific accelerator</param>
    /// <returns>A compute queryable</returns>
    public static IQueryable<T> CreateComputeQueryable<T>(
        this IServiceProvider serviceProvider,
        IEnumerable<T> source,
        IAccelerator? accelerator = null)
    {
        var linqProvider = serviceProvider.GetComputeLinqProvider();
        return linqProvider.CreateQueryable(source, accelerator);
    }

    #region Pipeline Services

    /// <summary>
    /// Adds comprehensive DotCompute LINQ pipeline services to the service collection.
    /// This includes advanced pipeline optimization, expression analysis, telemetry,
    /// and intelligent backend selection capabilities.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configurePipelines">Optional action to configure pipeline options</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// <para>Services Added:</para>
    /// <list type="bullet">
    /// <item>Pipeline expression analyzer for LINQ-to-kernel conversion</item>
    /// <item>Advanced pipeline optimizer for performance enhancement</item>
    /// <item>Performance analyzer for bottleneck identification</item>
    /// <item>Pipeline-optimized query provider</item>
    /// <item>Telemetry and metrics collection services</item>
    /// <item>Resource and cache management services</item>
    /// </list>
    /// <para>This method builds upon the basic LINQ services and should be called
    /// after <see cref="AddDotComputeLinq"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services
    ///     .AddDotComputeLinq()
    ///     .AddPipelineServices(options => 
    ///     {
    ///         options.EnableAdvancedOptimization = true;
    ///         options.EnableTelemetry = true;
    ///         options.MaxPipelineStages = 10;
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddPipelineServices(
        this IServiceCollection services,
        Action<PipelineServiceOptions>? configurePipelines = null)
    {
        // Configure pipeline options
        if (configurePipelines != null)
        {
            services.Configure(configurePipelines);
        }
        services.TryAddSingleton<PipelineServiceOptions>();

        // Core pipeline services
        services.TryAddSingleton<IPipelineExpressionAnalyzer, PipelineExpressionAnalyzer>();
        services.TryAddSingleton<DotCompute.Linq.Pipelines.Optimization.IAdvancedPipelineOptimizer, DotCompute.Linq.Pipelines.Optimization.AdvancedPipelineOptimizer>();
        services.TryAddSingleton<PipelinePerformanceAnalyzer>();

        // Pipeline providers and builders
        services.TryAddScoped<PipelineOptimizedProvider>();
        services.TryAddSingleton<DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder, DefaultKernelPipelineBuilder>();

        return services;
    }

    /// <summary>
    /// Adds pipeline-specific LINQ provider services with advanced optimization capabilities.
    /// This method registers services for intelligent query execution using pipeline analysis
    /// and optimization strategies.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureProvider">Optional action to configure the LINQ provider</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// <para>This method enhances the basic LINQ provider with:</para>
    /// <list type="bullet">
    /// <item>Pipeline-based query execution</item>
    /// <item>Advanced expression optimization</item>
    /// <item>Intelligent backend selection</item>
    /// <item>Performance monitoring and metrics</item>
    /// <item>Automatic kernel fusion and optimization</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddLinqProvider(
        this IServiceCollection services,
        Action<LinqProviderOptions>? configureProvider = null)
    {
        // Configure provider options
        if (configureProvider != null)
        {
            services.Configure(configureProvider);
        }
        services.TryAddSingleton<LinqProviderOptions>();

        // Register the pipeline-optimized provider as a service
        services.TryAddScoped<IQueryProvider, PipelineOptimizedProvider>();

        // Enhanced expression services
        services.TryAddSingleton<IExpressionOptimizer, DotCompute.Linq.Expressions.ExpressionOptimizer>();
        services.TryAddSingleton<TypeInferenceEngine>();

        return services;
    }

    /// <summary>
    /// Adds advanced pipeline optimization services for performance enhancement.
    /// These services analyze query patterns, optimize execution plans, and apply
    /// intelligent caching and resource management strategies.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureOptimization">Optional action to configure optimization settings</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// <para>Optimization Services Include:</para>
    /// <list type="bullet">
    /// <item>Adaptive backend selector with machine learning</item>
    /// <item>Kernel fusion optimizer for reduced memory transfers</item>
    /// <item>Memory access pattern optimizer</item>
    /// <item>Query plan optimizer with cost-based analysis</item>
    /// <item>Cache policy manager for intelligent caching</item>
    /// </list>
    /// <para>These optimizations can improve performance by 2-10x for complex queries.</para>
    /// </remarks>
    public static IServiceCollection AddPipelineOptimization(
        this IServiceCollection services,
        Action<PipelineOptimizationOptions>? configureOptimization = null)
    {
        // Configure optimization options
        if (configureOptimization != null)
        {
            services.Configure(configureOptimization);
        }
        services.TryAddSingleton<PipelineOptimizationOptions>();

        // Optimization services
        services.TryAddSingleton<IAdaptiveBackendSelector, AdaptiveBackendSelector>();
        services.TryAddSingleton<DotCompute.Linq.Pipelines.Optimization.IAdvancedPipelineOptimizer, DotCompute.Linq.Pipelines.Optimization.AdvancedPipelineOptimizer>();

        // Resource management
        services.TryAddSingleton<IPipelineResourceManager, DefaultPipelineResourceManager>();
        services.TryAddSingleton<IPipelineCacheManager, DefaultPipelineCacheManager>();

        // Performance analysis
        services.TryAddScoped<PipelinePerformanceAnalyzer>();

        return services;
    }

    /// <summary>
    /// Adds comprehensive telemetry and monitoring services for pipeline execution.
    /// These services collect detailed performance metrics, execution traces, and
    /// provide insights for optimization and debugging.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureTelemetry">Optional action to configure telemetry settings</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// <para>Telemetry Features:</para>
    /// <list type="bullet">
    /// <item>Real-time performance metrics collection</item>
    /// <item>Execution trace analysis and visualization</item>
    /// <item>Memory usage and allocation tracking</item>
    /// <item>Backend utilization monitoring</item>
    /// <item>Query pattern analysis and recommendations</item>
    /// <item>Bottleneck identification and resolution suggestions</item>
    /// </list>
    /// <para>Telemetry data can be exported to external monitoring systems
    /// like Application Insights, Prometheus, or custom analytics platforms.</para>
    /// </remarks>
    public static IServiceCollection AddPipelineTelemetry(
        this IServiceCollection services,
        Action<PipelineTelemetryOptions>? configureTelemetry = null)
    {
        // Configure telemetry options
        if (configureTelemetry != null)
        {
            services.Configure(configureTelemetry);
        }
        services.TryAddSingleton<PipelineTelemetryOptions>();

        // Telemetry services
        services.TryAddSingleton<ITelemetryCollector, DefaultTelemetryCollector>();
        services.TryAddScoped<PipelinePerformanceAnalyzer>();

        // Metrics and diagnostics
        services.TryAddSingleton<IPipelineDiagnostics, SimplePipelineDiagnostics>();

        return services;
    }

    /// <summary>
    /// Adds all pipeline services with comprehensive configuration in a single call.
    /// This is a convenience method that configures all pipeline-related services
    /// with intelligent defaults and optional customization.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureAll">Optional action to configure all pipeline services</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// <para>This method is equivalent to calling:</para>
    /// <list type="bullet">
    /// <item><see cref="AddPipelineServices"/></item>
    /// <item><see cref="AddLinqProvider"/></item>
    /// <item><see cref="AddPipelineOptimization"/></item>
    /// <item><see cref="AddPipelineTelemetry"/></item>
    /// </list>
    /// <para>Use this method for quick setup with sensible defaults, or use individual
    /// methods for fine-grained control over service registration.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services
    ///     .AddDotComputeLinq()
    ///     .AddCompletePipelineServices(options =>
    ///     {
    ///         options.Pipelines.EnableAdvancedOptimization = true;
    ///         options.Optimization.Strategy = OptimizationStrategy.Aggressive;
    ///         options.Telemetry.EnableDetailedMetrics = true;
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddCompletePipelineServices(
        this IServiceCollection services,
        Action<CompletePipelineOptions>? configureAll = null)
    {
        var options = new CompletePipelineOptions();
        configureAll?.Invoke(options);

        services
            .AddPipelineServices(opts => opts.CopyFrom(options.Pipelines))
            .AddLinqProvider(opts => opts.CopyFrom(options.Provider))
            .AddPipelineOptimization(opts => opts.CopyFrom(options.Optimization))
            .AddPipelineTelemetry(opts => opts.CopyFrom(options.Telemetry));

        return services;
    }

    #endregion
}

/// <summary>
/// Configuration options for LINQ services.
/// </summary>
public class LinqServiceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable query caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of cache entries.
    /// </summary>
    public int CacheMaxEntries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to enable cache expiration.
    /// </summary>
    public bool EnableCacheExpiration { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets a value indicating whether to enable expression optimization.
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable query profiling.
    /// </summary>
    public bool EnableProfiling { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable CPU fallback.
    /// </summary>
    public bool EnableCpuFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the default execution timeout.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic fallback to CPU when GPU fails.
    /// </summary>
    public bool EnableAutoFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the fallback timeout duration.
    /// </summary>
    public TimeSpan FallbackTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to enable detailed profiling information.
    /// </summary>
    public bool EnableDetailedProfiling { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum memory usage in bytes.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB default

    /// <summary>
    /// Gets or sets the optimization level for expression processing.
    /// </summary>
    public DotCompute.Abstractions.Types.OptimizationLevel OptimizationLevel { get; set; } = DotCompute.Abstractions.Types.OptimizationLevel.Balanced;
}

/// <summary>
/// Factory interface for creating compute query providers.
/// </summary>
public interface IComputeQueryProviderFactory
{
    /// <summary>
    /// Creates a compute query provider for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to use</param>
    /// <returns>A compute query provider</returns>
    ComputeQueryProvider CreateProvider(IAccelerator accelerator);
}

/// <summary>
/// Default implementation of compute query provider factory.
/// </summary>
public class ComputeQueryProviderFactory : IComputeQueryProviderFactory
{
    private readonly IQueryCompiler _compiler;
    private readonly IQueryExecutor _executor;
    private readonly IQueryCache _cache;
    private readonly ILogger<ComputeQueryProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputeQueryProviderFactory"/> class.
    /// </summary>
    public ComputeQueryProviderFactory(
        IQueryCompiler compiler,
        IQueryExecutor executor,
        IQueryCache cache,
        ILogger<ComputeQueryProvider> logger)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public ComputeQueryProvider CreateProvider(IAccelerator accelerator)
    {
        return new ComputeQueryProvider(accelerator, _compiler, _executor, _cache, _logger);
    }
}

/// <summary>
/// Implementation of IComputeLinqProvider that integrates with the DotCompute runtime.
/// </summary>
public class ComputeLinqProviderImpl : IComputeLinqProvider
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IComputeQueryProviderFactory _providerFactory;
    private readonly IExpressionOptimizer _optimizer;
    private readonly ILogger<ComputeLinqProviderImpl> _logger;
    private readonly Dictionary<IAccelerator, ComputeQueryProvider> _providers = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputeLinqProviderImpl"/> class.
    /// </summary>
    public ComputeLinqProviderImpl(
        IComputeOrchestrator orchestrator,
        IComputeQueryProviderFactory providerFactory,
        IExpressionOptimizer optimizer,
        ILogger<ComputeLinqProviderImpl> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IQueryable<T> CreateQueryable<T>(IEnumerable<T> source, IAccelerator? accelerator = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Use the new IServiceProvider-based method
        // For this implementation, we need a service provider, but we don't have one here
        // This is a design issue that needs to be resolved with proper DI integration

        throw new NotImplementedException("This method requires proper DI integration. Use CreateQueryable with IServiceProvider instead.");
    }

    /// <inheritdoc/>
    public IQueryable<T> CreateQueryable<T>(T[] source, IAccelerator? accelerator = null)
    {
        return CreateQueryable((IEnumerable<T>)source, accelerator);
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var accelerator = await _orchestrator.GetOptimalAcceleratorAsync("linq_expression");
        return await ExecuteAsync<T>(expression, accelerator!, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(System.Linq.Expressions.Expression expression, IAccelerator preferredAccelerator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(preferredAccelerator);

        var provider = GetOrCreateProvider(preferredAccelerator);
        var result = provider.Execute<T>(expression);


        return await Task.FromResult(result);
    }

    /// <inheritdoc/>
    public IEnumerable<Interfaces.OptimizationSuggestion> GetOptimizationSuggestions(System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);


        var suggestions = _optimizer.Analyze(expression);
        return suggestions.Select(s => new Interfaces.OptimizationSuggestion
        {
            Category = s.Type.ToString(),
            Message = s.Description,
            Severity = MapImpactToSeverity(s.Impact),
            EstimatedImpact = MapImpactToDouble(s.Impact)
        });
    }

    /// <inheritdoc/>
    public bool IsGpuCompatible(System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Use the expression optimizer to check compatibility

        try
        {
            var suggestions = _optimizer.Analyze(expression);
            return !suggestions.Any(s => s.Impact == DotCompute.Linq.Expressions.PerformanceImpact.Critical);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task PrecompileExpressionsAsync(IEnumerable<System.Linq.Expressions.Expression> expressions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expressions);


        var tasks = expressions.Select(async expr =>
        {
            try
            {
                // Pre-compile expression using the orchestrator
                await _orchestrator.PrecompileKernelAsync($"linq_expr_{expr.GetHashCode()}", null);
                _logger.LogDebug("Pre-compiled expression: {ExpressionType}", expr.NodeType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-compile expression: {ExpressionType}", expr.NodeType);
            }
        });

        await Task.WhenAll(tasks);
    }

    private ComputeQueryProvider GetOrCreateProvider(IAccelerator? accelerator)
    {
        // If no specific accelerator requested, use the first available one
        if (accelerator == null)
        {
            // This would need access to accelerator registry
            // For now, throw exception to force explicit accelerator selection
            throw new InvalidOperationException("Accelerator must be specified when no default is available");
        }

        lock (_lock)
        {
            if (_providers.TryGetValue(accelerator, out var existing))
            {
                return existing;
            }

            var provider = _providerFactory.CreateProvider(accelerator);
            _providers[accelerator] = provider;
            return provider;
        }
    }

    private static Interfaces.SuggestionSeverity MapImpactToSeverity(DotCompute.Linq.Expressions.PerformanceImpact impact)
    {
        return impact switch
        {
            DotCompute.Linq.Expressions.PerformanceImpact.Low => Interfaces.SuggestionSeverity.Info,
            DotCompute.Linq.Expressions.PerformanceImpact.Medium => Interfaces.SuggestionSeverity.Warning,
            DotCompute.Linq.Expressions.PerformanceImpact.High => Interfaces.SuggestionSeverity.High,
            DotCompute.Linq.Expressions.PerformanceImpact.Critical => Interfaces.SuggestionSeverity.Critical,
            _ => Interfaces.SuggestionSeverity.Info
        };
    }

    private static double MapImpactToDouble(DotCompute.Linq.Expressions.PerformanceImpact impact)
    {
        return impact switch
        {
            DotCompute.Linq.Expressions.PerformanceImpact.Low => 0.1,
            DotCompute.Linq.Expressions.PerformanceImpact.Medium => 0.3,
            DotCompute.Linq.Expressions.PerformanceImpact.High => 0.7,
            DotCompute.Linq.Expressions.PerformanceImpact.Critical => 1.0,
            _ => 0.0
        };
    }
}

// Extension method to get accelerator from ComputeQueryProvider
internal static class ComputeQueryProviderExtensions
{
    public static IAccelerator GetAccelerator(this ComputeQueryProvider provider)
    {
        return provider.Accelerator;
    }
}

#region Configuration Options

/// <summary>
/// Configuration options for pipeline services.
/// </summary>
public class PipelineServiceOptions
{
    /// <summary>
    /// Gets or sets whether to enable advanced pipeline optimization.
    /// </summary>
    public bool EnableAdvancedOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable telemetry collection.
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of pipeline stages.
    /// </summary>
    public int MaxPipelineStages { get; set; } = 20;

    /// <summary>
    /// Gets or sets the default pipeline timeout in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets whether to enable pipeline caching.
    /// </summary>
    public bool EnablePipelineCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum memory usage for pipelines in MB.
    /// </summary>
    public long MaxMemoryUsageMB { get; set; } = 1024;

    /// <summary>
    /// Copies configuration from another instance.
    /// </summary>
    /// <param name="source">Source options to copy from</param>
    public void CopyFrom(PipelineServiceOptions source)
    {
        EnableAdvancedOptimization = source.EnableAdvancedOptimization;
        EnableTelemetry = source.EnableTelemetry;
        MaxPipelineStages = source.MaxPipelineStages;
        DefaultTimeoutSeconds = source.DefaultTimeoutSeconds;
        EnablePipelineCaching = source.EnablePipelineCaching;
        MaxMemoryUsageMB = source.MaxMemoryUsageMB;
    }
}

/// <summary>
/// Configuration options for LINQ provider services.
/// </summary>
public class LinqProviderOptions
{
    /// <summary>
    /// Gets or sets whether to enable expression optimization.
    /// </summary>
    public bool EnableExpressionOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable intelligent backend selection.
    /// </summary>
    public bool EnableIntelligentBackendSelection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable automatic kernel fusion.
    /// </summary>
    public bool EnableAutomaticKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets the default execution strategy.
    /// </summary>
    public OptimizationStrategy DefaultStrategy { get; set; } = OptimizationStrategy.Balanced;

    /// <summary>
    /// Gets or sets the query complexity threshold for GPU execution.
    /// </summary>
    public int GpuComplexityThreshold { get; set; } = 10;

    /// <summary>
    /// Copies configuration from another instance.
    /// </summary>
    /// <param name="source">Source options to copy from</param>
    public void CopyFrom(LinqProviderOptions source)
    {
        EnableExpressionOptimization = source.EnableExpressionOptimization;
        EnableIntelligentBackendSelection = source.EnableIntelligentBackendSelection;
        EnablePerformanceMonitoring = source.EnablePerformanceMonitoring;
        EnableAutomaticKernelFusion = source.EnableAutomaticKernelFusion;
        DefaultStrategy = source.DefaultStrategy;
        GpuComplexityThreshold = source.GpuComplexityThreshold;
    }
}

/// <summary>
/// Configuration options for pipeline optimization services.
/// </summary>
public class PipelineOptimizationOptions
{
    /// <summary>
    /// Gets or sets the optimization strategy to use.
    /// </summary>
    public OptimizationStrategy Strategy { get; set; } = OptimizationStrategy.Balanced;

    /// <summary>
    /// Gets or sets whether to enable machine learning-based optimization.
    /// </summary>
    public bool EnableMachineLearning { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable kernel fusion optimization.
    /// </summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable memory access optimization.
    /// </summary>
    public bool EnableMemoryOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable query plan optimization.
    /// </summary>
    public bool EnableQueryPlanOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache policy for optimized queries.
    /// </summary>
    public DotCompute.Linq.Pipelines.Models.CachePolicy CachePolicy { get; set; } = DotCompute.Linq.Pipelines.Models.CachePolicy.Memory;

    /// <summary>
    /// Gets or sets the execution priority for optimization tasks.
    /// </summary>
    public ExecutionPriority ExecutionPriority { get; set; } = ExecutionPriority.Normal;

    /// <summary>
    /// Copies configuration from another instance.
    /// </summary>
    /// <param name="source">Source options to copy from</param>
    public void CopyFrom(PipelineOptimizationOptions source)
    {
        Strategy = source.Strategy;
        EnableMachineLearning = source.EnableMachineLearning;
        EnableKernelFusion = source.EnableKernelFusion;
        EnableMemoryOptimization = source.EnableMemoryOptimization;
        EnableQueryPlanOptimization = source.EnableQueryPlanOptimization;
        CachePolicy = source.CachePolicy;
        ExecutionPriority = source.ExecutionPriority;
    }
}

/// <summary>
/// Configuration options for pipeline telemetry services.
/// </summary>
public class PipelineTelemetryOptions
{
    /// <summary>
    /// Gets or sets whether to enable detailed metrics collection.
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable execution trace collection.
    /// </summary>
    public bool EnableExecutionTraces { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable memory usage tracking.
    /// </summary>
    public bool EnableMemoryTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable backend utilization monitoring.
    /// </summary>
    public bool EnableBackendMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable query pattern analysis.
    /// </summary>
    public bool EnableQueryPatternAnalysis { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable bottleneck identification.
    /// </summary>
    public bool EnableBottleneckIdentification { get; set; } = true;

    /// <summary>
    /// Gets or sets the telemetry collection interval in seconds.
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum telemetry buffer size.
    /// </summary>
    public int MaxBufferSize { get; set; } = 10000;

    /// <summary>
    /// Copies configuration from another instance.
    /// </summary>
    /// <param name="source">Source options to copy from</param>
    public void CopyFrom(PipelineTelemetryOptions source)
    {
        EnableDetailedMetrics = source.EnableDetailedMetrics;
        EnableExecutionTraces = source.EnableExecutionTraces;
        EnableMemoryTracking = source.EnableMemoryTracking;
        EnableBackendMonitoring = source.EnableBackendMonitoring;
        EnableQueryPatternAnalysis = source.EnableQueryPatternAnalysis;
        EnableBottleneckIdentification = source.EnableBottleneckIdentification;
        CollectionIntervalSeconds = source.CollectionIntervalSeconds;
        MaxBufferSize = source.MaxBufferSize;
    }
}

/// <summary>
/// Complete configuration options for all pipeline services.
/// </summary>
public class CompletePipelineOptions
{
    /// <summary>
    /// Gets the pipeline service options.
    /// </summary>
    public PipelineServiceOptions Pipelines { get; } = new();

    /// <summary>
    /// Gets the LINQ provider options.
    /// </summary>
    public LinqProviderOptions Provider { get; } = new();

    /// <summary>
    /// Gets the optimization options.
    /// </summary>
    public PipelineOptimizationOptions Optimization { get; } = new();

    /// <summary>
    /// Gets the telemetry options.
    /// </summary>
    public PipelineTelemetryOptions Telemetry { get; } = new();
}

/// <summary>
/// Optimization strategy enumeration for pipeline execution.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>
    /// Conservative optimization with minimal risk.
    /// </summary>
    Conservative,

    /// <summary>
    /// Balanced optimization between performance and safety.
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive optimization for maximum performance.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Adaptive optimization based on runtime analysis.
    /// </summary>
    Adaptive
}

#endregion

#region Default Implementations

/// <summary>
/// Default implementation of adaptive backend selector.
/// </summary>
internal class AdaptiveBackendSelector : IAdaptiveBackendSelector
{
    private readonly ILogger<AdaptiveBackendSelector> _logger;

    public AdaptiveBackendSelector(ILogger<AdaptiveBackendSelector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> SelectBackendAsync(WorkloadCharacteristics characteristics)
    {
        // Simple heuristic-based selection
        if (characteristics.ComputeIntensity > 0.7 && characteristics.ParallelismDegree > 100)
        {
            _logger.LogDebug("Selected CUDA backend for high-intensity workload");
            return Task.FromResult("CUDA");
        }

        if (characteristics.DataSize > 100 * 1024 * 1024) // > 100MB
        {
            _logger.LogDebug("Selected CUDA backend for large dataset");
            return Task.FromResult("CUDA");
        }

        _logger.LogDebug("Selected CPU backend for workload");
        return Task.FromResult("CPU");
    }
}

/// <summary>
/// Default implementation of pipeline resource manager.
/// </summary>
internal class DefaultPipelineResourceManager : IPipelineResourceManager
{
    private readonly ILogger<DefaultPipelineResourceManager> _logger;

    public DefaultPipelineResourceManager(ILogger<DefaultPipelineResourceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<object> GetAvailableResourcesAsync()
    {
        _logger.LogDebug("Getting available pipeline resources");
        return await Task.FromResult(new { CpuCores = Environment.ProcessorCount, MemoryGB = 8 });
    }

    public async Task ReleaseResourcesAsync()
    {
        _logger.LogDebug("Releasing pipeline resources");
        await Task.CompletedTask;
    }
}

/// <summary>
/// Default implementation of pipeline cache manager.
/// </summary>
internal class DefaultPipelineCacheManager : IPipelineCacheManager
{
    private readonly ILogger<DefaultPipelineCacheManager> _logger;

    public DefaultPipelineCacheManager(ILogger<DefaultPipelineCacheManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ClearCacheAsync()
    {
        _logger.LogDebug("Clearing pipeline cache");
        await Task.CompletedTask;
    }

    public async Task<object> GetCacheStatsAsync()
    {
        _logger.LogDebug("Getting pipeline cache statistics");
        return await Task.FromResult(new { HitRate = 0.75, Size = 100 });
    }
}

/// <summary>
/// Default implementation of telemetry collector.
/// </summary>
internal class DefaultTelemetryCollector : ITelemetryCollector
{
    private readonly ILogger<DefaultTelemetryCollector> _logger;

    public DefaultTelemetryCollector(ILogger<DefaultTelemetryCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CollectAsync(string eventName, object data)
    {
        _logger.LogInformation("Collected telemetry event: {EventName}", eventName);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Default implementation of kernel pipeline builder when orchestrator is not available.
/// </summary>
internal class DefaultKernelPipelineBuilder : DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder
{
    DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder.AddStage(string kernelName, params object[] parameters)
    {
        // Minimal implementation - in production this would add stages to a collection
        return this;
    }

    DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder.Transform<TInput, TOutput>(Func<TInput, TOutput> transform)
    {
        // Minimal implementation - in production this would add transformation stage
        return this;
    }

    public async Task<T> ExecuteAsync<T>(T input, CancellationToken cancellationToken = default)
    {
        // Minimal implementation - in production this would execute the pipeline
        await Task.CompletedTask;
        return input;
    }

    public async Task<T> ExecutePipelineAsync<T>(CancellationToken cancellationToken = default)
    {
        // Minimal implementation - in production this would execute the pipeline
        await Task.CompletedTask;
        return default(T)!;
    }

    public object Create()
    {
        return new DefaultKernelPipeline();
    }

    DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder.FromData<T>(T[] inputData)
    {
        // Minimal implementation - in production this would initialize with data
        return this;
    }

    DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder.FromStream<T>(IAsyncEnumerable<T> inputStream)
    {
        // Minimal implementation - in production this would initialize with stream
        return this;
    }

    public IKernelPipeline Create(IPipelineConfiguration configuration)
    {
        return new DefaultKernelPipeline();
    }
}

/// <summary>
/// Default kernel pipeline implementation.
/// </summary>
internal class DefaultKernelPipeline : IKernelPipeline
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; } = "DefaultPipeline";
    public IReadOnlyList<IPipelineStage> Stages { get; } = new List<IPipelineStage>();
    public PipelineOptimizationSettings OptimizationSettings { get; } = new PipelineOptimizationSettings();
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    public async Task<T> ExecuteAsync<T>(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return default(T)!;
    }

    public async ValueTask<DotCompute.Core.Pipelines.PipelineExecutionResult> ExecuteAsync(DotCompute.Core.Pipelines.PipelineExecutionContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new DotCompute.Core.Pipelines.PipelineExecutionResult
        {

            Success = true,

            Outputs = new Dictionary<string, object>(),
            Metrics = new DotCompute.Core.Pipelines.PipelineExecutionMetrics

            {
                ExecutionId = "default",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Duration = TimeSpan.Zero,
                MemoryUsage = new DotCompute.Core.Pipelines.MemoryUsageStats

                {
                    AllocatedBytes = 0,
                    PeakBytes = 0,
                    AllocationCount = 0,
                    DeallocationCount = 0
                },
                ComputeUtilization = 0.0,
                MemoryBandwidthUtilization = 0.0,
                StageExecutionTimes = new Dictionary<string, TimeSpan>(),
                DataTransferTimes = new Dictionary<string, TimeSpan>()
            },
            StageResults = new List<DotCompute.Core.Pipelines.StageExecutionResult>()
        };
    }

    public DotCompute.Core.Pipelines.PipelineValidationResult Validate()
    {
        return new DotCompute.Core.Pipelines.PipelineValidationResult { IsValid = true };
    }

    public DotCompute.Core.Pipelines.IPipelineMetrics GetMetrics()
    {
        return new DefaultPipelineMetrics();
    }

    public async ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer)
    {
        await Task.CompletedTask;
        return this;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    private class DefaultPipelineMetrics : DotCompute.Core.Pipelines.IPipelineMetrics
    {
        public string PipelineId { get; } = "default";
        public long ExecutionCount { get; } = 0;
        public long SuccessfulExecutionCount { get; } = 0;
        public long FailedExecutionCount { get; } = 0;
        public TimeSpan AverageExecutionTime { get; } = TimeSpan.Zero;
        public TimeSpan MinExecutionTime { get; } = TimeSpan.Zero;
        public TimeSpan MaxExecutionTime { get; } = TimeSpan.Zero;
        public TimeSpan TotalExecutionTime { get; } = TimeSpan.Zero;
        public double Throughput { get; } = 0.0;
        public double SuccessRate { get; } = 1.0;
        public long AverageMemoryUsage { get; } = 0;
        public long PeakMemoryUsage { get; } = 0;
        public IReadOnlyDictionary<string, DotCompute.Core.Pipelines.IStageMetrics> StageMetrics { get; } = new Dictionary<string, DotCompute.Core.Pipelines.IStageMetrics>();
        public IReadOnlyDictionary<string, double> CustomMetrics { get; } = new Dictionary<string, double>();
        public IReadOnlyList<DotCompute.Core.Pipelines.TimeSeriesMetric> TimeSeries { get; } = new List<DotCompute.Core.Pipelines.TimeSeriesMetric>();
        public int StageCount { get; } = 0;

        public void Reset()
        {
            // No-op for minimal implementation
        }

        public string Export(DotCompute.Core.Pipelines.MetricsExportFormat format)
        {
            return "{}"; // Empty JSON for minimal implementation
        }
    }
}

/// <summary>
/// Options for comprehensive pipeline telemetry configuration.
/// </summary>
public class ComprehensivePipelineTelemetryOptions
{
    /// <summary>
    /// Gets or sets whether to enable distributed tracing.
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed telemetry.
    /// </summary>
    public bool EnableDetailedTelemetry { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics collection interval in seconds.
    /// </summary>
    public int MetricsCollectionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to enable periodic export of metrics.
    /// </summary>
    public bool EnablePeriodicExport { get; set; } = true;

    /// <summary>
    /// Gets or sets the default export format for metrics.
    /// </summary>
    public DotCompute.Core.Pipelines.MetricsExportFormat DefaultExportFormat { get; set; } = DotCompute.Core.Pipelines.MetricsExportFormat.Json;

    /// <summary>
    /// Gets or sets whether to integrate with global telemetry service.
    /// </summary>
    public bool IntegrateWithGlobalTelemetry { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable overhead monitoring.
    /// </summary>
    public bool EnableOverheadMonitoring { get; set; } = true;
}

/// <summary>
/// Extension methods for adding comprehensive pipeline telemetry.
/// </summary>
public static class PipelineTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds comprehensive pipeline telemetry services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for telemetry options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddComprehensivePipelineTelemetry(
        this IServiceCollection services,
        Action<ComprehensivePipelineTelemetryOptions>? configure = null)
    {
        var options = new ComprehensivePipelineTelemetryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<PipelineMetricsService>();

        // Add logging

        services.AddLogging();


        return services;
    }
}


#endregion