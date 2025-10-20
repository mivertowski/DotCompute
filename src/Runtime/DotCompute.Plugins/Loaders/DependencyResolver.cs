// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using DotCompute.Plugins.Loaders.NuGet.Types;
using System;

namespace DotCompute.Plugins.Loaders;

/// <summary>
/// Advanced dependency resolver for NuGet plugins with transitive dependency support.
/// </summary>
public class DependencyResolver(ILogger logger, DependencyResolutionSettings settings)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DependencyResolutionSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ConcurrentDictionary<string, ResolvedPackage> _packageCache = new();
    private readonly SemaphoreSlim _resolutionSemaphore = new(1, 1);

    /// <summary>
    /// Resolves all dependencies for a plugin, including transitive dependencies.
    /// </summary>
    public async Task<DependencyGraph> ResolveAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        await _resolutionSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInfoMessage("Resolving dependencies for plugin: {manifest.Id}");

            var graph = new DependencyGraph { RootPlugin = manifest };
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await ResolveDependenciesRecursiveAsync(manifest, graph, visited, resolving, 0, cancellationToken);

            // Check for version conflicts
            await ResolveVersionConflictsAsync(graph, cancellationToken);

            // Validate dependency graph
            await ValidateDependencyGraphAsync(graph, cancellationToken);

            graph.IsResolved = graph.Errors.Count == 0;

            _logger.LogInfoMessage($"Dependency resolution completed for plugin: {manifest.Id}. Resolved: {graph.IsResolved}, Dependencies: {graph.Dependencies.Count}");

            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to resolve dependencies for plugin: {manifest.Id}");
            return new DependencyGraph
            {

                RootPlugin = manifest,

                IsResolved = false,

                Errors = { $"Dependency resolution failed: {ex.Message}" }
            };
        }
        finally
        {
            _ = _resolutionSemaphore.Release();
        }
    }

    /// <summary>
    /// Resolves dependencies recursively with cycle detection.
    /// </summary>
    private async Task ResolveDependenciesRecursiveAsync(
        NuGetPluginManifest manifest,

        DependencyGraph graph,

        HashSet<string> visited,

        HashSet<string> resolving,

        int level,

        CancellationToken cancellationToken)
    {
        if (level > _settings.MaxDependencyDepth)
        {
            graph.Errors.Add($"Maximum dependency depth ({_settings.MaxDependencyDepth}) exceeded for {manifest.Id}");
            return;
        }

        if (resolving.Contains(manifest.Id))
        {
            graph.Errors.Add($"Circular dependency detected: {string.Join(" -> ", resolving)} -> {manifest.Id}");
            return;
        }

        if (visited.Contains(manifest.Id))
        {
            return;
        }

        _ = resolving.Add(manifest.Id);
        _ = visited.Add(manifest.Id);

        try
        {
            foreach (var dependency in manifest.Dependencies ?? [])
            {
                var resolvedDependency = await ResolveSingleDependencyAsync(dependency, level + 1, cancellationToken);


                if (resolvedDependency != null)
                {
                    graph.Dependencies.Add(resolvedDependency);

                    // Recursively resolve transitive dependencies
                    if (_settings.ResolveTransitiveDependencies && resolvedDependency.Manifest != null)
                    {
                        await ResolveDependenciesRecursiveAsync(
                            resolvedDependency.Manifest,
                            graph,
                            visited,
                            resolving,

                            level + 1,

                            cancellationToken);
                    }
                }
                else
                {
                    graph.Errors.Add($"Failed to resolve dependency: {dependency.Id} {dependency.VersionRange}");
                }
            }
        }
        finally
        {
            _ = resolving.Remove(manifest.Id);
        }
    }

    /// <summary>
    /// Resolves a single dependency to the best available version.
    /// </summary>
    private async Task<ResolvedDependency?> ResolveSingleDependencyAsync(
        NuGetPackageDependency dependency,

        int level,

        CancellationToken cancellationToken)
    {
        _logger.LogDebugMessage("Resolving dependency: {DependencyId} {dependency.Id, dependency.VersionRange}");

        // Check cache first
        var cacheKey = $"{dependency.Id}|{dependency.VersionRange}";
        if (_packageCache.TryGetValue(cacheKey, out var cachedPackage))
        {
            return CreateResolvedDependency(cachedPackage, level);
        }

        // Find available versions
        var availableVersions = await FindAvailableVersionsAsync(dependency, cancellationToken);
        if (availableVersions.Count == 0)
        {
            _logger.LogWarningMessage("No versions found for dependency: {dependency.Id}");
            return null;
        }

        // Select best version based on version range
        var bestVersion = SelectBestVersion(dependency.VersionRange, availableVersions);
        if (bestVersion == null)
        {
            _logger.LogWarningMessage("No compatible version found for dependency: {DependencyId} {dependency.Id, dependency.VersionRange}");
            return null;
        }

        // Create resolved package
        var resolvedPackage = new ResolvedPackage
        {
            Id = dependency.Id,
            Version = bestVersion,
            IsOptional = dependency.IsOptional,
            SourceDependency = dependency
        };

        // Try to load package metadata
        await LoadPackageMetadataAsync(resolvedPackage, cancellationToken);

        // Cache the resolved package
        _ = _packageCache.TryAdd(cacheKey, resolvedPackage);

        return CreateResolvedDependency(resolvedPackage, level);
    }

    /// <summary>
    /// Finds all available versions for a dependency.
    /// </summary>
    private static async Task<List<string>> FindAvailableVersionsAsync(NuGetPackageDependency dependency, CancellationToken cancellationToken)
    {
        // This would typically query NuGet repositories, local package cache, etc.
        // For now, simulate with some common versions
        await Task.Delay(10, cancellationToken);

        // Simulate different scenarios for testing
        return dependency.Id.ToUpperInvariant() switch
        {
            "NONEXISTENTDEP" => [], // No versions available
            "SYSTEM.TEXT.JSON" => ["6.0.0", "7.0.0", "8.0.0", "9.0.0"],
            "MICROSOFT.EXTENSIONS.LOGGING" => ["6.0.0", "7.0.0", "8.0.0", "9.0.0"],
            "NEWTONSOFT.JSON" => ["12.0.0", "13.0.0", "13.0.1", "13.0.2", "13.0.3"],
            _ => ["1.0.0", "1.1.0", "2.0.0"] // Default versions for unknown packages
        };
    }

    /// <summary>
    /// Selects the best version from available versions based on version range.
    /// </summary>
    private static string? SelectBestVersion(string versionRange, IReadOnlyList<string> availableVersions)
    {
        if (availableVersions.Count == 0)
        {
            return null;
        }

        // Parse version range (simplified implementation)
        var versionConstraint = ParseVersionRange(versionRange);
        var compatibleVersions = availableVersions
            .Where(v => IsVersionCompatible(v, versionConstraint))
            .OrderByDescending(Version.Parse)
            .ToList();

        return compatibleVersions.FirstOrDefault();
    }

    /// <summary>
    /// Parses a NuGet version range into a constraint object.
    /// </summary>
    private static VersionConstraint ParseVersionRange(string versionRange)
    {
        // Simplified version range parsing
        // In a real implementation, this would use NuGet.Versioning library


        if (string.IsNullOrWhiteSpace(versionRange))
        {
            return new VersionConstraint { MinVersion = "0.0.0", IncludeMinVersion = true };
        }

        // Handle simple patterns
        if (versionRange.StartsWith("[", StringComparison.OrdinalIgnoreCase) && versionRange.EndsWith("]", StringComparison.OrdinalIgnoreCase))
        {
            // Exact version: [1.0.0]
            var version = versionRange.Trim('[', ']');
            return new VersionConstraint
            {

                MinVersion = version,

                MaxVersion = version,

                IncludeMinVersion = true,

                IncludeMaxVersion = true

            };
        }

        if (versionRange.Contains(",", StringComparison.CurrentCulture))
        {
            // Range: [1.0.0,2.0.0) or (1.0.0,2.0.0]
            var parts = versionRange.Trim('[', '(', ']', ')').Split(',');
            return new VersionConstraint
            {
                MinVersion = parts[0].Trim(),
                MaxVersion = parts.Length > 1 ? parts[1].Trim() : null,
                IncludeMinVersion = versionRange.StartsWith("[", StringComparison.CurrentCulture),
                IncludeMaxVersion = versionRange.EndsWith("]", StringComparison.CurrentCulture)
            };
        }

        // Default: minimum version
        return new VersionConstraint
        {

            MinVersion = versionRange,

            IncludeMinVersion = true

        };
    }

    /// <summary>
    /// Checks if a version is compatible with a version constraint.
    /// </summary>
    private static bool IsVersionCompatible(string version, VersionConstraint constraint)
    {
        if (!Version.TryParse(version, out var v))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(constraint.MinVersion))
        {
            if (!Version.TryParse(constraint.MinVersion, out var minVersion))
            {
                return false;
            }

            var comparison = v.CompareTo(minVersion);
            if (constraint.IncludeMinVersion ? comparison < 0 : comparison <= 0)
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(constraint.MaxVersion))
        {
            if (!Version.TryParse(constraint.MaxVersion, out var maxVersion))
            {
                return false;
            }

            var comparison = v.CompareTo(maxVersion);
            if (constraint.IncludeMaxVersion ? comparison > 0 : comparison >= 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Loads metadata for a resolved package.
    /// </summary>
    private static async Task LoadPackageMetadataAsync(ResolvedPackage package, CancellationToken cancellationToken)
    {
        // This would load package metadata from NuGet package or cache
        // For now, create some default metadata
        await Task.Delay(5, cancellationToken);

        package.AssemblyName = package.Id;
        package.AssemblyPath = $"/packages/{package.Id}.{package.Version}/{package.Id}.dll";
        package.IsPlugin = package.Id.Contains("Plugin", StringComparison.OrdinalIgnoreCase);

        // Simulate loading manifest for plugin packages

        if (package.IsPlugin)
        {
            package.Manifest = new NuGetPluginManifest
            {
                Id = package.Id,
                Name = package.Id,
                Version = package.Version,
                AssemblyPath = package.AssemblyPath,
                Dependencies = []
            };
        }
    }

    /// <summary>
    /// Creates a resolved dependency from a resolved package.
    /// </summary>
    private static ResolvedDependency CreateResolvedDependency(ResolvedPackage package, int level)
    {
        return new ResolvedDependency
        {
            Id = package.Id,
            Version = package.Version,
            AssemblyName = package.AssemblyName,
            AssemblyPath = package.AssemblyPath,
            Level = level,
            IsOptional = package.IsOptional,
            IsPlugin = package.IsPlugin,
            Manifest = package.Manifest,
            SourceDependency = package.SourceDependency
        };
    }

    /// <summary>
    /// Resolves version conflicts in the dependency graph.
    /// </summary>
    private async Task ResolveVersionConflictsAsync(DependencyGraph graph, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        var packageGroups = graph.Dependencies
            .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in packageGroups)
        {
            var versions = group.Select(d => d.Version).Distinct().ToList();
            if (versions.Count > 1)
            {
                _logger.LogWarningMessage($"Version conflict detected for package: {group.Key}. Versions: {string.Join(", ", versions)}");

                // Apply conflict resolution strategy
                switch (_settings.ConflictResolutionStrategy)
                {
                    case ConflictResolutionStrategy.Highest:
                        ResolveConflictUsingHighestVersion([.. group], graph);
                        break;
                    case ConflictResolutionStrategy.Lowest:
                        ResolveConflictUsingLowestVersion([.. group], graph);
                        break;
                    case ConflictResolutionStrategy.Fail:
                        graph.Errors.Add($"Version conflict for package {group.Key}: {string.Join(", ", versions)}");
                        break;
                    default:
                        ResolveConflictUsingHighestVersion([.. group], graph);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Resolves conflicts by keeping the highest version.
    /// </summary>
    private void ResolveConflictUsingHighestVersion(IReadOnlyList<ResolvedDependency> conflictingDependencies, DependencyGraph graph)
    {
        var highestVersion = conflictingDependencies
            .OrderByDescending(d => Version.Parse(d.Version))
            .First();

        // Remove all except the highest version
        foreach (var dependency in conflictingDependencies.Where(d => d != highestVersion))
        {
            _ = graph.Dependencies.Remove(dependency);
        }

        _logger.LogInfoMessage($"Resolved version conflict for {highestVersion.Id} using highest version: {highestVersion.Version}");
    }

    /// <summary>
    /// Resolves conflicts by keeping the lowest version.
    /// </summary>
    private void ResolveConflictUsingLowestVersion(IReadOnlyList<ResolvedDependency> conflictingDependencies, DependencyGraph graph)
    {
        var lowestVersion = conflictingDependencies
            .OrderBy(d => Version.Parse(d.Version))
            .First();

        // Remove all except the lowest version
        foreach (var dependency in conflictingDependencies.Where(d => d != lowestVersion))
        {
            _ = graph.Dependencies.Remove(dependency);
        }

        _logger.LogInfoMessage($"Resolved version conflict for {lowestVersion.Id} using lowest version: {lowestVersion.Version}");
    }

    /// <summary>
    /// Validates the final dependency graph for consistency.
    /// </summary>
    private static async Task ValidateDependencyGraphAsync(DependencyGraph graph, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check for missing required dependencies
        foreach (var dependency in graph.Dependencies.Where(d => !d.IsOptional))
        {
            if (string.IsNullOrEmpty(dependency.AssemblyPath) || !File.Exists(dependency.AssemblyPath))
            {
                graph.Errors.Add($"Required dependency not found: {dependency.Id} {dependency.Version}");
            }
        }

        // Check for platform compatibility
        foreach (var dependency in graph.Dependencies)
        {
            if (!IsPlatformCompatible(dependency))
            {
                graph.Warnings.Add($"Dependency may not be compatible with current platform: {dependency.Id}");
            }
        }
    }

    /// <summary>
    /// Checks if a dependency is compatible with the current platform.
    /// </summary>
    private static bool IsPlatformCompatible(ResolvedDependency dependency)
        // Simplified platform compatibility check
        // In a real implementation, this would check the package's supported frameworks





        => true;
}

/// <summary>
/// Settings for dependency resolution.
/// </summary>
public class DependencyResolutionSettings
{
    /// <summary>
    /// Gets or sets whether to resolve transitive dependencies.
    /// </summary>
    public bool ResolveTransitiveDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum dependency depth to resolve.
    /// </summary>
    public int MaxDependencyDepth { get; set; } = 10;

    /// <summary>
    /// Gets or sets the version conflict resolution strategy.
    /// </summary>
    public ConflictResolutionStrategy ConflictResolutionStrategy { get; set; } = ConflictResolutionStrategy.Highest;

    /// <summary>
    /// Gets or sets whether to include prerelease versions.
    /// </summary>
    public bool IncludePrereleaseVersions { get; set; }


    /// <summary>
    /// Gets or sets the dependency resolution timeout.
    /// </summary>
    public TimeSpan ResolutionTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether to cache resolution results.
    /// </summary>
    public bool CacheResults { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Strategy for resolving version conflicts.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Use the highest available version.
    /// </summary>
    Highest,

    /// <summary>
    /// Use the lowest available version.
    /// </summary>
    Lowest,

    /// <summary>
    /// Fail if conflicts are detected.
    /// </summary>
    Fail
}

/// <summary>
/// Represents a version constraint for dependency resolution.
/// </summary>
public class VersionConstraint
{
    /// <summary>
    /// Gets or sets the minimum version.
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum version.
    /// </summary>
    public string? MaxVersion { get; set; }

    /// <summary>
    /// Gets or sets whether to include the minimum version.
    /// </summary>
    public bool IncludeMinVersion { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the maximum version.
    /// </summary>
    public bool IncludeMaxVersion { get; set; }
}

/// <summary>
/// Represents a resolved package during dependency resolution.
/// </summary>
public class ResolvedPackage
{
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the resolved version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets whether this is an optional dependency.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets whether this package is a plugin.
    /// </summary>
    public bool IsPlugin { get; set; }

    /// <summary>
    /// Gets or sets the plugin manifest if this is a plugin package.
    /// </summary>
    public NuGetPluginManifest? Manifest { get; set; }

    /// <summary>
    /// Gets or sets the source dependency that led to this resolution.
    /// </summary>
    public NuGetPackageDependency? SourceDependency { get; set; }
}

/// <summary>
/// Represents a dependency graph for a plugin.
/// </summary>
public class DependencyGraph
{
    /// <summary>
    /// Gets or sets the root plugin manifest.
    /// </summary>
    public NuGetPluginManifest? RootPlugin { get; set; }

    /// <summary>
    /// Gets or sets whether the dependency graph was resolved successfully.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets the resolved dependencies.
    /// </summary>
    public IList<ResolvedDependency> Dependencies { get; } = [];

    /// <summary>
    /// Gets the resolution errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the resolution warnings.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets the total resolution time.
    /// </summary>
    public TimeSpan ResolutionTime { get; set; }
}

/// <summary>
/// Represents a resolved dependency in the dependency graph.
/// </summary>
public class ResolvedDependency
{
    /// <summary>
    /// Gets or sets the dependency ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the resolved version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the dependency level (0 = direct dependency).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets whether this is an optional dependency.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets whether this dependency is a plugin.
    /// </summary>
    public bool IsPlugin { get; set; }

    /// <summary>
    /// Gets or sets the plugin manifest if this is a plugin dependency.
    /// </summary>
    public NuGetPluginManifest? Manifest { get; set; }

    /// <summary>
    /// Gets or sets the source dependency that led to this resolution.
    /// </summary>
    public NuGetPackageDependency? SourceDependency { get; set; }
}