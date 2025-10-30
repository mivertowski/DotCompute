
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing a component response from the Sonatype OSS Index API.
/// Contains vulnerability information for a specific software component.
/// </summary>
internal sealed class OssIndexComponent
{
    /// <summary>
    /// Gets or sets the array of vulnerabilities associated with this component.
    /// Contains all known security issues for the queried component.
    /// </summary>
    /// <value>An array of vulnerability objects, or null if no vulnerabilities were found.</value>
    public OssIndexVulnerability[]? Vulnerabilities { get; set; }
}
