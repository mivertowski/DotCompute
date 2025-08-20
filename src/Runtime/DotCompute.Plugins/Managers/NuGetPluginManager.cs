// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Loaders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Plugins.Managers;

/// <summary>
/// Advanced NuGet plugin manager with comprehensive lifecycle management, hot reloading, and monitoring.
/// </summary>
public class NuGetPluginManager : IDisposable
{
    private readonly ILogger<NuGetPluginManager> _logger;
    private readonly NuGetPluginLoader _pluginLoader;
    private readonly NuGetPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, ManagedPlugin> _managedPlugins = new();
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly PluginMetricsCollector _metricsCollector;
    private readonly Timer _periodicMaintenanceTimer;
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private bool _disposed;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginManager"/> class.
    /// </summary>
    public NuGetPluginManager(ILogger<NuGetPluginManager>? logger = null, NuGetPluginManagerOptions? options = null)
    {
        _logger = logger ?? new NullLogger<NuGetPluginManager>();
        _options = options ?? new NuGetPluginManagerOptions();


        var loaderOptions = new NuGetPluginLoaderOptions
        {
            PluginDirectories = _options.PluginDirectories,
            SecurityPolicy = _options.SecurityPolicy,
            DependencyResolution = _options.DependencyResolution,
            CompatibilitySettings = _options.CompatibilitySettings
        };

        // Note: We'd need a logger factory to create a specific logger for NuGetPluginLoader
        // For now, we'll cast the logger if possible or create a null logger

        var pluginLoaderLogger = _logger as ILogger<NuGetPluginLoader> ??

                                 Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance;
        _pluginLoader = new NuGetPluginLoader(pluginLoaderLogger, loaderOptions);
        _healthMonitor = new PluginHealthMonitor(_logger);
        _metricsCollector = new PluginMetricsCollector(_logger);

        // Set up periodic maintenance

        _periodicMaintenanceTimer = new Timer(PeriodicMaintenance, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Gets whether the manager is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the count of managed plugins.
    /// </summary>
    public int PluginCount => _managedPlugins.Count;

    /// <summary>
    /// Gets the count of running plugins.
    /// </summary>
    public int RunningPluginCount => _managedPlugins.Values.Count(p => p.Plugin?.State == PluginState.Running);

    /// <summary>
    /// Raised when a plugin state changes.
    /// </summary>
    public event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    /// <summary>
    /// Raised when a plugin error occurs.
    /// </summary>
    public event EventHandler<PluginErrorEventArgs>? PluginError;

    /// <summary>
    /// Raised when a plugin health status changes.
    /// </summary>
    public event EventHandler<PluginHealthChangedEventArgs>? PluginHealthChanged;

    /// <summary>
    /// Initializes the plugin manager and starts background services.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isInitialized)
        {
            return;
        }

        _logger.LogInformation("Initializing NuGet plugin manager");

        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Start health monitoring
            await _healthMonitor.StartAsync(cancellationToken);

            // Start metrics collection
            await _metricsCollector.StartAsync(cancellationToken);

            // Start periodic maintenance
            _ = _periodicMaintenanceTimer.Change(_options.MaintenanceInterval, _options.MaintenanceInterval);

            // Auto-discover and load plugins if enabled
            if (_options.AutoDiscoverPlugins)
            {
                await DiscoverAndLoadPluginsAsync(cancellationToken);
            }

            _isInitialized = true;
            _logger.LogInformation("NuGet plugin manager initialized successfully");
        }
        finally
        {
            _ = _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Discovers and loads all available plugins.
    /// </summary>
    public async Task DiscoverAndLoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Starting plugin discovery and loading");

        var manifests = await _pluginLoader.DiscoverPluginsAsync(cancellationToken);
        _logger.LogInformation("Discovered {Count} plugins", manifests.Count);

        var loadTasks = new List<Task<(string PluginId, bool Success)>>();

        foreach (var manifest in manifests)
        {
            if (_managedPlugins.ContainsKey(manifest.Id))
            {
                _logger.LogDebug("Plugin {PluginId} is already loaded, skipping", manifest.Id);
                continue;
            }

            loadTasks.Add(LoadPluginSafeAsync(manifest, cancellationToken));
        }

        var results = await Task.WhenAll(loadTasks);
        var successCount = results.Count(r => r.Success);

        _logger.LogInformation("Plugin discovery and loading completed. Loaded: {Loaded}/{Total}",
            successCount, manifests.Count);
    }

    /// <summary>
    /// Loads a plugin by ID from discovered plugins.
    /// </summary>
    public async Task<bool> LoadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        if (_managedPlugins.ContainsKey(pluginId))
        {
            _logger.LogWarning("Plugin {PluginId} is already loaded", pluginId);
            return true;
        }

        _logger.LogInformation("Loading plugin: {PluginId}", pluginId);

        // Discover plugins to find the requested one
        var manifests = await _pluginLoader.DiscoverPluginsAsync(cancellationToken);
        var manifest = manifests.FirstOrDefault(m => m.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            _logger.LogError("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        var (_, success) = await LoadPluginSafeAsync(manifest, cancellationToken);
        return success;
    }

    /// <summary>
    /// Unloads a plugin by ID.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        if (!_managedPlugins.TryGetValue(pluginId, out var managedPlugin))
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        _logger.LogInformation("Unloading plugin: {PluginId}", pluginId);

        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Stop the plugin if it's running
            if (managedPlugin.Plugin?.State == PluginState.Running)
            {
                await managedPlugin.Plugin.StopAsync(cancellationToken);
            }

            // Unload from the loader
            _ = await _pluginLoader.UnloadPluginAsync(pluginId, cancellationToken);

            // Remove from managed plugins
            _ = _managedPlugins.TryRemove(pluginId, out _);

            // Stop health monitoring for this plugin
            _healthMonitor.StopMonitoring(pluginId);

            // Clean up metrics
            _metricsCollector.RemovePlugin(pluginId);

            _logger.LogInformation("Plugin unloaded successfully: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload plugin: {PluginId}", pluginId);
            return false;
        }
        finally
        {
            _ = _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Reloads a plugin by unloading and loading it again.
    /// </summary>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        _logger.LogInformation("Reloading plugin: {PluginId}", pluginId);

        var wasRunning = false;
        if (_managedPlugins.TryGetValue(pluginId, out var managedPlugin))
        {
            wasRunning = managedPlugin.Plugin?.State == PluginState.Running;
        }

        // Unload the plugin
        var unloadSuccess = await UnloadPluginAsync(pluginId, cancellationToken);
        if (!unloadSuccess)
        {
            _logger.LogError("Failed to unload plugin for reload: {PluginId}", pluginId);
            return false;
        }

        // Load the plugin again
        var loadSuccess = await LoadPluginAsync(pluginId, cancellationToken);
        if (!loadSuccess)
        {
            _logger.LogError("Failed to load plugin after reload: {PluginId}", pluginId);
            return false;
        }

        // Start the plugin if it was running before
        if (wasRunning && _managedPlugins.TryGetValue(pluginId, out var newManagedPlugin))
        {
            try
            {
                await newManagedPlugin.Plugin!.StartAsync(cancellationToken);
                _logger.LogInformation("Plugin reloaded and started successfully: {PluginId}", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start plugin after reload: {PluginId}", pluginId);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Starts a loaded plugin.
    /// </summary>
    public async Task<bool> StartPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        if (!_managedPlugins.TryGetValue(pluginId, out var managedPlugin))
        {
            _logger.LogError("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        if (managedPlugin.Plugin == null)
        {
            _logger.LogError("Plugin instance is null: {PluginId}", pluginId);
            return false;
        }

        if (managedPlugin.Plugin.State == PluginState.Running)
        {
            _logger.LogWarning("Plugin is already running: {PluginId}", pluginId);
            return true;
        }

        _logger.LogInformation("Starting plugin: {PluginId}", pluginId);

        try
        {
            await managedPlugin.Plugin.StartAsync(cancellationToken);
            _logger.LogInformation("Plugin started successfully: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start plugin: {PluginId}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Stops a running plugin.
    /// </summary>
    public async Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        if (!_managedPlugins.TryGetValue(pluginId, out var managedPlugin))
        {
            _logger.LogError("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        if (managedPlugin.Plugin == null)
        {
            _logger.LogError("Plugin instance is null: {PluginId}", pluginId);
            return false;
        }

        if (managedPlugin.Plugin.State != PluginState.Running)
        {
            _logger.LogWarning("Plugin is not running: {PluginId} (State: {State})", pluginId, managedPlugin.Plugin.State);
            return true;
        }

        _logger.LogInformation("Stopping plugin: {PluginId}", pluginId);

        try
        {
            await managedPlugin.Plugin.StopAsync(cancellationToken);
            _logger.LogInformation("Plugin stopped successfully: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop plugin: {PluginId}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Gets a managed plugin by ID.
    /// </summary>
    public ManagedPlugin? GetPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        return _managedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// Gets all managed plugins.
    /// </summary>
    public IReadOnlyList<ManagedPlugin> GetAllPlugins()
    {
        ThrowIfDisposed();
        return _managedPlugins.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets plugins filtered by state.
    /// </summary>
    public IReadOnlyList<ManagedPlugin> GetPluginsByState(PluginState state)
    {
        ThrowIfDisposed();
        return _managedPlugins.Values
            .Where(p => p.Plugin?.State == state)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets comprehensive health report for all plugins.
    /// </summary>
    public async Task<PluginHealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _healthMonitor.GenerateHealthReportAsync(_managedPlugins.Values, cancellationToken);
    }

    /// <summary>
    /// Gets comprehensive metrics report for all plugins.
    /// </summary>
    public async Task<PluginMetricsReport> GetMetricsReportAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _metricsCollector.GenerateMetricsReportAsync(_managedPlugins.Values, cancellationToken);
    }

    /// <summary>
    /// Performs a security scan of all loaded plugins.
    /// </summary>
    public async Task<ManagerSecurityReport> PerformSecurityScanAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Performing security scan of all plugins");

        var report = new ManagerSecurityReport
        {
            ScanDate = DateTimeOffset.UtcNow,
            TotalPluginsScanned = _managedPlugins.Count
        };

        var scanTasks = _managedPlugins.Values
            .Where(p => p.LoadedPluginInfo != null)
            .Select(async plugin =>
            {
                try
                {
                    var scanResult = await _pluginLoader.ScanForVulnerabilitiesAsync(cancellationToken);
                    if (plugin.LoadedPluginInfo!.Manifest.Id == scanResult.PluginId)
                    {
                        report.PluginScans.Add(scanResult);


                        if (scanResult.HasCriticalVulnerabilities)
                        {
                            report.CriticalVulnerabilityCount += scanResult.CriticalVulnerabilities.Count;
                        }

                        if (scanResult.HasHighRiskVulnerabilities)
                        {
                            report.HighRiskVulnerabilityCount += scanResult.HighRiskVulnerabilities.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Security scan failed for plugin: {PluginId}", plugin.LoadedPluginInfo?.Manifest.Id);
                    report.ScanErrors.Add($"Scan failed for {plugin.LoadedPluginInfo?.Manifest.Id}: {ex.Message}");
                }
            });

        await Task.WhenAll(scanTasks);

        report.ScanDuration = DateTimeOffset.UtcNow - report.ScanDate;

        _logger.LogInformation("Security scan completed. Critical: {Critical}, High Risk: {HighRisk}",
            report.CriticalVulnerabilityCount, report.HighRiskVulnerabilityCount);

        return report;
    }

    /// <summary>
    /// Safely loads a plugin with comprehensive error handling.
    /// </summary>
    private async Task<(string PluginId, bool Success)> LoadPluginSafeAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Loading plugin: {PluginId}", manifest.Id);

            var loadResult = await _pluginLoader.LoadPluginAsync(manifest, cancellationToken);


            if (!loadResult.IsLoaded || loadResult.Plugin == null)
            {
                _logger.LogError("Failed to load plugin {PluginId}: {Error}", manifest.Id, loadResult.ErrorMessage);
                return (manifest.Id, false);
            }

            var managedPlugin = new ManagedPlugin
            {
                LoadedPluginInfo = loadResult.Plugin,
                Plugin = loadResult.Plugin.Plugin,
                LoadedAt = DateTimeOffset.UtcNow,
                LastHealthCheck = DateTimeOffset.UtcNow,
                Health = PluginHealth.Healthy
            };

            // Subscribe to plugin events
            if (managedPlugin.Plugin != null)
            {
                managedPlugin.Plugin.StateChanged += OnPluginStateChanged;
                managedPlugin.Plugin.ErrorOccurred += OnPluginError;
                managedPlugin.Plugin.HealthChanged += OnPluginHealthChanged;
            }

            _ = _managedPlugins.TryAdd(manifest.Id, managedPlugin);

            // Start health monitoring
            _healthMonitor.StartMonitoring(manifest.Id, managedPlugin);

            // Start metrics collection
            _metricsCollector.AddPlugin(manifest.Id, managedPlugin);

            _logger.LogInformation("Successfully loaded plugin: {PluginId}", manifest.Id);
            return (manifest.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading plugin: {PluginId}", manifest.Id);
            return (manifest.Id, false);
        }
    }

    /// <summary>
    /// Periodic maintenance tasks.
    /// </summary>
    private async void PeriodicMaintenance(object? state)
    {
        try
        {
            _logger.LogDebug("Starting periodic maintenance");

            // Check plugin health
            await CheckPluginHealthAsync();

            // Clean up metrics
            _metricsCollector.CleanupOldMetrics();

            // Check for plugin updates if enabled
            if (_options.CheckForUpdates)
            {
                await CheckForPluginUpdatesAsync();
            }

            _logger.LogDebug("Periodic maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic maintenance");
        }
    }

    /// <summary>
    /// Checks the health of all managed plugins.
    /// </summary>
    private async Task CheckPluginHealthAsync()
    {
        foreach (var (pluginId, managedPlugin) in _managedPlugins)
        {
            try
            {
                var health = await _healthMonitor.CheckPluginHealthAsync(pluginId, managedPlugin);
                managedPlugin.Health = health.Health;
                managedPlugin.LastHealthCheck = DateTimeOffset.UtcNow;

                if (health.Health == PluginHealth.Critical && _options.AutoRestartFailedPlugins)
                {
                    _logger.LogWarning("Plugin {PluginId} is critical, attempting restart", pluginId);
                    _ = await ReloadPluginAsync(pluginId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for plugin: {PluginId}", pluginId);
            }
        }
    }

    /// <summary>
    /// Checks for plugin updates.
    /// </summary>
    private async Task CheckForPluginUpdatesAsync()
    {
        // This would check for plugin updates from NuGet sources
        await Task.CompletedTask;
        _logger.LogDebug("Checking for plugin updates (not implemented)");
    }

    /// <summary>
    /// Handles plugin state change events.
    /// </summary>
    private void OnPluginStateChanged(object? sender, PluginStateChangedEventArgs e)
    {
        _logger.LogInformation("Plugin state changed: {OldState} -> {NewState}", e.OldState, e.NewState);
        PluginStateChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// Handles plugin error events.
    /// </summary>
    private void OnPluginError(object? sender, PluginErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Plugin error occurred: {Context}", e.Context);
        PluginError?.Invoke(sender, e);
    }

    /// <summary>
    /// Handles plugin health change events.
    /// </summary>
    private void OnPluginHealthChanged(object? sender, PluginHealthChangedEventArgs e)
    {
        _logger.LogInformation("Plugin health changed: {OldHealth} -> {NewHealth}", e.OldHealth, e.NewHealth);
        PluginHealthChanged?.Invoke(sender, e);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, nameof(NuGetPluginManager));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _periodicMaintenanceTimer?.Dispose();

        // Unload all plugins

        foreach (var (pluginId, _) in _managedPlugins.ToList())
        {
            try
            {
                _ = UnloadPluginAsync(pluginId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin during dispose: {PluginId}", pluginId);
            }
        }

        _pluginLoader?.Dispose();
        _healthMonitor?.Dispose();
        _metricsCollector?.Dispose();
        _operationSemaphore?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for the NuGet plugin manager.
/// </summary>
public class NuGetPluginManagerOptions
{
    /// <summary>
    /// Gets or sets the plugin directories to monitor.
    /// </summary>
    public List<string> PluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to auto-discover plugins on startup.
    /// </summary>
    public bool AutoDiscoverPlugins { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to check for plugin updates.
    /// </summary>
    public bool CheckForUpdates { get; set; }


    /// <summary>
    /// Gets or sets whether to auto-restart failed plugins.
    /// </summary>
    public bool AutoRestartFailedPlugins { get; set; }


    /// <summary>
    /// Gets or sets the periodic maintenance interval.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the metrics collection interval.
    /// </summary>
    public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the security policy.
    /// </summary>
    public SecurityPolicy? SecurityPolicy { get; set; }

    /// <summary>
    /// Gets or sets the dependency resolution settings.
    /// </summary>
    public DependencyResolutionSettings DependencyResolution { get; set; } = new();

    /// <summary>
    /// Gets or sets the compatibility settings.
    /// </summary>
    public CompatibilitySettings CompatibilitySettings { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable hot reloading.
    /// </summary>
    public bool EnableHotReloading { get; set; }


    /// <summary>
    /// Gets or sets the maximum number of concurrent plugin operations.
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount;
}

/// <summary>
/// Represents a managed plugin with extended metadata and monitoring.
/// </summary>
public class ManagedPlugin
{
    /// <summary>
    /// Gets or sets the loaded plugin information.
    /// </summary>
    public LoadedNuGetPlugin? LoadedPluginInfo { get; set; }

    /// <summary>
    /// Gets or sets the plugin instance.
    /// </summary>
    public IBackendPlugin? Plugin { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was loaded.
    /// </summary>
    public DateTimeOffset LoadedAt { get; set; }

    /// <summary>
    /// Gets or sets the last health check time.
    /// </summary>
    public DateTimeOffset LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the current health status.
    /// </summary>
    public PluginHealth Health { get; set; } = PluginHealth.Unknown;

    /// <summary>
    /// Gets or sets the plugin uptime.
    /// </summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - LoadedAt;

    /// <summary>
    /// Gets or sets whether the plugin is monitored.
    /// </summary>
    public bool IsMonitored { get; set; } = true;

    /// <summary>
    /// Gets or sets the plugin restart count.
    /// </summary>
    public int RestartCount { get; set; }

    /// <summary>
    /// Gets or sets the last restart time.
    /// </summary>
    public DateTimeOffset? LastRestartAt { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for the plugin.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Security report for the plugin manager.
/// </summary>
public class ManagerSecurityReport
{
    /// <summary>
    /// Gets or sets the scan date.
    /// </summary>
    public DateTimeOffset ScanDate { get; set; }

    /// <summary>
    /// Gets or sets the scan duration.
    /// </summary>
    public TimeSpan ScanDuration { get; set; }

    /// <summary>
    /// Gets or sets the total number of plugins scanned.
    /// </summary>
    public int TotalPluginsScanned { get; set; }

    /// <summary>
    /// Gets or sets the number of critical vulnerabilities found.
    /// </summary>
    public int CriticalVulnerabilityCount { get; set; }

    /// <summary>
    /// Gets or sets the number of high-risk vulnerabilities found.
    /// </summary>
    public int HighRiskVulnerabilityCount { get; set; }

    /// <summary>
    /// Gets the plugin scan results.
    /// </summary>
    public List<SecurityScanResult> PluginScans { get; } = [];

    /// <summary>
    /// Gets the scan errors.
    /// </summary>
    public List<string> ScanErrors { get; } = [];

    /// <summary>
    /// Gets whether any critical vulnerabilities were found.
    /// </summary>
    public bool HasCriticalVulnerabilities => CriticalVulnerabilityCount > 0;

    /// <summary>
    /// Gets whether any high-risk vulnerabilities were found.
    /// </summary>
    public bool HasHighRiskVulnerabilities => HighRiskVulnerabilityCount > 0;
}