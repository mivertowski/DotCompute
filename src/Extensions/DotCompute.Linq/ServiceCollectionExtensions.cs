using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Extensions;

/// <summary>
/// Extension methods for registering DotCompute LINQ services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DotCompute LINQ services to the service collection
    /// </summary>
    public static IServiceCollection AddDotComputeLinq(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add core services
        services.TryAddSingleton<IComputeLinqProvider, ComputeLinqProvider>();

        return services;
    }
}

/// <summary>
/// Interface for compute LINQ provider
/// </summary>
public interface IComputeLinqProvider
{
    /// <summary>
    /// Creates a compute-enabled queryable from the given source
    /// </summary>
    [RequiresUnreferencedCode("LINQ expressions may reference methods that could be trimmed.")]
    [RequiresDynamicCode("LINQ expression compilation requires dynamic code generation.")]
    public IQueryable<T> CreateComputeQueryable<T>(IEnumerable<T> source);
}

/// <summary>
/// Default implementation of compute LINQ provider
/// </summary>
internal sealed class ComputeLinqProvider : IComputeLinqProvider
{
    private readonly ILogger<ComputeLinqProvider> _logger;

    public ComputeLinqProvider(ILogger<ComputeLinqProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RequiresUnreferencedCode("LINQ expressions may reference methods that could be trimmed.")]
    [RequiresDynamicCode("LINQ expression compilation requires dynamic code generation.")]
    public IQueryable<T> CreateComputeQueryable<T>(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _logger.LogDebug("Creating compute queryable for type {Type}", typeof(T).Name);

        return source.AsQueryable().AsComputeQueryable();
    }
}
