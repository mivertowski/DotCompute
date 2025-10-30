// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Optimization.Enums;

namespace DotCompute.Core.Optimization.Selection;

/// <summary>
/// Result of backend selection process.
/// </summary>
public class BackendSelection
{
    /// <summary>Selected backend accelerator</summary>
    public IAccelerator? SelectedBackend { get; set; }

    /// <summary>Backend identifier</summary>
    public string BackendId { get; set; } = string.Empty;

    /// <summary>Confidence in the selection from 0.0 to 1.0</summary>
    public float ConfidenceScore { get; set; }

    /// <summary>Human-readable reason for the selection</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Strategy used for selection</summary>
    public SelectionStrategy SelectionStrategy { get; set; }

    /// <summary>Additional metadata about the selection</summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
