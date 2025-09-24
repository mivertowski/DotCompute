// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.DependencyInjection.Validation;

/// <summary>
/// Default plugin validator implementation.
/// </summary>
internal sealed class PluginValidator : IPluginValidator
{
    private readonly ILogger<PluginValidator> _logger;

    public PluginValidator(ILogger<PluginValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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