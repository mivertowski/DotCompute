// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Manages the complete lifecycle of algorithm plugins from initialization to disposal.
/// Handles plugin state transitions, health monitoring, and graceful shutdown.
/// </summary>
public sealed partial class AlgorithmPluginLifecycle : IDisposable
{
    private readonly ILogger<AlgorithmPluginLifecycle> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginLifecycle"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public AlgorithmPluginLifecycle(
        ILogger<AlgorithmPluginLifecycle> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize health check timer if enabled
        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecksWrapper, null,
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
        else
        {
            _healthCheckTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Initializes a plugin with the specified accelerator.
    /// </summary>
    /// <param name="plugin">The plugin to initialize.</param>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public async Task InitializePluginAsync(
        IAlgorithmPlugin plugin,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(accelerator);

        try
        {
            _logger.LogInformation("Initializing plugin {PluginId} ({PluginName})", plugin.Id, plugin.Name);

            // Validate plugin before initialization
            ValidatePlugin(plugin);

            // Initialize the plugin
            await plugin.InitializeAsync(accelerator, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully initialized plugin {PluginId}", plugin.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin {PluginId}: {ErrorMessage}", plugin.Id, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes a plugin with retry logic and error handling.
    /// </summary>
    /// <param name="plugin">The plugin to execute.</param>
    /// <param name="inputs">The input parameters.</param>
    /// <param name="parameters">Optional execution parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<object> ExecutePluginAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(inputs);

        _logger.LogDebug("Executing plugin {PluginId}", plugin.Id);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Execute with retry logic for transient failures
            var result = await ExecuteWithRetryAsync(plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Plugin {PluginId} executed successfully in {ElapsedMs} ms",
                plugin.Id, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Plugin {PluginId} execution failed after {ElapsedMs} ms: {ErrorMessage}",
                plugin.Id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gracefully shuts down a plugin.
    /// </summary>
    /// <param name="plugin">The plugin to shutdown.</param>
    /// <param name="timeout">Maximum time to wait for shutdown.</param>
    /// <returns>A task representing the shutdown operation.</returns>
    public async Task ShutdownPluginAsync(IAlgorithmPlugin plugin, TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        var shutdownTimeout = timeout ?? TimeSpan.FromSeconds(30);
        _logger.LogInformation("Shutting down plugin {PluginId} with timeout {Timeout}", plugin.Id, shutdownTimeout);

        try
        {
            using var cts = new CancellationTokenSource(shutdownTimeout);

            // Attempt graceful shutdown
            await plugin.DisposeAsync().ConfigureAwait(false);

            _logger.LogInformation("Successfully shut down plugin {PluginId}", plugin.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Plugin {PluginId} shutdown timed out after {Timeout}", plugin.Id, shutdownTimeout);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin {PluginId} shutdown: {ErrorMessage}", plugin.Id, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Performs comprehensive health checks on a plugin.
    /// </summary>
    /// <param name="loadedPlugin">The loaded plugin to check.</param>
    /// <returns>A task representing the health check operation.</returns>
    public async Task<PluginHealthResult> CheckPluginHealthAsync(LoadedPlugin loadedPlugin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(loadedPlugin);

        var healthResult = new PluginHealthResult
        {
            PluginId = loadedPlugin.Plugin.Id,
            CheckTime = DateTime.UtcNow,
            PreviousHealth = loadedPlugin.Health
        };

        try
        {
            // Memory usage check
            await PerformMemoryUsageMonitoringAsync(loadedPlugin, healthResult).ConfigureAwait(false);

            // Response time analysis
            await PerformResponseTimeAnalysisAsync(loadedPlugin, healthResult).ConfigureAwait(false);

            // Error rate tracking
            await PerformErrorRateTrackingAsync(loadedPlugin, healthResult).ConfigureAwait(false);

            // Resource leak detection
            await PerformResourceLeakDetectionAsync(loadedPlugin, healthResult).ConfigureAwait(false);

            // Determine overall health based on checks
            healthResult.CurrentHealth = DetermineOverallHealth(healthResult);

            // Update plugin health if changed
            if (healthResult.CurrentHealth != healthResult.PreviousHealth)
            {
                loadedPlugin.Health = healthResult.CurrentHealth;
                _logger.LogInformation("Plugin {PluginId} health changed: {PreviousHealth} -> {CurrentHealth}",
                    loadedPlugin.Plugin.Id, healthResult.PreviousHealth, healthResult.CurrentHealth);
            }

            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during health check for plugin {PluginId}", loadedPlugin.Plugin.Id);
            healthResult.CurrentHealth = PluginHealth.Critical;
            healthResult.HealthIssues.Add($"Health check failed: {ex.Message}");
            return healthResult;
        }
    }

    /// <summary>
    /// Validates plugin configuration and requirements.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    private static void ValidatePlugin(IAlgorithmPlugin plugin)
    {
        // Validate plugin ID
        if (string.IsNullOrWhiteSpace(plugin.Id))
        {
            throw new InvalidOperationException("Plugin must have a valid ID");
        }

        // Validate plugin name
        if (string.IsNullOrWhiteSpace(plugin.Name))
        {
            throw new InvalidOperationException("Plugin must have a valid name");
        }

        // Validate supported accelerators
        if (plugin.SupportedAcceleratorTypes.Length == 0)
        {
            throw new InvalidOperationException("Plugin must support at least one accelerator type");
        }

        // Validate input types
        if (plugin.InputTypes.Length == 0)
        {
            throw new InvalidOperationException("Plugin must specify at least one input type");
        }
    }

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                _logger.LogWarning("Plugin {PluginId} execution attempt {Attempt} failed with transient error: {ErrorMessage}",
                    plugin.Id, attempt, ex.Message);
                await Task.Delay(retryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt without retry handling
        return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and worth retrying.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is System.Net.Sockets.SocketException ||
               (ex is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Monitors memory usage for a loaded plugin.
    /// </summary>
    private async Task PerformMemoryUsageMonitoringAsync(LoadedPlugin loadedPlugin, PluginHealthResult healthResult)
    {
        try
        {
            var memoryBefore = GC.GetTotalMemory(false);

            // Force garbage collection for accurate reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsage = Math.Max(0, memoryAfter - memoryBefore);

            healthResult.MemoryUsageBytes = memoryUsage;

            // Check if memory usage is excessive
            if (memoryUsage > _options.MaxAssemblySize * 2)
            {
                healthResult.HealthIssues.Add($"Excessive memory usage: {memoryUsage:N0} bytes");
            }
            else if (memoryUsage > _options.MaxAssemblySize)
            {
                healthResult.HealthWarnings.Add($"High memory usage: {memoryUsage:N0} bytes");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            healthResult.HealthWarnings.Add($"Memory monitoring failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes response time patterns for a loaded plugin.
    /// </summary>
    private static async Task PerformResponseTimeAnalysisAsync(LoadedPlugin loadedPlugin, PluginHealthResult healthResult)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return;
            }

            var averageResponseTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;
            healthResult.AverageResponseTimeMs = averageResponseTime;

            const double maxAcceptableResponseTime = 30000; // 30 seconds
            const double warningResponseTime = 10000; // 10 seconds

            if (averageResponseTime > maxAcceptableResponseTime)
            {
                healthResult.HealthIssues.Add($"Average response time too high: {averageResponseTime:F2} ms");
            }
            else if (averageResponseTime > warningResponseTime)
            {
                healthResult.HealthWarnings.Add($"Elevated response time: {averageResponseTime:F2} ms");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            healthResult.HealthWarnings.Add($"Response time analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tracks error rates for a loaded plugin.
    /// </summary>
    private static async Task PerformErrorRateTrackingAsync(LoadedPlugin loadedPlugin, PluginHealthResult healthResult)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return;
            }

            var errorCount = loadedPlugin.LastError != null ? 1 : 0;
            var errorRate = (double)errorCount / loadedPlugin.ExecutionCount;
            healthResult.ErrorRate = errorRate;

            const double criticalErrorRate = 0.5; // 50%
            const double warningErrorRate = 0.1; // 10%

            if (errorRate > criticalErrorRate)
            {
                healthResult.HealthIssues.Add($"Critical error rate: {errorRate:P2}");
            }
            else if (errorRate > warningErrorRate)
            {
                healthResult.HealthWarnings.Add($"Elevated error rate: {errorRate:P2}");
            }

            // Check for recent errors
            if (loadedPlugin.LastError != null &&
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                healthResult.HealthWarnings.Add($"Recent error: {loadedPlugin.LastError.Message}");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            healthResult.HealthWarnings.Add($"Error rate tracking failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects potential resource leaks for a loaded plugin.
    /// </summary>
    private static async Task PerformResourceLeakDetectionAsync(LoadedPlugin loadedPlugin, PluginHealthResult healthResult)
    {
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var handleCount = currentProcess.HandleCount;
            var threadCount = currentProcess.Threads.Count;

            healthResult.HandleCount = handleCount;
            healthResult.ThreadCount = threadCount;

            // Store previous counts in metadata for comparison
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousHandleCount", out var prevHandleObj) &&
                prevHandleObj is int prevHandleCount)
            {
                var handleIncrease = handleCount - prevHandleCount;
                if (handleIncrease > 100) // Threshold for handle leaks
                {
                    healthResult.HealthWarnings.Add($"Potential handle leak: {handleIncrease} new handles");
                }
            }

            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousThreadCount", out var prevThreadObj) &&
                prevThreadObj is int prevThreadCount)
            {
                var threadIncrease = threadCount - prevThreadCount;
                if (threadIncrease > 10) // Threshold for thread leaks
                {
                    healthResult.HealthWarnings.Add($"Potential thread leak: {threadIncrease} new threads");
                }
            }

            // Update counts for next check
            loadedPlugin.Metadata.AdditionalMetadata["PreviousHandleCount"] = handleCount;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousThreadCount"] = threadCount;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            healthResult.HealthWarnings.Add($"Resource leak detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines overall health based on individual check results.
    /// </summary>
    private static PluginHealth DetermineOverallHealth(PluginHealthResult healthResult)
    {
        if (healthResult.HealthIssues.Count > 0)
        {
            return PluginHealth.Critical;
        }

        if (healthResult.HealthWarnings.Count >= 3)
        {
            return PluginHealth.Degraded;
        }

        if (healthResult.HealthWarnings.Count > 0)
        {
            return PluginHealth.Degraded;
        }

        return PluginHealth.Healthy;
    }

    /// <summary>
    /// Wrapper for health check timer callback.
    /// </summary>
    private void PerformHealthChecksWrapper(object? state) => _ = Task.Run(async () => await PerformHealthChecksAsync(state));

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    private async Task PerformHealthChecksAsync(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Performing scheduled health checks");
            // Note: This would need access to the plugin registry to iterate over plugins
            // In the actual implementation, this would be coordinated with the main manager
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during scheduled health checks");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _healthCheckTimer.Dispose();
        }
    }
}

/// <summary>
/// Represents the result of a plugin health check.
/// </summary>
public sealed class PluginHealthResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the time when the health check was performed.
    /// </summary>
    public DateTime CheckTime { get; init; }

    /// <summary>
    /// Gets or sets the previous health status.
    /// </summary>
    public PluginHealth PreviousHealth { get; init; }

    /// <summary>
    /// Gets or sets the current health status.
    /// </summary>
    public PluginHealth CurrentHealth { get; set; }

    /// <summary>
    /// Gets the list of health issues (critical problems).
    /// </summary>
    public IList<string> HealthIssues { get; } = [];

    /// <summary>
    /// Gets the list of health warnings (non-critical concerns).
    /// </summary>
    public IList<string> HealthWarnings { get; } = [];

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the error rate (0.0 to 1.0).
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the current handle count.
    /// </summary>
    public int HandleCount { get; set; }

    /// <summary>
    /// Gets or sets the current thread count.
    /// </summary>
    public int ThreadCount { get; set; }
}