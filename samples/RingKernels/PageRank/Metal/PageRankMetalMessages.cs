// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using MemoryPack;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// Represents a PageRank contribution sent from one node to another (Metal-optimized).
/// </summary>
/// <remarks>
/// <para>
/// Metal-specific optimizations:
/// - 16-byte aligned structure for optimal GPU cache performance
/// - Uses float (32-bit) for rank values (matches Metal precision)
/// - No padding needed (4 + 4 + 4 + 4 = 16 bytes)
/// </para>
/// <para>
/// Memory layout (16 bytes):
/// - SourceNodeId: 4 bytes (int)
/// - TargetNodeId: 4 bytes (int)
/// - Contribution: 4 bytes (float)
/// - Iteration: 4 bytes (int)
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct PageRankContribution
{
    /// <summary>Source node ID.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Target node ID.</summary>
    public int TargetNodeId { get; set; }

    /// <summary>The contribution value (source rank / outbound edges).</summary>
    public float Contribution { get; set; }

    /// <summary>Current iteration number.</summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Represents the current rank of a node, sent for aggregation (Metal-optimized).
/// </summary>
/// <remarks>
/// <para>
/// Metal-specific optimizations:
/// - 16-byte aligned structure
/// - Optimized for unified memory access patterns
/// </para>
/// <para>
/// Memory layout (16 bytes):
/// - NodeId: 4 bytes (int)
/// - Rank: 4 bytes (float)
/// - OutboundEdgeCount: 4 bytes (int)
/// - Iteration: 4 bytes (int)
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct NodeRankUpdate
{
    /// <summary>The node ID.</summary>
    public int NodeId { get; set; }

    /// <summary>The current rank value.</summary>
    public float Rank { get; set; }

    /// <summary>Number of outbound edges from this node.</summary>
    public int OutboundEdgeCount { get; set; }

    /// <summary>Current iteration.</summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Represents aggregated rank result after an iteration (Metal-optimized).
/// </summary>
/// <remarks>
/// <para>
/// Metal-specific optimizations:
/// - 16-byte aligned structure
/// - float precision matches Metal GPU capabilities
/// </para>
/// <para>
/// Memory layout (16 bytes):
/// - NodeId: 4 bytes (int)
/// - NewRank: 4 bytes (float)
/// - Delta: 4 bytes (float)
/// - Iteration: 4 bytes (int)
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct RankAggregationResult
{
    /// <summary>The node ID.</summary>
    public int NodeId { get; set; }

    /// <summary>The new computed rank.</summary>
    public float NewRank { get; set; }

    /// <summary>The change from previous rank (for convergence check).</summary>
    public float Delta { get; set; }

    /// <summary>Iteration number.</summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Command to start a new PageRank iteration (Metal-optimized).
/// </summary>
/// <remarks>
/// <para>
/// Metal-specific optimizations:
/// - 16-byte aligned structure
/// - Broadcast to all kernels via unified memory
/// </para>
/// <para>
/// Memory layout (16 bytes):
/// - Iteration: 4 bytes (int)
/// - TotalNodes: 4 bytes (int)
/// - DampingFactor: 4 bytes (float)
/// - _padding: 4 bytes (reserved)
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct StartIterationCommand
{
    /// <summary>Iteration number to start.</summary>
    public int Iteration { get; set; }

    /// <summary>Total number of nodes in the graph.</summary>
    public int TotalNodes { get; set; }

    /// <summary>Damping factor (typically 0.85).</summary>
    public float DampingFactor { get; set; }

    /// <summary>Reserved for alignment.</summary>
    private int _padding;
}

/// <summary>
/// Convergence check result from ConvergenceChecker kernel (Metal-specific).
/// </summary>
/// <remarks>
/// <para>
/// Used to signal when PageRank has converged and iterations can stop.
/// </para>
/// <para>
/// Memory layout (16 bytes):
/// - Iteration: 4 bytes (int)
/// - MaxDelta: 4 bytes (float)
/// - HasConverged: 4 bytes (int, 0=false, 1=true)
/// - NodesProcessed: 4 bytes (int)
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct ConvergenceCheckResult
{
    /// <summary>The iteration that was checked.</summary>
    public int Iteration { get; set; }

    /// <summary>The maximum delta observed across all nodes.</summary>
    public float MaxDelta { get; set; }

    /// <summary>Whether convergence threshold was met (0=false, 1=true).</summary>
    public int HasConverged { get; set; }

    /// <summary>Number of nodes that were processed.</summary>
    public int NodesProcessed { get; set; }
}

/// <summary>
/// Metal-specific graph edge information with fixed-size edge array.
/// </summary>
/// <remarks>
/// <para>
/// For Metal implementation, we use a fixed maximum number of edges per node.
/// For graphs with more edges, use separate edge buffer approach.
/// </para>
/// <para>
/// Max edges per node: 32 (typical for web graphs)
/// Memory layout: Variable size based on EdgeCount
/// </para>
/// </remarks>
[MemoryPackable]
public partial struct MetalGraphNode
{
    /// <summary>Node ID.</summary>
    public int NodeId { get; set; }

    /// <summary>Current rank value.</summary>
    public float CurrentRank { get; set; }

    /// <summary>Number of outbound edges (≤ 32).</summary>
    public int EdgeCount { get; set; }

    /// <summary>Reserved for alignment.</summary>
    private int _padding;

    /// <summary>
    /// Outbound edge target node IDs (fixed array of 32).
    /// Only first EdgeCount elements are valid.
    /// </summary>
    [MemoryPackInclude]
    public int[] TargetNodeIds { get; set; }

    /// <summary>
    /// Creates a node with a fixed-size edge array.
    /// </summary>
    public static MetalGraphNode Create(int nodeId, float initialRank, int[] edges)
    {
        var node = new MetalGraphNode
        {
            NodeId = nodeId,
            CurrentRank = initialRank,
            EdgeCount = Math.Min(edges.Length, 32),
            TargetNodeIds = new int[32]
        };

        Array.Copy(edges, node.TargetNodeIds, node.EdgeCount);
        return node;
    }
}
