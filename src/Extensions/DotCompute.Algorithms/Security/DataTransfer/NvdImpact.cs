
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing impact assessment information from the National Vulnerability Database.
/// Contains CVSS scoring and impact metrics for vulnerability severity classification.
/// </summary>
internal sealed class NvdImpact
{
    /// <summary>
    /// Gets or sets the CVSS version 3 base metrics for the vulnerability.
    /// Contains the standardized vulnerability scoring information used for severity assessment.
    /// </summary>
    /// <value>The CVSS v3 base metric information, or null if not available.</value>
    public NvdBaseMetricV3? BaseMetricV3 { get; set; }
}