
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Enums;

/// <summary>
/// Represents the health status of a plugin.
/// </summary>
public enum PluginHealth
{
    /// <summary>
    /// Plugin health status is unknown or not yet determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Plugin is operating normally with no issues.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Plugin is experiencing minor issues but is still functional.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Plugin is experiencing serious issues and may not function properly.
    /// </summary>
    Critical = 3
}