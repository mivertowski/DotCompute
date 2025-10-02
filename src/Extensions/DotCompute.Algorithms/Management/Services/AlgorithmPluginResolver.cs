// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Factories;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Services;

/// <summary>
/// Provides dependency resolution and plugin matching capabilities.
/// Handles plugin discovery based on requirements, compatibility checks, and optimal selection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AlgorithmPluginResolver"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="options">Configuration options.</param>
/// <param name="registry">The plugin registry.</param>
public sealed class AlgorithmPluginResolver(
    ILogger<AlgorithmPluginResolver> logger,
    AlgorithmPluginManagerOptions options,
    AlgorithmPluginRegistry registry) : IDisposable
{
    private readonly ILogger<AlgorithmPluginResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private bool _disposed;

    /// <summary>
    /// Resolves the best plugin for the given requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <returns>The best matching plugin if found; otherwise, null.</returns>
    public IAlgorithmPlugin? ResolvePlugin(PluginRequirements requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(requirements);

        _logger.LogDebug("Resolving plugin for requirements: {Requirements}", requirements);

        // Get all healthy plugins
        var candidates = _registry.GetHealthyPlugins().ToList();
        if (!candidates.Any())
        {
            _logger.LogWarning("No healthy plugins available for resolution");
            return null;
        }

        // Apply filtering based on requirements
        candidates = ApplyRequirementFilters(candidates, requirements);
        if (!candidates.Any())
        {
            _logger.LogInformation("No plugins match the specified requirements");
            return null;
        }

        // Score and rank candidates
        var scoredCandidates = ScorePlugins(candidates, requirements)
            .OrderByDescending(sc => sc.Score)
            .ToList();

        var bestPlugin = scoredCandidates.First().Plugin;
        _logger.LogInformation("Resolved plugin {PluginId} (score: {Score}) for requirements", 
            bestPlugin.Id, scoredCandidates.First().Score);

        return bestPlugin;
    }

    /// <summary>
    /// Resolves multiple plugins that match the given requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>Collection of matching plugins ordered by suitability.</returns>
    public IEnumerable<IAlgorithmPlugin> ResolvePlugins(PluginRequirements requirements, int maxResults = 10)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(requirements);

        _logger.LogDebug("Resolving up to {MaxResults} plugins for requirements", maxResults);

        var candidates = _registry.GetHealthyPlugins().ToList();
        if (!candidates.Any())
        {
            return Enumerable.Empty<IAlgorithmPlugin>();
        }

        // Apply filtering
        candidates = ApplyRequirementFilters(candidates, requirements);
        
        // Score and return top results
        var results = ScorePlugins(candidates, requirements)
            .OrderByDescending(sc => sc.Score)
            .Take(maxResults)
            .Select(sc => sc.Plugin)
            .ToList();

        _logger.LogInformation("Resolved {ResultCount} plugins for requirements", results.Count);
        return results;
    }

    /// <summary>
    /// Checks if a plugin is compatible with the given requirements.
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <param name="requirements">The requirements to check against.</param>
    /// <returns>Compatibility result with details.</returns>
    public PluginCompatibilityResult CheckCompatibility(IAlgorithmPlugin plugin, PluginRequirements requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(requirements);

        var result = new PluginCompatibilityResult
        {
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            IsCompatible = true
        };

        // Check accelerator type compatibility
        if (requirements.RequiredAcceleratorType.HasValue)
        {
            var hasAccelerator = plugin.SupportedAcceleratorTypes.Contains(requirements.RequiredAcceleratorType.Value);
            if (!hasAccelerator)
            {
                result.IsCompatible = false;
                result.IncompatibilityReasons.Add($"Does not support required accelerator: {requirements.RequiredAcceleratorType.Value}");
            }
            else
            {
                result.CompatibilityScore += 20;
            }
        }

        // Check input type compatibility
        if (requirements.InputType != null)
        {
            var supportsInputType = plugin.InputTypes.Contains(requirements.InputType);
            if (!supportsInputType)
            {
                result.IsCompatible = false;
                result.IncompatibilityReasons.Add($"Does not support input type: {requirements.InputType.Name}");
            }
            else
            {
                result.CompatibilityScore += 15;
            }
        }

        // Check output type compatibility
        if (requirements.OutputType != null)
        {
            var outputTypeMatches = requirements.OutputType.IsAssignableFrom(plugin.OutputType);
            if (!outputTypeMatches)
            {
                result.IsCompatible = false;
                result.IncompatibilityReasons.Add($"Output type {plugin.OutputType.Name} not compatible with required {requirements.OutputType.Name}");
            }
            else
            {
                result.CompatibilityScore += 15;
            }
        }

        // Check performance requirements
        if (requirements.PerformanceRequirements != null)
        {
            var performanceProfile = plugin.GetPerformanceProfile();
            var perfResult = CheckPerformanceCompatibility(performanceProfile, requirements.PerformanceRequirements);
            
            result.CompatibilityScore += perfResult.Score;
            if (!perfResult.IsAcceptable)
            {
                result.CompatibilityScore -= 10; // Penalty for poor performance match
                result.CompatibilityWarnings.Add(perfResult.Warning);
            }
        }

        // Check version compatibility
        if (requirements.MinimumVersion != null)
        {
            if (plugin.Version < requirements.MinimumVersion)
            {
                result.IsCompatible = false;
                result.IncompatibilityReasons.Add($"Version {plugin.Version} is below minimum required {requirements.MinimumVersion}");
            }
            else
            {
                result.CompatibilityScore += 5;
            }
        }

        if (requirements.MaximumVersion != null)
        {
            if (plugin.Version > requirements.MaximumVersion)
            {
                result.IsCompatible = false;
                result.IncompatibilityReasons.Add($"Version {plugin.Version} is above maximum allowed {requirements.MaximumVersion}");
            }
            else
            {
                result.CompatibilityScore += 5;
            }
        }

        // Check plugin health status
        var loadedPlugin = _registry.GetLoadedPlugin(plugin.Id);
        if (loadedPlugin != null)
        {
            switch (loadedPlugin.Health)
            {
                case PluginHealth.Healthy:
                    result.CompatibilityScore += 10;
                    break;
                case PluginHealth.Degraded:
                    result.CompatibilityScore -= 5;
                    result.CompatibilityWarnings.Add("Plugin is in degraded health state");
                    break;
                case PluginHealth.Critical:
                    result.IsCompatible = false;
                    result.IncompatibilityReasons.Add("Plugin is in critical health state");
                    break;
            }
        }

        // Add bonus for frequently used plugins
        if (loadedPlugin?.ExecutionCount > 100)
        {
            result.CompatibilityScore += 5;
        }

        return result;
    }

    /// <summary>
    /// Gets all plugins that support a specific accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAccelerator(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPluginsByAcceleratorType(acceleratorType);
    }

    /// <summary>
    /// Gets all plugins that can process a specific input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPluginsByInputType(inputType);
    }

    /// <summary>
    /// Resolves plugin dependencies and returns load order.
    /// </summary>
    /// <param name="pluginIds">Collection of plugin IDs to resolve dependencies for.</param>
    /// <returns>Ordered list of plugin IDs representing load order.</returns>
    public IEnumerable<string> ResolveDependencyOrder(IEnumerable<string> pluginIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pluginIds);

        var pluginIdList = pluginIds.ToList();
        var resolved = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var pluginId in pluginIdList)
        {
            if (!visited.Contains(pluginId))
            {
                ResolveDependenciesRecursive(pluginId, resolved, visited, visiting);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Applies requirement filters to the candidate list.
    /// </summary>
    private static List<IAlgorithmPlugin> ApplyRequirementFilters(IReadOnlyList<IAlgorithmPlugin> candidates, PluginRequirements requirements)
    {
        var filtered = candidates;

        // Filter by accelerator type
        if (requirements.RequiredAcceleratorType.HasValue)
        {
            filtered = [.. filtered.Where(p => p.SupportedAcceleratorTypes.Contains(requirements.RequiredAcceleratorType.Value))];
        }

        // Filter by input type
        if (requirements.InputType != null)
        {
            filtered = [.. filtered.Where(p => p.InputTypes.Contains(requirements.InputType))];
        }

        // Filter by output type
        if (requirements.OutputType != null)
        {
            filtered = [.. filtered.Where(p => requirements.OutputType.IsAssignableFrom(p.OutputType))];
        }

        // Filter by version requirements
        if (requirements.MinimumVersion != null)
        {
            filtered = [.. filtered.Where(p => p.Version >= requirements.MinimumVersion)];
        }

        if (requirements.MaximumVersion != null)
        {
            filtered = [.. filtered.Where(p => p.Version <= requirements.MaximumVersion)];
        }

        return filtered;
    }

    /// <summary>
    /// Scores plugins based on how well they match the requirements.
    /// </summary>
    private IEnumerable<ScoredPlugin> ScorePlugins(IReadOnlyList<IAlgorithmPlugin> candidates, PluginRequirements requirements)
    {
        foreach (var plugin in candidates)
        {
            var compatibilityResult = CheckCompatibility(plugin, requirements);
            var score = compatibilityResult.CompatibilityScore;

            // Additional scoring factors
            var loadedPlugin = _registry.GetLoadedPlugin(plugin.Id);
            if (loadedPlugin != null)
            {
                // Prefer plugins with better execution history
                if (loadedPlugin.ExecutionCount > 0)
                {
                    var averageExecutionTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;
                    score += Math.Max(0, 10 - (int)(averageExecutionTime / 1000)); // Bonus for faster plugins
                }

                // Penalty for recent errors
                if (loadedPlugin.LastError != null && 
                    DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromHours(1))
                {
                    score -= 15;
                }
            }

            yield return new ScoredPlugin { Plugin = plugin, Score = score };
        }
    }

    /// <summary>
    /// Checks performance compatibility between a plugin's profile and requirements.
    /// </summary>
    private static PerformanceCompatibilityResult CheckPerformanceCompatibility(
        PerformanceProfile profile, 
        PerformanceRequirements requirements)
    {
        var result = new PerformanceCompatibilityResult { IsAcceptable = true, Score = 0 };

        // Check memory requirements
        if (requirements.MaxMemoryUsage.HasValue && profile.MemoryRequirementMB > requirements.MaxMemoryUsage.Value)
        {
            result.IsAcceptable = false;
            result.Warning = $"Memory usage {profile.MemoryRequirementMB} exceeds maximum {requirements.MaxMemoryUsage}";
            return result;
        }

        // Score based on performance characteristics
        if (profile.EstimatedExecutionTimeMs <= requirements.MaxExecutionTime)
        {
            result.Score += 10;
        }
        else
        {
            result.Score -= 5;
            result.Warning = $"Execution time {profile.EstimatedExecutionTimeMs} exceeds preferred maximum {requirements.MaxExecutionTime}";
        }

        return result;
    }

    /// <summary>
    /// Recursively resolves plugin dependencies.
    /// </summary>
    private void ResolveDependenciesRecursive(string pluginId, IReadOnlyList<string> resolved, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visiting.Contains(pluginId))
        {
            throw new InvalidOperationException($"Circular dependency detected involving plugin {pluginId}");
        }

        if (visited.Contains(pluginId))
        {
            return;
        }

        _ = visiting.Add(pluginId);

        // Get plugin dependencies from metadata
        var loadedPlugin = _registry.GetLoadedPlugin(pluginId);
        if (loadedPlugin?.Metadata.Dependencies != null)
        {
            foreach (var dependency in loadedPlugin.Metadata.Dependencies)
            {
                ResolveDependenciesRecursive(dependency, resolved, visited, visiting);
            }
        }

        _ = visiting.Remove(pluginId);
        _ = visited.Add(pluginId);
        resolved.Add(pluginId);
    }

    /// <inheritdoc/>
    public void Dispose() => _disposed = true;

    /// <summary>
    /// Represents a plugin with its compatibility score.
    /// </summary>
    private sealed class ScoredPlugin
    {
        /// <summary>
        /// Gets or sets the plugin.
        /// </summary>
        /// <value>The plugin.</value>
        public required IAlgorithmPlugin Plugin { get; init; }
        /// <summary>
        /// Gets or sets the score.
        /// </summary>
        /// <value>The score.</value>
        public int Score { get; init; }
    }

    /// <summary>
    /// Represents performance compatibility check result.
    /// </summary>
    private sealed class PerformanceCompatibilityResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether acceptable.
        /// </summary>
        /// <value>The is acceptable.</value>
        public bool IsAcceptable { get; set; }
        /// <summary>
        /// Gets or sets the score.
        /// </summary>
        /// <value>The score.</value>
        public int Score { get; set; }
        /// <summary>
        /// Gets or sets the warning.
        /// </summary>
        /// <value>The warning.</value>
        public string Warning { get; set; } = string.Empty;
    }
}

/// <summary>
/// Represents requirements for plugin resolution.
/// </summary>
public sealed class PluginRequirements
{
    /// <summary>
    /// Gets or sets the required accelerator type.
    /// </summary>
    public AcceleratorType? RequiredAcceleratorType { get; set; }

    /// <summary>
    /// Gets or sets the input type the plugin must support.
    /// </summary>
    public Type? InputType { get; set; }

    /// <summary>
    /// Gets or sets the output type the plugin must produce.
    /// </summary>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the minimum version requirement.
    /// </summary>
    public Version? MinimumVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum version requirement.
    /// </summary>
    public Version? MaximumVersion { get; set; }

    /// <summary>
    /// Gets or sets performance requirements.
    /// </summary>
    public PerformanceRequirements? PerformanceRequirements { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var parts = new List<string>();
        
        if (RequiredAcceleratorType.HasValue)
        {
            parts.Add($"Accelerator: {RequiredAcceleratorType.Value}");
        }


        if (InputType != null)
        {
            parts.Add($"Input: {InputType.Name}");
        }


        if (OutputType != null)
        {
            parts.Add($"Output: {OutputType.Name}");
        }


        if (MinimumVersion != null)
        {
            parts.Add($"MinVersion: {MinimumVersion}");
        }


        if (MaximumVersion != null)
        {

            parts.Add($"MaxVersion: {MaximumVersion}");
        }


        return string.Join(", ", parts);
    }
}

/// <summary>
/// Represents performance requirements for plugin resolution.
/// </summary>
public sealed class PerformanceRequirements
{
    /// <summary>
    /// Gets or sets the maximum acceptable execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum acceptable memory usage in bytes.
    /// </summary>
    public long? MaxMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the minimum acceptable throughput (operations per second).
    /// </summary>
    public double? MinThroughput { get; set; }
}

/// <summary>
/// Represents the result of a plugin compatibility check.
/// </summary>
public sealed class PluginCompatibilityResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// Gets or sets whether the plugin is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets or sets the compatibility score (higher is better).
    /// </summary>
    public int CompatibilityScore { get; set; }

    /// <summary>
    /// Gets the list of incompatibility reasons.
    /// </summary>
    public IList<string> IncompatibilityReasons { get; } = [];

    /// <summary>
    /// Gets the list of compatibility warnings.
    /// </summary>
    public IList<string> CompatibilityWarnings { get; } = [];
}