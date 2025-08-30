// <copyright file="GraphEnums.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums;

/// <summary>
/// Graph execution state enumeration.
/// </summary>
public enum GraphExecutionState
{
    /// <summary>Graph is not initialized.</summary>
    Uninitialized = 0,
    /// <summary>Graph is ready for execution.</summary>
    Ready = 1,
    /// <summary>Graph is currently executing.</summary>
    Executing = 2,
    /// <summary>Graph execution completed successfully.</summary>
    Completed = 3,
    /// <summary>Graph execution failed.</summary>
    Failed = 4,
    /// <summary>Graph was cancelled.</summary>
    Cancelled = 5
}

/// <summary>
/// Graph node type enumeration.
/// </summary>
public enum GraphNodeType
{
    /// <summary>Kernel execution node.</summary>
    Kernel = 0,
    /// <summary>Memory copy node.</summary>
    MemoryCopy = 1,
    /// <summary>Memory set node.</summary>
    MemorySet = 2,
    /// <summary>Host execution node.</summary>
    Host = 3,
    /// <summary>Graph execution node.</summary>
    Graph = 4,
    /// <summary>Empty node for synchronization.</summary>
    Empty = 5,
    /// <summary>Memory copy node.</summary>
    Memcpy = 6
}

/// <summary>
/// Graph optimization level enumeration.
/// </summary>
public enum GraphOptimizationLevel
{
    /// <summary>No optimization.</summary>
    None = 0,
    /// <summary>Basic optimizations.</summary>
    Basic = 1,
    /// <summary>Balanced optimization and compilation time.</summary>
    Balanced = 2,
    /// <summary>Aggressive optimizations.</summary>
    Aggressive = 3
}