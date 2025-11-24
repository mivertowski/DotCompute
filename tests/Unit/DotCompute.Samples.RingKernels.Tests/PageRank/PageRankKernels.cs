// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Samples.RingKernels.Tests.PageRank;

/// <summary>
/// Ring kernels for distributed PageRank computation.
///
/// Architecture:
/// - ContributionSender: Receives node rank updates, sends contributions to neighbors
/// - RankAggregator: Collects contributions for each node, computes new ranks
/// - ConvergenceChecker: Monitors rank changes to detect convergence
///
/// Message flow:
/// 1. StartIterationCommand → ContributionSender
/// 2. ContributionSender → PageRankContribution → RankAggregator
/// 3. RankAggregator → RankAggregationResult → ConvergenceChecker
/// 4. ConvergenceChecker → NodeRankUpdate → ContributionSender (next iteration)
/// </summary>
public static class PageRankKernels
{
    /// <summary>
    /// Damping factor for PageRank (typically 0.85).
    /// </summary>
    public const float DampingFactor = 0.85f;

    /// <summary>
    /// Convergence threshold - stop when all rank changes are below this.
    /// </summary>
    public const float ConvergenceThreshold = 0.0001f;

    /// <summary>
    /// Kernel that sends PageRank contributions to neighbor nodes.
    ///
    /// For each node, divides current rank by outbound edge count
    /// and sends contributions to all target nodes.
    /// </summary>
    [RingKernel(
        KernelId = "pagerank_contribution_sender",
        Capacity = 4096,
        InputQueueSize = 1024,
        OutputQueueSize = 2048,
        MaxInputMessageSizeBytes = 1024,
        MaxOutputMessageSizeBytes = 256,
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 64,
        EnableTimestamps = true,
        PublishesToKernels = new[] { "pagerank_rank_aggregator" })]
    public static void ContributionSenderKernel(RingKernelContext ctx, GraphEdgeInfo nodeInfo)
    {
        // Synchronize before processing
        ctx.SyncThreads();

        // Calculate contribution per outbound edge
        int edgeCount = nodeInfo.TargetNodeIds?.Length ?? 0;
        if (edgeCount == 0)
        {
            return; // Dangling node - no outbound edges
        }

        float contribution = nodeInfo.CurrentRank / edgeCount;

        // Send contribution to each neighbor
        for (int i = 0; i < edgeCount; i++)
        {
            var targetId = nodeInfo.TargetNodeIds![i];
            var msg = new PageRankContribution
            {
                SourceNodeId = nodeInfo.SourceNodeId,
                TargetNodeId = targetId,
                Contribution = contribution,
                Iteration = 0 // Will be set by coordinator
            };

            // Use K2K messaging to send to aggregator
            ctx.SendToKernel("pagerank_rank_aggregator", msg);
        }
    }

    /// <summary>
    /// Kernel that aggregates contributions and computes new ranks.
    ///
    /// Receives contributions from multiple nodes, sums them up,
    /// and applies the PageRank formula: (1-d)/N + d * sum(contributions)
    /// </summary>
    [RingKernel(
        KernelId = "pagerank_rank_aggregator",
        Capacity = 8192,
        InputQueueSize = 4096,
        OutputQueueSize = 1024,
        MaxInputMessageSizeBytes = 256,
        MaxOutputMessageSizeBytes = 256,
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 128,
        EnableTimestamps = true,
        SubscribesToKernels = new[] { "pagerank_contribution_sender" },
        PublishesToKernels = new[] { "pagerank_convergence_checker" },
        UseBarriers = true,
        BarrierScope = BarrierScope.ThreadBlock)]
    public static void RankAggregatorKernel(RingKernelContext ctx, PageRankContribution contribution)
    {
        // This kernel aggregates contributions
        // In a real implementation, we'd use shared memory to accumulate
        // contributions for each node before computing the new rank

        // Memory fence to ensure visibility
        ctx.ThreadFence();

        // For now, we'll output each contribution for verification
        // A production implementation would batch these
        var result = new RankAggregationResult
        {
            NodeId = contribution.TargetNodeId,
            NewRank = contribution.Contribution, // Partial - needs aggregation
            Delta = 0, // Computed after full aggregation
            Iteration = contribution.Iteration
        };

        ctx.SendToKernel("pagerank_convergence_checker", result);
    }

    /// <summary>
    /// Kernel that checks for convergence and triggers next iteration.
    ///
    /// Collects rank updates, computes total delta, and decides
    /// whether to continue iterations or signal completion.
    /// </summary>
    [RingKernel(
        KernelId = "pagerank_convergence_checker",
        Capacity = 2048,
        InputQueueSize = 1024,
        OutputQueueSize = 256,
        MaxInputMessageSizeBytes = 256,
        MaxOutputMessageSizeBytes = 512,
        ProcessingMode = RingProcessingMode.Adaptive,
        EnableTimestamps = true,
        SubscribesToKernels = new[] { "pagerank_rank_aggregator" },
        MemoryConsistency = MemoryConsistencyModel.Sequential,
        EnableCausalOrdering = true)]
    public static void ConvergenceCheckerKernel(RingKernelContext ctx, RankAggregationResult result)
    {
        // Check if this node's rank change is significant
        float absDelta = result.Delta >= 0 ? result.Delta : -result.Delta;

        if (absDelta > ConvergenceThreshold)
        {
            // Node hasn't converged yet - output for next iteration
            var update = new NodeRankUpdate
            {
                NodeId = result.NodeId,
                Rank = result.NewRank,
                OutboundEdgeCount = 0, // Will be filled by coordinator
                Iteration = result.Iteration + 1
            };

            ctx.EnqueueOutput(update);
        }
    }
}
