// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Core.Optimization.Selection;

/// <summary>
/// Constraints for backend selection.
/// </summary>
public class SelectionConstraints
{
    /// <summary>Backends that are explicitly allowed</summary>
    public HashSet<string>? AllowedBackends { get; set; }

    /// <summary>Backends that are explicitly disallowed</summary>
    public HashSet<string>? DisallowedBackends { get; set; }

    /// <summary>Maximum acceptable execution time in milliseconds</summary>
    public double? MaxExecutionTimeMs { get; set; }

    /// <summary>Maximum acceptable memory usage in MB</summary>
    public long? MaxMemoryUsageMB { get; set; }

    /// <summary>Minimum required confidence score</summary>
    public float? MinConfidenceScore { get; set; }

    /// <summary>Custom constraint predicates</summary>
    public List<Func<string, bool>> CustomConstraints { get; set; } = new();

    /// <summary>
    /// Checks if a backend is allowed based on all constraints.
    /// </summary>
    public bool IsBackendAllowed(string backendId)
    {
        if (DisallowedBackends?.Contains(backendId) == true)
        {
            return false;
        }

        if (AllowedBackends != null && !AllowedBackends.Contains(backendId))
        {
            return false;
        }

        return CustomConstraints.All(constraint => constraint(backendId));
    }
}