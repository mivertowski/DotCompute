// <copyright file="AlgorithmPluginManagerOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Algorithms.Types.Security;

namespace DotCompute.Algorithms.Management.Configuration;

/// <summary>
/// Configuration options for the Algorithm Plugin Manager.
/// Controls security, isolation, monitoring, and lifecycle management of algorithm plugins.
/// </summary>
public sealed class AlgorithmPluginManagerOptions
{
    /// <summary>
    /// Gets or sets whether plugin isolation is enabled.
    /// When enabled, each plugin runs in its own assembly load context.
    /// </summary>
    public bool EnablePluginIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether security validation is enabled.
    /// Includes signature verification, malware scanning, and permission checks.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether hot reload is enabled.
    /// Allows plugins to be updated without restarting the application.
    /// </summary>
    public bool EnableHotReload { get; set; }


    /// <summary>
    /// Gets or sets whether health checks are enabled.
    /// Periodically validates plugin health and performance.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval.
    /// Determines how frequently plugin health is assessed.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum assembly size in bytes.
    /// Prevents loading of excessively large plugin assemblies.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Gets the allowed plugin directories.
    /// Only plugins from these directories will be loaded for security.
    /// </summary>
    public List<string> AllowedPluginDirectories { get; } = [];

    /// <summary>
    /// Gets trusted assembly publishers for signature validation.
    /// Only assemblies signed by these publishers will be loaded when signature validation is enabled.
    /// </summary>
    public List<string> TrustedPublishers { get; } = [];

    /// <summary>
    /// Gets or sets whether digital signatures are required.
    /// When enabled, only digitally signed assemblies will be loaded.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether strong names are required.
    /// When enabled, only strongly-named assemblies will be loaded.
    /// </summary>
    public bool RequireStrongName { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum security level required.
    /// Plugins must meet this security level to be loaded.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets whether metadata analysis is enabled.
    /// Analyzes assembly metadata for suspicious patterns and vulnerabilities.
    /// </summary>
    public bool EnableMetadataAnalysis { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Windows Defender scanning is enabled.
    /// Uses Windows Defender to scan plugin assemblies for malware.
    /// </summary>
    public bool EnableWindowsDefenderScanning { get; set; } = true;

    /// <summary>
    /// Gets or sets whether malware scanning is enabled.
    /// Performs comprehensive malware analysis on plugin assemblies.
    /// </summary>
    public bool EnableMalwareScanning { get; set; } = true;
}