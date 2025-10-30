// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Represents a suspicious code pattern found during analysis.
/// </summary>
internal class SuspiciousCodePattern
{
    /// <summary>
    /// Gets or sets the pattern.
    /// </summary>
    /// <value>The pattern.</value>
    public required string Pattern { get; set; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public SeverityLevel Severity { get; set; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public required string Description { get; set; }
    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    /// <value>The location.</value>
    public required string Location { get; set; }
}
