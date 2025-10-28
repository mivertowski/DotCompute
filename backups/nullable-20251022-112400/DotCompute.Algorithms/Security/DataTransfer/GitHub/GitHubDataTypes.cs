// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer.GitHub;

/// <summary>
/// Represents a GitHub Security Advisory from the GitHub API.
/// Contains comprehensive information about security vulnerabilities tracked by GitHub.
/// </summary>
internal sealed class GitHubAdvisory
{
    /// <summary>
    /// Gets or sets the GitHub Security Advisory identifier (e.g., "GHSA-xxxx-xxxx-xxxx").
    /// </summary>
    public required string GhsaId { get; set; }

    /// <summary>
    /// Gets or sets the CVE identifier if the advisory has one assigned.
    /// </summary>
    public string? CveId { get; set; }

    /// <summary>
    /// Gets or sets the brief summary of the security advisory.
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// Gets or sets the detailed description of the security vulnerability.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the severity level as reported by GitHub.
    /// Values include: "low", "moderate", "high", "critical"
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Gets or sets the date when the advisory was first published.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the date when the advisory was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the version range affected by this vulnerability.
    /// Specifies which versions of the package are vulnerable.
    /// </summary>
    public required string VulnerableVersionRange { get; set; }

    /// <summary>
    /// Gets or sets the array of reference URLs for additional information.
    /// </summary>
    public GitHubReference[]? References { get; set; }
}

/// <summary>
/// Represents a reference URL from a GitHub Security Advisory.
/// Points to additional information, patches, or documentation.
/// </summary>
internal sealed class GitHubReference
{
    /// <summary>
    /// Gets or sets the reference URL.
    /// </summary>
    public required string Url { get; set; }
}