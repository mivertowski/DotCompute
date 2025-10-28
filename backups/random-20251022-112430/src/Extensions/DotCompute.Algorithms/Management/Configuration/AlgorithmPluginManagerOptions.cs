#nullable enable

// <copyright file="AlgorithmPluginManagerOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Security;

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
    public IList<string> AllowedPluginDirectories { get; } = [];

    /// <summary>
    /// Gets trusted assembly publishers for signature validation.
    /// Only assemblies signed by these publishers will be loaded when signature validation is enabled.
    /// </summary>
    public IList<string> TrustedPublishers { get; } = [];

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

    /// <summary>
    /// Gets or sets the timeout for plugin operations.
    /// Prevents plugins from running indefinitely and causing system hangs.
    /// </summary>
    public TimeSpan PluginTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum memory usage allowed per plugin in megabytes.
    /// Prevents plugins from consuming excessive system memory.
    /// </summary>
    public int MaxMemoryUsageMB { get; set; } = 512; // 512 MB default limit

    /// <summary>
    /// Gets or sets whether plugins are allowed to access the file system.
    /// When disabled, plugins will be restricted from file I/O operations.
    /// </summary>
    public bool AllowFileSystemAccess { get; set; }  // Secure by default

    /// <summary>
    /// Gets or sets whether plugins are allowed to access the network.
    /// When disabled, plugins will be restricted from network operations.
    /// </summary>
    public bool AllowNetworkAccess { get; set; }  // Secure by default

    /// <summary>
    /// Gets or sets whether plugins must be security transparent.
    /// Requires plugins to be marked with SecurityTransparent attributes for enhanced security.
    /// </summary>
    public bool RequireSecurityTransparency { get; set; } = true;

    /// <summary>
    /// Gets or sets whether retry policies are enabled for plugin execution.
    /// When enabled, failed plugin executions will be retried based on configured settings.
    /// </summary>
    public bool EnableRetryPolicies { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed plugin executions.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base retry delay in milliseconds.
    /// Actual delay will be calculated using exponential backoff.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets the circuit breaker threshold.
    /// Number of failures before the circuit breaker opens.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the circuit breaker duration in seconds.
    /// How long the circuit breaker stays open before attempting to reset.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// Determines how long plugin metadata and compiled assemblies are cached.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the timeout for plugin initialization.
    /// Maximum time allowed for a plugin to complete initialization.
    /// </summary>
    public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for plugin shutdown.
    /// Maximum time allowed for a plugin to complete graceful shutdown.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the scan interval for plugin discovery.
    /// Determines how frequently the system scans for new or updated plugins.
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the delay before restarting a failed plugin.
    /// Time to wait before attempting to restart a plugin that has crashed.
    /// </summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(5);
}