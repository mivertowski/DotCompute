// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Validation;

/// <summary>
/// Plugin validator interface.
/// </summary>
public interface IPluginValidator
{
    /// <summary>
    /// Validates a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <returns>Validation result.</returns>
    public Task<PluginValidationResult> ValidateAsync(object plugin);
}