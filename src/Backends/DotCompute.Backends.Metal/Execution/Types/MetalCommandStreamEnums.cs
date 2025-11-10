// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types;

/// <summary>
/// Flags controlling Metal command stream behavior.
/// </summary>
public enum MetalStreamFlags
{
    /// <summary>Default stream behavior.</summary>
    Default,

    /// <summary>Concurrent execution allowing overlap with other streams.</summary>
    Concurrent,

    /// <summary>Serial execution preventing overlap with other operations.</summary>
    Serial
}

/// <summary>
/// Priority levels for Metal command stream scheduling.
/// </summary>
/// <remarks>
/// Higher priority streams may preempt lower priority streams on capable hardware.
/// </remarks>
public enum MetalStreamPriority
{
    /// <summary>Low priority background work.</summary>
    Low,

    /// <summary>Normal priority for typical operations.</summary>
    Normal,

    /// <summary>High priority for latency-sensitive operations.</summary>
    High
}
