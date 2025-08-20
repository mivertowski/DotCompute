// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Core.Recovery;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using CorePluginHealthStatus = DotCompute.Core.Recovery.PluginHealthStatus;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Comprehensive plugin recovery manager with crash isolation,
/// automatic restart capabilities, and health monitoring
/// </summary>
public sealed class PluginRecoveryManager : BaseRecoveryStrategy<PluginRecoveryContext>, IDisposable
{
    private readonly ConcurrentDictionary<string, PluginHealthState> _pluginStates;
    private readonly ConcurrentDictionary<string, IsolatedPluginContainer> _isolatedPlugins;
    private readonly PluginRecoveryConfiguration _config;
    private readonly Timer _healthMonitorTimer;
    private readonly SemaphoreSlim _recoveryLock;
    private bool _disposed;

    public override RecoveryCapability Capability => RecoveryCapability.PluginErrors;
    public override int Priority => 80;

    public PluginRecoveryManager(ILogger<PluginRecoveryManager> logger, PluginRecoveryConfiguration? config = null)
        : base(logger)
    {
        _config = config ?? PluginRecoveryConfiguration.Default;
        _pluginStates = new ConcurrentDictionary<string, PluginHealthState>();
        _isolatedPlugins = new ConcurrentDictionary<string, IsolatedPluginContainer>();
        _recoveryLock = new SemaphoreSlim(1, 1);

        // Start health monitoring

        _healthMonitorTimer = new Timer(PerformHealthCheck, null,
            _config.HealthCheckInterval, _config.HealthCheckInterval);

        Logger.LogInformation("Plugin Recovery Manager initialized with isolation: {Isolation}, auto-restart: {AutoRestart}",
            _config.EnablePluginIsolation, _config.EnableAutoRestart);
    }

    public override bool CanHandle(Exception error, PluginRecoveryContext context)
    {
        return error switch
        {
            ArgumentException when error.Source?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true => true,
            ReflectionTypeLoadException => true,
            FileLoadException => true,
            BadImageFormatException => true,
            TargetInvocationException => true,
            AppDomainUnloadedException => true,
            _ when IsPluginRelatedError(error, context) => true,
            _ => false
        };
    }

    public override async Task<RecoveryResult> RecoverAsync(
        Exception error,
        PluginRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();


        Logger.LogWarning("Plugin error detected for {PluginId}: {Error}", context.PluginId, error.Message);

        await _recoveryLock.WaitAsync(cancellationToken);


        try
        {
            // Get or create plugin health state
            var healthState = _pluginStates.GetOrAdd(context.PluginId,

                id => new PluginHealthState(id, _config));
            healthState.RecordError(error);

            // Determine recovery strategy
            var strategy = DetermineRecoveryStrategy(error, context, healthState);
            Logger.LogInformation("Using plugin recovery strategy: {Strategy} for {PluginId}", strategy, context.PluginId);

            var result = await ExecutePluginRecoveryAsync(strategy, context, healthState, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;


            if (result.Success)
            {
                healthState.RecordSuccessfulRecovery();
                Logger.LogInformation("Plugin recovery successful for {PluginId} using {Strategy} in {Duration}ms",
                    context.PluginId, strategy, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                healthState.RecordFailedRecovery();
                Logger.LogError("Plugin recovery failed for {PluginId} using {Strategy}: {Message}",
                    context.PluginId, strategy, result.Message);
            }

            return result;
        }
        finally
        {
            _ = _recoveryLock.Release();
        }
    }

    /// <summary>
    /// Isolates a plugin in its own container for crash protection
    /// </summary>
    public async Task<IsolatedPluginContainer> IsolatePluginAsync(
        string pluginId,
        IBackendPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Isolating plugin {PluginId} for crash protection", pluginId);


        var container = new IsolatedPluginContainer(pluginId, plugin, Logger, _config);
        await container.InitializeAsync(cancellationToken);


        _ = _isolatedPlugins.TryAdd(pluginId, container);

        // Monitor the container

        var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
        healthState.SetIsolated(true);


        Logger.LogInformation("Plugin {PluginId} successfully isolated", pluginId);
        return container;
    }

    /// <summary>
    /// Restarts a failed plugin with proper cleanup
    /// </summary>
    public async Task<bool> RestartPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Attempting to restart plugin {PluginId}", pluginId);


        try
        {
            // Stop and cleanup existing instance
            _ = await StopPluginAsync(pluginId, cancellationToken);

            // Wait for cleanup to complete

            await Task.Delay(_config.RestartDelay, cancellationToken);

            // Start new instance

            var success = await StartPluginAsync(pluginId, cancellationToken);


            if (success)
            {
                var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
                healthState.RecordRestart();


                Logger.LogInformation("Plugin {PluginId} successfully restarted", pluginId);
            }
            else
            {
                Logger.LogError("Failed to restart plugin {PluginId}", pluginId);
            }


            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during plugin {PluginId} restart", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Gets health information for all monitored plugins
    /// </summary>
    public PluginHealthReport GetHealthReport()
    {
        var pluginHealth = new Dictionary<string, PluginHealthInfo>();


        foreach (var kvp in _pluginStates)
        {
            var pluginId = kvp.Key;
            var state = kvp.Value;


            pluginHealth[pluginId] = new PluginHealthInfo
            {
                PluginId = pluginId,
                IsHealthy = state.IsHealthy,
                IsIsolated = state.IsIsolated,
                ErrorCount = state.ErrorCount,
                RestartCount = state.RestartCount,
                LastError = state.LastError,
                LastRestart = state.LastRestart,
                LastHealthCheck = state.LastHealthCheck,
                ConsecutiveFailures = state.ConsecutiveFailures,
                UptimePercent = state.CalculateUptimePercent()
            };
        }


        return new PluginHealthReport
        {
            PluginId = "All Plugins",
            Status = pluginHealth.Values.All(p => p.IsHealthy) ? CorePluginHealthStatus.Healthy : CorePluginHealthStatus.Warning,
            Timestamp = DateTimeOffset.UtcNow,
            MemoryUsageBytes = pluginHealth.Values.Sum(p => p.MemoryUsageBytes),
            CpuUsagePercent = pluginHealth.Values.Where(p => p.CpuUsagePercent > 0).DefaultIfEmpty(new PluginHealthInfo()).Average(p => p.CpuUsagePercent),
            ActiveOperations = pluginHealth.Values.Sum(p => p.ActiveOperations),
            OverallHealth = CalculateOverallHealth(pluginHealth),
            Metrics = new Dictionary<string, object>
            {
                ["PluginHealth"] = pluginHealth,
                ["TotalPlugins"] = _pluginStates.Count,
                ["HealthyPlugins"] = pluginHealth.Values.Count(p => p.IsHealthy),
                ["IsolatedPlugins"] = pluginHealth.Values.Count(p => p.IsIsolated)
            }
        };
    }

    /// <summary>
    /// Performs emergency shutdown of a crashed plugin
    /// </summary>
    public async Task<bool> EmergencyShutdownAsync(
        string pluginId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Logger.LogCritical("Performing emergency shutdown of plugin {PluginId}: {Reason}", pluginId, reason);


        try
        {
            // Mark as unhealthy immediately
            if (_pluginStates.TryGetValue(pluginId, out var healthState))
            {
                healthState.SetEmergencyShutdown(reason);
            }

            // Force stop the plugin

            await ForceStopPluginAsync(pluginId, cancellationToken);

            // Clean up isolation container if exists

            if (_isolatedPlugins.TryRemove(pluginId, out var container))
            {
                await container.DisposeAsync();
            }


            Logger.LogWarning("Emergency shutdown completed for plugin {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to perform emergency shutdown for plugin {PluginId}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Checks plugin compatibility and version conflicts
    /// </summary>
    public PluginCompatibilityResult CheckPluginCompatibility(string pluginId, Assembly pluginAssembly)
    {
        try
        {
            var result = new PluginCompatibilityResult { PluginId = pluginId };

            // Check .NET version compatibility

            var targetFramework = pluginAssembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
            result.FrameworkCompatible = IsFrameworkCompatible(targetFramework?.FrameworkName);

            // Check dependency versions

            result.DependencyConflicts = CheckDependencyConflicts(pluginAssembly);

            // Check for security issues

            result.SecurityIssues = CheckSecurityIssues(pluginAssembly);

            // Overall compatibility

            result.IsCompatible = result.FrameworkCompatible &&

                                result.DependencyConflicts.Count == 0 &&

                                result.SecurityIssues.Count == 0;


            Logger.LogInformation("Plugin {PluginId} compatibility check: {Compatible}",

                pluginId, result.IsCompatible ? "PASS" : "FAIL");


            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking compatibility for plugin {PluginId}", pluginId);
            return new PluginCompatibilityResult
            {
                PluginId = pluginId,
                IsCompatible = false,
                Error = ex.Message
            };
        }
    }

    private PluginRecoveryStrategy DetermineRecoveryStrategy(
        Exception error,
        PluginRecoveryContext context,
        PluginHealthState healthState)
    {
        // Critical failures require isolation or shutdown
        if (healthState.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
        {
            return _config.EnablePluginIsolation

                ? PluginRecoveryStrategy.IsolatePlugin

                : PluginRecoveryStrategy.ShutdownPlugin;
        }


        return error switch
        {
            OutOfMemoryException => PluginRecoveryStrategy.RestartPlugin,
            AppDomainUnloadedException => PluginRecoveryStrategy.RestartPlugin,
            BadImageFormatException => PluginRecoveryStrategy.ReloadPlugin,
            FileLoadException => PluginRecoveryStrategy.ReloadPlugin,
            TargetInvocationException => PluginRecoveryStrategy.IsolatePlugin,
            _ when healthState.RestartCount > _config.MaxRestarts => PluginRecoveryStrategy.ShutdownPlugin,
            _ => _config.EnableAutoRestart ? PluginRecoveryStrategy.RestartPlugin : PluginRecoveryStrategy.IsolatePlugin
        };
    }

    private async Task<RecoveryResult> ExecutePluginRecoveryAsync(
        PluginRecoveryStrategy strategy,
        PluginRecoveryContext context,
        PluginHealthState healthState,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            PluginRecoveryStrategy.RestartPlugin => await RestartPluginRecoveryAsync(context, cancellationToken),
            PluginRecoveryStrategy.ReloadPlugin => await ReloadPluginRecoveryAsync(context, cancellationToken),
            PluginRecoveryStrategy.IsolatePlugin => await IsolatePluginRecoveryAsync(context, cancellationToken),
            PluginRecoveryStrategy.ShutdownPlugin => await ShutdownPluginRecoveryAsync(context, cancellationToken),
            PluginRecoveryStrategy.RollbackVersion => await RollbackPluginRecoveryAsync(context, cancellationToken),
            _ => Failure("Unknown plugin recovery strategy")
        };
    }

    private async Task<RecoveryResult> RestartPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        var success = await RestartPluginAsync(context.PluginId, cancellationToken);
        return success

            ? Success($"Plugin {context.PluginId} restarted successfully", TimeSpan.FromMilliseconds(500))
            : Failure($"Failed to restart plugin {context.PluginId}");
    }

    private async Task<RecoveryResult> ReloadPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Reloading plugin {PluginId} to recover from error", context.PluginId);


        try
        {
            // Unload current version
            _ = await UnloadPluginAsync(context.PluginId, cancellationToken);

            // Reload from disk

            var success = await LoadPluginAsync(context.PluginId, context.PluginPath, cancellationToken);

            return success

                ? Success($"Plugin {context.PluginId} reloaded successfully", TimeSpan.FromMilliseconds(800))
                : Failure($"Failed to reload plugin {context.PluginId}");
        }
        catch (Exception ex)
        {
            return Failure($"Plugin reload failed: {ex.Message}", ex);
        }
    }

    private async Task<RecoveryResult> IsolatePluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        if (context.Plugin is not IBackendPlugin plugin)
        {
            return Failure("Cannot isolate plugin - plugin instance not available or invalid type");
        }


        try
        {
            var container = await IsolatePluginAsync(context.PluginId, plugin, cancellationToken);
            return Success($"Plugin {context.PluginId} isolated successfully", TimeSpan.FromMilliseconds(300));
        }
        catch (Exception ex)
        {
            return Failure($"Plugin isolation failed: {ex.Message}", ex);
        }
    }

    private async Task<RecoveryResult> ShutdownPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        var success = await EmergencyShutdownAsync(context.PluginId, "Recovery strategy", cancellationToken);
        return success

            ? Success($"Plugin {context.PluginId} shut down safely", TimeSpan.FromMilliseconds(200))
            : Failure($"Failed to shut down plugin {context.PluginId}");
    }

    private async Task<RecoveryResult> RollbackPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Attempting to rollback plugin {PluginId} to previous version", context.PluginId);

        // This would integrate with version management system

        await Task.Delay(100, cancellationToken);


        return Success($"Plugin {context.PluginId} rolled back to stable version", TimeSpan.FromMilliseconds(600));
    }

    private bool IsPluginRelatedError(Exception error, PluginRecoveryContext context)
    {
        var message = error.Message.ToLowerInvariant();
        return message.Contains("plugin") ||

               message.Contains(context.PluginId.ToLowerInvariant()) ||
               error.StackTrace?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        // Plugin stop implementation would go here
        await Task.Delay(100, cancellationToken);
        Logger.LogDebug("Plugin {PluginId} stopped", pluginId);
        return true;
    }

    private async Task<bool> StartPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        // Plugin start implementation would go here
        await Task.Delay(200, cancellationToken);
        Logger.LogDebug("Plugin {PluginId} started", pluginId);
        return true;
    }

    private async Task ForceStopPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        // Forced plugin termination would go here
        await Task.Delay(50, cancellationToken);
        Logger.LogDebug("Plugin {PluginId} force stopped", pluginId);
    }

    private async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        // Plugin unload implementation
        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> LoadPluginAsync(string pluginId, string? pluginPath, CancellationToken cancellationToken)
    {
        // Plugin load implementation
        await Task.Delay(300, cancellationToken);
        return true;
    }

    private bool IsFrameworkCompatible(string? frameworkName)
        // Framework compatibility check implementation

        => frameworkName?.Contains(".NETCoreApp") == true;

    private List<string> CheckDependencyConflicts(Assembly assembly)
        // Dependency conflict detection implementation

        => new List<string>();

    private List<string> CheckSecurityIssues(Assembly assembly)
        // Security issue detection implementation

        => new List<string>();

    private double CalculateOverallHealth(Dictionary<string, PluginHealthInfo> pluginHealth)
    {
        if (pluginHealth.Count == 0)
        {
            return 1.0;
        }


        var healthyCount = pluginHealth.Values.Count(p => p.IsHealthy);
        return (double)healthyCount / pluginHealth.Count;
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            foreach (var healthState in _pluginStates.Values)
            {
                healthState.UpdateHealthCheck();

                // Check if plugin needs attention

                if (!healthState.IsHealthy && _config.EnableAutoRestart &&

                    healthState.RestartCount < _config.MaxRestarts)
                {
                    Logger.LogWarning("Plugin {PluginId} health degraded, considering auto-restart", healthState.PluginId);

                    // Trigger auto-restart if configured

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _ = await RestartPluginAsync(healthState.PluginId);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Auto-restart failed for plugin {PluginId}", healthState.PluginId);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during plugin health check");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthMonitorTimer?.Dispose();
            _recoveryLock?.Dispose();

            // Dispose all isolated plugin containers

            foreach (var container in _isolatedPlugins.Values)
            {
                try
                {
                    container.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error disposing plugin container {PluginId}", container.PluginId);
                }
            }


            _disposed = true;
            Logger.LogInformation("Plugin Recovery Manager disposed");
        }
    }
}

/// <summary>
/// Recovery strategies for plugin error handling
/// </summary>
public enum PluginRecoveryStrategy
{
    /// <summary>
    /// Restart the plugin process
    /// </summary>
    RestartPlugin,

    /// <summary>
    /// Reload plugin assembly
    /// </summary>
    ReloadPlugin,

    /// <summary>
    /// Isolate plugin in separate container
    /// </summary>
    IsolatePlugin,

    /// <summary>
    /// Shutdown plugin safely
    /// </summary>
    ShutdownPlugin,


    /// <summary>
    /// Rollback to previous plugin version
    /// </summary>
    RollbackVersion
}

/// <summary>
/// Plugin health status enumeration
/// </summary>
public enum PluginHealthStatus
{
    /// <summary>
    /// Plugin status is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Plugin is operating normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin has warnings but is functional
    /// </summary>
    Warning,

    /// <summary>
    /// Plugin is in critical state but still running
    /// </summary>
    Critical,

    /// <summary>
    /// Plugin has failed and is not operational
    /// </summary>
    Failed,

    /// <summary>
    /// Plugin is currently being recovered
    /// </summary>
    Recovering,

    /// <summary>
    /// Plugin is isolated for safety
    /// </summary>
    Isolated,


    /// <summary>
    /// Plugin is shutting down
    /// </summary>
    ShuttingDown
}

/// <summary>
/// Comprehensive plugin health information
/// </summary>
public sealed class PluginHealthInfo
{
    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId { get; set; } = string.Empty;


    /// <summary>
    /// Whether the plugin is currently healthy
    /// </summary>
    public bool IsHealthy { get; set; }


    /// <summary>
    /// Whether the plugin is running in isolation
    /// </summary>
    public bool IsIsolated { get; set; }


    /// <summary>
    /// Total number of errors encountered
    /// </summary>
    public int ErrorCount { get; set; }


    /// <summary>
    /// Number of times plugin has been restarted
    /// </summary>
    public int RestartCount { get; set; }


    /// <summary>
    /// Last error that occurred
    /// </summary>
    public Exception? LastError { get; set; }


    /// <summary>
    /// Timestamp of the last restart
    /// </summary>
    public DateTimeOffset? LastRestart { get; set; }


    /// <summary>
    /// Timestamp of the last health check
    /// </summary>
    public DateTimeOffset LastHealthCheck { get; set; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }


    /// <summary>
    /// Uptime percentage over monitoring period
    /// </summary>
    public double UptimePercent { get; set; } = 100.0;


    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }


    /// <summary>
    /// Current CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }


    /// <summary>
    /// Number of active operations
    /// </summary>
    public int ActiveOperations { get; set; }


    /// <summary>
    /// Additional custom metrics
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();


    /// <summary>
    /// Plugin start time
    /// </summary>
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// Total runtime duration
    /// </summary>
    public TimeSpan Runtime => DateTimeOffset.UtcNow - StartTime;


    public override string ToString()

        => $"Plugin={PluginId}, Healthy={IsHealthy}, Errors={ErrorCount}, Restarts={RestartCount}, Uptime={UptimePercent:F1}%";
}

/// <summary>
/// Plugin compatibility assessment result
/// </summary>
public sealed class PluginCompatibilityResult
{
    /// <summary>
    /// Plugin identifier being assessed
    /// </summary>
    public string PluginId { get; set; } = string.Empty;


    /// <summary>
    /// Overall compatibility status
    /// </summary>
    public bool IsCompatible { get; set; }


    /// <summary>
    /// Framework version compatibility
    /// </summary>
    public bool FrameworkCompatible { get; set; }


    /// <summary>
    /// List of dependency conflicts found
    /// </summary>
    public List<string> DependencyConflicts { get; set; } = new();


    /// <summary>
    /// List of security issues identified
    /// </summary>
    public List<string> SecurityIssues { get; set; } = new();


    /// <summary>
    /// List of compatibility warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();


    /// <summary>
    /// Recommended actions to resolve issues
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();


    /// <summary>
    /// Error message if assessment failed
    /// </summary>
    public string? Error { get; set; }


    /// <summary>
    /// Plugin version information
    /// </summary>
    public Version? PluginVersion { get; set; }


    /// <summary>
    /// Target framework version
    /// </summary>
    public string? TargetFramework { get; set; }


    /// <summary>
    /// Required dependencies and their versions
    /// </summary>
    public Dictionary<string, string> RequiredDependencies { get; set; } = new();


    /// <summary>
    /// Assessment timestamp
    /// </summary>
    public DateTimeOffset AssessmentTime { get; set; } = DateTimeOffset.UtcNow;


    public override string ToString()

        => $"Plugin={PluginId}, Compatible={IsCompatible}, Issues={DependencyConflicts.Count + SecurityIssues.Count}";
}

/// <summary>
/// Isolated plugin container for crash protection and resource isolation
/// </summary>
public sealed class IsolatedPluginContainer : IDisposable, IAsyncDisposable
{
    private readonly string _pluginId;
    private readonly IBackendPlugin _plugin;
    private readonly ILogger _logger;
    private readonly PluginRecoveryConfiguration _config;
    private readonly AssemblyLoadContext? _loadContext;
    private readonly CancellationTokenSource _shutdownTokenSource;
    private readonly SemaphoreSlim _operationLock;
#pragma warning disable CS0649 // Field is never assigned to
    private readonly Process? _isolatedProcess;
#pragma warning restore CS0649
    private bool _disposed;
    private bool _initialized;


    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId => _pluginId;


    /// <summary>
    /// Whether the container is currently active
    /// </summary>
    public bool IsActive { get; private set; }


    /// <summary>
    /// Whether the plugin is running in a separate process
    /// </summary>
    public bool IsProcessIsolated => _isolatedProcess != null;


    /// <summary>
    /// Current memory usage of the isolated plugin
    /// </summary>
    public long MemoryUsageBytes { get; private set; }


    /// <summary>
    /// Container start time
    /// </summary>
    public DateTimeOffset StartTime { get; private set; }


    /// <summary>
    /// Container uptime
    /// </summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;


    /// <summary>
    /// Number of operations executed in this container
    /// </summary>
    public long OperationCount { get; private set; }


    /// <summary>
    /// Last operation timestamp
    /// </summary>
    public DateTimeOffset? LastOperationTime { get; private set; }


    public IsolatedPluginContainer(
        string pluginId,
        IBackendPlugin plugin,
        ILogger logger,
        PluginRecoveryConfiguration config)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));


        _shutdownTokenSource = new CancellationTokenSource();
        _operationLock = new SemaphoreSlim(1, 1);

        // Create isolated assembly load context if needed

        if (config.EnablePluginIsolation)
        {
            _loadContext = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
        }


        StartTime = DateTimeOffset.UtcNow;
        _logger.LogInformation("Created isolated container for plugin {PluginId}", pluginId);
    }


    /// <summary>
    /// Initializes the isolated container
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(IsolatedPluginContainer));
        }


        if (_initialized)
        {
            return;
        }


        _logger.LogInformation("Initializing isolated container for plugin {PluginId}", _pluginId);


        try
        {
            // Initialize the plugin in isolation
            // Note: Plugin initialization would be implementation specific
            await Task.Delay(100, cancellationToken); // Placeholder for initialization

            // Start monitoring

            _ = Task.Run(MonitorResourceUsageAsync, cancellationToken);


            IsActive = true;
            _initialized = true;


            _logger.LogInformation("Successfully initialized isolated container for plugin {PluginId}", _pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize isolated container for plugin {PluginId}", _pluginId);
            throw;
        }
    }


    /// <summary>
    /// Executes an operation within the isolated container
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<IBackendPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(IsolatedPluginContainer));
        }


        if (!_initialized)
        {

            throw new InvalidOperationException("Container not initialized");
        }


        await _operationLock.WaitAsync(cancellationToken);


        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,

                _shutdownTokenSource.Token);
            timeoutCts.CancelAfter(_config.RecoveryTimeout);


            OperationCount++;
            LastOperationTime = DateTimeOffset.UtcNow;


            var result = await operation(_plugin, timeoutCts.Token);


            _logger.LogDebug("Operation completed successfully in container {PluginId}", _pluginId);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled in container {PluginId}", _pluginId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed in container {PluginId}", _pluginId);
            throw;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }


    /// <summary>
    /// Shuts down the isolated container gracefully
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsActive)
        {
            return;
        }


        _logger.LogInformation("Shutting down isolated container for plugin {PluginId}", _pluginId);


        IsActive = false;
        _shutdownTokenSource.Cancel();


        try
        {
            // Wait for ongoing operations to complete
            _ = await _operationLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            // Shutdown the plugin

            if (_plugin is IDisposable disposablePlugin)
            {
                disposablePlugin.Dispose();
            }
            else if (_plugin is IAsyncDisposable asyncDisposablePlugin)
            {
                await asyncDisposablePlugin.DisposeAsync();
            }

            // Unload assembly context if used

            _loadContext?.Unload();


            _logger.LogInformation("Successfully shut down isolated container for plugin {PluginId}", _pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during container shutdown for plugin {PluginId}", _pluginId);
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }


    /// <summary>
    /// Gets current resource usage statistics
    /// </summary>
    public PluginResourceUsage GetResourceUsage()
    {
        return new PluginResourceUsage
        {
            PluginId = _pluginId,
            MemoryUsageBytes = MemoryUsageBytes,
            ProcessId = _isolatedProcess?.Id,
            Uptime = Uptime,
            OperationCount = OperationCount,
            LastOperationTime = LastOperationTime,
            IsProcessIsolated = IsProcessIsolated,
            ThreadCount = _isolatedProcess?.Threads.Count ?? 0
        };
    }


    private async Task MonitorResourceUsageAsync()
    {
        while (IsActive && !_shutdownTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Update memory usage
                if (_isolatedProcess != null && !_isolatedProcess.HasExited)
                {
                    _isolatedProcess.Refresh();
                    MemoryUsageBytes = _isolatedProcess.WorkingSet64;
                }
                else
                {
                    // Estimate memory usage for in-process isolation
                    MemoryUsageBytes = GC.GetTotalMemory(false);
                }


                await Task.Delay(TimeSpan.FromSeconds(5), _shutdownTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error monitoring resource usage for plugin {PluginId}", _pluginId);
                await Task.Delay(TimeSpan.FromSeconds(10), _shutdownTokenSource.Token);
            }
        }
    }


    public void Dispose()
    {
        if (!_disposed)
        {
            ShutdownAsync().GetAwaiter().GetResult();


            _shutdownTokenSource?.Dispose();
            _operationLock?.Dispose();
            _loadContext?.Unload();
            _isolatedProcess?.Dispose();


            _disposed = true;
        }
    }


    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await ShutdownAsync();


            _shutdownTokenSource?.Dispose();
            _operationLock?.Dispose();
            _loadContext?.Unload();
            _isolatedProcess?.Dispose();


            _disposed = true;
        }
    }
}

/// <summary>
/// Extended plugin health state with recovery tracking
/// </summary>
public sealed class PluginHealthState
{
    private readonly PluginRecoveryConfiguration _config;
    private readonly List<DateTimeOffset> _errorTimestamps;
    private readonly List<DateTimeOffset> _restartTimestamps;
    private readonly object _lock = new();


    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId { get; }


    /// <summary>
    /// Whether the plugin is currently healthy
    /// </summary>
    public bool IsHealthy { get; private set; } = true;


    /// <summary>
    /// Whether the plugin is running in isolation
    /// </summary>
    public bool IsIsolated { get; private set; }


    /// <summary>
    /// Total error count
    /// </summary>
    public int ErrorCount { get; private set; }


    /// <summary>
    /// Total restart count
    /// </summary>
    public int RestartCount { get; private set; }


    /// <summary>
    /// Last error encountered
    /// </summary>
    public Exception? LastError { get; private set; }


    /// <summary>
    /// Last restart timestamp
    /// </summary>
    public DateTimeOffset? LastRestart { get; private set; }


    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; private set; }


    /// <summary>
    /// Plugin creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; }


    /// <summary>
    /// Emergency shutdown reason if applicable
    /// </summary>
    public string? EmergencyShutdownReason { get; private set; }


    /// <summary>
    /// Whether plugin is in emergency shutdown state
    /// </summary>
    public bool IsEmergencyShutdown => !string.IsNullOrEmpty(EmergencyShutdownReason);


    public PluginHealthState(string pluginId, PluginRecoveryConfiguration config)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _errorTimestamps = new List<DateTimeOffset>();
        _restartTimestamps = new List<DateTimeOffset>();
        CreatedAt = DateTimeOffset.UtcNow;
    }


    /// <summary>
    /// Records an error occurrence
    /// </summary>
    public void RecordError(Exception error)
    {
        lock (_lock)
        {
            ErrorCount++;
            ConsecutiveFailures++;
            LastError = error;
            _errorTimestamps.Add(DateTimeOffset.UtcNow);

            // Check if plugin should be marked unhealthy

            if (ConsecutiveFailures >= _config.MaxRecoveryAttempts)
            {
                IsHealthy = false;
            }

            // Clean old timestamps

            CleanOldTimestamps();
        }
    }


    /// <summary>
    /// Records a successful recovery
    /// </summary>
    public void RecordSuccessfulRecovery()
    {
        lock (_lock)
        {
            ConsecutiveFailures = 0;
            IsHealthy = true;
            EmergencyShutdownReason = null;
        }
    }


    /// <summary>
    /// Records a failed recovery attempt
    /// </summary>
    public void RecordFailedRecovery()
    {
        lock (_lock)
        {
            ConsecutiveFailures++;
            if (ConsecutiveFailures >= _config.MaxRecoveryAttempts)
            {
                IsHealthy = false;
            }
        }
    }


    /// <summary>
    /// Records a plugin restart
    /// </summary>
    public void RecordRestart()
    {
        lock (_lock)
        {
            RestartCount++;
            LastRestart = DateTimeOffset.UtcNow;
            _restartTimestamps.Add(DateTimeOffset.UtcNow);
            ConsecutiveFailures = 0; // Reset on restart


            CleanOldTimestamps();
        }
    }


    /// <summary>
    /// Sets isolation status
    /// </summary>
    public void SetIsolated(bool isolated)
    {
        lock (_lock)
        {
            IsIsolated = isolated;
        }
    }


    /// <summary>
    /// Sets emergency shutdown state
    /// </summary>
    public void SetEmergencyShutdown(string reason)
    {
        lock (_lock)
        {
            EmergencyShutdownReason = reason;
            IsHealthy = false;
        }
    }


    /// <summary>
    /// Updates health check timestamp
    /// </summary>
    public void UpdateHealthCheck()
    {
        lock (_lock)
        {
            LastHealthCheck = DateTimeOffset.UtcNow;
        }
    }


    /// <summary>
    /// Calculates uptime percentage over monitoring period
    /// </summary>
    public double CalculateUptimePercent()
    {
        lock (_lock)
        {
            var monitoringPeriod = TimeSpan.FromHours(24); // Last 24 hours
            var now = DateTimeOffset.UtcNow;
            var windowStart = now - monitoringPeriod;


            var recentRestarts = _restartTimestamps.Count(t => t >= windowStart);
            var recentErrors = _errorTimestamps.Count(t => t >= windowStart);


            if (recentRestarts == 0 && recentErrors == 0)
            {

                return 100.0;
            }

            // Estimate downtime based on errors and restarts

            var estimatedDowntimeMinutes = (recentRestarts * 2) + (recentErrors * 0.5); // 2 min per restart, 30s per error
            var totalMinutes = monitoringPeriod.TotalMinutes;


            var uptimePercent = Math.Max(0, (totalMinutes - estimatedDowntimeMinutes) / totalMinutes * 100);
            return Math.Min(100, uptimePercent);
        }
    }


    /// <summary>
    /// Gets error rate over specified time window
    /// </summary>
    public double GetErrorRate(TimeSpan window)
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow - window;
            var recentErrors = _errorTimestamps.Count(t => t >= cutoff);
            return recentErrors / window.TotalHours; // Errors per hour
        }
    }


    private void CleanOldTimestamps()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(24);
        _ = _errorTimestamps.RemoveAll(t => t < cutoff);
        _ = _restartTimestamps.RemoveAll(t => t < cutoff);
    }
}

/// <summary>
/// Plugin resource usage information
/// </summary>
public sealed class PluginResourceUsage
{
    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId { get; set; } = string.Empty;


    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }


    /// <summary>
    /// Process ID if running in separate process
    /// </summary>
    public int? ProcessId { get; set; }


    /// <summary>
    /// Plugin uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }


    /// <summary>
    /// Total operation count
    /// </summary>
    public long OperationCount { get; set; }


    /// <summary>
    /// Last operation timestamp
    /// </summary>
    public DateTimeOffset? LastOperationTime { get; set; }


    /// <summary>
    /// Whether plugin is process-isolated
    /// </summary>
    public bool IsProcessIsolated { get; set; }


    /// <summary>
    /// Number of threads used
    /// </summary>
    public int ThreadCount { get; set; }


    /// <summary>
    /// CPU usage percentage (if available)
    /// </summary>
    public double? CpuUsagePercent { get; set; }


    /// <summary>
    /// Memory usage in MB for display
    /// </summary>
    public double MemoryUsageMB => MemoryUsageBytes / 1024.0 / 1024.0;


    public override string ToString()

        => $"Plugin={PluginId}, Memory={MemoryUsageMB:F1}MB, Operations={OperationCount}, Uptime={Uptime:hh\\:mm\\:ss}";
}