// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection.Logging;

/// <summary>
/// Example computation logger implementation
/// </summary>
public class ComputationLogger : IComputationLogger
{
    private readonly ILogger<ComputationLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputationLogger"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public ComputationLogger(ILogger<ComputationLogger> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogInitializationAsync(string pluginId, int dataSize)
    {
        _logger.LogInformation("Plugin {PluginId} initialized with {DataSize} bytes of data",
            pluginId, dataSize);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task LogExecutionAsync(string pluginId, int inputSize)
    {
        _logger.LogInformation("Plugin {PluginId} executed with input size {InputSize}",
            pluginId, inputSize);
        await Task.CompletedTask;
    }
}