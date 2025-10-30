// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Selection;

/// <summary>
/// Constraints for backend selection.
/// </summary>
public class SelectionConstraints
{

    /// <summary>Backends that are explicitly allowed</summary>
    /// <remarks>CA2227: Acceptable for constraint configuration objects that support fluent initialization.</remarks>
#pragma warning disable CA2227 // Collection properties should be read only
    public HashSet<string>? AllowedBackends { get; set; }

    /// <summary>Backends that are explicitly disallowed</summary>
    /// <remarks>CA2227: Acceptable for constraint configuration objects that support fluent initialization.</remarks>
    public HashSet<string>? DisallowedBackends { get; set; }
#pragma warning restore CA2227

    /// <summary>Maximum acceptable execution time in milliseconds</summary>
    public double? MaxExecutionTimeMs { get; set; }

    /// <summary>Maximum acceptable memory usage in MB</summary>
    public long? MaxMemoryUsageMB { get; set; }

    /// <summary>Minimum required confidence score</summary>
    public float? MinConfidenceScore { get; set; }

    /// <summary>Custom constraint predicates</summary>
    public ICollection<Func<string, bool>> CustomConstraints { get; } = [];

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
