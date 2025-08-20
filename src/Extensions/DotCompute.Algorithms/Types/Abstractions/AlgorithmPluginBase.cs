// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Types.Abstractions;


/// <summary>
/// Base class for algorithm plugins providing common functionality.
/// </summary>
public abstract class AlgorithmPluginBase : IAlgorithmPlugin
{
    /// <summary>
    /// Gets the plugin identifier.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public abstract string Version { get; }

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the supported accelerator types.
    /// </summary>
    public abstract IEnumerable<AcceleratorType> SupportedAccelerators { get; }

    /// <summary>
    /// Gets the supported input types.
    /// </summary>
    public abstract IEnumerable<Type> InputTypes { get; }

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger? Logger { get; private set; }

    /// <summary>
    /// Initializes the plugin with the given accelerator and logger.
    /// </summary>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A task representing the async initialization.</returns>
    public virtual ValueTask InitializeAsync(IAccelerator accelerator, ILogger? logger = null)
    {
        Logger = logger;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the algorithm with the given input data.
    /// </summary>
    /// <param name="input">Input data for the algorithm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The algorithm result.</returns>
    public abstract ValueTask<object> ExecuteAsync(object input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the input is compatible with this plugin.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>True if the input is valid, false otherwise.</returns>
    public virtual bool ValidateInput(object input)
    {
        if (input == null)
        {
            return false;
        }

        return InputTypes.Contains(input.GetType());
    }

    /// <summary>
    /// Gets the performance profile for this plugin.
    /// </summary>
    /// <returns>The performance profile.</returns>
    public virtual AlgorithmPerformanceProfile GetPerformanceProfile()
    {
        return new AlgorithmPerformanceProfile
        {
            AlgorithmId = Id,
            EstimatedExecutionTimeMs = 100, // Default estimate
            MemoryRequirementMB = 10, // Default estimate
            ComputeComplexity = ComputeComplexity.Medium
        };
    }

    /// <summary>
    /// Disposes the plugin resources.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
