// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Plugins.Exceptions;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - plugin loading has dynamic logging requirements

namespace DotCompute.Plugins.Core
{
    /// <summary>
    /// Simplified plugin system for DotCompute.
    /// </summary>
    public class PluginSystem : IDisposable
    {
        private readonly ILogger<PluginSystem> _logger;
        private readonly Dictionary<string, LoadedPlugin> _plugins;
        private readonly Lock _lock = new();
        private bool _disposed;
        private bool _isInitialized;
        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginSystem"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        public PluginSystem(ILogger<PluginSystem> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugins = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginSystem"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        public PluginSystem(ILogger<PluginSystem> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _plugins = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginSystem"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="logger">The logger.</param>
        public PluginSystem(Configuration.PluginOptions options, ILogger<PluginSystem>? logger = null)
        {
            _logger = logger ?? new NullLogger<PluginSystem>();
            _plugins = [];
        }

        /// <summary>
        /// Gets whether the plugin system is initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the plugin system.
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _isInitialized = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads a plugin from assembly path.
        /// </summary>
        public async Task<IBackendPlugin?> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            // Auto-discover the plugin type from the assembly
            var pluginType = await DiscoverPluginTypeAsync(assemblyPath);
            if (pluginType != null)
            {
                return await LoadPluginAsync(assemblyPath, pluginType, cancellationToken);
            }
            return null;
        }

        /// <summary>
        /// Loads a plugin instance directly.
        /// </summary>
        public async Task<IBackendPlugin?> LoadPluginAsync(IBackendPlugin plugin, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            ArgumentNullException.ThrowIfNull(plugin);

            try
            {
                _logger?.LogInformation("Loading plugin {Id} ({Name})", plugin.Id, plugin.Name);

                // Validate the plugin first
                var validationResult = plugin.Validate();
                if (!validationResult.IsValid)
                {
                    throw new PluginLoadException($"Plugin validation failed: {string.Join(", ", validationResult.Errors)}", plugin.Id, "");
                }

                // Store loaded plugin info
                lock (_lock)
                {
                    _plugins[plugin.Id] = new LoadedPlugin
                    {
                        Plugin = plugin,
                        LoadContext = null!, // Not applicable for direct plugin loading
                        Assembly = plugin.GetType().Assembly,
                        LoadTime = DateTime.UtcNow
                    };
                }

                // Set plugin state to Loaded if it implements BackendPluginBase
                if (plugin is BackendPluginBase basePlugin)
                {
                    basePlugin.SetState(PluginState.Loaded);
                }

                // Initialize the plugin with cancellation support
                if (_serviceProvider != null)
                {
                    await plugin.InitializeAsync(_serviceProvider, cancellationToken);
                }

                _logger?.LogInformation("Successfully loaded plugin {Id} ({Name})", plugin.Id, plugin.Name);
                return plugin;
            }
            catch (PluginLoadException)
            {
                // Re-throw plugin load exceptions without wrapping
                throw;
            }
            catch (OperationCanceledException)
            {
                // Remove plugin from collection if initialization was cancelled
                lock (_lock)
                {
                    _plugins.Remove(plugin.Id);
                }
                _logger?.LogWarning("Plugin {Id} loading was cancelled", plugin.Id);
                throw;
            }
            catch (Exception ex)
            {
                // Remove plugin from collection on any failure
                lock (_lock)
                {
                    _plugins.Remove(plugin.Id);
                }
                _logger?.LogError(ex, "Failed to load plugin {Id}", plugin.Id);
                throw new PluginLoadException($"Failed to load plugin {plugin.Id}", plugin.Id, "", ex);
            }
        }

        /// <summary>
        /// Loads a plugin from assembly path.
        /// </summary>
        public Task<IBackendPlugin?> LoadPluginAsync(string assemblyPath, string pluginTypeName, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                _logger.LogInformation("Loading plugin from {Path}, type: {Type}", assemblyPath, pluginTypeName);

                // Create isolated load context
                var context = new PluginAssemblyLoadContext(assemblyPath);

                // Load the assembly
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' - Plugin loading requires dynamic assembly loading
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026

                // Find the plugin type
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' - Plugin loading requires dynamic type loading
                var pluginType = assembly.GetType(pluginTypeName);
#pragma warning restore IL2026
                if (pluginType == null)
                {
                    _logger.LogError("Plugin type {Type} not found in assembly", pluginTypeName);
                    return Task.FromResult<IBackendPlugin?>(null);
                }

                // Verify it implements IBackendPlugin
                if (!typeof(IBackendPlugin).IsAssignableFrom(pluginType))
                {
                    _logger.LogError("Type {Type} does not implement IBackendPlugin", pluginTypeName);
                    return Task.FromResult<IBackendPlugin?>(null);
                }

                // Create instance - use factory method for AOT compatibility
#pragma warning disable IL2072 // DynamicallyAccessedMembers - Plugin instantiation requires dynamic type handling
                var instance = CreatePluginInstance(pluginType);
#pragma warning restore IL2072
                if (instance == null)
                {
                    _logger.LogError("Failed to create instance of {Type}", pluginTypeName);
                    return Task.FromResult<IBackendPlugin?>(null);
                }

                // Store loaded plugin info
                lock (_lock)
                {
                    _plugins[instance.Id] = new LoadedPlugin
                    {
                        Plugin = instance,
                        LoadContext = context,
                        Assembly = assembly,
                        LoadTime = DateTime.UtcNow
                    };
                }

                // Set plugin state to Loaded if it implements BackendPluginBase
                if (instance is BackendPluginBase basePlugin)
                {
                    basePlugin.SetState(PluginState.Loaded);
                }

                _logger.LogInformation("Successfully loaded plugin {Id} ({Name})", instance.Id, instance.Name);
                return Task.FromResult<IBackendPlugin?>(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Path}", assemblyPath);
                return Task.FromResult<IBackendPlugin?>(null);
            }
        }

        /// <summary>
        /// Unloads a plugin.
        /// </summary>
        public Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("Plugin ID cannot be null, empty, or whitespace.", nameof(pluginId));
            }

            lock (_lock)
            {
                if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
                {
                    _logger.LogWarning("Plugin {Id} not found", pluginId);
                    return Task.FromResult(false);
                }

                try
                {
                    _logger.LogInformation("Unloading plugin {Id}", pluginId);

                    var disposalSucceeded = true;
                    var contextUnloadSucceeded = true;

                    // Try to dispose the plugin
                    try
                    {
                        loadedPlugin.Plugin.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispose plugin {Id}, continuing with unload", pluginId);
                        disposalSucceeded = false;
                    }

                    // Remove from collection regardless of disposal result
                    _plugins.Remove(pluginId);

                    // Try to unload the context if it exists
                    try
                    {
                        if (loadedPlugin.LoadContext != null)
                        {
                            loadedPlugin.LoadContext.Unload();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to unload context for plugin {Id}, continuing", pluginId);
                        contextUnloadSucceeded = false;
                    }

                    var success = disposalSucceeded && contextUnloadSucceeded;
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully unloaded plugin {Id}", pluginId);
                    }
                    else
                    {
                        _logger.LogWarning("Plugin {Id} was removed but disposal/context unload had issues", pluginId);
                    }
                    
                    return Task.FromResult(success);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error during plugin {Id} unload", pluginId);
                    
                    // Try to remove from collection as last resort
                    try
                    {
                        _plugins.Remove(pluginId);
                    }
                    catch
                    {
                        // Ignore - we tried our best
                    }
                    
                    return Task.FromResult(false);
                }
            }
        }

        /// <summary>
        /// Gets a loaded plugin by ID.
        /// </summary>
        public IBackendPlugin? GetPlugin(string pluginId)
        {
            lock (_lock)
            {
                return _plugins.TryGetValue(pluginId, out var plugin) ? plugin.Plugin : null;
            }
        }

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        public IEnumerable<IBackendPlugin> GetLoadedPlugins()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lock)
            {
                return [.. _plugins.Values.Select(p => p.Plugin)];
            }
        }

        /// <summary>
        /// Discovers plugin types in an assembly.
        /// </summary>
        public static IEnumerable<Type> DiscoverPluginTypes(Assembly assembly)
        {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' - Plugin discovery requires type enumeration
            return assembly.GetTypes()
#pragma warning restore IL2026
                .Where(t => !t.IsAbstract &&
                           !t.IsInterface &&
                           typeof(IBackendPlugin).IsAssignableFrom(t));
        }

        /// <summary>
        /// Discovers the first plugin type in an assembly.
        /// </summary>
        private Task<string?> DiscoverPluginTypeAsync(string assemblyPath)
        {
            try
            {
                // Create temporary load context
                var context = new PluginAssemblyLoadContext(assemblyPath);
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' - Plugin loading requires dynamic assembly loading
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026

                var pluginTypes = DiscoverPluginTypes(assembly);
                var firstType = pluginTypes.FirstOrDefault();

                return Task.FromResult(firstType?.FullName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to discover plugin type in assembly {Path}", assemblyPath);
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>
        /// Creates a plugin instance using AOT-compatible factory method.
        /// Falls back to Activator.CreateInstance if dynamic code is available.
        /// </summary>
        private static IBackendPlugin? CreatePluginInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pluginType)
        {
            // For AOT compatibility, prefer factory methods or constructors with known signatures
            try
            {
                // Try to find a parameterless constructor
                var constructor = pluginType.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                {
                    // Use constructor directly for better AOT compatibility
                    return constructor.Invoke(null) as IBackendPlugin;
                }

                // Fall back to Activator.CreateInstance if dynamic code is available
                if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled)
                {
                    return Activator.CreateInstance(pluginType) as IBackendPlugin;
                }

                // For full AOT, we would need pre-registered factories
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                lock (_lock)
                {
                    foreach (var plugin in _plugins.ToList())
                    {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - acceptable in Dispose pattern
                        UnloadPluginAsync(plugin.Key).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                    }
                }
            }

            _disposed = true;
        }

        private sealed class LoadedPlugin
        {
            public IBackendPlugin Plugin { get; set; } = null!;
            public PluginAssemblyLoadContext LoadContext { get; set; } = null!;
            public Assembly Assembly { get; set; } = null!;
            public DateTime LoadTime { get; set; }
        }
    }

    /// <summary>
    /// Assembly load context for plugin isolation.
    /// </summary>
    public class PluginAssemblyLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        /// <summary>
        /// When overridden in a derived class, allows an assembly to be resolved based on its <see cref="System.Reflection.AssemblyName" />.
        /// </summary>
        /// <param name="assemblyName">The object that describes the assembly to be resolved.</param>
        /// <returns>
        /// The resolved assembly, or <see langword="null" />.
        /// </returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' - Plugin assembly loading requires dynamic loading
                return LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026
            }

            return null;
        }

        /// <summary>
        /// Allows derived class to load an unmanaged library by name.
        /// </summary>
        /// <param name="unmanagedDllName">Name of the unmanaged library. Typically this is the filename without its path or extensions.</param>
        /// <returns>
        /// A handle to the loaded library, or <see cref="nint.Zero" />.
        /// </returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
