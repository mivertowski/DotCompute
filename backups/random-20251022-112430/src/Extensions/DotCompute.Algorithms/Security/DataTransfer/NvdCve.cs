#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing CVE information from the National Vulnerability Database.
/// Contains the core vulnerability identification and descriptive information.
/// </summary>
internal sealed class NvdCve
{
    /// <summary>
    /// Gets or sets the CVE identifier for the vulnerability.
    /// The standardized identifier used across vulnerability databases (e.g., "CVE-2024-12345").
    /// </summary>
    /// <value>The CVE identifier as a string, or null if not available.</value>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the date when the vulnerability was first published.
    /// Indicates when the security issue became publicly known.
    /// </summary>
    /// <value>The published date as a DateTime.</value>
    public DateTime Published { get; set; }

    /// <summary>
    /// Gets or sets the date when the vulnerability information was last modified.
    /// Indicates the freshness of the vulnerability data.
    /// </summary>
    /// <value>The last modified date as a DateTime.</value>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the array of description objects for the vulnerability.
    /// Contains detailed explanations of the security issue in potentially multiple languages.
    /// </summary>
    /// <value>An array of description objects, or null if no descriptions are available.</value>
    public NvdDescription[]? Description { get; set; }

    /// <summary>
    /// Gets or sets the array of reference objects for the vulnerability.
    /// Contains links to additional information, patches, advisories, or related resources.
    /// </summary>
    /// <value>An array of reference objects, or null if no references are available.</value>
    public NvdReference[]? References { get; set; }
}