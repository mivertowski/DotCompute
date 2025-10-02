// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Security;

/// <summary>
/// Result of a security vulnerability scan.
/// </summary>
public class SecurityScanResult
{
    /// <summary>
    /// Gets or sets the plugin ID that was scanned.
    /// </summary>
    public string PluginId { get; set; } = "";

    /// <summary>
    /// Gets or sets the plugin version that was scanned.
    /// </summary>
    public string PluginVersion { get; set; } = "";

    /// <summary>
    /// Gets or sets the scan date.
    /// </summary>
    public DateTimeOffset ScanDate { get; set; }

    /// <summary>
    /// Gets the critical vulnerabilities found.
    /// </summary>
    public IList<SecurityVulnerability> CriticalVulnerabilities { get; } = [];

    /// <summary>
    /// Gets the high-risk vulnerabilities found.
    /// </summary>
    public IList<SecurityVulnerability> HighRiskVulnerabilities { get; } = [];

    /// <summary>
    /// Gets the medium-risk vulnerabilities found.
    /// </summary>
    public IList<SecurityVulnerability> MediumRiskVulnerabilities { get; } = [];

    /// <summary>
    /// Gets the low-risk vulnerabilities found.
    /// </summary>
    public IList<SecurityVulnerability> LowRiskVulnerabilities { get; } = [];

    /// <summary>
    /// Gets all vulnerabilities found.
    /// </summary>
    public IEnumerable<SecurityVulnerability> AllVulnerabilities
        => CriticalVulnerabilities.Concat(HighRiskVulnerabilities)
            .Concat(MediumRiskVulnerabilities)
            .Concat(LowRiskVulnerabilities);

    /// <summary>
    /// Gets whether critical vulnerabilities were found.
    /// </summary>
    public bool HasCriticalVulnerabilities => CriticalVulnerabilities.Count > 0;

    /// <summary>
    /// Gets whether high-risk vulnerabilities were found.
    /// </summary>
    public bool HasHighRiskVulnerabilities => HighRiskVulnerabilities.Count > 0;

    /// <summary>
    /// Gets the scan errors.
    /// </summary>
    public IList<string> ScanErrors { get; } = [];

    /// <summary>
    /// Gets or sets the scan duration.
    /// </summary>
    public TimeSpan ScanDuration { get; set; }

    /// <summary>
    /// Gets or sets whether the scan had errors.
    /// </summary>
    public bool HasErrors { get; set; }

    /// <summary>
    /// Gets or sets the error messages from the scan.
    /// </summary>
    public IList<string> ErrorMessages { get; } = [];
}