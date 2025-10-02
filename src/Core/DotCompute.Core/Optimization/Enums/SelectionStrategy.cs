// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Enums;
/// <summary>
/// An selection strategy enumeration.
/// </summary>

/// <summary>
/// Strategies used for backend selection.
/// </summary>
public enum SelectionStrategy
{
    Historical,      // Based on historical performance data
    RealTime,        // Based on current system performance
    Characteristics, // Based on workload characteristics
    Priority,        // Based on backend priority order
    Fallback,        // Fallback when other strategies fail
    OnlyOption       // Only one backend available
}