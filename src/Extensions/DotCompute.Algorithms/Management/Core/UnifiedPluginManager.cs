// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Security.Core;
using DotCompute.Algorithms.Management.Services;
using DotCompute.Algorithms.Management.Resolver;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Unified plugin manager that consolidates all plugin management responsibilities
/// into a single, cohesive service following clean architecture principles.
/// This replaces the scattered AlgorithmPluginManager* classes.
/// </summary>
public sealed partial class UnifiedPluginManager : IDisposable
{
    private readonly ILogger<UnifiedPluginManager> _logger;
    private readonly IUnifiedSecurityValidator _securityValidator;
    private readonly AlgorithmPluginDiscovery _discoveryService;
    private readonly AlgorithmLifecycleManager _lifecycleManager;
    private readonly AlgorithmPluginExecutor _executionService;
    private readonly AlgorithmPluginDependencyResolver _dependencyResolver;
    private readonly AlgorithmPluginHealthMonitor _healthMonitor;
    private readonly Dictionary<string, IAlgorithmPlugin> _loadedPlugins;
    private readonly object _lock = new();
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedPluginManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="securityValidator">Security validator for plugin validation.</param>
    /// <param name="configuration">Plugin manager configuration.</param>
    public UnifiedPluginManager(
        ILogger<UnifiedPluginManager> logger,
        IUnifiedSecurityValidator securityValidator,
        PluginManagerConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityValidator = securityValidator ?? throw new ArgumentNullException(nameof(securityValidator));

        var config = configuration ?? new PluginManagerConfiguration();

        _discoveryService = new AlgorithmPluginDiscovery(logger, config.DiscoveryOptions);
        _lifecycleManager = new AlgorithmLifecycleManager(logger, _securityValidator);
        _executionService = new AlgorithmPluginExecutor(logger);
        _dependencyResolver = new AlgorithmPluginDependencyResolver(logger);
        _healthMonitor = new AlgorithmPluginHealthMonitor(logger, config.HealthMonitoringOptions);
        _loadedPlugins = [];

        LogUnifiedPluginManagerInitialized();
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyDictionary<string, IAlgorithmPlugin> LoadedPlugins
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, IAlgorithmPlugin>(_loadedPlugins);
            }
        }
    }

    /// <summary>
    /// Gets plugin health status.
    /// </summary>
    public PluginHealthStatus HealthStatus => _healthMonitor.GetOverallHealth();

    /// <summary>
    /// Discovers plugins from configured sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered plugin metadata.</returns>
    public async Task<IReadOnlyList<PluginMetadata>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        LogPluginDiscoveryStarting();

        try
        {
            var discoveredPlugins = await _discoveryService.DiscoverAsync(cancellationToken).ConfigureAwait(false);

            LogPluginDiscoveryCompleted(discoveredPlugins.Count);
            return discoveredPlugins;
        }
        catch (Exception ex)
        {
            LogPluginDiscoveryFailed(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads a plugin from the specified path.
    /// </summary>
    /// <param name="pluginPath">Path to the plugin assembly.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded plugin instance.</returns>
    public async Task<IAlgorithmPlugin> LoadPluginAsync(string pluginPath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pluginPath);

        LogPluginLoadingStarting(pluginPath);

        try
        {
            // 1. Security validation
            var securityResult = await _securityValidator.ValidateAssemblyAsync(pluginPath, cancellationToken).ConfigureAwait(false);
            if (!securityResult.IsValid)
            {
                var violations = string.Join(", ", securityResult.Violations);
                throw new PluginSecurityException($"Plugin failed security validation: {violations}");
            }

            // 2. Lifecycle management (load, validate, initialize)
            var plugin = await _lifecycleManager.LoadPluginAsync(pluginPath, cancellationToken).ConfigureAwait(false);

            // 3. Dependency resolution
            await _dependencyResolver.ResolvePluginDependenciesAsync(plugin, cancellationToken).ConfigureAwait(false);

            // 4. Register plugin
            lock (_lock)
            {
                _loadedPlugins[plugin.Id] = plugin;
            }

            // 5. Start health monitoring
            _healthMonitor.StartMonitoring(plugin);

            LogPluginLoadingCompleted(pluginPath, plugin.Id);
            return plugin;
        }
        catch (Exception ex)
        {
            LogPluginLoadingFailed(pluginPath, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    /// <param name="pluginId">ID of the plugin to unload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was unloaded; otherwise, false.</returns>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        LogPluginUnloadingStarting(pluginId);

        try
        {
            IAlgorithmPlugin? plugin;
            lock (_lock)
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out plugin))
                {
                    LogPluginNotFound(pluginId);
                    return false;
                }

                _ = _loadedPlugins.Remove(pluginId);
            }

            // Stop health monitoring
            _healthMonitor.StopMonitoring(pluginId);

            // Unload through lifecycle manager
            await _lifecycleManager.UnloadPluginAsync(plugin, cancellationToken).ConfigureAwait(false);

            LogPluginUnloadingCompleted(pluginId);
            return true;
        }
        catch (Exception ex)
        {
            LogPluginUnloadingFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes a plugin with the specified inputs.
    /// </summary>
    /// <param name="pluginId">ID of the plugin to execute.</param>
    /// <param name="inputs">Input data for the plugin.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pluginId);
        ArgumentNullException.ThrowIfNull(inputs);

        IAlgorithmPlugin plugin;
        lock (_lock)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out plugin!))
            {
                throw new PluginNotFoundException($"Plugin '{pluginId}' is not loaded");
            }
        }

        LogPluginExecutionStarting(pluginId);

        try
        {
            var result = await _executionService.ExecuteAsync(plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            LogPluginExecutionCompleted(pluginId);
            return result;
        }
        catch (Exception ex)
        {
            LogPluginExecutionFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets plugin by ID.
    /// </summary>
    /// <param name="pluginId">Plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        lock (_lock)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Gets plugins by supported operation.
    /// </summary>
    /// <param name="operation">Operation name.</param>
    /// <returns>Collection of plugins that support the operation.</returns>
    public IReadOnlyList<IAlgorithmPlugin> GetPluginsByOperation(string operation)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(operation);

        lock (_lock)
        {
            return _loadedPlugins.Values
                .Where(p => p.SupportedOperations.Contains(operation))
                .ToList();
        }
    }

    /// <summary>
    /// Gets comprehensive plugin manager statistics.
    /// </summary>
    /// <returns>Plugin manager statistics.</returns>
    public PluginManagerStatistics GetStatistics()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            return new PluginManagerStatistics
            {
                TotalPluginsLoaded = _loadedPlugins.Count,
                HealthyPlugins = _healthMonitor.GetHealthyPluginCount(),
                UnhealthyPlugins = _healthMonitor.GetUnhealthyPluginCount(),
                HealthStatus = _healthMonitor.GetOverallHealth(),
                TotalExecutions = _executionService.TotalExecutions,
                SuccessfulExecutions = _executionService.SuccessfulExecutions,
                FailedExecutions = _executionService.FailedExecutions,
                AverageExecutionTime = _executionService.AverageExecutionTime
            };
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Unload all plugins
            lock (_lock)
            {
                foreach (var plugin in _loadedPlugins.Values)
                {
                    try
                    {
                        _lifecycleManager.UnloadPluginAsync(plugin, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error unloading plugin {PluginId} during disposal", plugin.Id);
                    }
                }
                _loadedPlugins.Clear();
            }

            // Dispose services
            _healthMonitor?.Dispose();
            _lifecycleManager?.Dispose();
            _executionService?.Dispose();
            _dependencyResolver?.Dispose();
            _discoveryService?.Dispose();

            LogUnifiedPluginManagerDisposed();
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Unified plugin manager initialized")]
    private partial void LogUnifiedPluginManagerInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting plugin discovery")]
    private partial void LogPluginDiscoveryStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin discovery completed: {PluginCount} plugins found")]
    private partial void LogPluginDiscoveryCompleted(int pluginCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin discovery failed: {ErrorMessage}")]
    private partial void LogPluginDiscoveryFailed(string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugin from: {PluginPath}")]
    private partial void LogPluginLoadingStarting(string pluginPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin loaded successfully: {PluginPath}, ID: {PluginId}")]
    private partial void LogPluginLoadingCompleted(string pluginPath, string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugin from: {PluginPath}, Error: {ErrorMessage}")]
    private partial void LogPluginLoadingFailed(string pluginPath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unloading plugin: {PluginId}")]
    private partial void LogPluginUnloadingStarting(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin unloaded successfully: {PluginId}")]
    private partial void LogPluginUnloadingCompleted(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unload plugin: {PluginId}, Error: {ErrorMessage}")]
    private partial void LogPluginUnloadingFailed(string pluginId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin not found: {PluginId}")]
    private partial void LogPluginNotFound(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing plugin: {PluginId}")]
    private partial void LogPluginExecutionStarting(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin execution completed: {PluginId}")]
    private partial void LogPluginExecutionCompleted(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin execution failed: {PluginId}, Error: {ErrorMessage}")]
    private partial void LogPluginExecutionFailed(string pluginId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unified plugin manager disposed")]
    private partial void LogUnifiedPluginManagerDisposed();

    #endregion
}

#region Supporting Types

/// <summary>
/// Plugin manager configuration.
/// </summary>
public sealed class PluginManagerConfiguration
{
    /// <summary>
    /// Gets or sets the plugin discovery options.
    /// </summary>
    public PluginDiscoveryOptions DiscoveryOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the health monitoring options.
    /// </summary>
    public PluginHealthMonitoringOptions HealthMonitoringOptions { get; set; } = new();
}

/// <summary>
/// Plugin discovery options.
/// </summary>
public sealed class PluginDiscoveryOptions
{
    /// <summary>
    /// Gets or sets the plugin directories to scan.
    /// </summary>
    public IList<string> PluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to scan subdirectories.
    /// </summary>
    public bool ScanSubdirectories { get; set; } = true;

    /// <summary>
    /// Gets or sets file patterns to match.
    /// </summary>
    public IList<string> FilePatterns { get; set; } = ["*.dll"];
}

/// <summary>
/// Plugin health monitoring options.
/// </summary>
public sealed class PluginHealthMonitoringOptions
{
    /// <summary>
    /// Gets or sets whether health monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum consecutive failures before marking as unhealthy.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;
}

/// <summary>
/// Plugin manager statistics.
/// </summary>
public readonly record struct PluginManagerStatistics
{
    public int TotalPluginsLoaded { get; init; }
    public int HealthyPlugins { get; init; }
    public int UnhealthyPlugins { get; init; }
    public PluginHealthStatus HealthStatus { get; init; }
    public long TotalExecutions { get; init; }
    public long SuccessfulExecutions { get; init; }
    public long FailedExecutions { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
}

/// <summary>
/// Plugin health status enumeration.
/// </summary>
public enum PluginHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Exception thrown when a plugin is not found.
/// </summary>
public sealed class PluginNotFoundException : Exception
{
    public PluginNotFoundException(string message) : base(message) { }
    public PluginNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when plugin security validation fails.
/// </summary>
public sealed class PluginSecurityException : Exception
{
    public PluginSecurityException(string message) : base(message) { }
    public PluginSecurityException(string message, Exception innerException) : base(message, innerException) { }
}

// NOTE: The following services would be implemented as separate files in a real system:
// - PluginDiscoveryService
// - PluginLifecycleManager
// - PluginExecutionService
// - PluginDependencyResolver
// - PluginHealthMonitor
// Each with their own focused responsibilities and clean interfaces.

#endregion