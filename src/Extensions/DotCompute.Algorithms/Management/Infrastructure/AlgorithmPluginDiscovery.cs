
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Infrastructure;

/// <summary>
/// Handles discovery of algorithm plugins from directories and file system monitoring.
/// Manages file watchers for hot reload functionality and plugin file detection.
/// </summary>
public sealed partial class AlgorithmPluginDiscovery(
    ILogger<AlgorithmPluginDiscovery> logger,
    AlgorithmPluginManagerOptions options) : IDisposable
{
    private readonly ILogger<AlgorithmPluginDiscovery> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private bool _disposed;
    /// <summary>
    /// Occurs when plugin file changed.
    /// </summary>

    /// <summary>
    /// Event raised when a plugin file is changed (for hot reload).
    /// </summary>
    public event EventHandler<PluginFileChangedEventArgs>? PluginFileChanged;
    /// <summary>
    /// Occurs when watcher error.
    /// </summary>

    /// <summary>
    /// Event raised when file watcher encounters an error.
    /// </summary>
    public event EventHandler<FileWatcherErrorEventArgs>? WatcherError;

    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered plugin assembly paths.</returns>
    public async Task<List<string>> DiscoverPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            LogDirectoryNotFound(pluginDirectory);
            return [];
        }

        LogDiscoveringPlugins(pluginDirectory);

        var pluginFiles = new List<string>();

        try
        {
            // Discover DLL files
            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories)
                .Where(file => !IsSystemAssembly(file))
                .ToList();

            // Discover EXE files (standalone plugins)
            var exeFiles = Directory.GetFiles(pluginDirectory, "*.exe", SearchOption.AllDirectories)
                .Where(file => !IsSystemAssembly(file))
                .ToList();

            pluginFiles.AddRange(dllFiles);
            pluginFiles.AddRange(exeFiles);

            // Filter by security policies if enabled
            if (_options.EnableSecurityValidation)
            {
                pluginFiles = await FilterBySecurityPolicyAsync(pluginFiles, cancellationToken).ConfigureAwait(false);
            }

            LogPluginsDiscovered(pluginFiles.Count, pluginDirectory);

            // Setup hot reload monitoring if enabled
            if (_options.EnableHotReload)
            {
                foreach (var pluginFile in pluginFiles)
                {
                    SetupHotReload(pluginFile);
                }
            }

            return pluginFiles;
        }
        catch (Exception ex)
        {
            LogPluginDiscoveryFailed(pluginDirectory, ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Discovers plugins in multiple directories.
    /// </summary>
    /// <param name="pluginDirectories">The directories to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping directory to discovered plugin paths.</returns>
    public async Task<Dictionary<string, List<string>>> DiscoverPluginsInDirectoriesAsync(
        IEnumerable<string> pluginDirectories,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pluginDirectories);

        var results = new Dictionary<string, List<string>>();
        var discoveryTasks = pluginDirectories.Select(async directory =>
        {
            var plugins = await DiscoverPluginsAsync(directory, cancellationToken).ConfigureAwait(false);
            return new { Directory = directory, Plugins = plugins };
        });

        var discoveryResults = await Task.WhenAll(discoveryTasks).ConfigureAwait(false);

        foreach (var result in discoveryResults)
        {
            results[result.Directory] = result.Plugins;
        }

        return results;
    }

    /// <summary>
    /// Sets up hot reload monitoring for a plugin assembly with enhanced file system monitoring.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to monitor.</param>
    public void SetupHotReload(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

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

            // Handle file changes
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Error += OnWatcherError;

            // Also watch for .pdb files (debug symbols) and .json manifest files
            SetupAdditionalFileWatchers(assemblyPath);

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
    /// <param name="assemblyPath">Path to the assembly to stop monitoring.</param>
    public void StopHotReload(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (_watchers.TryRemove(assemblyPath, out var watcher))
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileChanged;
                watcher.Created -= OnFileChanged;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
                LogHotReloadStopped(assemblyPath);
            }
            catch (Exception ex)
            {
                LogHotReloadStopFailed(assemblyPath, ex.Message);
            }
        }

        // Also stop additional file watchers
        StopAdditionalFileWatchers(assemblyPath);
    }

    /// <summary>
    /// Gets the file extensions that are monitored for plugins.
    /// </summary>
    /// <returns>Array of file extensions.</returns>
    public static string[] GetMonitoredExtensions() => [".dll", ".exe", ".pdb", ".json"];

    /// <summary>
    /// Checks if a file is a potential plugin assembly.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>True if it could be a plugin assembly; otherwise, false.</returns>
    public bool IsPotentialPluginFile(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var extension = Path.GetExtension(filePath).ToUpperInvariant();
        return (extension == ".dll" || extension == ".exe") && !IsSystemAssembly(filePath);
    }

    /// <summary>
    /// Validates that a directory is allowed for plugin discovery.
    /// </summary>
    /// <param name="directory">The directory to validate.</param>
    /// <returns>True if the directory is allowed; otherwise, false.</returns>
    public bool IsAllowedDirectory(string directory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (_options.AllowedPluginDirectories.Count == 0)
        {
            return true; // No restrictions
        }

        var normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        return _options.AllowedPluginDirectories.Any(allowed =>
        {
            var normalizedAllowed = Path.GetFullPath(allowed).TrimEnd(Path.DirectorySeparatorChar);
            return normalizedDirectory.StartsWith(normalizedAllowed, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Sets up additional file watchers for related files (PDB, JSON manifests).
    /// </summary>
    private void SetupAdditionalFileWatchers(string assemblyPath)
    {
        var directory = Path.GetDirectoryName(assemblyPath);
        if (directory == null)
        {
            return;
        }

        // Watch for .pdb files (debug symbols)

        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (File.Exists(pdbPath))
        {
            SetupFileWatcher(pdbPath, directory);
        }

        // Watch for .json manifest files
        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
        if (File.Exists(manifestPath))
        {
            SetupFileWatcher(manifestPath, directory);
        }
    }

    /// <summary>
    /// Sets up a file watcher for a specific file.
    /// </summary>
    private void SetupFileWatcher(string filePath, string directory)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            watcher.Error += OnWatcherError;
            _ = _watchers.TryAdd(filePath, watcher);
        }
        catch (Exception ex)
        {
            LogFileWatcherSetupFailed(filePath, ex.Message);
        }
    }

    /// <summary>
    /// Stops additional file watchers for related files.
    /// </summary>
    private void StopAdditionalFileWatchers(string assemblyPath)
    {
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");

        foreach (var path in new[] { pdbPath, manifestPath })
        {
            if (_watchers.TryRemove(path, out var watcher))
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    LogFileWatcherStopFailed(path, ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Handles file change events.
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Find the main assembly path for this file change
            var assemblyPath = e.FullPath;
            if (Path.GetExtension(assemblyPath).Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(assemblyPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                assemblyPath = Path.ChangeExtension(assemblyPath, ".dll");
                if (!File.Exists(assemblyPath))
                {
                    assemblyPath = Path.ChangeExtension(e.FullPath, ".exe");
                }
            }

            PluginFileChanged?.Invoke(this, new PluginFileChangedEventArgs
            {
                AssemblyPath = assemblyPath,
                ChangedFilePath = e.FullPath,
                ChangeType = e.ChangeType
            });

            LogFileChanged(e.FullPath, e.ChangeType.ToString());
        }
        catch (Exception ex)
        {
            LogFileChangeHandlingFailed(e.FullPath, ex.Message);
        }
    }

    /// <summary>
    /// Handles file watcher errors.
    /// </summary>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        try
        {
            WatcherError?.Invoke(this, new FileWatcherErrorEventArgs
            {
                Error = e.GetException(),
                Watcher = sender as FileSystemWatcher
            });

            LogWatcherError(e.GetException().Message);

            // Try to restart the watcher if it's a temporary error
            if (sender is FileSystemWatcher watcher)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        await Task.Delay(1000).ConfigureAwait(false);
                        watcher.EnableRaisingEvents = true;
                        LogWatcherRestarted(watcher.Path);
                    }
                    catch (Exception restartEx)
                    {
                        LogWatcherRestartFailed(watcher.Path, restartEx.Message);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogWatcherErrorHandlingFailed(ex.Message);
        }
    }

    /// <summary>
    /// Filters plugin files by security policy.
    /// </summary>
    private async Task<List<string>> FilterBySecurityPolicyAsync(IReadOnlyList<string> pluginFiles, CancellationToken cancellationToken)
    {
        var filteredFiles = new List<string>();

        foreach (var file in pluginFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsFileAllowedBySecurityPolicyAsync(file).ConfigureAwait(false))
            {
                filteredFiles.Add(file);
            }
            else
            {
                LogFileRejectedBySecurityPolicy(file);
            }
        }

        return filteredFiles;
    }

    /// <summary>
    /// Checks if a file is allowed by security policy.
    /// </summary>
    private async Task<bool> IsFileAllowedBySecurityPolicyAsync(string filePath)
    {
        try
        {
            // Basic checks
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > _options.MaxAssemblySize)
            {
                return false;
            }

            // Check directory policy
            if (!IsAllowedDirectory(Path.GetDirectoryName(filePath) ?? string.Empty))
            {
                return false;
            }

            await Task.CompletedTask; // Make async for consistency
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines if an assembly is a system assembly that should be ignored.
    /// </summary>
    private static bool IsSystemAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath).ToUpperInvariant();
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

            // Stop all watchers
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    LogWatcherDisposeFailed(ex);
                }
            }

            _watchers.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory not found: {Directory}")]
    private partial void LogDirectoryNotFound(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered {Count} plugins in directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to discover plugins in {Directory}: {Reason}")]
    private partial void LogPluginDiscoveryFailed(string directory, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload setup for {AssemblyPath}")]
    private partial void LogHotReloadSetup(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload setup failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadSetupFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload stopped for {AssemblyPath}")]
    private partial void LogHotReloadStopped(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload stop failed for {AssemblyPath}: {Reason}")]
    private partial void LogHotReloadStopFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "File watcher setup failed for {FilePath}: {Reason}")]
    private partial void LogFileWatcherSetupFailed(string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "File watcher stop failed for {FilePath}: {Reason}")]
    private partial void LogFileWatcherStopFailed(string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "File changed: {FilePath}, Change: {ChangeType}")]
    private partial void LogFileChanged(string filePath, string changeType);

    [LoggerMessage(Level = LogLevel.Error, Message = "File change handling failed for {FilePath}: {Reason}")]
    private partial void LogFileChangeHandlingFailed(string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Watcher error: {Error}")]
    private partial void LogWatcherError(string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Watcher restarted for path: {Path}")]
    private partial void LogWatcherRestarted(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Watcher restart failed for {Path}: {Reason}")]
    private partial void LogWatcherRestartFailed(string path, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Watcher error handling failed: {Error}")]
    private partial void LogWatcherErrorHandlingFailed(string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File rejected by security policy: {FilePath}")]
    private partial void LogFileRejectedBySecurityPolicy(string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispose file watcher")]
    private partial void LogWatcherDisposeFailed(Exception ex);

    #endregion
}

/// <summary>
/// Event arguments for plugin file change events.
/// </summary>
public sealed partial class PluginFileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    /// <value>The assembly path.</value>
    public required string AssemblyPath { get; init; }
    /// <summary>
    /// Gets or sets the changed file path.
    /// </summary>
    /// <value>The changed file path.</value>
    public required string ChangedFilePath { get; init; }
    /// <summary>
    /// Gets or sets the change type.
    /// </summary>
    /// <value>The change type.</value>
    public required WatcherChangeTypes ChangeType { get; init; }
}

/// <summary>
/// Event arguments for file watcher error events.
/// </summary>
public sealed partial class FileWatcherErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>The error.</value>
    public required Exception Error { get; init; }
    /// <summary>
    /// Gets or sets the watcher.
    /// </summary>
    /// <value>The watcher.</value>
    public required FileSystemWatcher? Watcher { get; init; }
}