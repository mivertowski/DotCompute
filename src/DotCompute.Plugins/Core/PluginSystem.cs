// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins.Exceptions;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Plugins.Core
{
    /// <summary>
    /// Simplified plugin system for DotCompute.
    /// </summary>
    public class PluginSystem : IDisposable
    {
        private readonly ILogger<PluginSystem> _logger;
        private readonly Dictionary<string, LoadedPlugin> _plugins;
        private readonly object _lock = new();
        private bool _disposed;
        private bool _isInitialized;

        public PluginSystem(ILogger<PluginSystem> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugins = new Dictionary<string, LoadedPlugin>();
        }

        public PluginSystem(Configuration.PluginOptions options, ILogger<PluginSystem>? logger = null)
        {
            _logger = logger ?? new NullLogger<PluginSystem>();
            _plugins = new Dictionary<string, LoadedPlugin>();
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
            if (_disposed)
                throw new ObjectDisposedException(nameof(PluginSystem));

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
            if (_disposed)
                throw new ObjectDisposedException(nameof(PluginSystem));

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

                _logger?.LogInformation("Successfully loaded plugin {Id} ({Name})", plugin.Id, plugin.Name);
                return plugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load plugin {Id}", plugin.Id);
                throw new PluginLoadException($"Failed to load plugin {plugin.Id}", plugin.Id, "", ex);
            }
        }

        /// <summary>
        /// Loads a plugin from assembly path.
        /// </summary>
        public async Task<IBackendPlugin?> LoadPluginAsync(string assemblyPath, string pluginTypeName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PluginSystem));

            try
            {
                _logger.LogInformation("Loading plugin from {Path}, type: {Type}", assemblyPath, pluginTypeName);

                // Create isolated load context
                var context = new PluginAssemblyLoadContext(assemblyPath);

                // Load the assembly
                var assembly = context.LoadFromAssemblyPath(assemblyPath);

                // Find the plugin type
                var pluginType = assembly.GetType(pluginTypeName);
                if (pluginType == null)
                {
                    _logger.LogError("Plugin type {Type} not found in assembly", pluginTypeName);
                    return null;
                }

                // Verify it implements IBackendPlugin
                if (!typeof(IBackendPlugin).IsAssignableFrom(pluginType))
                {
                    _logger.LogError("Type {Type} does not implement IBackendPlugin", pluginTypeName);
                    return null;
                }

                // Create instance - use factory method for AOT compatibility
                var instance = CreatePluginInstance(pluginType);
                if (instance == null)
                {
                    _logger.LogError("Failed to create instance of {Type}", pluginTypeName);
                    return null;
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

                _logger.LogInformation("Successfully loaded plugin {Id} ({Name})", instance.Id, instance.Name);
                return instance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Path}", assemblyPath);
                return null;
            }
        }

        /// <summary>
        /// Unloads a plugin.
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PluginSystem));

            lock (_lock)
            {
                if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
                {
                    _logger.LogWarning("Plugin {Id} not found", pluginId);
                    return false;
                }

                try
                {
                    _logger.LogInformation("Unloading plugin {Id}", pluginId);

                    // Dispose the plugin
                    loadedPlugin.Plugin.Dispose();

                    // Remove from collection
                    _plugins.Remove(pluginId);

                    // Unload the context
                    loadedPlugin.LoadContext.Unload();

                    _logger.LogInformation("Successfully unloaded plugin {Id}", pluginId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unload plugin {Id}", pluginId);
                    return false;
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
            lock (_lock)
            {
                return _plugins.Values.Select(p => p.Plugin).ToList();
            }
        }

        /// <summary>
        /// Discovers plugin types in an assembly.
        /// </summary>
        public static IEnumerable<Type> DiscoverPluginTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => !t.IsAbstract &&
                           !t.IsInterface &&
                           typeof(IBackendPlugin).IsAssignableFrom(t));
        }

        /// <summary>
        /// Discovers the first plugin type in an assembly.
        /// </summary>
        private async Task<string?> DiscoverPluginTypeAsync(string assemblyPath)
        {
            try
            {
                // Create temporary load context
                var context = new PluginAssemblyLoadContext(assemblyPath);
                var assembly = context.LoadFromAssemblyPath(assemblyPath);

                var pluginTypes = DiscoverPluginTypes(assembly);
                var firstType = pluginTypes.FirstOrDefault();

                return firstType?.FullName;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to discover plugin type in assembly {Path}", assemblyPath);
                return null;
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_lock)
            {
                foreach (var plugin in _plugins.ToList())
                {
                    UnloadPluginAsync(plugin.Key).GetAwaiter().GetResult();
                }
            }
        }

        private class LoadedPlugin
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
    public class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
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
    }
}
