// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Abstractions
{

/// <summary>
/// Base class for algorithm plugins providing common functionality.
/// </summary>
public abstract partial class AlgorithmPluginBase : IAlgorithmPlugin
{
    private readonly ILogger _logger;
    private IAccelerator? _accelerator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginBase"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    protected AlgorithmPluginBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract Version Version { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract AcceleratorType[] SupportedAccelerators { get; }

    /// <inheritdoc/>
    public abstract Type[] InputTypes { get; }

    /// <inheritdoc/>
    public abstract Type OutputType { get; }

    /// <summary>
    /// Gets the current accelerator instance.
    /// </summary>
    protected IAccelerator Accelerator => _accelerator ?? throw new InvalidOperationException("Plugin not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger Logger => _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing algorithm plugin {PluginName} with accelerator {AcceleratorName}")]
    private partial void LogInitializing(string pluginName, string acceleratorName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Algorithm plugin {PluginName} initialized successfully")]
    private partial void LogInitialized(string pluginName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing algorithm {PluginName} with {InputCount} inputs")]
    private partial void LogExecuting(string pluginName, int inputCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Algorithm {PluginName} execution completed")]
    private partial void LogExecutionCompleted(string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Algorithm {PluginName} execution failed")]
    private partial void LogExecutionFailed(string pluginName, Exception exception);

    /// <inheritdoc/>
    public virtual async Task InitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Check if accelerator type is supported
        var acceleratorType = GetAcceleratorType(accelerator);
        if (!SupportedAccelerators.Contains(acceleratorType))
        {
            throw new NotSupportedException($"Accelerator type {acceleratorType} is not supported by {Name}");
        }

        LogInitializing(Name, accelerator.Info.Name);

        _accelerator = accelerator;
        await OnInitializeAsync(accelerator, cancellationToken).ConfigureAwait(false);

        LogInitialized(Name);
    }

    /// <inheritdoc/>
    public async Task<object> ExecuteAsync(object[] inputs, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_accelerator == null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call InitializeAsync first.");
        }

        if (!ValidateInputs(inputs))
        {
            throw new ArgumentException("Invalid inputs provided to algorithm.");
        }

        LogExecuting(Name, inputs.Length);

        try
        {
            var result = await OnExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
            LogExecutionCompleted(Name);
            return result;
        }
        catch (Exception ex)
        {
            LogExecutionFailed(Name, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual bool ValidateInputs(object[] inputs)
    {
        if (inputs == null || inputs.Length != InputTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < inputs.Length; i++)
        {
            if (inputs[i] == null || !InputTypes[i].IsAssignableFrom(inputs[i].GetType()))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public abstract long EstimateMemoryRequirement(int[] inputSizes);

    /// <inheritdoc/>
    public abstract AlgorithmPerformanceProfile GetPerformanceProfile();

    /// <inheritdoc/>
    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await OnDisposeAsync().ConfigureAwait(false);
            _disposed = true;
        }
    }

    /// <summary>
    /// Called when the plugin is being initialized.
    /// </summary>
    /// <param name="accelerator">The accelerator instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    protected virtual Task OnInitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Implements the actual algorithm execution.
    /// </summary>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The algorithm result.</returns>
    protected abstract Task<object> OnExecuteAsync(object[] inputs, Dictionary<string, object>? parameters, CancellationToken cancellationToken);

    /// <summary>
    /// Called when the plugin is being disposed.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    protected virtual ValueTask OnDisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static AcceleratorType GetAcceleratorType(IAccelerator accelerator)
    {
        // Map device type string to AcceleratorType enum
        return accelerator.Info.DeviceType switch
        {
            "CPU" => AcceleratorType.CPU,
            "CUDA" => AcceleratorType.CUDA,
            "ROCm" => AcceleratorType.ROCm,
            "OneAPI" => AcceleratorType.OneAPI,
            "Metal" => AcceleratorType.Metal,
            "OpenCL" => AcceleratorType.OpenCL,
            "DirectML" => AcceleratorType.DirectML,
            _ => AcceleratorType.Custom
        };
    }
}}
