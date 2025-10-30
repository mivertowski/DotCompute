
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer.Nvd;

/// <summary>
/// Represents the root response structure from the NVD (National Vulnerability Database) API.
/// Contains an array of vulnerabilities matching the query criteria.
/// </summary>
internal sealed class NvdResponse
{
    /// <summary>
    /// Gets or sets the array of vulnerabilities returned from the NVD API.
    /// </summary>
    public NvdVulnerability[]? Vulnerabilities { get; set; }
}

/// <summary>
/// Represents a single vulnerability entry from the NVD API response.
/// Contains CVE information and impact metrics.
/// </summary>
internal sealed class NvdVulnerability
{
    /// <summary>
    /// Gets or sets the CVE (Common Vulnerabilities and Exposures) information.
    /// </summary>
    public NvdCve? Cve { get; set; }

    /// <summary>
    /// Gets or sets the impact assessment and CVSS scoring information.
    /// </summary>
    public NvdImpact? Impact { get; set; }
}

/// <summary>
/// Represents CVE (Common Vulnerabilities and Exposures) data from NVD.
/// Contains the core vulnerability identification and metadata.
/// </summary>
internal sealed class NvdCve
{
    /// <summary>
    /// Gets or sets the CVE identifier (e.g., "CVE-2023-1234").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the date when the vulnerability was first published.
    /// </summary>
    public DateTime Published { get; set; }

    /// <summary>
    /// Gets or sets the date when the vulnerability information was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the array of vulnerability descriptions in different languages.
    /// </summary>
    public NvdDescription[]? Description { get; set; }

    /// <summary>
    /// Gets or sets the array of reference URLs for additional information.
    /// </summary>
    public NvdReference[]? References { get; set; }
}

/// <summary>
/// Represents a description entry for a vulnerability from NVD.
/// May contain descriptions in multiple languages.
/// </summary>
internal sealed class NvdDescription
{
    /// <summary>
    /// Gets or sets the description text for the vulnerability.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents a reference URL from NVD vulnerability data.
/// Points to additional information about the vulnerability.
/// </summary>
internal sealed class NvdReference
{
    /// <summary>
    /// Gets or sets the reference URL.
    /// </summary>
    public required string Url { get; set; }
}

/// <summary>
/// Represents impact assessment data for a vulnerability from NVD.
/// Contains CVSS v3 scoring information.
/// </summary>
internal sealed class NvdImpact
{
    /// <summary>
    /// Gets or sets the base metric v3 scoring information.
    /// </summary>
    public NvdBaseMetricV3? BaseMetricV3 { get; set; }
}

/// <summary>
/// Represents CVSS v3 base metrics from NVD.
/// Contains the CVSS v3 scoring data.
/// </summary>
internal sealed class NvdBaseMetricV3
{
    /// <summary>
    /// Gets or sets the CVSS v3 scoring information.
    /// </summary>
    public NvdCvssV3? CvssV3 { get; set; }
}

/// <summary>
/// Represents CVSS v3 scoring information from NVD.
/// Contains the numerical base score for the vulnerability.
/// </summary>
internal sealed class NvdCvssV3
{
    /// <summary>
    /// Gets or sets the CVSS v3 base score (0.0 to 10.0).
    /// </summary>
    public double BaseScore { get; set; }
}
