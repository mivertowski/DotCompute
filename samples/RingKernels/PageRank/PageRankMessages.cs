// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using MemoryPack;

namespace DotCompute.Samples.RingKernels.PageRank;

/// <summary>
/// Represents a PageRank contribution sent from one node to another.
/// </summary>
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
/// Represents the current rank of a node, sent for aggregation.
/// </summary>
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
/// Represents aggregated rank result after an iteration.
/// </summary>
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
/// Command to start a new PageRank iteration.
/// </summary>
[MemoryPackable]
public partial struct StartIterationCommand
{
    /// <summary>Iteration number to start.</summary>
    public int Iteration { get; set; }

    /// <summary>Total number of nodes in the graph.</summary>
    public int TotalNodes { get; set; }

    /// <summary>Damping factor (typically 0.85).</summary>
    public float DampingFactor { get; set; }
}

/// <summary>
/// Represents graph edge information for a node.
/// </summary>
[MemoryPackable]
public partial struct GraphEdgeInfo
{
    /// <summary>Source node ID.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Array of target node IDs (outbound edges).</summary>
    public int[] TargetNodeIds { get; set; }

    /// <summary>Current rank of the source node.</summary>
    public float CurrentRank { get; set; }
}
