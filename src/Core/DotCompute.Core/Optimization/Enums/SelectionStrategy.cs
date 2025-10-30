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
    /// <summary>Historical performance data-based selection strategy.</summary>
    Historical,
    /// <summary>Real-time system performance-based selection strategy.</summary>
    RealTime,
    /// <summary>Workload characteristics-based selection strategy.</summary>
    Characteristics,
    /// <summary>Backend priority order-based selection strategy.</summary>
    Priority,
    /// <summary>Fallback selection strategy when other strategies fail.</summary>
    Fallback,
    /// <summary>Only option selection strategy when single backend is available.</summary>
    OnlyOption
}
