// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Resource utilization patterns
/// </summary>
public enum MetalResourcePattern
{
    /// <summary>
    /// Single-use resources
    /// </summary>
    SingleUse,

    /// <summary>
    /// Resources reused across multiple operations
    /// </summary>
    Reusable,

    /// <summary>
    /// Long-lived resources
    /// </summary>
    Persistent,

    /// <summary>
    /// Temporary resources with short lifetime
    /// </summary>
    Temporary,

    /// <summary>
    /// Streaming resources for data flow
    /// </summary>
    Streaming
}