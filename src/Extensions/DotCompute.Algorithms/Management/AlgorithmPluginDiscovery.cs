// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Logging;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles the discovery of algorithm plugins in directories and NuGet packages.
    /// </summary>
    public sealed partial class AlgorithmPluginDiscovery(ILogger<AlgorithmPluginDiscovery> logger, AlgorithmPluginManagerOptions options) : IDisposable
    {
        private readonly ILogger<AlgorithmPluginDiscovery> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
        private bool _disposed;

        /// <summary>
        /// Discovers plugins in the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory to scan for plugins.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of discovered plugin assembly files.</returns>
        public Task<IReadOnlyList<string>> DiscoverPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

            if (!Directory.Exists(pluginDirectory))
            {
                LogDirectoryNotFound(pluginDirectory);
                return Task.FromResult<IReadOnlyList<string>>([]);
            }

            LogDiscoveringPlugins(pluginDirectory);

            // Run file discovery on thread pool to avoid blocking
            return Task.Run(() =>
            {
                var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories)
                    .Where(file => !IsSystemAssembly(file))
                    .ToList();

                LogPluginsDiscovered(pluginFiles.Count, pluginDirectory);
                return (IReadOnlyList<string>)pluginFiles.AsReadOnly();
            }, cancellationToken);
        }

        /// <summary>
        /// Discovers plugin types within an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>The list of discovered plugin types.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
            Justification = "Plugin loading requires dynamic assembly inspection by design")]
        public static IEnumerable<Type> DiscoverPluginTypes(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));
        }

        /// <summary>
        /// Sets up hot reload monitoring for a plugin assembly.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <param name="onChanged">Callback when file changes are detected.</param>
        public void SetupHotReload(string assemblyPath, Func<string, Task> onChanged)
        {
            if (_disposed || !_options.EnableHotReload)
            {
                return;
            }

            if (_watchers.ContainsKey(assemblyPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(assemblyPath);
                if (directory == null)
                {
                    return;
                }


                var fileName = Path.GetFileName(assemblyPath);

                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                watcher.Changed += async (sender, e) => await HandleAssemblyChangedAsync(e.FullPath, onChanged);
                watcher.Created += async (sender, e) => await HandleAssemblyChangedAsync(e.FullPath, onChanged);
                watcher.Error += (sender, e) => LogFileWatcherError(e.GetException());

                // Also watch for .pdb files (debug symbols) and .json manifest files
                SetupAdditionalFileWatchers(assemblyPath, onChanged);

                _ = _watchers.TryAdd(assemblyPath, watcher);
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
        /// <param name="assemblyPath">The path to the assembly file.</param>
        public void StopHotReload(string assemblyPath)
        {
            if (_watchers.TryRemove(assemblyPath, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                LogHotReloadStopped(assemblyPath);
            }
        }

        private void SetupAdditionalFileWatchers(string assemblyPath, Func<string, Task> onChanged)
        {
            var directory = Path.GetDirectoryName(assemblyPath);
            if (directory == null)
            {
                return;
            }


            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var manifestPath = Path.ChangeExtension(assemblyPath, ".json");

            if (File.Exists(pdbPath))
            {
                var pdbWatcher = new FileSystemWatcher(directory, Path.GetFileName(pdbPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                pdbWatcher.Changed += async (sender, e) => await HandleAssemblyChangedAsync(assemblyPath, onChanged);
                _ = _watchers.TryAdd(pdbPath, pdbWatcher);
            }

            if (File.Exists(manifestPath))
            {
                var manifestWatcher = new FileSystemWatcher(directory, Path.GetFileName(manifestPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                manifestWatcher.Changed += async (sender, e) => await HandleAssemblyChangedAsync(assemblyPath, onChanged);
                _ = _watchers.TryAdd(manifestPath, manifestWatcher);
            }
        }

        private async Task HandleAssemblyChangedAsync(string filePath, Func<string, Task> onChanged)
        {
            try
            {
                // Small delay to allow file to be fully written
                await Task.Delay(500);

                // Find the corresponding assembly path
                var assemblyPath = GetAssemblyPathFromFile(filePath);
                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                {
                    LogHotReloadTriggered(filePath);
                    await onChanged(assemblyPath);
                }
            }
            catch (Exception ex)
            {
                LogHotReloadFailed(filePath, ex.Message);
            }
        }

        private static string GetAssemblyPathFromFile(string filePath)
        {
            // If this is a .pdb or .json file, find the corresponding assembly
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension is ".pdb" or ".json")
            {
                var assemblyPath = Path.ChangeExtension(filePath, ".dll");
                if (File.Exists(assemblyPath))
                {
                    return assemblyPath;
                }


                assemblyPath = Path.ChangeExtension(filePath, ".exe");
                if (File.Exists(assemblyPath))
                {

                    return assemblyPath;
                }

            }

            return filePath;
        }

        /// <summary>
        /// Determines if an assembly is a system assembly that should be ignored.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <returns>True if the assembly is a system assembly; otherwise, false.</returns>
        public static bool IsSystemAssembly(string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath).ToLowerInvariant();
            return fileName.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogFileWatcherDisposeFailed(ex);
                    }
                }
                _watchers.Clear();

                LogDiscoveryDisposed();
            }
        }

        // Logger messages
        [LoggerMessage(Level = LogLevel.Warning, Message = "Directory not found: {Directory}")]
        private partial void LogDirectoryNotFound(string directory);

        [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
        private partial void LogDiscoveringPlugins(string directory);

        [LoggerMessage(Level = LogLevel.Information, Message = "Discovered {Count} plugins in directory: {Directory}")]
        private partial void LogPluginsDiscovered(int count, string directory);

        [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload setup for {AssemblyPath}")]
        private partial void LogHotReloadSetup(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload stopped for {AssemblyPath}")]
        private partial void LogHotReloadStopped(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload setup failed for {AssemblyPath}: {Reason}")]
        private partial void LogHotReloadSetupFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload triggered for {FilePath}")]
        private partial void LogHotReloadTriggered(string filePath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload failed for {FilePath}: {Reason}")]
        private partial void LogHotReloadFailed(string filePath, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "File watcher error occurred")]
        private partial void LogFileWatcherError(Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing file watcher")]
        private partial void LogFileWatcherDisposeFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Information, Message = "AlgorithmPluginDiscovery disposed")]
        private partial void LogDiscoveryDisposed();
    }
}