
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Handles plugin execution with retry policies, circuit breakers, and performance tracking.
/// </summary>
public partial class AlgorithmPluginExecutor
{
    private readonly ILogger<AlgorithmPluginExecutor> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    public AlgorithmPluginExecutor(ILogger<AlgorithmPluginExecutor> logger, AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Build resilience pipeline with retry and circuit breaker policies
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => IsRetryableException(ex)),
                OnRetry = args =>
                {
                    LogRetryingPluginExecution("(Polly)", args.AttemptNumber, args.Outcome.Exception?.Message ?? string.Empty);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => IsRetryableException(ex)),
                OnOpened = args =>
                {
                    LogCircuitBreakerOpened(args.Outcome.Exception?.Message ?? "Multiple failures detected");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    LogCircuitBreakerClosed();
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    LogCircuitBreakerHalfOpened();
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Executes a plugin with the specified input and retry policies.
    /// </summary>
    public async Task<PluginExecutionResult> ExecutePluginAsync(
        IAlgorithmPlugin plugin,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);


        ArgumentNullException.ThrowIfNull(input);


        LogExecutingPlugin(plugin.Id, input.GetType().Name);

        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;

        try
        {
            // Execute with retry policy if enabled
            var result = _options.EnableRetryPolicies
                ? await ExecuteWithRetryAsync(plugin, input, executionId, cancellationToken)
                : await plugin.ExecuteAsync([input], null, cancellationToken);

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
    /// Executes a plugin with retry policies and circuit breaker using Polly resilience patterns.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object input,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        LogExecutingPluginWithExecutionId(plugin.Id, executionId);

        return await _resiliencePipeline.ExecuteAsync(
            async ct => await plugin.ExecuteAsync([input], null, ct),
            cancellationToken);
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

        var message = ex.Message?.ToUpperInvariant() ?? string.Empty;
        if (message.Contains("timeout", StringComparison.CurrentCulture) || message.Contains("temporary", StringComparison.CurrentCulture) || message.Contains("transient", StringComparison.Ordinal))
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker opened: {Reason}")]
    private partial void LogCircuitBreakerOpened(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker closed - normal operation resumed")]
    private partial void LogCircuitBreakerClosed();

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker half-opened - testing recovery")]
    private partial void LogCircuitBreakerHalfOpened();

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
