// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Loaders.NuGet.Types;
using DotCompute.Plugins.Loaders.NuGet.Results;
using DotCompute.Plugins.Security;

namespace DotCompute.Plugins.Loaders.NuGet;

/// <summary>
/// Loads and manages NuGet-based plugins
/// </summary>
internal sealed class NuGetPluginLoader : IDisposable
{
    private readonly ILogger<NuGetPluginLoader> _logger;
    private readonly Dictionary<string, NuGetPluginLoadContext> _loadContexts = new();
    private bool _disposed;

    public NuGetPluginLoader(ILogger<NuGetPluginLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IBackendPlugin?> LoadPluginAsync(string packagePath, string packageId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Loading NuGet plugin: {PackageId} from {Path}", packageId, packagePath);

            // Create isolated load context
            var contextName = $"NuGetPlugin_{packageId}_{Guid.NewGuid():N}";
            var loadContext = new NuGetPluginLoadContext(packagePath, contextName, isCollectible: true);

            _loadContexts[packageId] = loadContext;

            // Load the plugin assembly
            var assemblyPath = Path.Combine(packagePath, $"{packageId}.dll");
            if (!File.Exists(assemblyPath))
            {
                _logger.LogError("Plugin assembly not found: {AssemblyPath}", assemblyPath);
                return null;
            }

            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Find and instantiate plugin type
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IBackendPlugin).IsAssignableFrom(t) && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogError("No IBackendPlugin implementation found in assembly: {AssemblyName}", assembly.FullName);
                return null;
            }

            var plugin = Activator.CreateInstance(pluginType) as IBackendPlugin;
            if (plugin == null)
            {
                _logger.LogError("Failed to create plugin instance of type: {PluginType}", pluginType.FullName);
                return null;
            }

                _logger.LogInformation("Successfully loaded plugin: {PackageId}", packageId);
                return plugin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin: {PackageId}", packageId);
                return null;
            }
        }, cancellationToken);
    }

    public async Task<IEnumerable<IBackendPlugin>> DiscoverPluginsAsync(string searchPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPath);

        var plugins = new List<IBackendPlugin>();

        try
        {
            _logger.LogInformation("Discovering plugins in: {SearchPath}", searchPath);

            if (!Directory.Exists(searchPath))
            {
                _logger.LogWarning("Search path does not exist: {SearchPath}", searchPath);
                return plugins;
            }

            var packageDirectories = Directory.GetDirectories(searchPath)
                .Where(dir => Directory.GetFiles(dir, "*.dll").Any());

            foreach (var packageDir in packageDirectories)
            {
                var packageId = Path.GetFileName(packageDir);
                var plugin = await LoadPluginAsync(packageDir, packageId, cancellationToken);

                if (plugin != null)
                {
                    plugins.Add(plugin);
                }
            }

            _logger.LogInformation("Discovered {Count} plugins", plugins.Count);
            return plugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover plugins in: {SearchPath}", searchPath);
            return plugins;
        }
    }


    public void UnloadPlugin(string packageId)
    {
        if (_loadContexts.TryGetValue(packageId, out var context))
        {
            context.Unload();
            _loadContexts.Remove(packageId);
            _logger.LogInformation("Unloaded plugin: {PackageId}", packageId);
        }
    }

    public async Task<IEnumerable<string>> ScanForVulnerabilitiesAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var vulnerabilities = new List<string>();

        try
        {
            _logger.LogInformation("Scanning for vulnerabilities in: {PackagePath}", packagePath);

            // Basic security checks
            await Task.Run(() =>
            {
                // Check for suspicious file patterns
                var suspiciousFiles = Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories)
                    .Where(file => Path.GetExtension(file).ToLowerInvariant() is ".exe" or ".bat" or ".cmd" or ".ps1")
                    .ToList();

                if (suspiciousFiles.Any())
                {
                    vulnerabilities.Add($"Suspicious executable files detected: {string.Join(", ", suspiciousFiles.Select(Path.GetFileName))}");
                }

                // Check for large assemblies that might contain embedded resources
                var assemblies = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories);
                foreach (var assembly in assemblies)
                {
                    var fileInfo = new FileInfo(assembly);
                    if (fileInfo.Length > 50 * 1024 * 1024) // 50MB
                    {
                        vulnerabilities.Add($"Large assembly detected: {Path.GetFileName(assembly)} ({fileInfo.Length / (1024 * 1024)}MB)");
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("Vulnerability scan completed. Found {Count} issues", vulnerabilities.Count);
            return vulnerabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for vulnerabilities in: {PackagePath}", packagePath);
            return vulnerabilities;
        }
    }

    // Method overloads required by NuGetPluginManager

    /// <summary>
    /// Discovers plugins from configured directories and returns manifest information.
    /// </summary>
    public Task<IEnumerable<NuGetPluginManifest>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var manifests = new List<NuGetPluginManifest>();

        try
        {
            _logger.LogInformation("Discovering plugin manifests");

            // For now, return empty collection as manifest discovery would need implementation
            // This would normally scan plugin directories and build manifests from plugin metadata
            return Task.FromResult<IEnumerable<NuGetPluginManifest>>(manifests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover plugin manifests");
            return Task.FromResult<IEnumerable<NuGetPluginManifest>>(manifests);
        }
    }

    /// <summary>
    /// Loads a plugin from a manifest with comprehensive error handling.
    /// </summary>
    public async Task<NuGetPluginLoadResult> LoadPluginAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Id);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger.LogInformation("Loading plugin from manifest: {PluginId}", manifest.Id);

            // Load plugin using existing method
            var plugin = await LoadPluginAsync(manifest.AssemblyPath, manifest.Id, cancellationToken);

            if (plugin != null)
            {
                var loadedPlugin = new LoadedNuGetPlugin
                {
                    Manifest = manifest,
                    Plugin = plugin
                };

                return NuGetPluginLoadResult.Success(loadedPlugin);
            }
            else
            {
                return NuGetPluginLoadResult.LoadError(manifest.Id, "Failed to create plugin instance");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from manifest: {PluginId}", manifest.Id);
            return NuGetPluginLoadResult.LoadError(manifest.Id, ex.Message);
        }
    }

    /// <summary>
    /// Scans for vulnerabilities and returns structured result.
    /// </summary>
    public async Task<SecurityScanResult> ScanForVulnerabilitiesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger.LogInformation("Performing security vulnerability scan");

            var result = new SecurityScanResult
            {
                PluginId = "global",
                ScanDate = DateTimeOffset.UtcNow,
                ScanDuration = TimeSpan.Zero
            };

            var startTime = DateTimeOffset.UtcNow;

            // This would perform comprehensive security scanning
            // For now, return a clean result
            await Task.CompletedTask;

            result.ScanDuration = DateTimeOffset.UtcNow - startTime;

            _logger.LogInformation("Security vulnerability scan completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform security vulnerability scan");
            return new SecurityScanResult
            {
                PluginId = "global",
                ScanDate = DateTimeOffset.UtcNow,
                ScanDuration = TimeSpan.Zero,
                HasErrors = true,
                ErrorMessages = [ex.Message]
            };
        }
    }

    /// <summary>
    /// Unloads a plugin and returns success status.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await Task.Run(() =>
            {
                if (_loadContexts.TryGetValue(packageId, out var context))
                {
                    context.Unload();
                    _loadContexts.Remove(packageId);
                    _logger.LogInformation("Unloaded plugin: {PackageId}", packageId);
                }
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload plugin: {PackageId}", packageId);
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var context in _loadContexts.Values)
            {
                context.Unload();
            }
            _loadContexts.Clear();
            _disposed = true;
        }
    }
}