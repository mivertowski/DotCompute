#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing CVSS version 3 scoring information from the National Vulnerability Database.
/// Contains the base score used for vulnerability severity classification.
/// </summary>
internal sealed class NvdCvssV3
{
    /// <summary>
    /// Gets or sets the CVSS v3 base score for the vulnerability.
    /// A numeric score from 0.0 to 10.0 indicating the severity of the vulnerability,
    /// where higher scores represent more severe security issues.
    /// </summary>
    /// <value>The base score as a double between 0.0 and 10.0.</value>
    public double BaseScore { get; set; }
}