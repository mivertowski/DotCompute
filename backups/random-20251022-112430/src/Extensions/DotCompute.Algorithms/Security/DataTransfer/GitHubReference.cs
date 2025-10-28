#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing a reference link from a GitHub Security Advisory.
/// Contains a URL pointing to additional information about the vulnerability.
/// </summary>
internal sealed class GitHubReference
{
    /// <summary>
    /// Gets or sets the URL for the reference link.
    /// Points to additional information such as security advisories, patches, documentation, or exploit details.
    /// </summary>
    /// <value>The reference URL as a string.</value>
    public required string Url { get; set; }
}