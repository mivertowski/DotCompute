// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Security;

/// <summary>
/// Security policy configuration for plugin validation.
/// </summary>
public class SecurityPolicy
{
    /// <summary>
    /// Gets or sets whether to require digitally signed assemblies.
    /// </summary>
    public bool RequireSignedAssemblies { get; set; }

    /// <summary>
    /// Gets or sets the list of trusted certificate publishers.
    /// </summary>
    public List<string>? TrustedPublishers { get; set; }

    /// <summary>
    /// Gets or sets whether to scan for malicious code patterns.
    /// </summary>
    public bool ScanForMaliciousCode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to block packages with high-risk vulnerabilities.
    /// </summary>
    public bool BlockHighRiskPackages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to require strong name signing.
    /// </summary>
    public bool RequireStrongName { get; set; }

    /// <summary>
    /// Gets or sets whether to block suspicious assemblies.
    /// </summary>
    public bool BlockSuspiciousAssemblies { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of allowed dangerous assemblies.
    /// </summary>
    public IList<string> AllowedDangerousAssemblies { get; } = [];

    /// <summary>
    /// Gets or sets whether to block packages with critical vulnerabilities.
    /// </summary>
    public bool BlockCriticalVulnerabilities { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed assembly size in bytes.
    /// </summary>
    public long? MaxAssemblySize { get; set; }

    /// <summary>
    /// Gets or sets whether to block unsafe code.
    /// </summary>
    public bool BlockUnsafeCode { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed package sources.
    /// </summary>
    public List<string>? AllowedSources { get; set; }

    /// <summary>
    /// Gets or sets the blocked package sources.
    /// </summary>
    public List<string>? BlockedSources { get; set; }

    /// <summary>
    /// Gets or sets the allowed permissions for plugins.
    /// </summary>
    public List<string>? AllowedPermissions { get; set; }

    /// <summary>
    /// Gets or sets the vulnerability scan timeout.
    /// </summary>
    public TimeSpan VulnerabilityScanTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether to enable network security validation.
    /// </summary>
    public bool EnableNetworkValidation { get; set; } = true;
}