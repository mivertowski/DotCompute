// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading;

/// <summary>
/// Represents NUMA memory allocation policy.
/// </summary>
public sealed class NumaMemoryPolicy
{
    /// <summary>
    /// Gets or sets the preferred NUMA nodes for allocation.
    /// </summary>
    public int[] PreferredNodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the allocation strategy.
    /// </summary>
    public NumaAllocationStrategy Strategy { get; set; } = NumaAllocationStrategy.Preferred;

    /// <summary>
    /// Gets or sets whether to bind memory to specific nodes.
    /// </summary>
    public bool BindToNodes { get; set; }


    /// <summary>
    /// Gets or sets whether to prefer local node allocation.
    /// </summary>
    public bool PreferLocalNode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow remote node access.
    /// </summary>
    public bool AllowRemoteAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether interleaving is enabled.
    /// </summary>
    public bool InterleavingEnabled { get; set; }


    /// <summary>
    /// Creates a default NUMA memory policy.
    /// </summary>
    /// <returns>A new NumaMemoryPolicy instance.</returns>
    public static NumaMemoryPolicy CreateDefault()
    {
        return new NumaMemoryPolicy
        {
            Strategy = NumaAllocationStrategy.Interleaved,
            BindToNodes = false
        };
    }
}

/// <summary>
/// NUMA allocation strategies.
/// </summary>
public enum NumaAllocationStrategy
{
    /// <summary>
    /// Allocate from preferred nodes when possible.
    /// </summary>
    Preferred,

    /// <summary>
    /// Strictly bind to specified nodes.
    /// </summary>
    Bind,

    /// <summary>
    /// Interleave allocation across nodes.
    /// </summary>
    Interleaved,

    /// <summary>
    /// Use local node allocation.
    /// </summary>
    Local
}