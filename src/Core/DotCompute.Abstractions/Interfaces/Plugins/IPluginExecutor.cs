// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Abstractions.Interfaces.Plugins;

/// <summary>
/// Interface for plugin execution operations across the DotCompute ecosystem.
/// Provides standardized execution patterns with enhanced monitoring and retry logic.
/// </summary>
public interface IPluginExecutor
{
    /// <summary>
    /// Executes a plugin with the specified inputs and enhanced monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    /// <param name="plugin">The plugin to execute.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets execution statistics for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Execution statistics.</returns>
    Task<PluginExecutionStatistics> GetStatisticsAsync(string pluginId);

    /// <summary>
    /// Validates plugin inputs before execution.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <returns>Validation result.</returns>
    Task<ValidationResult> ValidateInputsAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null);
}

/// <summary>
/// Represents plugin execution statistics.
/// </summary>
public class PluginExecutionStatistics
{
    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets additional statistics.
    /// </summary>
    public Dictionary<string, object> AdditionalStatistics { get; set; } = [];
}