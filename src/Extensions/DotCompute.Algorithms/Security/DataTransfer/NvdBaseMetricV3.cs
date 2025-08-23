// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing CVSS version 3 base metrics from the National Vulnerability Database.
/// Contains the core CVSS v3 scoring information for vulnerability impact assessment.
/// </summary>
internal sealed class NvdBaseMetricV3
{
    /// <summary>
    /// Gets or sets the CVSS version 3 vector and score information.
    /// Contains the detailed CVSS v3 scoring data including base score and impact metrics.
    /// </summary>
    /// <value>The CVSS v3 information object, or null if not available.</value>
    public NvdCvssV3? CvssV3 { get; set; }
}