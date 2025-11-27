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

    /// <summary>
    /// Gets or sets whether plugin service isolation is enabled.
    /// When enabled, each plugin gets its own service provider with controlled fallback to parent services.
    /// </summary>
    /// <remarks>
    /// Plugin isolation ensures:
    /// <list type="bullet">
    /// <item>Each plugin has its own singleton instances</item>
    /// <item>Service leakage between plugins is prevented</item>
    /// <item>Plugin failures don't affect other plugins or the host</item>
    /// <item>Controlled access to parent services via <see cref="AllowHostServiceFallback"/></item>
    /// </list>
    /// </remarks>
    public bool EnablePluginIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets the service types that should be blocked from parent provider fallback.
    /// This list is applied when <see cref="EnablePluginIsolation"/> and <see cref="AllowHostServiceFallback"/> are both true.
    /// </summary>
    /// <remarks>
    /// Use this to prevent plugins from accessing specific host services for security or isolation reasons.
    /// For example, blocking <c>IConfiguration</c> prevents plugins from reading host configuration.
    /// </remarks>
    public IList<Type> BlockedParentServices { get; set; } = [];
}
