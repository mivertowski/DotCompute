// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Infrastructure.Services;

#region Plugin Activator

/// <summary>
/// Service for creating plugin instances with dependency injection support.
/// </summary>
/// <remarks>
/// <para>
/// Provides factory methods for creating plugin instances, automatically
/// resolving constructor dependencies from the service provider.
/// </para>
/// <para>
/// Prefer using this service over manual instantiation to ensure proper
/// dependency injection and lifecycle management.
/// </para>
/// </remarks>
public interface IPluginActivator
{
    /// <summary>
    /// Creates an instance of the specified type with dependency injection.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <returns>The created instance with dependencies injected.</returns>
    public object CreateInstance(Type type);

    /// <summary>
    /// Creates an instance of the specified type with dependency injection.
    /// </summary>
    /// <typeparam name="T">The type to create.</typeparam>
    /// <returns>The created instance with dependencies injected.</returns>
    public T CreateInstance<T>() where T : class;
}

/// <summary>
/// Default implementation of plugin activator using ActivatorUtilities.
/// </summary>
/// <remarks>
/// Uses Microsoft.Extensions.DependencyInjection.ActivatorUtilities for
/// efficient instance creation with constructor injection.
/// </remarks>
internal sealed class PluginActivator(IServiceProvider serviceProvider) : IPluginActivator
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc/>
    public object CreateInstance(Type type)
    {
        return ActivatorUtilities.CreateInstance(_serviceProvider, type);
    }

    /// <inheritdoc/>
    public T CreateInstance<T>() where T : class
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }
}

#endregion

#region Plugin Validator

/// <summary>
/// Service for validating plugin instances before execution.
/// </summary>
/// <remarks>
/// <para>
/// Provides validation logic to ensure plugins meet required contracts
/// and are safe to execute.
/// </para>
/// <para>
/// Extend this interface to implement custom validation rules specific
/// to your plugin system requirements.
/// </para>
/// </remarks>
public interface IPluginValidator
{
    /// <summary>
    /// Validates a plugin instance asynchronously.
    /// </summary>
    /// <param name="plugin">The plugin instance to validate.</param>
    /// <returns>Validation result indicating success or failure with details.</returns>
    public Task<PluginValidationResult> ValidateAsync(object plugin);
}

/// <summary>
/// Default plugin validator implementation with basic null checking.
/// </summary>
/// <remarks>
/// Provides minimal validation. Override or replace with custom implementation
/// for domain-specific validation rules.
/// </remarks>
internal sealed class PluginValidator(ILogger<PluginValidator> logger) : IPluginValidator
{
    /// <inheritdoc/>
    public async Task<PluginValidationResult> ValidateAsync(object plugin)
    {
        await Task.CompletedTask;

        // Basic validation - can be extended with custom rules
        if (plugin == null)
        {
            return new PluginValidationResult
            {
                IsValid = false,
                ErrorMessage = "Plugin instance is null"
            };
        }

        return new PluginValidationResult { IsValid = true };
    }
}

/// <summary>
/// Result of plugin validation indicating success or failure.
/// </summary>
/// <remarks>
/// Contains validation status and optional error message for failed validations.
/// </remarks>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Gets or sets whether the plugin passed validation.
    /// </summary>
    /// <value>True if valid; false if validation failed.</value>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    /// <value>Error description, or null if validation succeeded.</value>
    public string? ErrorMessage { get; set; }
}

#endregion

#region Plugin Metrics

/// <summary>
/// Service for collecting and reporting plugin execution metrics.
/// </summary>
/// <remarks>
/// <para>
/// Tracks plugin activation counts, execution times, and other operational metrics
/// for monitoring and diagnostics.
/// </para>
/// <para>
/// Integrate with application performance monitoring (APM) systems for
/// production telemetry.
/// </para>
/// </remarks>
public interface IPluginMetrics
{
    /// <summary>
    /// Records a plugin activation event.
    /// </summary>
    /// <param name="pluginType">The type of plugin that was activated.</param>
    public void RecordActivation(Type pluginType);

    /// <summary>
    /// Records plugin execution time.
    /// </summary>
    /// <param name="pluginType">The type of plugin that executed.</param>
    /// <param name="executionTime">The execution duration.</param>
    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime);

    /// <summary>
    /// Gets aggregated plugin metrics.
    /// </summary>
    /// <returns>Current metrics data for all tracked plugins.</returns>
    public PluginMetricsData GetMetrics();
}

/// <summary>
/// Default implementation of plugin metrics using concurrent dictionary for thread-safe tracking.
/// </summary>
/// <remarks>
/// Maintains in-memory metrics with efficient concurrent updates.
/// For persistent metrics, integrate with external telemetry systems.
/// </remarks>
internal sealed class PluginMetrics : IPluginMetrics
{
    private readonly ConcurrentDictionary<string, PluginMetric> _metrics = new();

    /// <inheritdoc/>
    public void RecordActivation(Type pluginType)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { ActivationCount = 1 },
            (_, existing) => existing with { ActivationCount = existing.ActivationCount + 1 });
    }

    /// <inheritdoc/>
    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { TotalExecutionTime = executionTime, ExecutionCount = 1 },
            (_, existing) => existing with
            {
                TotalExecutionTime = existing.TotalExecutionTime + executionTime,
                ExecutionCount = existing.ExecutionCount + 1
            });
    }

    /// <inheritdoc/>
    public PluginMetricsData GetMetrics()
    {
        return new PluginMetricsData
        {
            PluginMetrics = _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CollectionTime = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Aggregated metrics data for all tracked plugins.
/// </summary>
/// <remarks>
/// Snapshot of current metrics state at time of collection.
/// </remarks>
public sealed class PluginMetricsData
{
    /// <summary>
    /// Gets or sets the metrics indexed by plugin type name.
    /// </summary>
    /// <value>Dictionary mapping plugin type names to their metrics.</value>
    public Dictionary<string, PluginMetric> PluginMetrics { get; init; } = [];

    /// <summary>
    /// Gets or sets when these metrics were collected.
    /// </summary>
    /// <value>Collection timestamp.</value>
    public DateTime CollectionTime { get; set; }
}

/// <summary>
/// Metrics for an individual plugin type.
/// </summary>
/// <remarks>
/// Immutable record containing activation count, execution count, timing data,
/// and computed averages.
/// </remarks>
public sealed record PluginMetric
{
    /// <summary>
    /// Gets the number of times this plugin was activated.
    /// </summary>
    /// <value>Activation count.</value>
    public int ActivationCount { get; init; }

    /// <summary>
    /// Gets the number of times this plugin executed.
    /// </summary>
    /// <value>Execution count (may differ from activation if some activations failed).</value>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total cumulative execution time.
    /// </summary>
    /// <value>Sum of all execution durations.</value>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets the average execution time per invocation.
    /// </summary>
    /// <value>Mean execution duration, or zero if no executions recorded.</value>
    public TimeSpan AverageExecutionTime
        => ExecutionCount > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / ExecutionCount) : TimeSpan.Zero;
}

#endregion
