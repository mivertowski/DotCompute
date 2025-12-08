// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using MemoryPack;

namespace DotCompute.Samples.RingKernels.Tests.PageRank;

/// <summary>
/// Represents a PageRank contribution sent from one node to another.
/// </summary>
[MemoryPackable]
public partial struct PageRankContribution : IEquatable<PageRankContribution>
{
    /// <summary>Source node ID.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Target node ID.</summary>
    public int TargetNodeId { get; set; }

    /// <summary>The contribution value (source rank / outbound edges).</summary>
    public float Contribution { get; set; }

    /// <summary>Current iteration number.</summary>
    public int Iteration { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(PageRankContribution other)
    {
        return SourceNodeId == other.SourceNodeId &&
               TargetNodeId == other.TargetNodeId &&
               Contribution.Equals(other.Contribution) &&
               Iteration == other.Iteration;
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is PageRankContribution other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(SourceNodeId, TargetNodeId, Contribution, Iteration);

    /// <summary>Determines whether two instances are equal.</summary>
    public static bool operator ==(PageRankContribution left, PageRankContribution right) => left.Equals(right);

    /// <summary>Determines whether two instances are not equal.</summary>
    public static bool operator !=(PageRankContribution left, PageRankContribution right) => !left.Equals(right);
}

/// <summary>
/// Represents the current rank of a node, sent for aggregation.
/// </summary>
[MemoryPackable]
public partial struct NodeRankUpdate : IEquatable<NodeRankUpdate>
{
    /// <summary>The node ID.</summary>
    public int NodeId { get; set; }

    /// <summary>The current rank value.</summary>
    public float Rank { get; set; }

    /// <summary>Number of outbound edges from this node.</summary>
    public int OutboundEdgeCount { get; set; }

    /// <summary>Current iteration.</summary>
    public int Iteration { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(NodeRankUpdate other)
    {
        return NodeId == other.NodeId &&
               Rank.Equals(other.Rank) &&
               OutboundEdgeCount == other.OutboundEdgeCount &&
               Iteration == other.Iteration;
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is NodeRankUpdate other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(NodeId, Rank, OutboundEdgeCount, Iteration);

    /// <summary>Determines whether two instances are equal.</summary>
    public static bool operator ==(NodeRankUpdate left, NodeRankUpdate right) => left.Equals(right);

    /// <summary>Determines whether two instances are not equal.</summary>
    public static bool operator !=(NodeRankUpdate left, NodeRankUpdate right) => !left.Equals(right);
}

/// <summary>
/// Represents aggregated rank result after an iteration.
/// </summary>
[MemoryPackable]
public partial struct RankAggregationResult : IEquatable<RankAggregationResult>
{
    /// <summary>The node ID.</summary>
    public int NodeId { get; set; }

    /// <summary>The new computed rank.</summary>
    public float NewRank { get; set; }

    /// <summary>The change from previous rank (for convergence check).</summary>
    public float Delta { get; set; }

    /// <summary>Iteration number.</summary>
    public int Iteration { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(RankAggregationResult other)
    {
        return NodeId == other.NodeId &&
               NewRank.Equals(other.NewRank) &&
               Delta.Equals(other.Delta) &&
               Iteration == other.Iteration;
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is RankAggregationResult other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(NodeId, NewRank, Delta, Iteration);

    /// <summary>Determines whether two instances are equal.</summary>
    public static bool operator ==(RankAggregationResult left, RankAggregationResult right) => left.Equals(right);

    /// <summary>Determines whether two instances are not equal.</summary>
    public static bool operator !=(RankAggregationResult left, RankAggregationResult right) => !left.Equals(right);
}

/// <summary>
/// Command to start a new PageRank iteration.
/// </summary>
[MemoryPackable]
public partial struct StartIterationCommand : IEquatable<StartIterationCommand>
{
    /// <summary>Iteration number to start.</summary>
    public int Iteration { get; set; }

    /// <summary>Total number of nodes in the graph.</summary>
    public int TotalNodes { get; set; }

    /// <summary>Damping factor (typically 0.85).</summary>
    public float DampingFactor { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(StartIterationCommand other)
    {
        return Iteration == other.Iteration &&
               TotalNodes == other.TotalNodes &&
               DampingFactor.Equals(other.DampingFactor);
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is StartIterationCommand other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Iteration, TotalNodes, DampingFactor);

    /// <summary>Determines whether two instances are equal.</summary>
    public static bool operator ==(StartIterationCommand left, StartIterationCommand right) => left.Equals(right);

    /// <summary>Determines whether two instances are not equal.</summary>
    public static bool operator !=(StartIterationCommand left, StartIterationCommand right) => !left.Equals(right);
}

/// <summary>
/// Represents graph edge information for a node.
/// </summary>
[MemoryPackable]
public partial struct GraphEdgeInfo : IEquatable<GraphEdgeInfo>
{
    /// <summary>Source node ID.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Array of target node IDs (outbound edges).</summary>
    public int[] TargetNodeIds { get; set; }

    /// <summary>Current rank of the source node.</summary>
    public float CurrentRank { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(GraphEdgeInfo other)
    {
        return SourceNodeId == other.SourceNodeId &&
               CurrentRank.Equals(other.CurrentRank) &&
               ((TargetNodeIds is null && other.TargetNodeIds is null) ||
                (TargetNodeIds is not null && other.TargetNodeIds is not null &&
                 TargetNodeIds.AsSpan().SequenceEqual(other.TargetNodeIds)));
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is GraphEdgeInfo other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SourceNodeId);
        hash.Add(CurrentRank);
        if (TargetNodeIds is not null)
        {
            foreach (var id in TargetNodeIds)
            {
                hash.Add(id);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two instances are equal.</summary>
    public static bool operator ==(GraphEdgeInfo left, GraphEdgeInfo right) => left.Equals(right);

    /// <summary>Determines whether two instances are not equal.</summary>
    public static bool operator !=(GraphEdgeInfo left, GraphEdgeInfo right) => !left.Equals(right);
}
