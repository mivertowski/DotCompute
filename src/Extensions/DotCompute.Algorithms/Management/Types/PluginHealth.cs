// <copyright file="PluginHealth.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Algorithms.Management.Types;

/// <summary>
/// Represents the health status of a loaded plugin.
/// Used for monitoring and maintaining plugin reliability in production environments.
/// </summary>
public enum PluginHealth
{
    /// <summary>
    /// Health status is unknown or has not been checked.
    /// Initial state before the first health check is performed.
    /// </summary>
    Unknown,

    /// <summary>
    /// Plugin is healthy and operating normally.
    /// All health checks are passing and the plugin is responding correctly.
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin is degraded but still operational.
    /// Some non-critical issues detected but the plugin can still execute algorithms.
    /// </summary>
    Degraded,

    /// <summary>
    /// Plugin is unhealthy and should not be used.
    /// Critical issues detected that prevent proper operation.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Plugin is in critical condition and must be restarted.
    /// Severe issues that require immediate intervention.
    /// </summary>
    Critical,

    /// <summary>
    /// Plugin is being checked for health status.
    /// A health check operation is currently in progress.
    /// </summary>
    Checking,

    /// <summary>
    /// Plugin has been quarantined due to security concerns.
    /// The plugin exhibited suspicious behavior or failed security validation.
    /// </summary>
    Quarantined
}