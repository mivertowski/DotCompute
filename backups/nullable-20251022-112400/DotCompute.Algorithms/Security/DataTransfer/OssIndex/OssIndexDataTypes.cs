// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer.OssIndex;

/// <summary>
/// Represents a component response from the Sonatype OSS Index API.
/// Contains vulnerability information for a specific package component.
/// </summary>
internal sealed class OssIndexComponent
{
    /// <summary>
    /// Gets or sets the array of vulnerabilities associated with this component.
    /// </summary>
    public OssIndexVulnerability[]? Vulnerabilities { get; set; }
}

/// <summary>
/// Represents a vulnerability entry from the Sonatype OSS Index API.
/// Contains detailed information about a specific security vulnerability.
/// </summary>
internal sealed class OssIndexVulnerability
{
    /// <summary>
    /// Gets or sets the unique identifier for this vulnerability in the OSS Index.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the CVE identifier if the vulnerability has one assigned.
    /// </summary>
    public string? Cve { get; set; }

    /// <summary>
    /// Gets or sets the title or brief summary of the vulnerability.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the detailed description of the vulnerability.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the CVSS (Common Vulnerability Scoring System) score.
    /// Range: 0.0 to 10.0, where 10.0 represents the most severe vulnerabilities.
    /// </summary>
    public double CvssScore { get; set; }

    /// <summary>
    /// Gets or sets the date when the vulnerability was published.
    /// </summary>
    public string? PublishedDate { get; set; }

    /// <summary>
    /// Gets or sets the reference URL for additional information about the vulnerability.
    /// </summary>
    public required string Reference { get; set; }
}