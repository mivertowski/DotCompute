// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents kernel configuration settings that control how a kernel is compiled and executed.
/// This includes optimization levels, execution dimensions, and device-specific options.
/// </summary>
public class KernelConfiguration
{
    /// <summary>
    /// Gets or sets the kernel name for identification purposes.
    /// </summary>
    /// <value>The name of the kernel, or an empty string if not specified.</value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optimization level to use during kernel compilation.
    /// </summary>
    /// <value>The optimization level that controls compilation behavior and performance characteristics.</value>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets kernel-specific configuration options.
    /// </summary>
    /// <value>
    /// A dictionary containing kernel-specific options such as grid dimensions,
    /// block dimensions, shared memory size, and other execution parameters.
    /// </value>
    public Dictionary<string, object> Options { get; } = [];


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelConfiguration"/> class with default settings.
    /// </summary>
    public KernelConfiguration() { }


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelConfiguration"/> class with the specified execution dimensions.
    /// </summary>
    /// <param name="gridDim">The grid dimensions that define the overall execution space.</param>
    /// <param name="blockDim">The block dimensions that define the local execution group size.</param>
    /// <remarks>
    /// This constructor is commonly used for GPU kernels where execution is organized
    /// in a hierarchical grid-block structure. The grid dimension defines the number
    /// of blocks, and the block dimension defines the number of threads per block.
    /// </remarks>
    public KernelConfiguration(Dim3 gridDim, Dim3 blockDim)
    {
        Options["GridDimension"] = gridDim;
        Options["BlockDimension"] = blockDim;
    }

    /// <summary>
    /// Gets the grid dimension from the options, if specified.
    /// </summary>
    /// <returns>The grid dimension, or null if not configured.</returns>
    public Dim3? GetGridDimension() => Options.TryGetValue("GridDimension", out var value) && value is Dim3 dim ? dim : null;

    /// <summary>
    /// Gets the block dimension from the options, if specified.
    /// </summary>
    /// <returns>The block dimension, or null if not configured.</returns>
    public Dim3? GetBlockDimension() => Options.TryGetValue("BlockDimension", out var value) && value is Dim3 dim ? dim : null;

    /// <summary>
    /// Sets the grid and block dimensions for kernel execution.
    /// </summary>
    /// <param name="gridDim">The grid dimensions to set.</param>
    /// <param name="blockDim">The block dimensions to set.</param>
    public void SetExecutionDimensions(Dim3 gridDim, Dim3 blockDim)
    {
        Options["GridDimension"] = gridDim;
        Options["BlockDimension"] = blockDim;
    }

    /// <summary>
    /// Gets a strongly-typed option value from the configuration.
    /// </summary>
    /// <typeparam name="T">The expected type of the option value.</typeparam>
    /// <param name="key">The option key to retrieve.</param>
    /// <returns>The option value cast to the specified type, or the default value for the type if not found.</returns>
    public T? GetOption<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);


        if (Options.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }


        return default;
    }

    /// <summary>
    /// Sets an option value in the configuration.
    /// </summary>
    /// <param name="key">The option key to set.</param>
    /// <param name="value">The option value to store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public void SetOption(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Options[key] = value;
    }

    /// <summary>
    /// Removes an option from the configuration.
    /// </summary>
    /// <param name="key">The option key to remove.</param>
    /// <returns>True if the option was found and removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public bool RemoveOption(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Options.Remove(key);
    }
}