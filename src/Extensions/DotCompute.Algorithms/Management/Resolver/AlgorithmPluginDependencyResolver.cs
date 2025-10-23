
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Resolver;

/// <summary>
/// Resolves plugin dependencies and provides intelligent plugin selection based on requirements.
/// </summary>
public sealed partial class AlgorithmPluginDependencyResolver : IDisposable
{
    private readonly ILogger<AlgorithmPluginDependencyResolver> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly AlgorithmPluginRegistry _registry;
    private readonly ConcurrentDictionary<string, DependencyResolutionCache> _resolutionCache;
    private readonly ConcurrentDictionary<string, List<string>> _dependencyGraph;
    private readonly Timer? _cacheCleanupTimer;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the AlgorithmPluginDependencyResolver class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="registry">The registry.</param>

    public AlgorithmPluginDependencyResolver(
        ILogger<AlgorithmPluginDependencyResolver> logger,
        AlgorithmPluginManagerOptions options,
        AlgorithmPluginRegistry registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _resolutionCache = new ConcurrentDictionary<string, DependencyResolutionCache>();
        _dependencyGraph = new ConcurrentDictionary<string, List<string>>();

        // Setup cache cleanup timer
        _cacheCleanupTimer = new Timer(CleanupCache, null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    /// <summary>
    /// Resolves the best plugin for the given requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <returns>The best matching plugin if found; otherwise, null.</returns>
    public IAlgorithmPlugin? ResolvePlugin(PluginDependencyRequirements requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(requirements);

        // Check cache first
        var cacheKey = CreateCacheKey(requirements);
        if (_resolutionCache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiryTime > DateTime.UtcNow)
        {
            if (_registry.IsPluginRegistered(cached.PluginId))
            {
                LogResolutionCacheHit(cached.PluginId, requirements.ToString());
                return _registry.GetPlugin(cached.PluginId);
            }
            else
            {
                // Remove stale cache entry
                _ = _resolutionCache.TryRemove(cacheKey, out _);
            }
        }

        LogStartingResolution(requirements.ToString());

        // Find candidate plugins
        var candidates = FindCandidatePlugins(requirements);
        if (!candidates.Any())
        {
            LogNoPluginsFound(requirements.ToString());
            return null;
        }

        // Score and rank candidates
        var scoredCandidates = ScorePlugins(candidates, requirements);
        var bestPlugin = scoredCandidates.OrderByDescending(sc => sc.Score).First();

        // Cache the result
        _ = _resolutionCache.TryAdd(cacheKey, new DependencyResolutionCache
        {
            PluginId = bestPlugin.Plugin.Id,
            Requirements = requirements,
            ExpiryTime = DateTime.UtcNow.Add(_options.CacheExpiration),
            Score = bestPlugin.Score
        });

        LogResolutionSuccess(bestPlugin.Plugin.Id, bestPlugin.Score, requirements.ToString());
        return bestPlugin.Plugin;
    }

    /// <summary>
    /// Resolves multiple plugins for batch processing requirements.
    /// </summary>
    /// <param name="requirements">Collection of plugin requirements.</param>
    /// <returns>Dictionary mapping requirements to resolved plugins.</returns>
    public Dictionary<PluginDependencyRequirements, IAlgorithmPlugin?> ResolvePlugins(
        IEnumerable<PluginDependencyRequirements> requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(requirements);

        var results = new Dictionary<PluginDependencyRequirements, IAlgorithmPlugin?>();

        foreach (var requirement in requirements)
        {
            results[requirement] = ResolvePlugin(requirement);
        }

        return results;
    }

    /// <summary>
    /// Finds plugins with compatible output/input types for chaining operations.
    /// </summary>
    /// <param name="outputType">The output type from the first plugin.</param>
    /// <param name="requirements">Requirements for the second plugin.</param>
    /// <returns>Collection of compatible plugin chains.</returns>
    public IEnumerable<PluginChain> FindPluginChains(Type outputType, PluginDependencyRequirements requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(outputType);
        ArgumentNullException.ThrowIfNull(requirements);

        var chains = new List<PluginChain>();

        // Find plugins that can consume the output type
        var compatiblePlugins = _registry.GetPluginsByInputType(outputType);

        foreach (var plugin in compatiblePlugins)
        {
            // Check if plugin meets other requirements
            if (MeetsRequirements(plugin, requirements))
            {
                var chain = new PluginChain
                {
                    InputType = outputType,
                    Plugin = plugin,
                    OutputType = plugin.OutputType,
                    CompatibilityScore = CalculateCompatibilityScore(plugin, requirements)
                };

                chains.Add(chain);
            }
        }

        return chains.OrderByDescending(c => c.CompatibilityScore);
    }

    /// <summary>
    /// Analyzes plugin dependencies and detects potential conflicts.
    /// </summary>
    /// <param name="pluginId">The plugin ID to analyze.</param>
    /// <returns>Dependency analysis result.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
        Justification = "Plugin infrastructure requires dynamic assembly inspection for dependency analysis.")]
    public DependencyAnalysisResult AnalyzeDependencies(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var result = new DependencyAnalysisResult
        {
            PluginId = pluginId,
            AnalysisTime = DateTime.UtcNow
        };

        var plugin = _registry.GetPlugin(pluginId);
        if (plugin == null)
        {
            result.Errors.Add($"Plugin {pluginId} not found");
            return result;
        }

        try
        {
            // Analyze assembly dependencies
            var assembly = plugin.GetType().Assembly;
            var referencedAssemblies = assembly.GetReferencedAssemblies();

            foreach (var referencedAssembly in referencedAssemblies)
            {
                var dependency = new PluginDependency
                {
                    Name = referencedAssembly.Name ?? "Unknown",
                    Version = referencedAssembly.Version?.ToString() ?? "Unknown",
                    IsLoaded = IsAssemblyLoaded(referencedAssembly),
                    IsSystemAssembly = IsSystemAssembly(referencedAssembly.Name ?? "")
                };

                result.Dependencies.Add(dependency);

                // Check for version conflicts
                if (!dependency.IsSystemAssembly && dependency.IsLoaded)
                {
                    var loadedVersion = GetLoadedAssemblyVersion(referencedAssembly.Name ?? "");
                    if (loadedVersion != null && loadedVersion != referencedAssembly.Version)
                    {
                        result.Conflicts.Add($"Version conflict: {dependency.Name} requires {dependency.Version} but {loadedVersion} is loaded");
                    }
                }
            }

            // Analyze plugin-specific dependencies
            AnalyzePluginInterfaces(plugin, result);
            AnalyzeResourceDependencies(plugin, result);

            result.IsValid = result.Errors.Count == 0 && result.Conflicts.Count == 0;
            LogDependencyAnalysisCompleted(pluginId, result.Dependencies.Count, result.Conflicts.Count);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Analysis failed: {ex.Message}");
            LogDependencyAnalysisFailed(pluginId, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Gets plugin resolution statistics.
    /// </summary>
    /// <returns>Resolution statistics.</returns>
    public ResolutionStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var mostRequested = GetMostRequestedPlugins();
        return new ResolutionStatistics
        {
            TotalResolutions = _resolutionCache.Count,
            CacheHitRate = CalculateCacheHitRate(),
            AverageResolutionTime = CalculateAverageResolutionTime(),
            MostRequestedPlugins = mostRequested,
            DependencyGraphSize = _dependencyGraph.Count
        };
    }

    /// <summary>
    /// Finds candidate plugins based on requirements.
    /// </summary>
    private IEnumerable<IAlgorithmPlugin> FindCandidatePlugins(PluginDependencyRequirements requirements)
    {
        var candidates = _registry.GetHealthyPlugins();

        // Filter by accelerator type
        if (requirements.PreferredAcceleratorType.HasValue)
        {
            candidates = candidates.Where(p =>
                !p.SupportedAcceleratorTypes.IsDefaultOrEmpty &&
                p.SupportedAcceleratorTypes.Contains(requirements.PreferredAcceleratorType.Value));
        }

        // Filter by input type
        if (requirements.InputType != null)
        {
            candidates = candidates.Where(p =>
                !p.InputTypes.IsDefaultOrEmpty &&
                p.InputTypes.Contains(requirements.InputType));
        }

        // Filter by output type
        if (requirements.OutputType != null)
        {
            candidates = candidates.Where(p => p.OutputType == requirements.OutputType);
        }

        // Filter by category
        if (!string.IsNullOrEmpty(requirements.Category))
        {
            candidates = candidates.Where(p =>
                GetPluginCategory(p).Equals(requirements.Category, StringComparison.OrdinalIgnoreCase));
        }

        return candidates.ToList();
    }

    /// <summary>
    /// Scores plugins based on how well they match requirements.
    /// </summary>
    private IEnumerable<ScoredPlugin> ScorePlugins(IEnumerable<IAlgorithmPlugin> plugins, PluginDependencyRequirements requirements)
    {
        var scoredPlugins = new List<ScoredPlugin>();

        foreach (var plugin in plugins)
        {
            var score = CalculatePluginScore(plugin, requirements);
            scoredPlugins.Add(new ScoredPlugin { Plugin = plugin, Score = score });
        }

        return scoredPlugins;
    }

    /// <summary>
    /// Calculates a compatibility score for a plugin against requirements.
    /// </summary>
    private double CalculatePluginScore(IAlgorithmPlugin plugin, PluginDependencyRequirements requirements)
    {
        double score = 0.0;

        // Base score for being healthy and available
        score += 10.0;

        // Accelerator type match
        if (requirements.PreferredAcceleratorType.HasValue &&
            !plugin.SupportedAcceleratorTypes.IsDefaultOrEmpty &&
            plugin.SupportedAcceleratorTypes.Contains(requirements.PreferredAcceleratorType.Value))
        {
            score += 20.0;
        }

        // Exact type matches
        if (requirements.InputType != null &&
            !plugin.InputTypes.IsDefaultOrEmpty &&
            plugin.InputTypes.Contains(requirements.InputType))
        {
            score += 15.0;
        }

        if (requirements.OutputType != null && plugin.OutputType == requirements.OutputType)
        {
            score += 15.0;
        }

        // Performance considerations
        var performanceProfile = plugin.GetPerformanceProfile();
        if (requirements.PerformanceRequirements != null)
        {
            if (performanceProfile.MemoryRequirementMB <= requirements.PerformanceRequirements.MaxMemoryMB)
            {
                score += 10.0;
            }

            if (performanceProfile.EstimatedExecutionTimeMs <= requirements.PerformanceRequirements.MaxExecutionTime.TotalMilliseconds)
            {
                score += 10.0;
            }
        }

        // Category match
        if (!string.IsNullOrEmpty(requirements.Category) &&
            GetPluginCategory(plugin).Equals(requirements.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 5.0;
        }

        // Historical performance (execution count and success rate)
        var pluginInfo = _registry.GetLoadedPluginInfo(plugin.Id);
        if (pluginInfo != null)
        {
            if (pluginInfo.ExecutionCount > 0)
            {
                score += Math.Min(pluginInfo.ExecutionCount / 100.0 * 5.0, 5.0); // Max 5 points for experience
            }

            if (pluginInfo.LastError == null)
            {
                score += 5.0; // Bonus for no recent errors
            }
        }

        return score;
    }

    /// <summary>
    /// Checks if a plugin meets the specified requirements.
    /// </summary>
    private static bool MeetsRequirements(IAlgorithmPlugin plugin, PluginDependencyRequirements requirements)
    {
        // Check accelerator support
        if (requirements.PreferredAcceleratorType.HasValue &&
            (plugin.SupportedAcceleratorTypes.IsDefaultOrEmpty ||
             !plugin.SupportedAcceleratorTypes.Contains(requirements.PreferredAcceleratorType.Value)))
        {
            return false;
        }

        // Check input type compatibility
        if (requirements.InputType != null &&
            (plugin.InputTypes.IsDefaultOrEmpty ||
             !plugin.InputTypes.Contains(requirements.InputType)))
        {
            return false;
        }

        // Check output type match
        if (requirements.OutputType != null && plugin.OutputType != requirements.OutputType)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates compatibility score for plugin chaining.
    /// </summary>
    private double CalculateCompatibilityScore(IAlgorithmPlugin plugin, PluginDependencyRequirements requirements) => CalculatePluginScore(plugin, requirements);

    /// <summary>
    /// Gets the category for a plugin.
    /// </summary>
    private static string GetPluginCategory(IAlgorithmPlugin plugin)
    {
        var typeName = plugin.GetType().Name;
        var namespaceName = plugin.GetType().Namespace ?? "";

        if (namespaceName.Contains("Math", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Math", StringComparison.OrdinalIgnoreCase))
        {
            return "Mathematics";
        }

        if (namespaceName.Contains("Crypto", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Crypto", StringComparison.OrdinalIgnoreCase))
        {
            return "Cryptography";
        }

        return "General";
    }

    /// <summary>
    /// Creates a cache key for the requirements.
    /// </summary>
    private static string CreateCacheKey(PluginDependencyRequirements requirements)
    {
        var keyParts = new List<string>
        {
            requirements.PreferredAcceleratorType?.ToString() ?? "Any",
            requirements.InputType?.FullName ?? "Any",
            requirements.OutputType?.FullName ?? "Any",
            requirements.Category ?? "Any"
        };

        return string.Join("|", keyParts);
    }

    /// <summary>
    /// Analyzes plugin interface dependencies.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers",
        Justification = "Dependency resolution requires runtime type inspection of plugin instances")]
    private static void AnalyzePluginInterfaces(IAlgorithmPlugin plugin, DependencyAnalysisResult result)
    {
        var interfaces = plugin.GetType().GetInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface != typeof(IAlgorithmPlugin) && !iface.Assembly.GlobalAssemblyCache)
            {
                result.InterfaceDependencies.Add(iface.FullName ?? iface.Name);
            }
        }
    }

    /// <summary>
    /// Analyzes resource dependencies.
    /// </summary>
    private static void AnalyzeResourceDependencies(IAlgorithmPlugin plugin, DependencyAnalysisResult result)
    {
        // Check for required accelerator types
        if (plugin.SupportedAcceleratorTypes != null)
        {
            foreach (var acceleratorType in plugin.SupportedAcceleratorTypes)
            {
                result.ResourceDependencies.Add($"Accelerator: {acceleratorType}");
            }
        }
    }

    /// <summary>
    /// Checks if an assembly is loaded.
    /// </summary>
    private static bool IsAssemblyLoaded(AssemblyName assemblyName)
    {
        try
        {
            _ = Assembly.Load(assemblyName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines if an assembly is a system assembly.
    /// </summary>
    private static bool IsSystemAssembly(string assemblyName)
    {
        return assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the version of a loaded assembly.
    /// </summary>
    private static Version? GetLoadedAssemblyVersion(string assemblyName)
    {
        try
        {
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Equals(assemblyName, StringComparison.OrdinalIgnoreCase) == true);
            return loadedAssembly?.GetName().Version;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculates cache hit rate.
    /// </summary>
    private double CalculateCacheHitRate()
        // This would be tracked by actual cache hit/miss statistics in a real implementation

        => !_resolutionCache.IsEmpty ? 0.75 : 0.0; // Placeholder

    /// <summary>
    /// Calculates average resolution time.
    /// </summary>
    private static TimeSpan CalculateAverageResolutionTime()
        // This would be tracked by actual timing statistics in a real implementation

        => TimeSpan.FromMilliseconds(50); // Placeholder

    /// <summary>
    /// Gets the most requested plugins.
    /// </summary>
    private Dictionary<string, int> GetMostRequestedPlugins()
    {
        // This would be tracked by actual usage statistics in a real implementation
        return _resolutionCache.Values
            .GroupBy(c => c.PluginId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Cleans up expired cache entries.
    /// </summary>
    private void CleanupCache(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var expiredKeys = _resolutionCache
                .Where(kvp => kvp.Value.ExpiryTime <= DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _ = _resolutionCache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                LogCacheCleanup(expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            LogCacheCleanupError(ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _cacheCleanupTimer?.Dispose();
            _resolutionCache.Clear();
            _dependencyGraph.Clear();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for plugin {PluginId} with requirements: {Requirements}")]
    private partial void LogResolutionCacheHit(string pluginId, string requirements);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting plugin resolution for requirements: {Requirements}")]
    private partial void LogStartingResolution(string requirements);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No plugins found for requirements: {Requirements}")]
    private partial void LogNoPluginsFound(string requirements);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved plugin {PluginId} (score: {Score}) for requirements: {Requirements}")]
    private partial void LogResolutionSuccess(string pluginId, double score, string requirements);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dependency analysis completed for {PluginId}: {DependencyCount} dependencies, {ConflictCount} conflicts")]
    private partial void LogDependencyAnalysisCompleted(string pluginId, int dependencyCount, int conflictCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dependency analysis failed for {PluginId}: {Reason}")]
    private partial void LogDependencyAnalysisFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache cleanup removed {ExpiredCount} expired entries")]
    private partial void LogCacheCleanup(int expiredCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during cache cleanup")]
    private partial void LogCacheCleanupError(Exception ex);

    #endregion
}

/// <summary>
/// Requirements for plugin dependency resolution.
/// </summary>
public sealed class PluginDependencyRequirements
{
    /// <summary>
    /// Gets or sets the preferred accelerator type.
    /// </summary>
    public AcceleratorType? PreferredAcceleratorType { get; set; }

    /// <summary>
    /// Gets or sets the input data type.
    /// </summary>
    public Type? InputType { get; set; }

    /// <summary>
    /// Gets or sets the expected output type.
    /// </summary>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the plugin category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets performance requirements.
    /// </summary>
    public PerformanceRequirements? PerformanceRequirements { get; set; }
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


        if (OutputType != null)
        {
            parts.Add($"Output: {OutputType.Name}");
        }


        if (!string.IsNullOrEmpty(Category))
        {

            parts.Add($"Category: {Category}");
        }


        return string.Join(", ", parts);
    }
}

/// <summary>
/// Performance requirements for plugin selection.
/// </summary>
public sealed class PerformanceRequirements
{
    /// <summary>
    /// Gets or sets the maximum memory usage in MB.
    /// </summary>
    public long MaxMemoryMB { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Represents a plugin chain for sequential operations.
/// </summary>
public sealed class PluginChain
{
    /// <summary>
    /// Gets or sets the input type.
    /// </summary>
    public required Type InputType { get; init; }

    /// <summary>
    /// Gets or sets the plugin in the chain.
    /// </summary>
    public required IAlgorithmPlugin Plugin { get; init; }

    /// <summary>
    /// Gets or sets the output type.
    /// </summary>
    public required Type OutputType { get; init; }

    /// <summary>
    /// Gets or sets the compatibility score.
    /// </summary>
    public double CompatibilityScore { get; set; }
}

/// <summary>
/// Result of dependency analysis.
/// </summary>
public sealed class DependencyAnalysisResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    public DateTime AnalysisTime { get; set; }

    /// <summary>
    /// Gets or sets whether the analysis is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of dependencies.
    /// </summary>
    public IList<PluginDependency> Dependencies { get; } = [];

    /// <summary>
    /// Gets the list of dependency conflicts.
    /// </summary>
    public IList<string> Conflicts { get; } = [];

    /// <summary>
    /// Gets the list of analysis errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the list of interface dependencies.
    /// </summary>
    public IList<string> InterfaceDependencies { get; } = [];

    /// <summary>
    /// Gets the list of resource dependencies.
    /// </summary>
    public IList<string> ResourceDependencies { get; } = [];
}

/// <summary>
/// Represents a plugin dependency.
/// </summary>
public sealed class PluginDependency
{
    /// <summary>
    /// Gets or sets the dependency name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the dependency version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets whether the dependency is loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets whether this is a system assembly.
    /// </summary>
    public bool IsSystemAssembly { get; set; }
}

/// <summary>
/// Resolution statistics.
/// </summary>
public sealed class ResolutionStatistics
{
    /// <summary>
    /// Gets or sets the total number of resolutions.
    /// </summary>
    public int TotalResolutions { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the average resolution time.
    /// </summary>
    public TimeSpan AverageResolutionTime { get; set; }

    /// <summary>
    /// Gets or sets the most requested plugins.
    /// </summary>
    public Dictionary<string, int> MostRequestedPlugins { get; init; } = [];

    /// <summary>
    /// Gets or sets the dependency graph size.
    /// </summary>
    public int DependencyGraphSize { get; set; }
}

/// <summary>
/// Cached dependency resolution result.
/// </summary>
internal sealed class DependencyResolutionCache
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>
    public required string PluginId { get; init; }
    /// <summary>
    /// Gets or sets the requirements.
    /// </summary>
    /// <value>The requirements.</value>
    public required PluginDependencyRequirements Requirements { get; init; }
    /// <summary>
    /// Gets or sets the expiry time.
    /// </summary>
    /// <value>The expiry time.</value>
    public DateTime ExpiryTime { get; set; }
    /// <summary>
    /// Gets or sets the score.
    /// </summary>
    /// <value>The score.</value>
    public double Score { get; set; }
}

/// <summary>
/// Plugin with calculated score.
/// </summary>
internal sealed class ScoredPlugin
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
    public double Score { get; set; }
}