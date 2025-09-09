// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Operators;
using DotCompute.Linq.Providers;
using DotCompute.Linq.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<IExpressionOptimizer, ExpressionOptimizer>();
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
    private readonly Dictionary<IAccelerator, ComputeQueryProvider> _providers = new();
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
        
        var provider = GetOrCreateProvider(accelerator);
        var options = new ComputeQueryableExtensions.ComputeQueryOptions();
        
        return source.AsComputeQueryable(provider.GetAccelerator(), options);
    }

    /// <inheritdoc/>
    public IQueryable<T> CreateQueryable<T>(T[] source, IAccelerator? accelerator = null)
    {
        return CreateQueryable((IEnumerable<T>)source, accelerator);
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(System.Linq.Expressions.Expression expression)
    {
        var accelerator = await _orchestrator.GetOptimalAcceleratorAsync("linq_expression");
        return await ExecuteAsync<T>(expression, accelerator!);
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(System.Linq.Expressions.Expression expression, IAccelerator preferredAccelerator)
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
    public async Task PrecompileExpressionsAsync(IEnumerable<System.Linq.Expressions.Expression> expressions)
    {
        ArgumentNullException.ThrowIfNull(expressions);
        
        var tasks = expressions.Select(async expr =>
        {
            try
            {
                // Pre-compile expression using the orchestrator
                await _orchestrator.PrecompileKernelAsync($"linq_expr_{expr.GetHashCode()}");
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