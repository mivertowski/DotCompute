// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Types.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Infrastructure;

/// <summary>
/// Handles the loading of algorithm plugins from assemblies and NuGet packages.
/// Manages assembly load contexts, dependency resolution, and plugin instantiation.
/// </summary>
public sealed class AlgorithmPluginLoader : IDisposable
{
    private readonly ILogger<AlgorithmPluginLoader> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private bool _disposed;

    public AlgorithmPluginLoader(
        ILogger<AlgorithmPluginLoader> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded plugin types and their contexts.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Plugin system requires dynamic loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter contains annotations from the source parameter", Justification = "Plugin system requires dynamic loading")]
    public async Task<LoadedAssemblyResult> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        await _loadingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogLoadingAssembly(assemblyPath);

            // Load plugin metadata if available
            var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

            // Create isolated load context
            var loadContextName = $"PluginContext_{Path.GetFileNameWithoutExtension(assemblyPath)}_{Guid.NewGuid():N}";
            var loadContext = new PluginAssemblyLoadContext(loadContextName, assemblyPath, _options.EnablePluginIsolation);

            try
            {
                // Load assembly in isolated context
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                var assemblyName = assembly.GetName().Name ?? "Unknown";

                // Check if already loaded
                if (_loadContexts.ContainsKey(assemblyName))
                {
                    LogAssemblyAlreadyLoaded(assemblyName);
                    loadContext.Unload();
                    return new LoadedAssemblyResult
                    {
                        AssemblyPath = assemblyPath,
                        LoadedPlugins = [],
                        Success = false,
                        ErrorMessage = $"Assembly {assemblyName} is already loaded"
                    };
                }

                // Add to load contexts
                _loadContexts.TryAdd(assemblyName, loadContext);

                // Discover plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t))
                    .ToList();

                var loadedPlugins = new List<LoadedPluginType>();
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var pluginMetadata = metadata ?? CreateDefaultMetadata(pluginType, assemblyPath);
                        loadedPlugins.Add(new LoadedPluginType
                        {
                            Type = pluginType,
                            LoadContext = loadContext,
                            Assembly = assembly,
                            Metadata = pluginMetadata
                        });
                    }
                    catch (Exception ex)
                    {
                        LogPluginTypeLoadFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
                    }
                }

                LogAssemblyLoaded(loadedPlugins.Count, assemblyName);
                return new LoadedAssemblyResult
                {
                    AssemblyPath = assemblyPath,
                    LoadedPlugins = loadedPlugins,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                // Clean up load context on failure
                loadContext.Unload();
                LogAssemblyLoadFailed(assemblyPath, ex.Message);
                return new LoadedAssemblyResult
                {
                    AssemblyPath = assemblyPath,
                    LoadedPlugins = [],
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The NuGet package load result.</returns>
    public async Task<NuGetLoadResult> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
                loadResult.ResolvedDependencies.Length);

            // Load plugins from each assembly in the package
            var loadedPlugins = new List<LoadedPluginType>();
            var assemblyLoadTasks = loadResult.LoadedAssemblyPaths
                .Select(async assemblyPath =>
                {
                    try
                    {
                        var assemblyResult = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                        return assemblyResult;
                    }
                    catch (Exception ex)
                    {
                        LogNuGetAssemblyLoadFailed(assemblyPath, ex.Message);
                        return new LoadedAssemblyResult
                        {
                            AssemblyPath = assemblyPath,
                            LoadedPlugins = [],
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                });

            var assemblyResults = await Task.WhenAll(assemblyLoadTasks).ConfigureAwait(false);
            foreach (var result in assemblyResults.Where(r => r.Success))
            {
                loadedPlugins.AddRange(result.LoadedPlugins);
            }

            return new NuGetLoadResult
            {
                PackageId = loadResult.PackageIdentity.Id,
                Version = loadResult.PackageIdentity.Version.ToString(),
                LoadedPlugins = loadedPlugins,
                Success = true,
                SecurityValidationResult = loadResult.SecurityValidationResult,
                Warnings = loadResult.Warnings,
                LoadTime = loadResult.LoadTime
            };
        }
        catch (Exception ex)
        {
            LogNuGetPackageLoadFailed(packageSource, ex.Message);
            return new NuGetLoadResult
            {
                PackageId = packageSource,
                Version = "Unknown",
                LoadedPlugins = [],
                Success = false,
                ErrorMessage = ex.Message,
                SecurityValidationResult = string.Empty,
                Warnings = [],
                LoadTime = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Creates a plugin instance with proper error handling.
    /// </summary>
    /// <param name="pluginType">The plugin type to instantiate.</param>
    /// <returns>The created plugin instance, or null if creation failed.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin instantiation requires dynamic type handling")]
    public IAlgorithmPlugin? CreatePluginInstance(Type pluginType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pluginType);

        try
        {
            // Try parameterless constructor first
            var constructor = pluginType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                return constructor.Invoke(null) as IAlgorithmPlugin;
            }

            // Try with logger parameter if available
            var loggerConstructor = pluginType.GetConstructor([typeof(ILogger<>).MakeGenericType(pluginType)]);
            if (loggerConstructor != null)
            {
                // Create a null logger for plugin instantiation
                var nullLoggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(pluginType);
                var nullLogger = Activator.CreateInstance(nullLoggerType);
                return loggerConstructor.Invoke([nullLogger]) as IAlgorithmPlugin;
            }

            // Fallback to Activator.CreateInstance
            return Activator.CreateInstance(pluginType) as IAlgorithmPlugin;
        }
        catch (Exception ex)
        {
            LogPluginInstantiationFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets a load context by assembly name.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The load context if found; otherwise, null.</returns>
    public PluginAssemblyLoadContext? GetLoadContext(string assemblyName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _loadContexts.TryGetValue(assemblyName, out var context) ? context : null;
    }

    /// <summary>
    /// Unloads an assembly load context.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>True if unloaded successfully; otherwise, false.</returns>
    public bool UnloadContext(string assemblyName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loadContexts.TryRemove(assemblyName, out var context))
        {
            try
            {
                context.Unload();
                LogLoadContextUnloaded(assemblyName);
                return true;
            }
            catch (Exception ex)
            {
                LogLoadContextUnloadFailed(assemblyName, ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Loads plugin metadata from a manifest file.
    /// </summary>
    private async Task<PluginMetadata?> LoadPluginMetadataAsync(string assemblyPath)
    {
        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PluginMetadata>(json);
        }
        catch (Exception ex)
        {
            LogMetadataLoadFailed(manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Creates default metadata for a plugin type.
    /// </summary>
    private static PluginMetadata CreateDefaultMetadata(Type pluginType, string assemblyPath)
    {
        return new PluginMetadata
        {
            Id = pluginType.FullName ?? pluginType.Name,
            Name = pluginType.Name,
            Version = pluginType.Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Description = $"Plugin from {pluginType.Name}",
            Author = "Unknown",
            AssemblyPath = assemblyPath,
            LoadTime = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Unload all contexts
            foreach (var context in _loadContexts.Values)
            {
                try
                {
                    context.Unload();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unload context {ContextName}", context.Name);
                }
            }

            _loadContexts.Clear();
            _loadingSemaphore.Dispose();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading algorithm plugin from assembly {AssemblyPath}")]
    private partial void LogLoadingAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} algorithm plugins from assembly {AssemblyName}")]
    private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly {AssemblyName} is already loaded")]
    private partial void LogAssemblyAlreadyLoaded(string assemblyName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load assembly {AssemblyPath}: {Reason}")]
    private partial void LogAssemblyLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load plugin type {TypeName}: {Reason}")]
    private partial void LogPluginTypeLoadFailed(string typeName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to instantiate plugin {TypeName}: {Reason}")]
    private partial void LogPluginInstantiationFailed(string typeName, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugins from NuGet package: {PackageSource}")]
    private partial void LogLoadingFromNuGetPackage(string packageSource);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package loaded: {PackageId} v{Version}, {AssemblyCount} assemblies, {DependencyCount} dependencies")]
    private partial void LogNuGetPackageLoaded(string packageId, string version, int assemblyCount, int dependencyCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet assembly load failed for {AssemblyPath}: {Reason}")]
    private partial void LogNuGetAssemblyLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package load failed for {PackageSource}: {Reason}")]
    private partial void LogNuGetPackageLoadFailed(string packageSource, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
    private partial void LogMetadataLoadFailed(string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Load context unloaded: {AssemblyName}")]
    private partial void LogLoadContextUnloaded(string assemblyName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unload context {AssemblyName}: {Reason}")]
    private partial void LogLoadContextUnloadFailed(string assemblyName, string reason);

    #endregion
}

/// <summary>
/// Result of loading plugins from an assembly.
/// </summary>
public sealed class LoadedAssemblyResult
{
    public required string AssemblyPath { get; init; }
    public required List<LoadedPluginType> LoadedPlugins { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of loading plugins from a NuGet package.
/// </summary>
public sealed class NuGetLoadResult
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required List<LoadedPluginType> LoadedPlugins { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required string SecurityValidationResult { get; init; }
    public required string[] Warnings { get; init; }
    public required TimeSpan LoadTime { get; init; }
}

/// <summary>
/// Represents a loaded plugin type with its context.
/// </summary>
public sealed class LoadedPluginType
{
    public required Type Type { get; init; }
    public required PluginAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required PluginMetadata Metadata { get; init; }
}