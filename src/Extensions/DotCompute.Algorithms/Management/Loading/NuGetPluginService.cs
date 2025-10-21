// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Service responsible for loading plugins from NuGet packages.
/// </summary>
public sealed partial class NuGetPluginService : INuGetPluginService
{
    private readonly IPluginLifecycleManager _lifecycleManager;
    private readonly IPluginDiscoveryService _discoveryService;
    private readonly AlgorithmPluginManagerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="lifecycleManager">The plugin lifecycle manager.</param>
    /// <param name="discoveryService">The plugin discovery service.</param>
    /// <param name="options">Configuration options.</param>
    public NuGetPluginService(
        ILogger<NuGetPluginService> logger,
        IPluginLifecycleManager lifecycleManager,
        IPluginDiscoveryService discoveryService,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

        LogLoadingFromNuGetPackage(packageSource);

        try
        {
            // Create NuGet plugin loader with security settings matching our options
            var nugetOptions = new NuGetPluginLoaderOptions
            {
                CacheDirectory = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache"),
                DefaultTargetFramework = targetFramework ?? "net9.0",
                EnableSecurityValidation = _options.EnableSecurityValidation,
                RequirePackageSignature = _options.RequireDigitalSignature,
                EnableMalwareScanning = _options.EnableMalwareScanning,
                MaxAssemblySize = _options.MaxAssemblySize,
                MinimumSecurityLevel = _options.MinimumSecurityLevel,
                IncludePrerelease = false,
                MaxConcurrentDownloads = 2
            };

            // Configure trusted sources and blocked packages based on security policy
            if (_options.EnableSecurityValidation)
            {
                nugetOptions.AllowedPackagePrefixes.Add("DotCompute.");
                nugetOptions.AllowedPackagePrefixes.Add("Microsoft.");
                nugetOptions.AllowedPackagePrefixes.Add("System.");

                // Add any trusted publishers as allowed prefixes
                foreach (var publisher in _options.TrustedPublishers)
                {
                    nugetOptions.AllowedPackagePrefixes.Add(publisher);
                }
            }

            using var nugetLoader = new NuGetPluginLoader(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance,
                nugetOptions);

            // Load the package and get assembly paths
            var loadResult = await nugetLoader.LoadPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);

            LogNuGetPackageLoaded(
                loadResult.PackageIdentity.Id,
                loadResult.PackageIdentity.Version.ToString(),
                loadResult.LoadedAssemblyPaths.Length,
                loadResult.ResolvedDependencies.Count);

            // Load plugins from each assembly in the package
            var totalPluginsLoaded = 0;
            var assemblyLoadTasks = loadResult.LoadedAssemblyPaths
                .Select(async assemblyPath =>
                {
                    try
                    {
                        var count = await _discoveryService.LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                        return count;
                    }
                    catch (Exception ex)
                    {
                        LogNuGetAssemblyLoadFailed(assemblyPath, ex.Message);
                        return 0;
                    }
                });

            var pluginCounts = await Task.WhenAll(assemblyLoadTasks).ConfigureAwait(false);
            totalPluginsLoaded = pluginCounts.Sum();

            // Log dependency information
            if (loadResult.ResolvedDependencies.Count > 0)
            {
                LogNuGetDependenciesResolved(
                    loadResult.PackageIdentity.Id,
                    string.Join(", ", loadResult.ResolvedDependencies.Select(d => $"{d.Id} {d.VersionRange}")));
            }

            // Log security validation results
            if (!string.IsNullOrEmpty(loadResult.SecurityValidationResult))
            {
                LogNuGetSecurityValidation(loadResult.PackageIdentity.Id, loadResult.SecurityValidationResult);
            }

            // Log any warnings
            foreach (var warning in loadResult.Warnings)
            {
                LogNuGetPackageWarning(loadResult.PackageIdentity.Id, warning);
            }

            LogNuGetPackageLoadCompleted(
                loadResult.PackageIdentity.Id,
                loadResult.PackageIdentity.Version.ToString(),
                totalPluginsLoaded,
                loadResult.LoadTime.TotalMilliseconds);

            return totalPluginsLoaded;
        }
        catch (Exception ex)
        {
            LogNuGetPackageLoadFailed(packageSource, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        LogUpdatingNuGetPackage(packageId);

        try
        {
            // First, unregister any existing plugins from this package
            var existingPlugins = _lifecycleManager.RegisteredPlugins
                .Select(_lifecycleManager.GetLoadedPluginInfo)
                .Where(info => info != null && info.Metadata.AssemblyPath.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                .Select(info => info!.Plugin.Id)
                .ToList();

            foreach (var pluginId in existingPlugins)
            {
                _ = await _lifecycleManager.UnregisterPluginAsync(pluginId).ConfigureAwait(false);
                LogUnregisteredNuGetPlugin(pluginId, packageId);
            }

            // Load the updated package
            return await LoadPluginsFromNuGetPackageAsync(packageId, targetFramework, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogNuGetPackageUpdateFailed(packageId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        LogClearingNuGetCache(olderThan?.ToString() ?? "all");

        try
        {
            var nugetOptions = new NuGetPluginLoaderOptions
            {
                CacheDirectory = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache")
            };

            using var nugetLoader = new NuGetPluginLoader(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance,
                nugetOptions);

            await nugetLoader.ClearCacheAsync(olderThan).ConfigureAwait(false);
            LogNuGetCacheCleared();
        }
        catch (Exception ex)
        {
            LogNuGetCacheClearFailed(ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nugetOptions = new NuGetPluginLoaderOptions
            {
                CacheDirectory = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache")
            };

            using var nugetLoader = new NuGetPluginLoader(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance,
                nugetOptions);

            await Task.CompletedTask.ConfigureAwait(false); // Make async for consistency
            return nugetLoader.GetCachedPackages();
        }
        catch (Exception ex)
        {
            LogGetCachedPackagesFailed(ex.Message);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

        LogValidatingNuGetPackage(packageSource);

        try
        {
            var nugetOptions = new NuGetPluginLoaderOptions
            {
                CacheDirectory = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache"),
                DefaultTargetFramework = targetFramework ?? "net9.0",
                EnableSecurityValidation = _options.EnableSecurityValidation,
                RequirePackageSignature = _options.RequireDigitalSignature,
                EnableMalwareScanning = _options.EnableMalwareScanning,
                MaxAssemblySize = _options.MaxAssemblySize,
                MinimumSecurityLevel = _options.MinimumSecurityLevel
            };

            using var nugetLoader = new NuGetPluginLoader(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance,
                nugetOptions);

            // Load package to trigger validation
            var loadResult = await nugetLoader.LoadPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);

            var validationResult = new NuGetValidationResult
            {
                PackageId = loadResult.PackageIdentity.Id,
                Version = loadResult.PackageIdentity.Version.ToString(),
                IsValid = true,
                AssemblyCount = loadResult.LoadedAssemblyPaths.Length,
                DependencyCount = loadResult.ResolvedDependencies.Count,
                SecurityValidationPassed = !string.IsNullOrEmpty(loadResult.SecurityValidationResult),
                SecurityDetails = loadResult.SecurityValidationResult ?? "No security validation performed",
                Warnings = loadResult.Warnings.ToArray(),
                ValidationTime = loadResult.LoadTime,
                PackageSize = loadResult.TotalSize
            };

            LogNuGetPackageValidated(packageSource, validationResult.IsValid, validationResult.Warnings.Length);
            return validationResult;
        }
        catch (Exception ex)
        {
            LogNuGetPackageValidationFailed(packageSource, ex.Message);
            return new NuGetValidationResult
            {
                PackageId = packageSource,
                Version = "Unknown",
                IsValid = false,
                ValidationIssue = ex.Message,
                AssemblyCount = 0,
                DependencyCount = 0,
                SecurityValidationPassed = false,
                SecurityDetails = $"Validation failed: {ex.Message}",
                Warnings = [],
                ValidationTime = TimeSpan.Zero,
                PackageSize = 0
            };
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugins from NuGet package: {PackagePath}")]
    private static partial void LogLoadingFromNuGetPackage(ILogger logger, string packagePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package loaded: {PackageId} v{Version}, {AssemblyCount} assemblies, {DependencyCount} dependencies")]
    private static partial void LogNuGetPackageLoaded(ILogger logger, string packageId, string version, int assemblyCount, int dependencyCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet assembly load failed for {AssemblyPath}: {Reason}")]
    private static partial void LogNuGetAssemblyLoadFailed(ILogger logger, string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet dependencies resolved for {PackageId}: {Dependencies}")]
    private static partial void LogNuGetDependenciesResolved(ILogger logger, string packageId, string dependencies);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet security validation for {PackageId}: {ValidationResult}")]
    private static partial void LogNuGetSecurityValidation(ILogger logger, string packageId, string validationResult);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NuGet package warning for {PackageId}: {Warning}")]
    private static partial void LogNuGetPackageWarning(ILogger logger, string packageId, string warning);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package load completed: {PackageId} v{Version}, {PluginCount} plugins loaded in {ElapsedMs} ms")]
    private static partial void LogNuGetPackageLoadCompleted(ILogger logger, string packageId, string version, int pluginCount, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package load failed for {PackageSource}: {Reason}")]
    private static partial void LogNuGetPackageLoadFailed(ILogger logger, string packageSource, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating NuGet package: {PackageId}")]
    private static partial void LogUpdatingNuGetPackage(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered NuGet plugin {PluginId} from package {PackageId}")]
    private static partial void LogUnregisteredNuGetPlugin(ILogger logger, string pluginId, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package update failed for {PackageId}: {Reason}")]
    private static partial void LogNuGetPackageUpdateFailed(ILogger logger, string packageId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Clearing NuGet cache: {Filter}")]
    private static partial void LogClearingNuGetCache(ILogger logger, string filter);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet cache cleared")]
    private static partial void LogNuGetCacheCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet cache clear failed: {Reason}")]
    private static partial void LogNuGetCacheClearFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get cached packages: {Reason}")]
    private static partial void LogGetCachedPackagesFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validating NuGet package: {PackageSource}")]
    private static partial void LogValidatingNuGetPackage(ILogger logger, string packageSource);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package validated: {PackageSource}, Valid: {IsValid}, Warnings: {WarningCount}")]
    private static partial void LogNuGetPackageValidated(ILogger logger, string packageSource, bool isValid, int warningCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package validation failed for {PackageSource}: {Reason}")]
    private static partial void LogNuGetPackageValidationFailed(ILogger logger, string packageSource, string reason);

    #endregion
}