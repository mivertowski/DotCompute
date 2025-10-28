// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Represents the overall health status of the Metal execution environment
/// </summary>
public enum MetalExecutionHealth
{
    /// <summary>
    /// All systems operating normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Minor issues that don't affect core functionality
    /// </summary>
    Warning,

    /// <summary>
    /// Significant issues affecting performance
    /// </summary>
    Degraded,

    /// <summary>
    /// Critical failures preventing normal operation
    /// </summary>
    Critical,

    /// <summary>
    /// System is completely unavailable
    /// </summary>
    Offline
}