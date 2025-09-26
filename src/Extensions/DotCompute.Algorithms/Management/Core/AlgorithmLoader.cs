// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Responsible for loading algorithm plugins from assemblies with isolation and security validation.
/// </summary>
public sealed class AlgorithmLoader : IDisposable
{
    private readonly ILogger<AlgorithmLoader> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts;
    private readonly SemaphoreSlim _loadingSemaphore;
    private bool _disposed;

    public AlgorithmLoader(ILogger<AlgorithmLoader> logger, AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loadContexts = new ConcurrentDictionary<string, PluginAssemblyLoadContext>();
        _loadingSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded plugin instances with metadata.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Plugin system requires dynamic loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter contains annotations from the source parameter", Justification = "Plugin system requires dynamic loading")]
    public async Task<IEnumerable<LoadedPluginInfo>> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
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

            // Load plugin metadata if available
            var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

            // Version compatibility check
            if (metadata != null && !IsVersionCompatible(metadata.RequiredFrameworkVersion))
            {
                LogVersionIncompatible(assemblyPath, metadata.RequiredFrameworkVersion ?? "Unknown");
                return Enumerable.Empty<LoadedPluginInfo>();
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
                    return Enumerable.Empty<LoadedPluginInfo>();
                }

                // Add to load contexts
                _loadContexts.TryAdd(assemblyName, loadContext);

                // Discover plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

                var loadedPlugins = new List<LoadedPluginInfo>();
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

                            var loadedPlugin = new LoadedPluginInfo
                            {
                                Plugin = plugin,
                                Metadata = pluginMetadata,
                                LoadContext = loadContext,
                                Assembly = assembly,
                                LoadTime = DateTime.UtcNow,
                                State = PluginState.Loaded,
                                Health = PluginHealth.Unknown,
                                AssemblyLocation = assemblyPath,
                                LoadContextName = loadContextName
                            };

                            loadedPlugins.Add(loadedPlugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogPluginLoadFailed(ex);
                    }
                }

                LogAssemblyLoaded(loadedPlugins.Count, assemblyName);
                return loadedPlugins;
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
    /// Unloads plugins and cleans up load contexts.
    /// </summary>
    /// <param name="assemblyName">The assembly name to unload.</param>
    public void UnloadAssembly(string assemblyName)
    {
        if (_loadContexts.TryRemove(assemblyName, out var loadContext))
        {
            loadContext.Unload();
            LogAssemblyUnloaded(assemblyName);
        }
    }

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

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            foreach (var loadContext in _loadContexts.Values)
            {
                loadContext.Unload();
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load algorithm plugin")]
    private partial void LogPluginLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly {AssemblyName} is already loaded")]
    private partial void LogAssemblyAlreadyLoaded(string assemblyName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly version incompatible {AssemblyPath}, required: {RequiredVersion}")]
    private partial void LogVersionIncompatible(string assemblyPath, string requiredVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
    private partial void LogMetadataLoadFailed(string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assembly unloaded: {AssemblyName}")]
    private partial void LogAssemblyUnloaded(string assemblyName);

    #endregion
}

/// <summary>
/// Information about a loaded plugin including runtime context.
/// </summary>
public sealed class LoadedPluginInfo
{
    public required IAlgorithmPlugin Plugin { get; init; }
    public required PluginMetadata Metadata { get; init; }
    public required PluginAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required DateTime LoadTime { get; init; }
    public PluginState State { get; set; } = PluginState.Loaded;
    public PluginHealth Health { get; set; } = PluginHealth.Unknown;
    public long ExecutionCount { get; set; }
    public DateTime LastExecution { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public Exception? LastError { get; set; }
    public required string AssemblyLocation { get; set; }
    public required string LoadContextName { get; set; }
}

/// <summary>
/// Enhanced plugin assembly load context with isolation support.
/// </summary>
public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly bool _enableIsolation;

    public PluginAssemblyLoadContext(string name, string pluginPath, bool enableIsolation)
        : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _enableIsolation = enableIsolation;
    }

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

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private static bool IsSystemAssembly(AssemblyName assemblyName)
    {
        var name = assemblyName.Name?.ToLowerInvariant();
        return name != null && (
            name.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase));
    }
}