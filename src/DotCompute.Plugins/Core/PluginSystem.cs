// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

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

        public PluginSystem(ILogger<PluginSystem> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugins = new Dictionary<string, LoadedPlugin>();
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

                // Create instance
                var instance = Activator.CreateInstance(pluginType) as IBackendPlugin;
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