// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Represents a suspicious code pattern found during analysis.
/// </summary>
internal class SuspiciousCodePattern
{
    public required string Pattern { get; set; }
    public SeverityLevel Severity { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
}