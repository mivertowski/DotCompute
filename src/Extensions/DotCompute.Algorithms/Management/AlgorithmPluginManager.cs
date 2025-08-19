// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management
{

/// <summary>
/// Manages the loading, registration, and execution of algorithm plugins with advanced isolation and lifecycle management.
/// </summary>
public sealed partial class AlgorithmPluginManager : IAsyncDisposable
{
    private readonly ILogger<AlgorithmPluginManager> _logger;
    private readonly IAccelerator _accelerator;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly Timer _healthCheckTimer;
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private readonly SecurityPolicy _securityPolicy;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanningService _malwareScanner;
    private bool _disposed;

    /// <summary>
    /// Represents a loaded plugin with its context and metadata.
    /// </summary>
    private sealed class LoadedPlugin
    {
        public required IAlgorithmPlugin Plugin { get; init; }
        public required PluginAssemblyLoadContext LoadContext { get; init; }
        public required Assembly Assembly { get; init; }
        public required PluginMetadata Metadata { get; init; }
        public required DateTime LoadTime { get; init; }
        public PluginState State { get; set; } = PluginState.Loaded;
        public PluginHealth Health { get; set; } = PluginHealth.Unknown;
        public long ExecutionCount { get; set; }
        public DateTime LastExecution { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public Exception? LastError { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginManager"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="logger">The logger instance.</param>
    public AlgorithmPluginManager(IAccelerator accelerator, ILogger<AlgorithmPluginManager> logger)
        : this(accelerator, logger, new AlgorithmPluginManagerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginManager"/> class with options.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options for the plugin manager.</param>
    public AlgorithmPluginManager(IAccelerator accelerator, ILogger<AlgorithmPluginManager> logger, AlgorithmPluginManagerOptions options)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        // Initialize security components
        var securityLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityPolicy>.Instance;
        _securityPolicy = new SecurityPolicy(securityLogger);
        
        var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticodeValidator>.Instance;
        _authenticodeValidator = new AuthenticodeValidator(authenticodeLogger);
        
        var malwareLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MalwareScanningService>.Instance;
        _malwareScanner = new MalwareScanningService(malwareLogger, new MalwareScanningOptions
        {
            EnableWindowsDefender = _options.EnableWindowsDefenderScanning,
            MaxConcurrentScans = 2,
            ScanTimeout = TimeSpan.FromMinutes(1)
        });

        // Configure security policy from options
        ConfigureSecurityPolicy();
        
        // Initialize health check timer if enabled
        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecks, null, 
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
        else
        {
            _healthCheckTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading algorithm plugin from assembly {AssemblyPath}")]
    private partial void LogLoadingAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} algorithm plugins from assembly {AssemblyName}")]
    private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered algorithm plugin {PluginId} ({PluginName})")]
    private partial void LogPluginRegistered(string pluginId, string pluginName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Algorithm plugin {PluginId} already registered")]
    private partial void LogPluginAlreadyRegistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load algorithm plugin")]
    private partial void LogPluginLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing algorithm {PluginId}")]
    private partial void LogExecutingAlgorithm(string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly {AssemblyName} is already loaded")]
    private partial void LogAssemblyAlreadyLoaded(string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered algorithm plugin {PluginId}")]
    private partial void LogPluginUnregistered(string pluginId);

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _plugins.Keys;

    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins discovered and loaded.</returns>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(pluginDirectory))
        {
            LogDirectoryNotFound(pluginDirectory);
            return 0;
        }

        LogDiscoveringPlugins(pluginDirectory);

        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(file => !IsSystemAssembly(file))
            .ToList();

        var loadedCount = 0;

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var count = await LoadPluginsFromAssemblyAsync(pluginFile, cancellationToken).ConfigureAwait(false);
                loadedCount += count;

                // Setup hot reload if enabled
                if (_options.EnableHotReload)
                {
                    SetupHotReload(pluginFile);
                }
            }
            catch (Exception ex)
            {
                LogPluginDiscoveryFailed(pluginFile, ex.Message);
            }
        }

        LogPluginsDiscovered(loadedCount, pluginDirectory);
        return loadedCount;
    }

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public async Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource, 
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);
        ObjectDisposedException.ThrowIf(_disposed, this);

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
            var loadResult = await nugetLoader.LoadPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false)
                .ConfigureAwait(false);

            LogNuGetPackageLoaded(
                loadResult.PackageIdentity.Id, 
                _ = loadResult.PackageIdentity.Version.ToString(),
                loadResult.LoadedAssemblyPaths.Length,
                loadResult.ResolvedDependencies.Length);

            // Load plugins from each assembly in the package
            var totalPluginsLoaded = 0;
            var assemblyLoadTasks = loadResult.LoadedAssemblyPaths
                .Select(async assemblyPath =>
                {
                    try
                    {
                        var count = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false)
                            .ConfigureAwait(false);
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
            if (loadResult.ResolvedDependencies.Length > 0)
            {
                LogNuGetDependenciesResolved(
                    loadResult.PackageIdentity.Id,
                    string.Join(", ", loadResult.ResolvedDependencies.Select(d => $"{d.Id} {d.VersionRange}");
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
                _ = loadResult.PackageIdentity.Version.ToString(),
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

    /// <summary>
    /// Updates plugins from a NuGet package to the latest version.
    /// </summary>
    /// <param name="packageId">The package ID to update.</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded from the updated package.</returns>
    public async Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogUpdatingNuGetPackage(packageId);

        try
        {
            // First, unregister any existing plugins from this package
            var existingPlugins = _plugins.Values
                .Where(lp => lp.Metadata.AssemblyPath.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                .Select(lp => lp.Plugin.Id)
                .ToList();

            foreach (var pluginId in existingPlugins)
            {
                await UnregisterPluginAsync(pluginId).ConfigureAwait(false);
                LogUnregisteredNuGetPlugin(pluginId, packageId);
            }

            // Load the updated package
            return await LoadPluginsFromNuGetPackageAsync(packageId, targetFramework, cancellationToken).ConfigureAwait(false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogNuGetPackageUpdateFailed(packageId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    /// <param name="olderThan">Optional age filter - only clear packages older than this timespan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cache clearing operation.</returns>
    public async Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of cached NuGet package information.</returns>
    public async Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

    /// <summary>
    /// Validates a NuGet package before loading it.
    /// </summary>
    /// <param name="packageSource">The package source to validate.</param>
    /// <param name="targetFramework">Target framework for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details about the package.</returns>
    public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);
        ObjectDisposedException.ThrowIf(_disposed, this);

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
            var loadResult = await nugetLoader.LoadPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false)
                .ConfigureAwait(false);

            var validationResult = new NuGetValidationResult
            {
                PackageId = loadResult.PackageIdentity.Id,
                Version = loadResult.PackageIdentity.Version.ToString(),
                IsValid = true,
                AssemblyCount = loadResult.LoadedAssemblyPaths.Length,
                DependencyCount = loadResult.ResolvedDependencies.Length,
                SecurityValidationPassed = !string.IsNullOrEmpty(loadResult.SecurityValidationResult),
                SecurityDetails = loadResult.SecurityValidationResult ?? "No security validation performed",
                Warnings = loadResult.Warnings,
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
                ValidationError = ex.Message,
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

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Plugin system requires dynamic loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter contains annotations from the source parameter", Justification = "Plugin system requires dynamic loading")]
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        await _loadingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogLoadingAssembly(assemblyPath);

            // Security validation
            if (_options.EnableSecurityValidation && !await ValidateAssemblySecurityAsync(assemblyPath))
            {
                LogAssemblySecurityValidationFailed(assemblyPath);
                return 0;
            }

            // Load plugin metadata if available
            var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

            // Version compatibility check
            if (metadata != null && !IsVersionCompatible(metadata.RequiredFrameworkVersion))
            {
                LogVersionIncompatible(assemblyPath, metadata.RequiredFrameworkVersion ?? "Unknown");
                return 0;
            }

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
                    return 0;
                }

                // Add to load contexts
                _loadContexts.TryAdd(assemblyName, loadContext);

                // Discover plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

                var loadedCount = 0;
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        if (CreatePluginInstance(pluginType) is IAlgorithmPlugin plugin)
                        {
                            var pluginMetadata = metadata ?? new PluginMetadata
                            {
                                Id = plugin.Id,
                                Name = plugin.Name,
                                Version = plugin.Version.ToString(),
                                Description = plugin.Description,
                                Author = "Unknown",
                                AssemblyPath = assemblyPath,
                                LoadTime = DateTime.UtcNow
                            };

                            await RegisterPluginInternalAsync(plugin, loadContext, assembly, pluginMetadata, cancellationToken).ConfigureAwait(false);
                            loadedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogPluginLoadFailed(ex);
                    }
                }

                LogAssemblyLoaded(loadedCount, assemblyName);
                return loadedCount;
            }
            catch
            {
                // Clean up load context on failure
                loadContext.Unload();
                throw;
            }
        }
        catch (Exception ex)
        {
            LogPluginLoadFailed(ex);
            throw;
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// Registers a plugin instance (simplified overload for external plugins).
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the registration operation.</returns>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var metadata = new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version.ToString(),
            Description = plugin.Description,
            Author = "External",
            AssemblyPath = plugin.GetType().Assembly.Location,
            LoadTime = DateTime.UtcNow
        };

        // Use default load context for external plugins
        var loadContext = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly) as PluginAssemblyLoadContext
                          ?? new PluginAssemblyLoadContext($"External_{plugin.Id}", plugin.GetType().Assembly.Location, false);

        await RegisterPluginInternalAsync(plugin, loadContext, plugin.GetType().Assembly, metadata, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal method for registering plugins with full context information.
    /// </summary>
    private async Task RegisterPluginInternalAsync(
        IAlgorithmPlugin plugin,
        PluginAssemblyLoadContext loadContext,
        Assembly assembly,
        PluginMetadata metadata,
        CancellationToken cancellationToken)
    {
        var loadedPlugin = new LoadedPlugin
        {
            Plugin = plugin,
            LoadContext = loadContext,
            Assembly = assembly,
            Metadata = metadata,
            LoadTime = DateTime.UtcNow,
            State = PluginState.Loaded,
            Health = PluginHealth.Unknown
        };

        if (!_plugins.TryAdd(plugin.Id, loadedPlugin))
        {
            LogPluginAlreadyRegistered(plugin.Id);
            throw new InvalidOperationException($"Plugin with ID '{plugin.Id}' is already registered.");
        }

        try
        {
            // Initialize plugin
            loadedPlugin.State = PluginState.Initializing;
            await plugin.InitializeAsync(_accelerator, cancellationToken).ConfigureAwait(false);
            
            loadedPlugin.State = PluginState.Running;
            loadedPlugin.Health = PluginHealth.Healthy;
            
            LogPluginRegistered(plugin.Id, plugin.Name);
        }
        catch (Exception ex)
        {
            loadedPlugin.State = PluginState.Failed;
            loadedPlugin.Health = PluginHealth.Critical;
            loadedPlugin.LastError = ex;
            
            // Remove from plugins collection on initialization failure
            _plugins.TryRemove(plugin.Id, out _);
            
            LogPluginInitializationFailed(plugin.Id, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.TryGetValue(pluginId, out var loadedPlugin) ? loadedPlugin.Plugin : null;
    }

    /// <summary>
    /// Gets detailed information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            return null;
        }

        return new LoadedPluginInfo
        {
            Plugin = loadedPlugin.Plugin,
            Metadata = loadedPlugin.Metadata,
            State = loadedPlugin.State,
            Health = loadedPlugin.Health,
            LoadTime = loadedPlugin.LoadTime,
            ExecutionCount = loadedPlugin.ExecutionCount,
            LastExecution = loadedPlugin.LastExecution,
            TotalExecutionTime = loadedPlugin.TotalExecutionTime,
            LastError = loadedPlugin.LastError,
            AssemblyLocation = loadedPlugin.Assembly.Location,
            LoadContextName = loadedPlugin.LoadContext.Name ?? "Unknown"
        };
    }

    /// <summary>
    /// Gets all plugins that support the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.SupportedAccelerators.Contains(acceleratorType));
    }

    /// <summary>
    /// Gets all plugins that can process the specified input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.InputTypes.Contains(inputType));
    }

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values
            .Where(lp => lp.Health == PluginHealth.Healthy && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin);
    }

    /// <summary>
    /// Executes a plugin with the specified inputs and enhanced monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<object> ExecutePluginAsync(
        string pluginId, 
        object[] inputs, 
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' not found.");
        }

        // Check plugin health before execution
        if (loadedPlugin.Health == PluginHealth.Critical || loadedPlugin.State != PluginState.Running)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' is not in a healthy state for execution. Health: {loadedPlugin.Health}, State: {loadedPlugin.State}");
        }

        LogExecutingAlgorithm(pluginId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Update execution tracking
            loadedPlugin.ExecutionCount++;
            loadedPlugin.LastExecution = DateTime.UtcNow;

            // Execute with error recovery
            var result = await ExecuteWithRetryAsync(loadedPlugin.Plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            loadedPlugin.TotalExecutionTime += stopwatch.Elapsed;
            loadedPlugin.Health = PluginHealth.Healthy;
            loadedPlugin.LastError = null;

            LogPluginExecutionCompleted(pluginId, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            loadedPlugin.LastError = ex;
            
            // Determine health impact based on error
            if (ex is OutOfMemoryException or StackOverflowException)
            {
                loadedPlugin.Health = PluginHealth.Critical;
                loadedPlugin.State = PluginState.Failed;
            }
            else
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }

            LogPluginExecutionFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                LogPluginRetryingExecution(plugin.Id, attempt, ex.Message);
                await Task.Delay(retryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt without retry handling
        return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and worth retrying.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is SocketException ||
               (ex is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Unregisters a plugin with proper cleanup and isolation handling.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; otherwise, false.</returns>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryRemove(pluginId, out var loadedPlugin))
        {
            try
            {
                // Update state
                loadedPlugin.State = PluginState.Stopping;

                // Stop hot reload watching if active
                StopHotReload(loadedPlugin.Metadata.AssemblyPath);

                // Dispose the plugin
                await loadedPlugin.Plugin.DisposeAsync().ConfigureAwait(false);
                
                loadedPlugin.State = PluginState.Unloaded;

                LogPluginUnregistered(pluginId);
                return true;
            }
            catch (Exception ex)
            {
                loadedPlugin.State = PluginState.Failed;
                loadedPlugin.LastError = ex;
                LogPluginUnloadFailed(pluginId, ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Reloads a plugin (hot reload).
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was reloaded successfully; otherwise, false.</returns>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            return false;
        }

        LogPluginReloading(pluginId);

        try
        {
            // Store assembly path
            var assemblyPath = loadedPlugin.Metadata.AssemblyPath;

            // Unregister current plugin
            await UnregisterPluginAsync(pluginId).ConfigureAwait(false);

            // Small delay to allow for file system events
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            // Reload from assembly
            var loadedCount = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);

            LogPluginReloaded(pluginId, loadedCount > 0);
            return loadedCount > 0;
        }
        catch (Exception ex)
        {
            LogPluginReloadFailed(pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>Collection of plugin information.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _plugins.Values.Select(lp => new AlgorithmPluginInfo
        {
            Id = lp.Plugin.Id,
            Name = lp.Plugin.Name,
            Version = lp.Plugin.Version,
            Description = lp.Plugin.Description,
            SupportedAccelerators = lp.Plugin.SupportedAccelerators,
            InputTypes = [.. lp.Plugin.InputTypes.Select(t => t.FullName ?? t.Name)],
            OutputType = lp.Plugin.OutputType.FullName ?? lp.Plugin.OutputType.Name,
            PerformanceProfile = lp.Plugin.GetPerformanceProfile()
        });
    }

    #region Private Helper Methods

    /// <summary>
    /// Creates a plugin instance with proper error handling.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin instantiation requires dynamic type handling")]
    private static IAlgorithmPlugin? CreatePluginInstance(Type pluginType)
    {
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates assembly security using comprehensive security policies and scanning.
    /// </summary>
    private async Task<bool> ValidateAssemblySecurityAsync(string assemblyPath)
    {
        if (!_options.EnableSecurityValidation)
        {
            return true;
        }

        try
        {
            LogSecurityValidationStarting(assemblyPath);

            // Step 1: Digital signature validation (Authenticode)
            if (_options.RequireDigitalSignature)
            {
                var signatureResult = await _authenticodeValidator.ValidateAsync(assemblyPath);
                if (!signatureResult.IsValid || signatureResult.TrustLevel < TrustLevel.Medium)
                {
                    LogDigitalSignatureValidationFailed(assemblyPath, signatureResult.ErrorMessage ?? "Unknown error");
                    return false;
                }
                LogDigitalSignatureValidationPassed(assemblyPath, signatureResult.SignerName ?? "Unknown");
            }

            // Step 2: Strong name validation
            if (_options.RequireStrongName)
            {
                if (!await ValidateStrongNameAsync(assemblyPath))
                {
                    LogStrongNameValidationFailed(assemblyPath);
                    return false;
                }
                LogStrongNameValidationPassed(assemblyPath);
            }

            // Step 3: Malware scanning
            if (_options.EnableMalwareScanning)
            {
                var malwareResult = await _malwareScanner.ScanAssemblyAsync(assemblyPath);
                if (!malwareResult.IsClean || malwareResult.ThreatLevel >= ThreatLevel.Medium)
                {
                    LogMalwareScanningFailed(assemblyPath, malwareResult.ThreatDescription ?? "Unknown threat", malwareResult.ThreatLevel);
                    return false;
                }
                LogMalwareScanningPassed(assemblyPath);
            }

            // Step 4: Security policy evaluation
            var context = new SecurityEvaluationContext
            {
                AssemblyPath = assemblyPath,
                AssemblyBytes = await File.ReadAllBytesAsync(assemblyPath)
            };

            // Add certificate information if available
            if (_options.RequireDigitalSignature)
            {
                if (_authenticodeValidator.ExtractCertificateInfo(assemblyPath)?.Subject != null)
                {
                    // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
                    var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                    context.Certificate = cert != null ? new System.Security.Cryptography.X509Certificates.X509Certificate2(cert) : null;
                }
                else
                {
                    context.Certificate = null;
                }
            }

            // Add strong name key if available
            if (_options.RequireStrongName)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    context.StrongNameKey = assemblyName.GetPublicKey();
                }
                catch
                {
                    // Strong name validation will catch this
                }
            }

            var policyResult = _securityPolicy.EvaluateRules(context);
            if (!policyResult.IsAllowed)
            {
                LogSecurityPolicyViolation(assemblyPath, string.Join(", ", policyResult.Violations));
                return false;
            }

            // Log any warnings
            if (policyResult.Warnings.Count > 0)
            {
                LogSecurityPolicyWarnings(assemblyPath, string.Join(", ", policyResult.Warnings));
            }

            LogSecurityValidationPassed(assemblyPath, policyResult.SecurityLevel);
            return true;
        }
        catch (Exception ex)
        {
            LogSecurityValidationError(assemblyPath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Validates strong name signature of an assembly.
    /// </summary>
    private static async Task<bool> ValidateStrongNameAsync(string assemblyPath)
    {
        try
        {
            await Task.CompletedTask; // Make it async for consistency
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var publicKey = assemblyName.GetPublicKey();
            
            // Check if assembly has a public key (strong name)
            if (publicKey == null || publicKey.Length == 0)
            {
                return false;
            }

            // Basic validation: ensure key is of reasonable size
            if (publicKey.Length < 160) // Minimum for RSA-1024
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Configures the security policy from options.
    /// </summary>
    private void ConfigureSecurityPolicy()
    {
        _securityPolicy.RequireDigitalSignature = _options.RequireDigitalSignature;
        _securityPolicy.RequireStrongName = _options.RequireStrongName;
        _securityPolicy.MinimumSecurityLevel = _options.MinimumSecurityLevel;
        _securityPolicy.MaxAssemblySize = _options.MaxAssemblySize;
        _securityPolicy.EnableMalwareScanning = _options.EnableMalwareScanning;
        _securityPolicy.EnableMetadataAnalysis = _options.EnableMetadataAnalysis;

        // Add trusted publishers
        foreach (var publisher in _options.TrustedPublishers)
        {
            _securityPolicy.AddTrustedPublisher(publisher);
        }

        // Configure directory policies
        foreach (var directory in _options.AllowedPluginDirectories)
        {
            _securityPolicy.DirectoryPolicies[directory] = SecurityLevel.High;
        }
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
    /// Checks if the required framework version is compatible.
    /// </summary>
    private static bool IsVersionCompatible(string? requiredVersion)
    {
        if (string.IsNullOrEmpty(requiredVersion))
        {
            return true;
        }

        try
        {
            var required = Version.Parse(requiredVersion);
            var current = Environment.Version;
            return current >= required;
        }
        catch
        {
            return true; // If we can't parse, assume compatible
        }
    }

    /// <summary>
    /// Determines if an assembly is a system assembly that should be ignored.
    /// </summary>
    private static bool IsSystemAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath).ToLowerInvariant();
        return fileName.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets up hot reload monitoring for a plugin assembly with enhanced file system monitoring.
    /// </summary>
    private void SetupHotReload(string assemblyPath)
    {
        if (_watchers.ContainsKey(assemblyPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(assemblyPath);
            if (directory == null) return;

            var fileName = Path.GetFileName(assemblyPath);
            
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            // Handle both file changes and dependency changes
            watcher.Changed += OnAssemblyChanged;
            watcher.Created += OnAssemblyChanged;
            watcher.Error += OnFileWatcherError;

            // Also watch for .pdb files (debug symbols) and .json manifest files
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
            
            if (File.Exists(pdbPath))
            {
                var pdbWatcher = new FileSystemWatcher(directory, Path.GetFileName(pdbPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                pdbWatcher.Changed += OnAssemblyChanged;
                _watchers.TryAdd(pdbPath, pdbWatcher);
            }
            
            if (File.Exists(manifestPath))
            {
                var manifestWatcher = new FileSystemWatcher(directory, Path.GetFileName(manifestPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                manifestWatcher.Changed += OnAssemblyChanged;
                _watchers.TryAdd(manifestPath, manifestWatcher);
            }

            _watchers.TryAdd(assemblyPath, watcher);
            LogHotReloadSetup(assemblyPath);
        }
        catch (Exception ex)
        {
            LogHotReloadSetupFailed(assemblyPath, ex.Message);
        }
    }

    /// <summary>
    /// Stops hot reload monitoring for a plugin assembly.
    /// </summary>
    private void StopHotReload(string assemblyPath)
    {
        if (_watchers.TryRemove(assemblyPath, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnAssemblyChanged;
            watcher.Dispose();
        }
    }

    /// <summary>
    /// Handles assembly file changes for hot reload.
    /// </summary>
    private async void OnAssemblyChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Small delay to allow file to be fully written
            await Task.Delay(500).ConfigureAwait(false);

            // Find plugins from this assembly (check both direct path and related files)
            var assemblyPath = e.FullPath;
            
            // If this is a .pdb or .json file, find the corresponding assembly
            if (Path.GetExtension(assemblyPath).Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(assemblyPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                assemblyPath = Path.ChangeExtension(assemblyPath, ".dll");
                if (!File.Exists(assemblyPath))
                {
                    assemblyPath = Path.ChangeExtension(e.FullPath, ".exe");
                }
            }

            var pluginsToReload = _plugins.Values
                .Where(lp => string.Equals(lp.Metadata.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
                .Select(lp => lp.Plugin.Id)
                .ToList();

            foreach (var pluginId in pluginsToReload)
            {
                _ = Task.Run(async () => await ReloadPluginAsync(pluginId).ConfigureAwait(false));
            }
            
            if (pluginsToReload.Count > 0)
            {
                LogHotReloadTriggered(e.FullPath, pluginsToReload.Count);
            }
        }
        catch (Exception ex)
        {
            LogHotReloadFailed(e.FullPath, ex.Message);
        }
    }

    /// <summary>
    /// Handles file watcher errors.
    /// </summary>
    private async void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File watcher error occurred");
        
        // Try to restart the watcher if it's a temporary error
        if (sender is FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false); // Wait a bit before restarting
                watcher.EnableRaisingEvents = true;
                _logger.LogInformation("Successfully restarted file watcher for: {Path}", watcher.Path);
            }
            catch (Exception restartEx)
            {
                _logger.LogError(restartEx, "Failed to restart file watcher for: {Path}", watcher.Path);
            }
        }
    }

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    private async void PerformHealthChecks(object? state)
    {
        if (_disposed) return;

        try
        {
            foreach (var loadedPlugin in _plugins.Values.ToList())
            {
                await CheckPluginHealthAsync(loadedPlugin).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex.Message);
        }
    }

    /// <summary>
    /// Checks the health of a single plugin.
    /// </summary>
    private async Task CheckPluginHealthAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            var oldHealth = loadedPlugin.Health;
            
            // Check if plugin has been executing successfully
            if (loadedPlugin.LastError != null && 
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }
            else if (loadedPlugin.ExecutionCount > 0 && loadedPlugin.LastError == null)
            {
                loadedPlugin.Health = PluginHealth.Healthy;
            }

            // Sophisticated health checks implementation
            await PerformMemoryUsageMonitoringAsync(loadedPlugin);
            await PerformResponseTimeAnalysisAsync(loadedPlugin);
            await PerformErrorRateTrackingAsync(loadedPlugin);
            await PerformResourceLeakDetectionAsync(loadedPlugin);

            if (oldHealth != loadedPlugin.Health)
            {
                LogPluginHealthChanged(loadedPlugin.Plugin.Id, oldHealth, loadedPlugin.Health);
            }

            await Task.CompletedTask; // Placeholder for async health checks
        }
        catch (Exception ex)
        {
            loadedPlugin.Health = PluginHealth.Critical;
            loadedPlugin.LastError = ex;
        }
    }

    /// <summary>
    /// Monitors memory usage for a loaded plugin.
    /// </summary>
    private async Task PerformMemoryUsageMonitoringAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            // Get memory usage for the plugin's load context
            var memoryBefore = GC.GetTotalMemory(false);
            
            // Force garbage collection to get more accurate reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsage = memoryAfter - memoryBefore;
            
            // Check if memory usage is excessive
            if (memoryUsage > _options.MaxAssemblySize * 2) // More than 2x the assembly size
            {
                loadedPlugin.Health = PluginHealth.Degraded;
                loadedPlugin.LastError = new InvalidOperationException($"Plugin is using excessive memory: {memoryUsage:N0} bytes");
            }
            else if (memoryUsage > _options.MaxAssemblySize)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    loadedPlugin.Health = PluginHealth.Degraded;
                }
            }

            // Store memory metrics
            loadedPlugin.Metadata.AdditionalMetadata["MemoryUsage"] = memoryUsage;
            loadedPlugin.Metadata.AdditionalMetadata["MemoryCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to monitor memory usage for plugin: {PluginId}", loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Analyzes response time patterns for a loaded plugin.
    /// </summary>
    private async Task PerformResponseTimeAnalysisAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return; // No executions to analyze
            }

            var averageResponseTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;
            const double maxAcceptableResponseTime = 30000; // 30 seconds
            const double warningResponseTime = 10000; // 10 seconds

            if (averageResponseTime > maxAcceptableResponseTime)
            {
                loadedPlugin.Health = PluginHealth.Degraded;
                loadedPlugin.LastError = new TimeoutException($"Plugin average response time is too high: {averageResponseTime:F2} ms");
            }
            else if (averageResponseTime > warningResponseTime && loadedPlugin.Health == PluginHealth.Healthy)
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }

            // Check for response time degradation over time
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousAverageResponseTime", out var prevTimeObj) &&
                prevTimeObj is double prevTime)
            {
                var degradationThreshold = 1.5; // 50% increase
                if (averageResponseTime > prevTime * degradationThreshold)
                {
                    if (loadedPlugin.Health == PluginHealth.Healthy)
                    {
                        loadedPlugin.Health = PluginHealth.Degraded;
                    }
                }
            }

            // Store response time metrics
            loadedPlugin.Metadata.AdditionalMetadata["AverageResponseTime"] = averageResponseTime;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousAverageResponseTime"] = averageResponseTime;
            loadedPlugin.Metadata.AdditionalMetadata["ResponseTimeCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze response times for plugin: {PluginId}", loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Tracks error rates for a loaded plugin.
    /// </summary>
    private async Task PerformErrorRateTrackingAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return; // No executions to track
            }

            // Calculate error rate based on recent errors
            var errorCount = loadedPlugin.LastError != null ? 1 : 0;
            
            // Get historical error count if available
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("TotalErrorCount", out var totalErrorsObj) &&
                totalErrorsObj is long totalErrors)
            {
                errorCount = (int)totalErrors;
            }

            var errorRate = (double)errorCount / loadedPlugin.ExecutionCount;
            const double criticalErrorRate = 0.5; // 50% error rate
            const double warningErrorRate = 0.1; // 10% error rate

            if (errorRate > criticalErrorRate)
            {
                loadedPlugin.Health = PluginHealth.Critical;
                loadedPlugin.LastError = new InvalidOperationException($"Plugin has critical error rate: {errorRate:P2}");
            }
            else if (errorRate > warningErrorRate)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    loadedPlugin.Health = PluginHealth.Degraded;
                }
            }

            // Check for recent error spikes
            var recentExecutions = Math.Min(10, loadedPlugin.ExecutionCount);
            if (loadedPlugin.LastError != null && 
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                // Recent error detected
                var recentErrorWeight = 2.0; // Weight recent errors more heavily
                if (errorRate * recentErrorWeight > warningErrorRate)
                {
                    if (loadedPlugin.Health == PluginHealth.Healthy)
                    {
                        loadedPlugin.Health = PluginHealth.Degraded;
                    }
                }
            }

            // Store error rate metrics
            loadedPlugin.Metadata.AdditionalMetadata["ErrorRate"] = errorRate;
            loadedPlugin.Metadata.AdditionalMetadata["TotalErrorCount"] = errorCount;
            loadedPlugin.Metadata.AdditionalMetadata["ErrorRateCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track error rates for plugin: {PluginId}", loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Detects potential resource leaks for a loaded plugin.
    /// </summary>
    private async Task PerformResourceLeakDetectionAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            // Check for handle leaks
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var handleCount = currentProcess.HandleCount;
            
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousHandleCount", out var prevHandleObj) &&
                prevHandleObj is int prevHandleCount)
            {
                var handleIncrease = handleCount - prevHandleCount;
                const int handleLeakThreshold = 100; // Arbitrary threshold
                
                if (handleIncrease > handleLeakThreshold)
                {
                    if (loadedPlugin.Health == PluginHealth.Healthy)
                    {
                        loadedPlugin.Health = PluginHealth.Degraded;
                    }
                    
                    _logger.LogWarning("Potential handle leak detected for plugin {PluginId}: {HandleIncrease} new handles",
                        loadedPlugin.Plugin.Id, handleIncrease);
                }
            }

            // Check for thread leaks
            var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousThreadCount", out var prevThreadObj) &&
                prevThreadObj is int prevThreadCount)
            {
                var threadIncrease = threadCount - prevThreadCount;
                const int threadLeakThreshold = 10; // Conservative threshold
                
                if (threadIncrease > threadLeakThreshold)
                {
                    if (loadedPlugin.Health == PluginHealth.Healthy)
                    {
                        loadedPlugin.Health = PluginHealth.Degraded;
                    }
                    
                    _logger.LogWarning("Potential thread leak detected for plugin {PluginId}: {ThreadIncrease} new threads",
                        loadedPlugin.Plugin.Id, threadIncrease);
                }
            }

            // Store resource metrics
            loadedPlugin.Metadata.AdditionalMetadata["CurrentHandleCount"] = handleCount;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousHandleCount"] = handleCount;
            loadedPlugin.Metadata.AdditionalMetadata["CurrentThreadCount"] = threadCount;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousThreadCount"] = threadCount;
            loadedPlugin.Metadata.AdditionalMetadata["ResourceLeakCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect resource leaks for plugin: {PluginId}", loadedPlugin.Plugin.Id);
        }
    }

    #endregion

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Stop health check timer
            _healthCheckTimer.Dispose();

            // Stop all file watchers
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

            // Dispose security components
            _authenticodeValidator?.Dispose();
            _malwareScanner?.Dispose();

            // Dispose all plugins and their load contexts
            var disposeTasks = _plugins.Values.Select(async lp =>
            {
                try
                {
                    await lp.Plugin.DisposeAsync().ConfigureAwait(false);
                    lp.LoadContext.Unload();
                }
                catch (Exception ex)
                {
                    LogPluginDisposeFailed(lp.Plugin.Id, ex.Message);
                }
            });

            await Task.WhenAll(disposeTasks).ConfigureAwait(false);

            _plugins.Clear();
            _loadContexts.Clear();

            // Dispose semaphore
            _loadingSemaphore.Dispose();
        }
    }

    #region Logger Messages

    // Existing logger messages...
    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory not found: {Directory}")]
    private partial void LogDirectoryNotFound(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered and loaded {Count} plugins from directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to discover plugin from {AssemblyPath}: {Reason}")]
    private partial void LogPluginDiscoveryFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugins from NuGet package: {PackagePath}")]
    private partial void LogLoadingFromNuGetPackage(string packagePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Assembly security validation failed for {AssemblyPath}")]
    private partial void LogAssemblySecurityValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly version incompatible {AssemblyPath}, required: {RequiredVersion}")]
    private partial void LogVersionIncompatible(string assemblyPath, string requiredVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin initialization failed for {PluginId}: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin execution completed for {PluginId} in {ElapsedMs} ms")]
    private partial void LogPluginExecutionCompleted(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin execution failed for {PluginId}: {Reason}")]
    private partial void LogPluginExecutionFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrying plugin execution for {PluginId}, attempt {Attempt}: {Reason}")]
    private partial void LogPluginRetryingExecution(string pluginId, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin unload failed for {PluginId}: {Reason}")]
    private partial void LogPluginUnloadFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading plugin: {PluginId}")]
    private partial void LogPluginReloading(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin reload completed for {PluginId}, success: {Success}")]
    private partial void LogPluginReloaded(string pluginId, bool success);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin reload failed for {PluginId}: {Reason}")]
    private partial void LogPluginReloadFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly too large: {AssemblyPath}, size: {ActualSize}, max: {MaxSize}")]
    private partial void LogAssemblyTooLarge(string assemblyPath, long actualSize, long maxSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation error for {AssemblyPath}: {Reason}")]
    private partial void LogSecurityValidationError(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
    private partial void LogMetadataLoadFailed(string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload setup for {AssemblyPath}")]
    private partial void LogHotReloadSetup(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload setup failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadSetupFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed: {Reason}")]
    private partial void LogHealthCheckFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin health changed for {PluginId}: {OldHealth} -> {NewHealth}")]
    private partial void LogPluginHealthChanged(string pluginId, PluginHealth oldHealth, PluginHealth newHealth);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin dispose failed for {PluginId}: {Reason}")]
    private partial void LogPluginDisposeFailed(string pluginId, string reason);

    // Security validation logger messages
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting security validation for: {AssemblyPath}")]
    private partial void LogSecurityValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed for: {AssemblyPath}, Security Level: {SecurityLevel}")]
    private partial void LogSecurityValidationPassed(string assemblyPath, SecurityLevel securityLevel);

    [LoggerMessage(Level = LogLevel.Error, Message = "Digital signature validation failed for: {AssemblyPath}, Reason: {Reason}")]
    private partial void LogDigitalSignatureValidationFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature validation passed for: {AssemblyPath}, Signer: {Signer}")]
    private partial void LogDigitalSignatureValidationPassed(string assemblyPath, string signer);

    [LoggerMessage(Level = LogLevel.Error, Message = "Strong name validation failed for: {AssemblyPath}")]
    private partial void LogStrongNameValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strong name validation passed for: {AssemblyPath}")]
    private partial void LogStrongNameValidationPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Malware scanning failed for: {AssemblyPath}, Threat: {ThreatDescription}, Level: {ThreatLevel}")]
    private partial void LogMalwareScanningFailed(string assemblyPath, string threatDescription, ThreatLevel threatLevel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Malware scanning passed for: {AssemblyPath}")]
    private partial void LogMalwareScanningPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security policy violation for: {AssemblyPath}, Violations: {Violations}")]
    private partial void LogSecurityPolicyViolation(string assemblyPath, string violations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security policy warnings for: {AssemblyPath}, Warnings: {Warnings}")]
    private partial void LogSecurityPolicyWarnings(string assemblyPath, string warnings);

    // NuGet-specific logger messages
    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package loaded: {PackageId} v{Version}, {AssemblyCount} assemblies, {DependencyCount} dependencies")]
    private partial void LogNuGetPackageLoaded(string packageId, string version, int assemblyCount, int dependencyCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet assembly load failed for {AssemblyPath}: {Reason}")]
    private partial void LogNuGetAssemblyLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet dependencies resolved for {PackageId}: {Dependencies}")]
    private partial void LogNuGetDependenciesResolved(string packageId, string dependencies);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet security validation for {PackageId}: {ValidationResult}")]
    private partial void LogNuGetSecurityValidation(string packageId, string validationResult);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NuGet package warning for {PackageId}: {Warning}")]
    private partial void LogNuGetPackageWarning(string packageId, string warning);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package load completed: {PackageId} v{Version}, {PluginCount} plugins loaded in {ElapsedMs} ms")]
    private partial void LogNuGetPackageLoadCompleted(string packageId, string version, int pluginCount, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package load failed for {PackageSource}: {Reason}")]
    private partial void LogNuGetPackageLoadFailed(string packageSource, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating NuGet package: {PackageId}")]
    private partial void LogUpdatingNuGetPackage(string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered NuGet plugin {PluginId} from package {PackageId}")]
    private partial void LogUnregisteredNuGetPlugin(string pluginId, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package update failed for {PackageId}: {Reason}")]
    private partial void LogNuGetPackageUpdateFailed(string packageId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Clearing NuGet cache: {Filter}")]
    private partial void LogClearingNuGetCache(string filter);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet cache cleared")]
    private partial void LogNuGetCacheCleared();

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet cache clear failed: {Reason}")]
    private partial void LogNuGetCacheClearFailed(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get cached packages: {Reason}")]
    private partial void LogGetCachedPackagesFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validating NuGet package: {PackageSource}")]
    private partial void LogValidatingNuGetPackage(string packageSource);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package validated: {PackageSource}, Valid: {IsValid}, Warnings: {WarningCount}")]
    private partial void LogNuGetPackageValidated(string packageSource, bool isValid, int warningCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package validation failed for {PackageSource}: {Reason}")]
    private partial void LogNuGetPackageValidationFailed(string packageSource, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload triggered for {FilePath}, reloading {PluginCount} plugins")]
    private partial void LogHotReloadTriggered(string filePath, int pluginCount);

    #endregion
}

/// <summary>
/// Represents information about an algorithm plugin.
/// </summary>
public sealed class AlgorithmPluginInfo
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Gets or sets the plugin description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the supported accelerator types.
    /// </summary>
    public required AcceleratorType[] SupportedAccelerators { get; init; }

    /// <summary>
    /// Gets or sets the input type names.
    /// </summary>
    public required string[] InputTypes { get; init; }

    /// <summary>
    /// Gets or sets the output type name.
    /// </summary>
    public required string OutputType { get; init; }

    /// <summary>
    /// Gets or sets the performance profile.
    /// </summary>
    public required AlgorithmPerformanceProfile PerformanceProfile { get; init; }
}

/// <summary>
/// Configuration options for the Algorithm Plugin Manager.
/// </summary>
public sealed class AlgorithmPluginManagerOptions
{
    /// <summary>
    /// Gets or sets whether plugin isolation is enabled.
    /// </summary>
    public bool EnablePluginIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether security validation is enabled.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether hot reload is enabled.
    /// </summary>
    public bool EnableHotReload { get; set; } = false;

    /// <summary>
    /// Gets or sets whether health checks are enabled.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum assembly size in bytes.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Gets the allowed plugin directories.
    /// </summary>
    public List<string> AllowedPluginDirectories { get; } = [];

    /// <summary>
    /// Gets trusted assembly publishers (for signature validation).
    /// </summary>
    public List<string> TrustedPublishers { get; } = [];

    /// <summary>
    /// Gets or sets whether digital signatures are required.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether strong names are required.
    /// </summary>
    public bool RequireStrongName { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum security level required.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets whether metadata analysis is enabled.
    /// </summary>
    public bool EnableMetadataAnalysis { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Windows Defender scanning is enabled.
    /// </summary>
    public bool EnableWindowsDefenderScanning { get; set; } = true;

    /// <summary>
    /// Gets or sets whether malware scanning is enabled.
    /// </summary>
    public bool EnableMalwareScanning { get; set; } = true;
}

/// <summary>
/// Metadata about a plugin loaded from manifest or assembly.
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the plugin description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the plugin author.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public required string AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the load time.
    /// </summary>
    public DateTime LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the required framework version.
    /// </summary>
    public string? RequiredFrameworkVersion { get; set; }

    /// <summary>
    /// Gets the plugin dependencies.
    /// </summary>
    public List<string> Dependencies { get; } = [];

    /// <summary>
    /// Gets additional metadata.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; } = [];
}

/// <summary>
/// Detailed information about a loaded plugin including runtime state.
/// </summary>
public sealed class LoadedPluginInfo
{
    /// <summary>
    /// Gets or sets the plugin instance.
    /// </summary>
    public required IAlgorithmPlugin Plugin { get; set; }

    /// <summary>
    /// Gets or sets the plugin metadata.
    /// </summary>
    public required PluginMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the plugin state.
    /// </summary>
    public PluginState State { get; set; }

    /// <summary>
    /// Gets or sets the plugin health status.
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Gets or sets the load time.
    /// </summary>
    public DateTime LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime LastExecution { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the assembly location.
    /// </summary>
    public required string AssemblyLocation { get; set; }

    /// <summary>
    /// Gets or sets the load context name.
    /// </summary>
    public required string LoadContextName { get; set; }
}

/// <summary>
/// Plugin state enumeration.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Unknown state.
    /// </summary>
    Unknown,

    /// <summary>
    /// Plugin is being loaded.
    /// </summary>
    Loading,

    /// <summary>
    /// Plugin has been loaded but not initialized.
    /// </summary>
    Loaded,

    /// <summary>
    /// Plugin is being initialized.
    /// </summary>
    Initializing,

    /// <summary>
    /// Plugin is initialized and running.
    /// </summary>
    Running,

    /// <summary>
    /// Plugin is being stopped.
    /// </summary>
    Stopping,

    /// <summary>
    /// Plugin has failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Plugin has been unloaded.
    /// </summary>
    Unloaded
}

/// <summary>
/// Plugin health enumeration.
/// </summary>
public enum PluginHealth
{
    /// <summary>
    /// Health status unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// Plugin is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin is degraded but functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Plugin is unhealthy.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Plugin is in critical state.
    /// </summary>
    Critical
}

/// <summary>
/// Result of NuGet package validation.
/// </summary>
public sealed class NuGetValidationResult
{
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets whether the package is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets the validation error message (if not valid).
    /// </summary>
    public string? ValidationError { get; init; }

    /// <summary>
    /// Gets or sets the number of assemblies found in the package.
    /// </summary>
    public int AssemblyCount { get; init; }

    /// <summary>
    /// Gets or sets the number of dependencies.
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// Gets or sets whether security validation passed.
    /// </summary>
    public bool SecurityValidationPassed { get; init; }

    /// <summary>
    /// Gets or sets security validation details.
    /// </summary>
    public required string SecurityDetails { get; init; }

    /// <summary>
    /// Gets or sets validation warnings.
    /// </summary>
    public required string[] Warnings { get; init; }

    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    public TimeSpan ValidationTime { get; init; }

    /// <summary>
    /// Gets or sets the package size in bytes.
    /// </summary>
    public long PackageSize { get; init; }
}

/// <summary>
/// Enhanced plugin assembly load context with isolation support.
/// </summary>
public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly bool _enableIsolation;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAssemblyLoadContext"/> class.
    /// </summary>
    /// <param name="name">The name of the load context.</param>
    /// <param name="pluginPath">The path to the plugin assembly.</param>
    /// <param name="enableIsolation">Whether to enable isolation.</param>
    public PluginAssemblyLoadContext(string name, string pluginPath, bool enableIsolation)
        : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _enableIsolation = enableIsolation;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin assembly loading requires dynamic loading")]
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // For isolation, only load plugin dependencies from plugin directory
        if (_enableIsolation)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // Don't load system assemblies in isolated context
            if (IsSystemAssembly(assemblyName))
            {
                return null; // Let default context handle it
            }
        }
        else
        {
            // Non-isolated: try to resolve dependencies first
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Determines if an assembly is a system assembly.
    /// </summary>
    private static bool IsSystemAssembly(AssemblyName assemblyName)
    {
        var name = assemblyName.Name?.ToLowerInvariant();
        return name != null && (
            name.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase));
    }
}}
