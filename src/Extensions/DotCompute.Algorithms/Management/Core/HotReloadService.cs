
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Algorithms.Logging;
using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Service responsible for hot reload functionality.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HotReloadService"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="lifecycleManager">The plugin lifecycle manager.</param>
/// <param name="options">Configuration options.</param>
public sealed partial class HotReloadService(
    ILogger<HotReloadService> logger,
    IPluginLifecycleManager lifecycleManager,
    AlgorithmPluginManagerOptions options) : IHotReloadService, IDisposable
{
    private readonly ILogger<HotReloadService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPluginLifecycleManager _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private bool _disposed;

    /// <inheritdoc/>
    public void SetupHotReload(string assemblyPath)
    {
        if (!_options.EnableHotReload || _watchers.ContainsKey(assemblyPath))
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

            // Handle both file changes and dependency changes
            watcher.Changed += (sender, e) => _ = HandleAssemblyChangedAsync(sender, e);
            watcher.Created += (sender, e) => _ = HandleAssemblyChangedAsync(sender, e);
            watcher.Error += (sender, e) => _ = HandleFileWatcherErrorAsync(sender, e);

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
                pdbWatcher.Changed += (sender, e) => _ = HandleAssemblyChangedAsync(sender, e);
                _ = _watchers.TryAdd(pdbPath, pdbWatcher);
            }

            if (File.Exists(manifestPath))
            {
                var manifestWatcher = new FileSystemWatcher(directory, Path.GetFileName(manifestPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                manifestWatcher.Changed += (sender, e) => _ = HandleAssemblyChangedAsync(sender, e);
                _ = _watchers.TryAdd(manifestPath, manifestWatcher);
            }

            _ = _watchers.TryAdd(assemblyPath, watcher);
            LogHotReloadSetup(assemblyPath);
        }
        catch (Exception ex)
        {
            LogHotReloadSetupFailed(assemblyPath, ex.Message);
        }
    }

    /// <inheritdoc/>
    public void StopHotReload(string assemblyPath)
    {
        if (_watchers.TryRemove(assemblyPath, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            // Event handlers are anonymous lambdas, no need to unsubscribe explicitly
            watcher.Dispose();
        }
    }

    /// <inheritdoc/>
    public void StopAllHotReload()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            // Event handlers are anonymous lambdas, no need to unsubscribe explicitly
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    /// <summary>
    /// Async wrapper for handling assembly file changes.
    /// </summary>
    private async Task HandleAssemblyChangedAsync(object sender, FileSystemEventArgs e)
    {
        try
        {
            await OnAssemblyChangedAsync(sender, e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error handling assembly change for {e.FullPath}");
        }
    }

    /// <summary>
    /// Handles assembly file changes for hot reload.
    /// </summary>
    private async Task OnAssemblyChangedAsync(object sender, FileSystemEventArgs e)
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

            var pluginsToReload = new List<string>();
            foreach (var pluginId in _lifecycleManager.RegisteredPlugins)
            {
                var pluginInfo = _lifecycleManager.GetLoadedPluginInfo(pluginId);
                if (pluginInfo != null &&

                    string.Equals(pluginInfo.Metadata.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
                {
                    pluginsToReload.Add(pluginId);
                }
            }

            foreach (var pluginId in pluginsToReload)
            {
                _ = Task.Run(async () => await _lifecycleManager.ReloadPluginAsync(pluginId).ConfigureAwait(false));
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
    /// Async wrapper for handling file watcher errors.
    /// </summary>
    private async Task HandleFileWatcherErrorAsync(object sender, ErrorEventArgs e)
    {
        try
        {
            await OnFileWatcherErrorAsync(sender, e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error handling file watcher error");
        }
    }

    /// <summary>
    /// Handles file watcher errors.
    /// </summary>
    private async Task OnFileWatcherErrorAsync(object sender, ErrorEventArgs e)
    {
        _logger.LogErrorMessage(e.GetException(), "File watcher error occurred");

        // Try to restart the watcher if it's a temporary error
        if (sender is FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false); // Wait a bit before restarting
                watcher.EnableRaisingEvents = true;
                _logger.LogInfoMessage("Successfully restarted file watcher for: {watcher.Path}");
            }
            catch (Exception restartEx)
            {
                _logger.LogErrorMessage(restartEx, $"Failed to restart file watcher for: {watcher.Path}");
            }
        }
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            StopAllHotReload();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload setup for {AssemblyPath}")]
    private partial void LogHotReloadSetup(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload setup failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadSetupFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload triggered for {FilePath}, reloading {PluginCount} plugins")]
    private partial void LogHotReloadTriggered(string filePath, int pluginCount);

    #endregion
}
