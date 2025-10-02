// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Handles plugin loading operations with assembly isolation and lifecycle management.
/// </summary>
public sealed partial class AlgorithmAssemblyLoader(ILogger<AlgorithmAssemblyLoader> logger, AlgorithmPluginManagerOptions options) : IDisposable
{
    private readonly ILogger<AlgorithmAssemblyLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Loads plugins from an assembly with isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="metadata">Optional plugin metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded plugin instances.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Plugin system requires dynamic loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter contains annotations from the source parameter", Justification = "Plugin system requires dynamic loading")]
    public async Task<IReadOnlyList<LoadedPluginResult>> LoadPluginsFromAssemblyAsync(
        string assemblyPath,
        PluginMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        await _loadingSemaphore.WaitAsync(cancellationToken);
        try
        {
            LogLoadingAssembly(assemblyPath);

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
                    return Array.Empty<LoadedPluginResult>();
                }

                // Add to load contexts
                _ = _loadContexts.TryAdd(assemblyName, loadContext);

                // Discover and load plugin types
                var loadedPlugins = await LoadPluginTypesFromAssemblyAsync(assembly, loadContext, metadata, cancellationToken);

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
            LogPluginLoadFailed(assemblyPath, ex.Message);
            throw;
        }
        finally
        {
            _ = _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// Loads a specific plugin type from an assembly.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <param name="typeName">The full type name to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded plugin result if successful; otherwise, null.</returns>
    public async Task<LoadedPluginResult?> LoadPluginByTypeAsync(
        string assemblyPath,
        string typeName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var allPlugins = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken: cancellationToken);
        return allPlugins.FirstOrDefault(p => p.Plugin.GetType().FullName == typeName);
    }

    /// <summary>
    /// Unloads a plugin assembly and its load context.
    /// </summary>
    /// <param name="assemblyName">The assembly name to unload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully unloaded; otherwise, false.</returns>
    public async Task<bool> UnloadAssemblyAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        if (!_loadContexts.TryRemove(assemblyName, out var loadContext))
        {
            return false;
        }

        try
        {
            // Trigger unload
            loadContext.Unload();

            // Wait for garbage collection to complete unload
            for (var i = 0; i < 10 && loadContext.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100, cancellationToken);
            }

            var success = !loadContext.IsAlive;
            LogAssemblyUnloaded(assemblyName, success);
            return success;
        }
        catch (Exception ex)
        {
            LogAssemblyUnloadFailed(assemblyName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets information about all loaded assemblies.
    /// </summary>
    /// <returns>Collection of loaded assembly information.</returns>
    public IReadOnlyList<LoadedAssemblyInfo> GetLoadedAssemblies()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _loadContexts.Select(kvp => new LoadedAssemblyInfo
        {
            AssemblyName = kvp.Key,
            LoadContextName = kvp.Value.Name ?? "Unknown",
            IsAlive = kvp.Value.IsAlive,
            IsCollectible = kvp.Value.IsCollectible
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a plugin instance from a type with proper error handling.
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
    /// Loads plugin types from an assembly.
    /// </summary>
    private async Task<IReadOnlyList<LoadedPluginResult>> LoadPluginTypesFromAssemblyAsync(
        Assembly assembly,
        PluginAssemblyLoadContext loadContext,
        PluginMetadata? metadata,
        CancellationToken cancellationToken)
    {
        var loadedPlugins = new List<LoadedPluginResult>();

        // Discover plugin types
        var pluginTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

        foreach (var pluginType in pluginTypes)
        {
            try
            {
                if (CreatePluginInstance(pluginType) is IAlgorithmPlugin plugin)
                {
                    var pluginMetadata = metadata ?? CreateDefaultMetadata(plugin, assembly.Location);

                    var result = new LoadedPluginResult
                    {
                        Plugin = plugin,
                        LoadContext = loadContext,
                        Assembly = assembly,
                        Metadata = pluginMetadata,
                        LoadTime = DateTime.UtcNow,
                        Success = true
                    };

                    loadedPlugins.Add(result);
                    LogPluginLoaded(plugin.Id, pluginType.Name);
                }
            }
            catch (Exception ex)
            {
                var errorResult = new LoadedPluginResult
                {
                    LoadContext = loadContext,
                    Assembly = assembly,
                    LoadTime = DateTime.UtcNow,
                    Success = false,
                    Error = ex
                };

                loadedPlugins.Add(errorResult);
                LogPluginInstanceFailed(pluginType.Name, ex.Message);
            }
        }

        return loadedPlugins.AsReadOnly();
    }

    /// <summary>
    /// Creates default metadata for a plugin.
    /// </summary>
    private static PluginMetadata CreateDefaultMetadata(IAlgorithmPlugin plugin, string assemblyPath)
    {
        return new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version.ToString(),
            Description = plugin.Description,
            Author = "Unknown",
            AssemblyPath = assemblyPath,
            LoadTime = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _loadingSemaphore.Dispose();

            // Unload all contexts
            foreach (var kvp in _loadContexts)
            {
                try
                {
                    kvp.Value.Unload();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error unloading assembly context {ContextName}", kvp.Key);
                }
            }

            _loadContexts.Clear();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading assembly: {AssemblyPath}")]
    private partial void LogLoadingAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly {AssemblyName} is already loaded")]
    private partial void LogAssemblyAlreadyLoaded(string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} plugins from assembly {AssemblyName}")]
    private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugins from assembly {AssemblyPath}: {Reason}")]
    private partial void LogPluginLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded plugin {PluginId} of type {TypeName}")]
    private partial void LogPluginLoaded(string pluginId, string typeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create plugin instance for type {TypeName}: {Reason}")]
    private partial void LogPluginInstanceFailed(string typeName, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unloaded assembly {AssemblyName}, success: {Success}")]
    private partial void LogAssemblyUnloaded(string assemblyName, bool success);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to unload assembly {AssemblyName}: {Reason}")]
    private partial void LogAssemblyUnloadFailed(string assemblyName, string reason);

    #endregion
}

/// <summary>
/// Result of a plugin loading operation.
/// </summary>
public sealed partial class LoadedPluginResult
{
    /// <summary>
    /// Gets or sets the loaded plugin instance (if successful).
    /// </summary>
    public IAlgorithmPlugin? Plugin { get; init; }

    /// <summary>
    /// Gets or sets the load context.
    /// </summary>
    public required PluginAssemblyLoadContext LoadContext { get; init; }

    /// <summary>
    /// Gets or sets the assembly.
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Gets or sets the plugin metadata.
    /// </summary>
    public PluginMetadata? Metadata { get; init; }

    /// <summary>
    /// Gets or sets the load time.
    /// </summary>
    public DateTime LoadTime { get; init; }

    /// <summary>
    /// Gets or sets whether the loading was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the error (if loading failed).
    /// </summary>
    public Exception? Error { get; init; }
}

/// <summary>
/// Information about a loaded assembly.
/// </summary>
public sealed partial class LoadedAssemblyInfo
{
    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Gets or sets the load context name.
    /// </summary>
    public required string LoadContextName { get; init; }

    /// <summary>
    /// Gets or sets whether the load context is still alive.
    /// </summary>
    public bool IsAlive { get; init; }

    /// <summary>
    /// Gets or sets whether the load context is collectible.
    /// </summary>
    public bool IsCollectible { get; init; }
}

// PluginAssemblyLoadContext class moved to dedicated file: Management/Loading/PluginAssemblyLoadContext.cs