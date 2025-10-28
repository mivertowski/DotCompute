#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing a security advisory from the GitHub Security Advisory Database.
/// Contains comprehensive vulnerability information specific to GitHub's advisory format.
/// </summary>
internal sealed class GitHubAdvisory
{
    /// <summary>
    /// Gets or sets the GitHub Security Advisory (GHSA) identifier.
    /// The unique identifier assigned by GitHub for this security advisory.
    /// </summary>
    /// <value>The GHSA identifier as a string.</value>
    public required string GhsaId { get; set; }

    /// <summary>
    /// Gets or sets the Common Vulnerabilities and Exposures (CVE) identifier if applicable.
    /// The standardized CVE identifier associated with this advisory, if available.
    /// </summary>
    /// <value>The CVE identifier as a string, or null if not available.</value>
    public string? CveId { get; set; }

    /// <summary>
    /// Gets or sets the summary title of the security advisory.
    /// A brief description of the vulnerability or security issue.
    /// </summary>
    /// <value>The advisory summary as a string.</value>
    public required string Summary { get; set; }

    /// <summary>
    /// Gets or sets the detailed description of the security advisory.
    /// A comprehensive explanation of the vulnerability, its impact, and potential exploitation methods.
    /// </summary>
    /// <value>The advisory description as a string, or null if not available.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the vulnerability.
    /// GitHub's classification of the security issue severity (e.g., "critical", "high", "medium", "low").
    /// </summary>
    /// <value>The severity level as a string.</value>
    public required string Severity { get; set; }

    /// <summary>
    /// Gets or sets the date when the advisory was first published.
    /// Indicates when the security issue became publicly known through GitHub.
    /// </summary>
    /// <value>The published date as a DateTime.</value>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the date when the advisory was last updated.
    /// Indicates the freshness of the advisory information.
    /// </summary>
    /// <value>The updated date as a DateTime.</value>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the version range affected by this vulnerability.
    /// Specifies which versions of the package are vulnerable to this security issue.
    /// </summary>
    /// <value>The vulnerable version range as a string.</value>
    public required string VulnerableVersionRange { get; set; }

    /// <summary>
    /// Gets or sets the array of reference links for additional information.
    /// Contains URLs pointing to patches, documentation, or other relevant resources.
    /// </summary>
    /// <value>An array of reference objects, or null if no references are available.</value>
    public GitHubReference[]? References { get; set; }
}