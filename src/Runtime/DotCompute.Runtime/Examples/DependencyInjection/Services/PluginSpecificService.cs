// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Services;

/// <summary>
/// Example plugin-specific service implementation
/// </summary>
public class PluginSpecificService : IPluginSpecificService
{
    /// <inheritdoc />
    public async Task ProcessAsync() => await Task.Delay(5); // Simulate processing
}