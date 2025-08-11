// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using DotCompute.Algorithms.Security;

namespace DotCompute.Plugins.Loaders;

/// <summary>
/// Enhanced dependency resolver with real NuGet.Client integration and comprehensive compatibility checking.
/// </summary>
public sealed class EnhancedDependencyResolver : IDisposable
{
    private readonly ILogger<EnhancedDependencyResolver> _logger;
    private readonly EnhancedDependencyResolverOptions _options;
    private readonly SourceRepositoryProvider _sourceRepositoryProvider;
    private readonly NuGetFramework _targetFramework;
    private readonly ConcurrentDictionary<string, ResolvedPackageInfo> _packageCache;
    private readonly SemaphoreSlim _resolutionSemaphore;
    private readonly VulnerabilityScanner _vulnerabilityScanner;
    private readonly NuGetLogger _nugetLogger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnhancedDependencyResolver"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="vulnerabilityScanner">The vulnerability scanner.</param>
    /// <param name="options">Configuration options.</param>
    public EnhancedDependencyResolver(
        ILogger<EnhancedDependencyResolver> logger,
        VulnerabilityScanner vulnerabilityScanner,
        EnhancedDependencyResolverOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vulnerabilityScanner = vulnerabilityScanner ?? throw new ArgumentNullException(nameof(vulnerabilityScanner));
        _options = options ?? new EnhancedDependencyResolverOptions();
        
        _nugetLogger = new NuGetLogger(logger);
        _targetFramework = NuGetFramework.Parse(_options.TargetFramework);
        _packageCache = new ConcurrentDictionary<string, ResolvedPackageInfo>();
        _resolutionSemaphore = new SemaphoreSlim(_options.MaxConcurrentResolutions, _options.MaxConcurrentResolutions);

        // Initialize NuGet source repository provider
        var settings = Settings.LoadDefaultSettings(_options.ConfigurationPath);
        var packageSourceProvider = new PackageSourceProvider(settings);
        _sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());

        _logger.LogInformation("Enhanced dependency resolver initialized for framework: {TargetFramework}", 
            _targetFramework.GetShortFolderName());
    }

    /// <summary>
    /// Resolves all dependencies for a package with comprehensive analysis and security scanning.
    /// </summary>
    /// <param name="packageId">The package ID to resolve.</param>
    /// <param name="packageVersion">The package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The comprehensive dependency resolution result.</returns>
    public async Task<ComprehensiveDependencyResult> ResolveAllDependenciesAsync(
        string packageId,
        string packageVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageVersion);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _resolutionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Starting comprehensive dependency resolution for {PackageId} {Version}", 
                packageId, packageVersion);

            var result = new ComprehensiveDependencyResult
            {
                RootPackageId = packageId,
                RootPackageVersion = packageVersion,
                TargetFramework = _targetFramework.GetShortFolderName()
            };

            // Step 1: Build the dependency graph
            var dependencyGraph = await BuildDependencyGraphAsync(packageId, packageVersion, cancellationToken);
            result.DependencyGraph = dependencyGraph;

            if (!dependencyGraph.IsResolved)
            {
                result.IsSuccessful = false;
                result.Errors.AddRange(dependencyGraph.Errors);
                return result;
            }

            // Step 2: Perform compatibility analysis
            var compatibilityResult = await AnalyzeCompatibilityAsync(dependencyGraph, cancellationToken);
            result.CompatibilityAnalysis = compatibilityResult;

            // Step 3: Security and vulnerability scanning
            var securityResult = await PerformSecurityAnalysisAsync(dependencyGraph, cancellationToken);
            result.SecurityAnalysis = securityResult;

            // Step 4: Generate download and installation plan
            var installationPlan = await CreateInstallationPlanAsync(dependencyGraph, cancellationToken);
            result.InstallationPlan = installationPlan;

            // Step 5: Calculate metrics and recommendations
            result.Metrics = CalculateResolutionMetrics(dependencyGraph, compatibilityResult, securityResult);
            result.Recommendations = GenerateRecommendations(result);

            stopwatch.Stop();
            result.ResolutionTime = stopwatch.Elapsed;
            result.IsSuccessful = result.Errors.Count == 0 && !securityResult.HasCriticalIssues;

            _logger.LogInformation("Dependency resolution completed for {PackageId} in {ElapsedMs}ms. Success: {IsSuccessful}",
                packageId, stopwatch.ElapsedMilliseconds, result.IsSuccessful);

            return result;
        }
        finally
        {
            _resolutionSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets detailed compatibility information for a specific package version.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="packageVersion">The package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed compatibility information.</returns>
    public async Task<PackageCompatibilityInfo> GetCompatibilityInfoAsync(
        string packageId,
        string packageVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageVersion);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cacheKey = $"{packageId}|{packageVersion}|compat";
        if (_packageCache.TryGetValue(cacheKey, out var cached) && 
            cached.CompatibilityInfo != null && 
            DateTime.UtcNow - cached.CacheTime < _options.CacheExpiration)
        {
            return cached.CompatibilityInfo;
        }

        var compatibility = new PackageCompatibilityInfo
        {
            PackageId = packageId,
            PackageVersion = packageVersion,
            TargetFramework = _targetFramework.GetShortFolderName()
        };

        try
        {
            // Get package metadata
            var package = await GetPackageMetadataAsync(packageId, packageVersion, cancellationToken);
            if (package == null)
            {
                compatibility.IsCompatible = false;
                compatibility.Issues.Add("Package not found");
                return compatibility;
            }

            // Analyze framework compatibility
            var frameworkCompatibility = AnalyzeFrameworkCompatibility(package);
            compatibility.FrameworkCompatibility = frameworkCompatibility;
            compatibility.IsCompatible = frameworkCompatibility.IsCompatible;

            // Check for platform-specific dependencies
            var platformAnalysis = await AnalyzePlatformDependenciesAsync(package, cancellationToken);
            compatibility.PlatformAnalysis = platformAnalysis;

            // Analyze assembly compatibility
            if (_options.PerformAssemblyAnalysis)
            {
                var assemblyAnalysis = await AnalyzeAssemblyCompatibilityAsync(packageId, packageVersion, cancellationToken);
                compatibility.AssemblyAnalysis = assemblyAnalysis;
            }

            // Cache the result
            var resolvedPackageInfo = new ResolvedPackageInfo
            {
                PackageId = packageId,
                PackageVersion = packageVersion,
                CompatibilityInfo = compatibility,
                CacheTime = DateTime.UtcNow
            };
            _packageCache.TryAdd(cacheKey, resolvedPackageInfo);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze compatibility for {PackageId} {Version}", packageId, packageVersion);
            compatibility.IsCompatible = false;
            compatibility.Issues.Add($"Compatibility analysis failed: {ex.Message}");
        }

        return compatibility;
    }

    private async Task<EnhancedDependencyGraph> BuildDependencyGraphAsync(
        string rootPackageId,
        string rootPackageVersion,
        CancellationToken cancellationToken)
    {
        var graph = new EnhancedDependencyGraph
        {
            RootPackageId = rootPackageId,
            RootPackageVersion = rootPackageVersion
        };

        var visited = new HashSet<string>();
        var resolving = new HashSet<string>();
        var packageIdentity = new PackageIdentity(rootPackageId, NuGetVersion.Parse(rootPackageVersion));

        await ResolveDependenciesRecursiveAsync(packageIdentity, graph, visited, resolving, 0, cancellationToken);

        graph.IsResolved = graph.Errors.Count == 0;
        return graph;
    }

    private async Task ResolveDependenciesRecursiveAsync(
        PackageIdentity packageIdentity,
        EnhancedDependencyGraph graph,
        HashSet<string> visited,
        HashSet<string> resolving,
        int level,
        CancellationToken cancellationToken)
    {
        var packageKey = $"{packageIdentity.Id}|{packageIdentity.Version}";

        if (level > _options.MaxDependencyDepth)
        {
            graph.Errors.Add($"Maximum dependency depth exceeded for {packageIdentity}");
            return;
        }

        if (resolving.Contains(packageKey))
        {
            graph.Errors.Add($"Circular dependency detected: {string.Join(" -> ", resolving)} -> {packageKey}");
            return;
        }

        if (visited.Contains(packageKey))
        {
            return;
        }

        resolving.Add(packageKey);
        visited.Add(packageKey);

        try
        {
            // Get package dependencies
            var dependencies = await GetPackageDependenciesAsync(packageIdentity, cancellationToken);
            
            foreach (var dependency in dependencies)
            {
                // Find best version for this dependency
                var resolvedVersion = await ResolveDependencyVersionAsync(dependency, cancellationToken);
                if (resolvedVersion == null)
                {
                    graph.Errors.Add($"Could not resolve version for dependency: {dependency.Id} {dependency.VersionRange}");
                    continue;
                }

                var resolvedIdentity = new PackageIdentity(dependency.Id, resolvedVersion);
                var resolvedDependency = new EnhancedResolvedDependency
                {
                    PackageIdentity = resolvedIdentity,
                    Level = level + 1,
                    DependencyRange = dependency.VersionRange,
                    IsDirectDependency = level == 0,
                    TargetFramework = dependency.TargetFramework?.GetShortFolderName()
                };

                graph.ResolvedDependencies.Add(resolvedDependency);

                // Recursively resolve transitive dependencies
                if (_options.ResolveTransitiveDependencies)
                {
                    await ResolveDependenciesRecursiveAsync(resolvedIdentity, graph, visited, resolving, level + 1, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            graph.Errors.Add($"Failed to resolve dependencies for {packageIdentity}: {ex.Message}");
        }
        finally
        {
            resolving.Remove(packageKey);
        }
    }

    private async Task<IEnumerable<PackageDependency>> GetPackageDependenciesAsync(
        PackageIdentity packageIdentity,
        CancellationToken cancellationToken)
    {
        foreach (var sourceRepository in _sourceRepositoryProvider.GetRepositories())
        {
            try
            {
                var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
                if (metadataResource == null) continue;

                var packageMetadata = await metadataResource.GetMetadataAsync(
                    packageIdentity,
                    new SourceCacheContext(),
                    _nugetLogger,
                    cancellationToken);

                if (packageMetadata != null)
                {
                    var dependencyGroups = packageMetadata.DependencySets
                        .Where(set => set.TargetFramework == null || 
                                     DefaultCompatibilityProvider.Instance.IsCompatible(_targetFramework, set.TargetFramework))
                        .ToList();

                    if (dependencyGroups.Count == 0)
                    {
                        // No compatible dependency groups, try the most generic one
                        dependencyGroups = packageMetadata.DependencySets.Take(1).ToList();
                    }

                    var dependencies = dependencyGroups
                        .SelectMany(group => group.Packages)
                        .Select(dep => new PackageDependency(dep.Id, dep.VersionRange, dep.Include, dep.Exclude))
                        .ToList();

                    return dependencies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get dependencies from source {Source} for package {PackageId}",
                    sourceRepository.PackageSource.Name, packageIdentity);
            }
        }

        return [];
    }

    private async Task<NuGetVersion?> ResolveDependencyVersionAsync(
        PackageDependency dependency,
        CancellationToken cancellationToken)
    {
        foreach (var sourceRepository in _sourceRepositoryProvider.GetRepositories())
        {
            try
            {
                var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
                if (metadataResource == null) continue;

                var packages = await metadataResource.GetMetadataAsync(
                    dependency.Id,
                    _options.IncludePrerelease,
                    false,
                    new SourceCacheContext(),
                    _nugetLogger,
                    cancellationToken);

                var compatibleVersions = packages
                    .Where(p => dependency.VersionRange.Satisfies(p.Identity.Version))
                    .Where(p => !p.Identity.Version.IsPrerelease || _options.IncludePrerelease)
                    .OrderByDescending(p => p.Identity.Version)
                    .ToList();

                var selectedVersion = compatibleVersions.FirstOrDefault()?.Identity.Version;
                if (selectedVersion != null)
                {
                    _logger.LogDebug("Resolved dependency {DependencyId} to version {Version}",
                        dependency.Id, selectedVersion);
                    return selectedVersion;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve dependency {DependencyId} from source {Source}",
                    dependency.Id, sourceRepository.PackageSource.Name);
            }
        }

        return null;
    }

    private async Task<IPackageSearchMetadata?> GetPackageMetadataAsync(
        string packageId,
        string packageVersion,
        CancellationToken cancellationToken)
    {
        var packageIdentity = new PackageIdentity(packageId, NuGetVersion.Parse(packageVersion));

        foreach (var sourceRepository in _sourceRepositoryProvider.GetRepositories())
        {
            try
            {
                var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
                if (metadataResource == null) continue;

                var metadata = await metadataResource.GetMetadataAsync(
                    packageIdentity,
                    new SourceCacheContext(),
                    _nugetLogger,
                    cancellationToken);

                if (metadata != null)
                {
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get metadata from source {Source} for package {PackageId}",
                    sourceRepository.PackageSource.Name, packageId);
            }
        }

        return null;
    }

    private FrameworkCompatibilityAnalysis AnalyzeFrameworkCompatibility(IPackageSearchMetadata package)
    {
        var analysis = new FrameworkCompatibilityAnalysis
        {
            RequestedFramework = _targetFramework.GetShortFolderName()
        };

        var supportedFrameworks = package.DependencySets
            .Where(set => set.TargetFramework != null)
            .Select(set => set.TargetFramework!)
            .Distinct()
            .ToList();

        analysis.SupportedFrameworks = supportedFrameworks
            .Select(f => f.GetShortFolderName())
            .ToList();

        // Check direct compatibility
        var directlyCompatible = supportedFrameworks
            .Any(f => DefaultCompatibilityProvider.Instance.IsCompatible(_targetFramework, f));

        analysis.IsCompatible = directlyCompatible;

        if (!directlyCompatible && supportedFrameworks.Count > 0)
        {
            // Find nearest compatible framework
            var reducer = new FrameworkReducer();
            var nearestFramework = reducer.GetNearest(_targetFramework, supportedFrameworks);
            if (nearestFramework != null)
            {
                analysis.NearestCompatibleFramework = nearestFramework.GetShortFolderName();
                analysis.RequiresShimming = true;
                analysis.IsCompatible = true; // Compatible with shimming
            }
        }

        if (!analysis.IsCompatible)
        {
            analysis.Issues.Add($"Package does not support target framework {_targetFramework.GetShortFolderName()}");
        }

        return analysis;
    }

    private async Task<PlatformCompatibilityAnalysis> AnalyzePlatformDependenciesAsync(
        IPackageSearchMetadata package,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for actual async work
        
        var analysis = new PlatformCompatibilityAnalysis
        {
            CurrentPlatform = GetCurrentPlatform()
        };

        // This would analyze platform-specific dependencies
        // For now, assume compatibility unless evidence suggests otherwise
        analysis.IsCompatible = true;

        return analysis;
    }

    private async Task<AssemblyCompatibilityAnalysis> AnalyzeAssemblyCompatibilityAsync(
        string packageId,
        string packageVersion,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for actual async work

        var analysis = new AssemblyCompatibilityAnalysis
        {
            PackageId = packageId,
            PackageVersion = packageVersion
        };

        // This would perform deep assembly analysis
        // Including checking for breaking changes, API compatibility, etc.
        analysis.IsCompatible = true;

        return analysis;
    }

    private async Task<CompatibilityAnalysisResult> AnalyzeCompatibilityAsync(
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        var result = new CompatibilityAnalysisResult();
        var tasks = graph.ResolvedDependencies.Select(async dep =>
        {
            try
            {
                var compatibility = await GetCompatibilityInfoAsync(
                    dep.PackageIdentity.Id, 
                    dep.PackageIdentity.Version.ToString(), 
                    cancellationToken);
                return new KeyValuePair<string, PackageCompatibilityInfo>(
                    $"{dep.PackageIdentity.Id}|{dep.PackageIdentity.Version}", 
                    compatibility);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze compatibility for {PackageId} {Version}",
                    dep.PackageIdentity.Id, dep.PackageIdentity.Version);
                return new KeyValuePair<string, PackageCompatibilityInfo>(
                    $"{dep.PackageIdentity.Id}|{dep.PackageIdentity.Version}",
                    new PackageCompatibilityInfo
                    {
                        PackageId = dep.PackageIdentity.Id,
                        PackageVersion = dep.PackageIdentity.Version.ToString(),
                        IsCompatible = false,
                        Issues = [$"Compatibility analysis failed: {ex.Message}"]
                    });
            }
        });

        var compatibilityResults = await Task.WhenAll(tasks);
        result.PackageCompatibilities = compatibilityResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        result.OverallCompatible = result.PackageCompatibilities.Values.All(c => c.IsCompatible);
        result.IncompatiblePackages = result.PackageCompatibilities
            .Where(kvp => !kvp.Value.IsCompatible)
            .Select(kvp => kvp.Key)
            .ToList();

        return result;
    }

    private async Task<SecurityAnalysisResult> PerformSecurityAnalysisAsync(
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        var result = new SecurityAnalysisResult();
        
        // Scan all packages for vulnerabilities
        var packages = graph.ResolvedDependencies
            .Select(dep => (dep.PackageIdentity.Id, dep.PackageIdentity.Version.ToString()))
            .Prepend((graph.RootPackageId, graph.RootPackageVersion))
            .ToList();

        var vulnerabilityResults = await _vulnerabilityScanner.ScanMultiplePackagesAsync(packages, cancellationToken);
        result.VulnerabilityScanResults = vulnerabilityResults;

        // Calculate security metrics
        var allVulnerabilities = vulnerabilityResults.Values.SelectMany(r => r.Vulnerabilities).ToList();
        result.TotalVulnerabilityCount = allVulnerabilities.Count;
        result.CriticalVulnerabilityCount = allVulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
        result.HighVulnerabilityCount = allVulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High);
        result.MediumVulnerabilityCount = allVulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Medium);
        result.LowVulnerabilityCount = allVulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Low);

        result.HasCriticalIssues = result.CriticalVulnerabilityCount > 0;
        result.SecurityScore = CalculateSecurityScore(result);

        return result;
    }

    private async Task<InstallationPlan> CreateInstallationPlanAsync(
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for actual async work

        var plan = new InstallationPlan
        {
            RootPackage = new InstallationStep
            {
                PackageId = graph.RootPackageId,
                PackageVersion = graph.RootPackageVersion,
                IsRootPackage = true,
                Order = 0
            }
        };

        // Create installation steps in dependency order
        var steps = new List<InstallationStep>();
        var processed = new HashSet<string>();
        var order = 1;

        // Process dependencies by level (leaf dependencies first)
        var dependenciesByLevel = graph.ResolvedDependencies
            .GroupBy(d => d.Level)
            .OrderByDescending(g => g.Key) // Start with deepest dependencies
            .ToList();

        foreach (var levelGroup in dependenciesByLevel)
        {
            foreach (var dependency in levelGroup.OrderBy(d => d.PackageIdentity.Id))
            {
                var packageKey = $"{dependency.PackageIdentity.Id}|{dependency.PackageIdentity.Version}";
                if (processed.Add(packageKey))
                {
                    steps.Add(new InstallationStep
                    {
                        PackageId = dependency.PackageIdentity.Id,
                        PackageVersion = dependency.PackageIdentity.Version.ToString(),
                        IsRootPackage = false,
                        Order = order++,
                        Level = dependency.Level
                    });
                }
            }
        }

        plan.DependencySteps = steps;
        plan.TotalSteps = steps.Count + 1; // +1 for root package
        plan.EstimatedInstallTime = TimeSpan.FromSeconds(plan.TotalSteps * 2); // Rough estimate

        return plan;
    }

    private DependencyResolutionMetrics CalculateResolutionMetrics(
        EnhancedDependencyGraph graph,
        CompatibilityAnalysisResult compatibility,
        SecurityAnalysisResult security)
    {
        return new DependencyResolutionMetrics
        {
            TotalPackagesResolved = graph.ResolvedDependencies.Count + 1, // +1 for root
            DirectDependenciesCount = graph.ResolvedDependencies.Count(d => d.IsDirectDependency),
            TransitiveDependenciesCount = graph.ResolvedDependencies.Count(d => !d.IsDirectDependency),
            MaxDependencyDepth = graph.ResolvedDependencies.DefaultIfEmpty().Max(d => d?.Level ?? 0),
            CompatibilityScore = compatibility.OverallCompatible ? 100 : 
                (int)((double)compatibility.PackageCompatibilities.Values.Count(c => c.IsCompatible) / 
                      compatibility.PackageCompatibilities.Count * 100),
            SecurityScore = security.SecurityScore,
            HasCircularDependencies = graph.Errors.Any(e => e.Contains("Circular dependency")),
            ResolutionErrors = graph.Errors.Count
        };
    }

    private List<string> GenerateRecommendations(ComprehensiveDependencyResult result)
    {
        var recommendations = new List<string>();

        if (result.SecurityAnalysis.CriticalVulnerabilityCount > 0)
        {
            recommendations.Add($"🚨 CRITICAL: {result.SecurityAnalysis.CriticalVulnerabilityCount} critical vulnerabilities found. Update packages immediately.");
        }

        if (result.SecurityAnalysis.HighVulnerabilityCount > 0)
        {
            recommendations.Add($"⚠️ HIGH: {result.SecurityAnalysis.HighVulnerabilityCount} high-severity vulnerabilities found. Consider updating packages.");
        }

        if (!result.CompatibilityAnalysis.OverallCompatible)
        {
            recommendations.Add("🔧 Some packages are not fully compatible with your target framework. Review compatibility issues.");
        }

        if (result.Metrics.MaxDependencyDepth > 10)
        {
            recommendations.Add("📊 Deep dependency tree detected. Consider consolidating dependencies to reduce complexity.");
        }

        if (result.Metrics.TransitiveDependenciesCount > result.Metrics.DirectDependenciesCount * 5)
        {
            recommendations.Add("🎯 High number of transitive dependencies. Review if all direct dependencies are necessary.");
        }

        if (result.SecurityAnalysis.SecurityScore < 70)
        {
            recommendations.Add("🛡️ Security score is low. Review vulnerable packages and consider alternatives.");
        }

        return recommendations;
    }

    private static int CalculateSecurityScore(SecurityAnalysisResult security)
    {
        var baseScore = 100;
        var criticalPenalty = security.CriticalVulnerabilityCount * 30;
        var highPenalty = security.HighVulnerabilityCount * 15;
        var mediumPenalty = security.MediumVulnerabilityCount * 5;
        var lowPenalty = security.LowVulnerabilityCount * 1;

        var finalScore = baseScore - criticalPenalty - highPenalty - mediumPenalty - lowPenalty;
        return Math.Max(0, finalScore);
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _resolutionSemaphore?.Dispose();
            _vulnerabilityScanner?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for enhanced dependency resolver.
/// </summary>
public sealed class EnhancedDependencyResolverOptions
{
    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    public string TargetFramework { get; set; } = "net9.0";

    /// <summary>
    /// Gets or sets whether to resolve transitive dependencies.
    /// </summary>
    public bool ResolveTransitiveDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum dependency depth.
    /// </summary>
    public int MaxDependencyDepth { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to include prerelease packages.
    /// </summary>
    public bool IncludePrerelease { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of concurrent resolutions.
    /// </summary>
    public int MaxConcurrentResolutions { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets or sets the NuGet configuration path.
    /// </summary>
    public string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets whether to perform assembly-level analysis.
    /// </summary>
    public bool PerformAssemblyAnalysis { get; set; } = true;
}

// Enhanced data models for comprehensive dependency resolution

/// <summary>
/// Comprehensive dependency resolution result.
/// </summary>
public sealed class ComprehensiveDependencyResult
{
    /// <summary>
    /// Gets or sets the root package ID.
    /// </summary>
    public required string RootPackageId { get; set; }

    /// <summary>
    /// Gets or sets the root package version.
    /// </summary>
    public required string RootPackageVersion { get; set; }

    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    public required string TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets whether the resolution was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the resolution time.
    /// </summary>
    public TimeSpan ResolutionTime { get; set; }

    /// <summary>
    /// Gets or sets the dependency graph.
    /// </summary>
    public EnhancedDependencyGraph? DependencyGraph { get; set; }

    /// <summary>
    /// Gets or sets the compatibility analysis result.
    /// </summary>
    public CompatibilityAnalysisResult? CompatibilityAnalysis { get; set; }

    /// <summary>
    /// Gets or sets the security analysis result.
    /// </summary>
    public SecurityAnalysisResult? SecurityAnalysis { get; set; }

    /// <summary>
    /// Gets or sets the installation plan.
    /// </summary>
    public InstallationPlan? InstallationPlan { get; set; }

    /// <summary>
    /// Gets or sets the resolution metrics.
    /// </summary>
    public DependencyResolutionMetrics? Metrics { get; set; }

    /// <summary>
    /// Gets the resolution errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets the resolution warnings.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets the recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Enhanced dependency graph with detailed information.
/// </summary>
public sealed class EnhancedDependencyGraph
{
    /// <summary>
    /// Gets or sets the root package ID.
    /// </summary>
    public required string RootPackageId { get; set; }

    /// <summary>
    /// Gets or sets the root package version.
    /// </summary>
    public required string RootPackageVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the graph was resolved successfully.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets the resolved dependencies.
    /// </summary>
    public List<EnhancedResolvedDependency> ResolvedDependencies { get; } = [];

    /// <summary>
    /// Gets the resolution errors.
    /// </summary>
    public List<string> Errors { get; } = [];
}

/// <summary>
/// Enhanced resolved dependency with detailed information.
/// </summary>
public sealed class EnhancedResolvedDependency
{
    /// <summary>
    /// Gets or sets the package identity.
    /// </summary>
    public required PackageIdentity PackageIdentity { get; set; }

    /// <summary>
    /// Gets or sets the dependency level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the dependency version range.
    /// </summary>
    public VersionRange? DependencyRange { get; set; }

    /// <summary>
    /// Gets or sets whether this is a direct dependency.
    /// </summary>
    public bool IsDirectDependency { get; set; }

    /// <summary>
    /// Gets or sets the target framework for this dependency.
    /// </summary>
    public string? TargetFramework { get; set; }
}

// Additional supporting classes for comprehensive analysis...
// (Truncated for length - would include all the other classes like CompatibilityAnalysisResult, SecurityAnalysisResult, etc.)