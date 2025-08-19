// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Security;

/// <summary>
/// Configuration for the plugin sandbox environment.
/// </summary>
public class SandboxConfiguration
{
    /// <summary>
    /// Gets or sets the default execution timeout for sandboxed operations.
    /// </summary>
    public TimeSpan DefaultExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum number of concurrent sandboxed plugins.
    /// </summary>
    public int MaxConcurrentPlugins { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default resource limits for plugins.
    /// </summary>
    public ResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable detailed security logging.
    /// </summary>
    public bool EnableDetailedSecurityLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable automatic plugin termination on violations.
    /// </summary>
    public bool EnableAutomaticTermination { get; set; } = true;

    /// <summary>
    /// Gets or sets the monitoring interval for resource usage.
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the security incident retention period.
    /// </summary>
    public TimeSpan SecurityIncidentRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets the list of globally blocked assembly names.
    /// </summary>
    public HashSet<string> GloballyBlockedAssemblies { get; } = new()
    {
        "System.Management",
        "Microsoft.Win32.Registry",
        "System.Diagnostics.Process"
    };

    /// <summary>
    /// Gets the list of dangerous API patterns to monitor.
    /// </summary>
    public HashSet<string> DangerousAPIPatterns { get; } = new()
    {
        "CreateProcess",
        "LoadLibrary",
        "GetProcAddress",
        "VirtualAlloc",
        "WriteProcessMemory",
        "CreateRemoteThread",
        "SetWindowsHookEx",
        "RegOpenKeyEx",
        "RegSetValueEx"
    };

    /// <summary>
    /// Gets or sets the trusted certificate store for assembly verification.
    /// </summary>
    public string? TrustedCertificateStore { get; set; }

    /// <summary>
    /// Gets or sets whether to require strong name validation.
    /// </summary>
    public bool RequireStrongNameValidation { get; set; } = true;

    /// <summary>
    /// Creates a default restrictive sandbox configuration.
    /// </summary>
    public static SandboxConfiguration CreateRestrictive()
    {
        return new SandboxConfiguration
        {
            DefaultExecutionTimeout = TimeSpan.FromMinutes(1),
            MaxConcurrentPlugins = 5,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMB = 50,
                MaxCpuUsagePercent = 25,
                MaxExecutionTimeSeconds = 60,
                MaxThreads = 2,
                MaxFileIOPerSecond = 10,
                MaxNetworkIOPerSecond = 5
            },
            EnableDetailedSecurityLogging = true,
            EnableAutomaticTermination = true,
            MonitoringInterval = TimeSpan.FromMilliseconds(500),
            RequireStrongNameValidation = true
        };
    }

    /// <summary>
    /// Creates a moderate sandbox configuration for trusted plugins.
    /// </summary>
    public static SandboxConfiguration CreateModerate()
    {
        return new SandboxConfiguration
        {
            DefaultExecutionTimeout = TimeSpan.FromMinutes(5),
            MaxConcurrentPlugins = 10,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMB = 256,
                MaxCpuUsagePercent = 50,
                MaxExecutionTimeSeconds = 300,
                MaxThreads = 4,
                MaxFileIOPerSecond = 100,
                MaxNetworkIOPerSecond = 50
            },
            EnableDetailedSecurityLogging = true,
            EnableAutomaticTermination = true,
            MonitoringInterval = TimeSpan.FromSeconds(1),
            RequireStrongNameValidation = false
        };
    }

    /// <summary>
    /// Creates a permissive sandbox configuration for development environments.
    /// </summary>
    public static SandboxConfiguration CreatePermissive()
    {
        return new SandboxConfiguration
        {
            DefaultExecutionTimeout = TimeSpan.FromMinutes(30),
            MaxConcurrentPlugins = 20,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMB = 1024,
                MaxCpuUsagePercent = 80,
                MaxExecutionTimeSeconds = 1800,
                MaxThreads = 8,
                MaxFileIOPerSecond = 1000,
                MaxNetworkIOPerSecond = 500
            },
            EnableDetailedSecurityLogging = false,
            EnableAutomaticTermination = false,
            MonitoringInterval = TimeSpan.FromSeconds(5),
            RequireStrongNameValidation = false
        };
    }

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (DefaultExecutionTimeout <= TimeSpan.Zero)
        {
            errors.Add("DefaultExecutionTimeout must be positive");
        }

        if (MaxConcurrentPlugins <= 0)
        {
            errors.Add("MaxConcurrentPlugins must be positive");
        }

        if (MonitoringInterval <= TimeSpan.Zero)
        {
            errors.Add("MonitoringInterval must be positive");
        }

        if (SecurityIncidentRetention < TimeSpan.FromHours(1))
        {
            errors.Add("SecurityIncidentRetention must be at least 1 hour");
        }

        // Validate resource limits
        if (ResourceLimits.MaxMemoryMB <= 0)
        {
            errors.Add("ResourceLimits.MaxMemoryMB must be positive");
        }

        if (ResourceLimits.MaxCpuUsagePercent <= 0 || ResourceLimits.MaxCpuUsagePercent > 100)
        {
            errors.Add("ResourceLimits.MaxCpuUsagePercent must be between 1 and 100");
        }

        if (ResourceLimits.MaxExecutionTimeSeconds <= 0)
        {
            errors.Add("ResourceLimits.MaxExecutionTimeSeconds must be positive");
        }

        if (ResourceLimits.MaxThreads <= 0)
        {
            errors.Add("ResourceLimits.MaxThreads must be positive");
        }

        return errors;
    }
}