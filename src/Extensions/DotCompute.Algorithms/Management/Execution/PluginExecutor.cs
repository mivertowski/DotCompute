
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Net.Sockets;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Execution;

/// <summary>
/// Service responsible for executing plugins with monitoring and retry logic.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginExecutor"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="lifecycleManager">The plugin lifecycle manager.</param>
public sealed partial class PluginExecutor(ILogger<PluginExecutor> logger, IPluginLifecycleManager lifecycleManager) : IPluginExecutor
{
    private readonly ILogger<PluginExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPluginLifecycleManager _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));

    /// <inheritdoc/>
    public async Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(inputs);

        var plugin = _lifecycleManager.GetPlugin(pluginId) ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found.");
        var pluginInfo = _lifecycleManager.GetLoadedPluginInfo(pluginId) ?? throw new InvalidOperationException($"Plugin '{pluginId}' information not found.");

        // Check plugin health before execution
        if (pluginInfo.Health == PluginHealth.Critical || pluginInfo.State != PluginState.Running)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' is not in a healthy state for execution. Health: {pluginInfo.Health}, State: {pluginInfo.State}");
        }

        LogExecutingAlgorithm(pluginId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Execute with error recovery
            var result = await ExecuteWithRetryAsync(plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            LogPluginExecutionCompleted(pluginId, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogPluginExecutionFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<object> ExecuteWithRetryAsync(
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
                LogPluginRetryingExecution(plugin.Id, attempt, ex.Message);
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
               ex is SocketException ||
               (ex is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing algorithm {PluginId}")]
    private partial void LogExecutingAlgorithm(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin execution completed for {PluginId} in {ElapsedMs} ms")]
    private partial void LogPluginExecutionCompleted(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin execution failed for {PluginId}: {Reason}")]
    private partial void LogPluginExecutionFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrying plugin execution for {PluginId}, attempt {Attempt}: {Reason}")]
    private partial void LogPluginRetryingExecution(string pluginId, int attempt, string reason);

    #endregion
}
