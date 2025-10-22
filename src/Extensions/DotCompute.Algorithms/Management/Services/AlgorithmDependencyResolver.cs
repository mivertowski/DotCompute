
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using DotCompute.Plugins.Recovery;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Services;

/// <summary>
/// Resolves plugin dependencies and provides best-match plugin selection.
/// </summary>
public sealed partial class AlgorithmDependencyResolver(
    ILogger<AlgorithmDependencyResolver> logger,
    AlgorithmRegistry registry) : IDisposable
{
    private readonly ILogger<AlgorithmDependencyResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private bool _disposed;

    /// <summary>
    /// Resolves the best plugin for the given requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <returns>The best matching plugin if found; otherwise, null.</returns>
    public IAlgorithmPlugin? ResolvePlugin(AlgorithmRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var candidates = GetCandidatePlugins(requirements);
        if (!candidates.Any())
        {
            LogNoMatchingPlugins(requirements.ToString());
            return null;
        }

        var bestMatch = ScoreAndSelectBestMatch(candidates, requirements);
        if (bestMatch != null)
        {
            LogPluginSelected(bestMatch.Id, requirements.ToString());
        }

        return bestMatch;
    }

    /// <summary>
    /// Resolves multiple plugins that match the requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <returns>Collection of matching plugins ordered by score.</returns>
    public IEnumerable<IAlgorithmPlugin> ResolveMultiplePlugins(AlgorithmRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var candidates = GetCandidatePlugins(requirements);
        return ScorePlugins(candidates, requirements)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Checks if plugin dependencies are satisfied.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <returns>True if all dependencies are satisfied; otherwise, false.</returns>
    public bool AreDependenciesSatisfied(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId);
        if (loadedPlugin == null)
        {
            return false;
        }

        // Check if all dependencies are available
        foreach (var dependency in loadedPlugin.Metadata.Dependencies)
        {
            if (_registry.GetPlugin(dependency) == null)
            {
                LogMissingDependency(pluginId, dependency);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets dependency chain for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Ordered list of plugin IDs representing the dependency chain.</returns>
    public IReadOnlyList<string> GetDependencyChain(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var visited = new HashSet<string>();
        var dependencyChain = new List<string>();

        BuildDependencyChain(pluginId, visited, dependencyChain);

        return dependencyChain;
    }

    /// <summary>
    /// Detects circular dependencies in the plugin system.
    /// </summary>
    /// <returns>Collection of circular dependency chains found.</returns>
    public IEnumerable<List<string>> DetectCircularDependencies()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var circularDependencies = new List<List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var plugin in _registry.GetAllLoadedPlugins())
        {
            if (!visited.Contains(plugin.Plugin.Id))
            {
                var path = new List<string>();
                if (HasCircularDependency(plugin.Plugin.Id, visited, recursionStack, path))
                {
                    circularDependencies.Add([.. path]);
                }
            }
        }

        return circularDependencies;
    }

    /// <summary>
    /// Gets candidate plugins that match the basic requirements.
    /// </summary>
    private IEnumerable<IAlgorithmPlugin> GetCandidatePlugins(AlgorithmRequirements requirements)
    {
        var healthyPlugins = _registry.GetHealthyPlugins();

        var candidates = healthyPlugins.Where(plugin =>
        {
            // Check accelerator type compatibility
            if (requirements.PreferredAcceleratorType.HasValue &&
                !plugin.SupportedAcceleratorTypes.Contains(requirements.PreferredAcceleratorType.Value))
            {
                return false;
            }

            // Check input type compatibility
            if (requirements.InputType != null &&
                !plugin.InputTypes.Any(supportedType =>
                    supportedType.IsAssignableFrom(requirements.InputType) ||
                    requirements.InputType.IsAssignableFrom(supportedType)))
            {
                return false;
            }

            // Check output type compatibility
            if (requirements.ExpectedOutputType != null &&
                !IsOutputTypeCompatible(plugin, requirements.ExpectedOutputType))
            {
                return false;
            }

            // Check version requirements
            if (!string.IsNullOrEmpty(requirements.MinimumVersion) &&
                !IsVersionCompatible(plugin.Version, requirements.MinimumVersion))
            {
                return false;
            }

            // Check performance requirements
            if (requirements.MaxExecutionTime.HasValue &&
                !MeetsPerformanceRequirements(plugin, requirements.MaxExecutionTime.Value))
            {
                return false;
            }

            return true;
        });

        return candidates;
    }

    /// <summary>
    /// Scores plugins and selects the best match.
    /// </summary>
    private IAlgorithmPlugin? ScoreAndSelectBestMatch(IEnumerable<IAlgorithmPlugin> candidates, AlgorithmRequirements requirements)
    {
        var scores = ScorePlugins(candidates, requirements);
        return scores.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
    }

    /// <summary>
    /// Scores plugins based on how well they match the requirements.
    /// </summary>
    private Dictionary<IAlgorithmPlugin, double> ScorePlugins(IEnumerable<IAlgorithmPlugin> candidates, AlgorithmRequirements requirements)
    {
        var scores = new Dictionary<IAlgorithmPlugin, double>();

        foreach (var plugin in candidates)
        {
            var score = 0.0;

            // Score based on accelerator type preference (weight: 30%)
            if (requirements.PreferredAcceleratorType.HasValue &&
                plugin.SupportedAcceleratorTypes.Contains(requirements.PreferredAcceleratorType.Value))
            {
                score += 30.0;
            }

            // Score based on type compatibility (weight: 25%)
            if (requirements.InputType != null)
            {
                var compatibilityScore = CalculateTypeCompatibilityScore(plugin, requirements.InputType);
                score += compatibilityScore * 25.0;
            }

            // Score based on version (weight: 15%)
            if (!string.IsNullOrEmpty(requirements.MinimumVersion))
            {
                var versionScore = CalculateVersionScore(plugin.Version, requirements.MinimumVersion);
                score += versionScore * 15.0;
            }

            // Score based on performance history (weight: 20%)
            var performanceScore = CalculatePerformanceScore(plugin, requirements);
            score += performanceScore * 20.0;

            // Score based on reliability/health (weight: 10%)
            var reliabilityScore = CalculateReliabilityScore(plugin);
            score += reliabilityScore * 10.0;

            scores[plugin] = score;
        }

        return scores;
    }

    /// <summary>
    /// Calculates type compatibility score.
    /// </summary>
    private static double CalculateTypeCompatibilityScore(IAlgorithmPlugin plugin, Type inputType)
    {
        foreach (var supportedType in plugin.InputTypes)
        {
            if (supportedType == inputType)
            {
                return 1.0; // Perfect match
            }
            if (supportedType.IsAssignableFrom(inputType))
            {
                return 0.8; // Good compatibility
            }
            if (inputType.IsAssignableFrom(supportedType))
            {
                return 0.6; // Acceptable compatibility
            }
        }
        return 0.0;
    }

    /// <summary>
    /// Calculates version compatibility score.
    /// </summary>
    private static double CalculateVersionScore(Version pluginVersion, string minimumVersion)
    {
        if (!Version.TryParse(minimumVersion, out var minVersion))
        {
            return 0.5; // Default score for unparseable version
        }

        if (pluginVersion >= minVersion)
        {
            // Newer versions get higher scores, but not too much higher
            var versionDiff = pluginVersion.CompareTo(minVersion);
            return Math.Min(1.0, 0.8 + (versionDiff * 0.1));
        }

        return 0.0; // Version too old
    }

    /// <summary>
    /// Calculates performance score based on execution history.
    /// </summary>
    private double CalculatePerformanceScore(IAlgorithmPlugin plugin, AlgorithmRequirements requirements)
    {
        var loadedPlugin = _registry.GetLoadedPluginInfo(plugin.Id);
        if (loadedPlugin == null || loadedPlugin.ExecutionCount == 0)
        {
            return 0.5; // Default score for new plugins
        }

        var averageExecutionTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;

        if (requirements.MaxExecutionTime.HasValue)
        {
            var maxTime = requirements.MaxExecutionTime.Value.TotalMilliseconds;
            if (averageExecutionTime > maxTime)
            {
                return 0.0; // Exceeds performance requirements
            }

            // Score based on how much faster it is than the requirement
            return Math.Min(1.0, 1.0 - (averageExecutionTime / maxTime));
        }

        // Score based on general performance (faster is better)
        const double acceptableTime = 5000; // 5 seconds
        return Math.Max(0.0, Math.Min(1.0, 1.0 - (averageExecutionTime / acceptableTime)));
    }

    /// <summary>
    /// Calculates reliability score based on plugin health and error history.
    /// </summary>
    private double CalculateReliabilityScore(IAlgorithmPlugin plugin)
    {
        var loadedPlugin = _registry.GetLoadedPluginInfo(plugin.Id);
        if (loadedPlugin == null)
        {
            return 0.5; // Default score
        }

        // Score based on health status
        var healthScore = loadedPlugin.Health switch
        {
            PluginHealth.Healthy => 1.0,
            PluginHealth.Degraded => 0.7,
            PluginHealth.Critical => 0.3,
            PluginHealth.Unknown => 0.5,
            _ => 0.5
        };

        // Factor in error rate if there's execution history
        if (loadedPlugin.ExecutionCount > 0)
        {
            var errorRate = loadedPlugin.LastError != null ? 0.1 : 0.0; // Simplified error rate
            healthScore *= (1.0 - errorRate);
        }

        return healthScore;
    }

    /// <summary>
    /// Checks if the output type is compatible.
    /// </summary>
    private static bool IsOutputTypeCompatible(IAlgorithmPlugin plugin, Type expectedOutputType)
        // This would require additional metadata about plugin output types
        // For now, we'll assume compatibility

        => true;

    /// <summary>
    /// Checks if the plugin version meets the minimum requirement.
    /// </summary>
    private static bool IsVersionCompatible(Version pluginVersion, string minimumVersion)
    {
        if (!Version.TryParse(minimumVersion, out var minVersion))
        {
            return true; // If we can't parse, assume compatible
        }

        return pluginVersion >= minVersion;
    }

    /// <summary>
    /// Checks if the plugin meets performance requirements.
    /// </summary>
    private bool MeetsPerformanceRequirements(IAlgorithmPlugin plugin, TimeSpan maxExecutionTime)
    {
        var loadedPlugin = _registry.GetLoadedPluginInfo(plugin.Id);
        if (loadedPlugin == null || loadedPlugin.ExecutionCount == 0)
        {
            return true; // No history to check against
        }

        var averageExecutionTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;
        return averageExecutionTime <= maxExecutionTime.TotalMilliseconds;
    }

    /// <summary>
    /// Builds dependency chain recursively.
    /// </summary>
    private void BuildDependencyChain(string pluginId, HashSet<string> visited, List<string> dependencyChain)
    {
        if (visited.Contains(pluginId))
        {
            return;
        }

        _ = visited.Add(pluginId);
        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId);
        if (loadedPlugin == null)
        {
            return;
        }

        // Add dependencies first
        foreach (var dependency in loadedPlugin.Metadata.Dependencies)
        {
            BuildDependencyChain(dependency, visited, dependencyChain);
        }

        // Add this plugin after its dependencies
        dependencyChain.Add(pluginId);
    }

    /// <summary>
    /// Checks for circular dependencies using DFS.
    /// </summary>
    private bool HasCircularDependency(string pluginId, HashSet<string> visited, HashSet<string> recursionStack, List<string> path)
    {
        _ = visited.Add(pluginId);
        _ = recursionStack.Add(pluginId);
        path.Add(pluginId);

        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId);
        if (loadedPlugin != null)
        {
            foreach (var dependency in loadedPlugin.Metadata.Dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    if (HasCircularDependency(dependency, visited, recursionStack, path))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(dependency))
                {
                    // Found circular dependency
                    var circularStart = path.IndexOf(dependency);
                    path.RemoveRange(0, circularStart);
                    return true;
                }
            }
        }

        _ = recursionStack.Remove(pluginId);
        path.RemoveAt(path.Count - 1);
        return false;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Warning, Message = "No matching plugins found for requirements: {Requirements}")]
    private partial void LogNoMatchingPlugins(string requirements);

    [LoggerMessage(Level = LogLevel.Information, Message = "Selected plugin {PluginId} for requirements: {Requirements}")]
    private partial void LogPluginSelected(string pluginId, string requirements);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Missing dependency {Dependency} for plugin {PluginId}")]
    private partial void LogMissingDependency(string pluginId, string dependency);

    #endregion
}

/// <summary>
/// Requirements for algorithm dependency resolution.
/// </summary>
public sealed partial class AlgorithmRequirements
{
    /// <summary>
    /// Gets or sets the preferred accelerator type.
    /// </summary>
    /// <value>The preferred accelerator type.</value>
    public AcceleratorType? PreferredAcceleratorType { get; set; }
    /// <summary>
    /// Gets or sets the input type.
    /// </summary>
    /// <value>The input type.</value>
    public Type? InputType { get; set; }
    /// <summary>
    /// Gets or sets the expected output type.
    /// </summary>
    /// <value>The expected output type.</value>
    public Type? ExpectedOutputType { get; set; }
    /// <summary>
    /// Gets or sets the minimum version.
    /// </summary>
    /// <value>The minimum version.</value>
    public string? MinimumVersion { get; set; }
    /// <summary>
    /// Gets or sets the max execution time.
    /// </summary>
    /// <value>The max execution time.</value>
    public TimeSpan? MaxExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the require high reliability.
    /// </summary>
    /// <value>The require high reliability.</value>
    public bool RequireHighReliability { get; set; }
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override string ToString()
    {
        var parts = new List<string>();

        if (PreferredAcceleratorType.HasValue)
        {
            parts.Add($"Accelerator: {PreferredAcceleratorType}");
        }


        if (InputType != null)
        {
            parts.Add($"Input: {InputType.Name}");
        }


        if (ExpectedOutputType != null)
        {
            parts.Add($"Output: {ExpectedOutputType.Name}");
        }


        if (!string.IsNullOrEmpty(MinimumVersion))
        {
            parts.Add($"MinVersion: {MinimumVersion}");
        }


        if (MaxExecutionTime.HasValue)
        {
            parts.Add($"MaxTime: {MaxExecutionTime}");
        }


        if (RequireHighReliability)
        {

            parts.Add("HighReliability: true");
        }


        return string.Join(", ", parts);
    }
}