// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Core;

/// <summary>
/// Configuration options for consolidated plugin service provider.
/// </summary>
public sealed class ConsolidatedPluginServiceProviderOptions
{
    /// <summary>
    /// Gets or sets whether to allow fallback to host services.
    /// </summary>
    public bool AllowHostServiceFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of plugin scopes.
    /// </summary>
    public int MaxPluginScopes { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable service validation.
    /// </summary>
    public bool EnableServiceValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the service resolution timeout.
    /// </summary>
    public TimeSpan ServiceResolutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}