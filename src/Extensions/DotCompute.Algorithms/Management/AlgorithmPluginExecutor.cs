// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;
using System;
// TODO: Add Polly package reference to DotCompute.Algorithms.csproj for resilience patterns

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Handles plugin execution with retry policies, circuit breakers, and performance tracking.
/// </summary>
public partial class AlgorithmPluginExecutor(ILogger<AlgorithmPluginExecutor> logger, AlgorithmPluginManagerOptions options)
{
    private readonly ILogger<AlgorithmPluginExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Executes a plugin with the specified input and retry policies.
    /// </summary>
    public async Task<PluginExecutionResult> ExecutePluginAsync(
        IAlgorithmPlugin plugin,
        object input,
        CancellationToken cancellationToken = default)
    {
        if (plugin == null)
        {

            throw new ArgumentNullException(nameof(plugin));
        }


        if (input == null)
        {

            throw new ArgumentNullException(nameof(input));
        }


        LogExecutingPlugin(plugin.Id, input.GetType().Name);

        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;
        object? result = null;
        var attempts = 0;

        try
        {
            // Execute with retry policy if enabled
            if (_options.EnableRetryPolicies)
            {
                result = await ExecuteWithRetryAsync(plugin, input, executionId, cancellationToken);
            }
            else
            {
                result = await plugin.ExecuteAsync([input], null, cancellationToken);
            }

            stopwatch.Stop();

            LogPluginExecutedSuccessfully(plugin.Id, stopwatch.ElapsedMilliseconds);

            return new PluginExecutionResult
            {
                Success = true,
                Result = result,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Attempts = attempts,
                ExecutionId = executionId
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            lastException = ex;

            LogPluginExecutionFailed(ex, plugin.Id, stopwatch.ElapsedMilliseconds);

            return new PluginExecutionResult
            {
                Success = false,
                Result = null,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Attempts = attempts,
                ExecutionId = executionId,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Executes a plugin with retry policies and circuit breaker.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object input,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        // TODO: Re-enable Polly resilience patterns after adding package reference
        // Simple retry logic for now
        Exception? lastException = null;
        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * _options.RetryDelayMilliseconds);
                    LogRetryingPluginExecution(plugin.Id, attempt, lastException?.Message ?? string.Empty);
                    await Task.Delay(delay, cancellationToken);
                }

                LogExecutingPluginWithExecutionId(plugin.Id, executionId);

                return await plugin.ExecuteAsync([input], null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry on cancellation
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == _options.MaxRetryAttempts || !IsRetryableException(ex))
                {
                    LogPluginExecutionFailedAfterAttempts(ex, plugin.Id, attempt + 1);
                    throw;
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Execution failed");
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    private static bool IsRetryableException(Exception ex)
    {
        // Don't retry on cancellation
        if (ex is OperationCanceledException)
        {
            return false;
        }

        // Don't retry on argument exceptions

        if (ex is ArgumentException)
        {
            return false;
        }

        // Don't retry on not supported exceptions

        if (ex is NotSupportedException)
        {
            return false;
        }

        // Retry on transient errors

        if (ex is TimeoutException)
        {
            return true;
        }

        // Check for specific error messages or types that indicate transient errors

        var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
        if (message.Contains("timeout", StringComparison.CurrentCulture) || message.Contains("temporary", StringComparison.CurrentCulture) || message.Contains("transient"))
        {
            return true;
        }

        // Default to retrying for unknown exceptions

        return true;
    }

    /// <summary>
    /// Validates that the input is compatible with the plugin.
    /// </summary>
    public bool ValidateInput(IAlgorithmPlugin plugin, object input)
    {
        if (plugin == null || input == null)
        {
            return false;
        }


        var inputType = input.GetType();
        var expectedTypes = plugin.InputTypes;

        foreach (var expectedType in expectedTypes)
        {
            if (expectedType.IsAssignableFrom(inputType))
            {
                LogInputTypeCompatible(inputType.Name, plugin.Id);
                return true;
            }
        }

        LogInputTypeNotCompatible(inputType.Name, plugin.Id);
        return false;
    }

    #region LoggerMessage Delegates

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing plugin: {PluginId} with input type: {InputType}")]
    private partial void LogExecutingPlugin(string pluginId, string inputType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} executed successfully in {ElapsedMs}ms")]
    private partial void LogPluginExecutedSuccessfully(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} execution failed after {ElapsedMs}ms")]
    private partial void LogPluginExecutionFailed(Exception exception, string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retrying plugin {PluginId} execution (attempt {RetryCount}) after error: {Error}")]
    private partial void LogRetryingPluginExecution(string pluginId, int retryCount, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing plugin {PluginId} with execution ID: {ExecutionId}")]
    private partial void LogExecutingPluginWithExecutionId(string pluginId, Guid executionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} execution failed after {Attempts} attempts")]
    private partial void LogPluginExecutionFailedAfterAttempts(Exception exception, string pluginId, int attempts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Input type {InputType} is compatible with plugin {PluginId}")]
    private partial void LogInputTypeCompatible(string inputType, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Input type {InputType} is not compatible with plugin {PluginId}")]
    private partial void LogInputTypeNotCompatible(string inputType, string pluginId);

    #endregion
}

/// <summary>
/// Represents the result of a plugin execution.
/// </summary>
public class PluginExecutionResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; init; }
    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    /// <value>The result.</value>
    public object? Result { get; init; }
    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>The execution time ms.</value>
    public long ExecutionTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the attempts.
    /// </summary>
    /// <value>The attempts.</value>
    public int Attempts { get; init; }
    /// <summary>
    /// Gets or sets the execution identifier.
    /// </summary>
    /// <value>The execution id.</value>
    public Guid ExecutionId { get; init; }
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>The error.</value>
    public Exception? Error { get; init; }
}

/// <summary>
/// Options for plugin execution policies.
/// </summary>
public static class AlgorithmPluginExecutionOptions
{
    /// <summary>
    /// The default enable retry policies.
    /// </summary>
    public const bool DefaultEnableRetryPolicies = true;
    /// <summary>
    /// The default max retry attempts.
    /// </summary>
    public const int DefaultMaxRetryAttempts = 3;
    /// <summary>
    /// The default retry delay milliseconds.
    /// </summary>
    public const int DefaultRetryDelayMilliseconds = 100;
    /// <summary>
    /// The default circuit breaker threshold.
    /// </summary>
    public const int DefaultCircuitBreakerThreshold = 5;
    /// <summary>
    /// The default circuit breaker duration seconds.
    /// </summary>
    public const int DefaultCircuitBreakerDurationSeconds = 30;
}