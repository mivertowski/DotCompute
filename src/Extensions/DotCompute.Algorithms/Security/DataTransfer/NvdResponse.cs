// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing the root response from the National Vulnerability Database (NVD) API.
/// Used for deserializing JSON responses from NVD vulnerability queries.
/// </summary>
internal sealed class NvdResponse
{
    /// <summary>
    /// Gets or sets the array of vulnerabilities returned by the NVD API.
    /// Contains the vulnerability data matching the search criteria.
    /// </summary>
    /// <value>An array of NVD vulnerability objects, or null if no vulnerabilities were returned.</value>
    public NvdVulnerability[]? Vulnerabilities { get; set; }
}