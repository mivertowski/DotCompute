// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.DependencyInjection.Validation;

/// <summary>
/// Default plugin validator implementation.
/// </summary>
#pragma warning disable CS9113 // Parameter 'logger' is unread - reserved for future logging
internal sealed class PluginValidator(ILogger<PluginValidator> logger) : IPluginValidator
{
    /// <summary>
    /// Validates the async.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<PluginValidationResult> ValidateAsync(object plugin)
    {
        await Task.CompletedTask;

        // Basic validation - can be extended
        if (plugin == null)
        {
            return new PluginValidationResult { IsValid = false, ErrorMessage = "Plugin instance is null" };
        }

        return new PluginValidationResult { IsValid = true };
    }
}

#pragma warning restore CS9113