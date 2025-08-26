// <copyright file="GraphNodes.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Execution.Graph.Enums;

namespace DotCompute.Backends.CUDA.Execution.Graph.Nodes;

/// <summary>
/// Base class for all graph nodes.
/// </summary>
public abstract class GraphNode
{
    /// <summary>
    /// Gets the unique identifier for this node.
    /// </summary>
    public Guid NodeId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the type of this graph node.
    /// </summary>
    public abstract GraphNodeType NodeType { get; }

    /// <summary>
    /// Gets or sets the list of dependency nodes.
    /// </summary>
    public List<GraphNode> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets user-defined data associated with this node.
    /// </summary>
    public object? UserData { get; set; }
}

/// <summary>
/// Represents a kernel execution node in a CUDA graph.
/// </summary>
public sealed class KernelNode : GraphNode
{
    /// <inheritdoc/>
    public override GraphNodeType NodeType => GraphNodeType.Kernel;

    /// <summary>
    /// Gets or sets the kernel function name.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets the grid dimensions.
    /// </summary>
    public (int X, int Y, int Z) GridDim { get; set; } = (1, 1, 1);

    /// <summary>
    /// Gets or sets the block dimensions.
    /// </summary>
    public (int X, int Y, int Z) BlockDim { get; set; } = (1, 1, 1);

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public uint SharedMemSize { get; set; }

    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public object[]? Parameters { get; set; }
}

/// <summary>
/// Represents a memory copy node in a CUDA graph.
/// </summary>
public sealed class MemoryCopyNode : GraphNode
{
    /// <inheritdoc/>
    public override GraphNodeType NodeType => GraphNodeType.MemoryCopy;

    /// <summary>
    /// Gets or sets the source pointer.
    /// </summary>
    public IntPtr Source { get; set; }

    /// <summary>
    /// Gets or sets the destination pointer.
    /// </summary>
    public IntPtr Destination { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes to copy.
    /// </summary>
    public nuint ByteCount { get; set; }
}