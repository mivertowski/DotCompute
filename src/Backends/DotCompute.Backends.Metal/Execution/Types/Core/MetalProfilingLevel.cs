// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Performance profiling levels
/// </summary>
public enum MetalProfilingLevel
{
    /// <summary>
    /// No profiling
    /// </summary>
    None,

    /// <summary>
    /// Basic timing information
    /// </summary>
    Basic,

    /// <summary>
    /// Detailed operation metrics
    /// </summary>
    Detailed,

    /// <summary>
    /// Comprehensive profiling with all metrics
    /// </summary>
    Comprehensive,

    /// <summary>
    /// Debug-level profiling with maximum detail
    /// </summary>
    Debug
}
