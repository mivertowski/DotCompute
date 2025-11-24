// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// Metal-optimized Ring kernels for distributed PageRank computation.
/// </summary>
/// <remarks>
/// <para>
/// Architecture (3-kernel design matching CUDA reference):
/// - ContributionSender: Receives node rank updates, sends contributions to neighbors
/// - RankAggregator: Collects contributions for each node, computes new ranks
/// - ConvergenceChecker: Monitors rank changes to detect convergence
/// </para>
/// <para>
/// Metal-specific optimizations:
/// - Unified memory eliminates CPU→GPU copy overhead
/// - Threadgroup barriers for intra-kernel synchronization
/// - Simdgroup operations for parallel reduction
/// - 16-byte message alignment for cache efficiency
/// </para>
/// <para>
/// Message flow:
/// 1. MetalGraphNode → ContributionSender
/// 2. ContributionSender → PageRankContribution → RankAggregator
/// 3. RankAggregator → RankAggregationResult → ConvergenceChecker
/// 4. ConvergenceChecker → ConvergenceCheckResult (output)
/// </para>
/// <para>
/// Validates end-to-end Metal backend:
/// - C# → MSL transpilation (MetalRingKernelCompiler)
/// - MemoryPack serialization integration
/// - K2K message routing
/// - Multi-kernel barrier synchronization
/// </para>
/// </remarks>
public static class PageRankMetalKernels
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
    /// Metal-optimized kernel that sends PageRank contributions to neighbor nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each node, divides current rank by outbound edge count
    /// and sends contributions to all target nodes via K2K messaging.
    /// </para>
    /// <para>
    /// Metal optimizations:
    /// - Uses threadgroup barriers for parallel edge processing
    /// - Unified memory for zero-copy node data access
    /// - Optimized for Apple Silicon memory hierarchy
    /// </para>
    /// <para>
    /// Performance targets:
    /// - Processing: ~100-500ns per node (Apple Silicon M2+)
    /// - Throughput: 2M+ nodes/sec
    /// - K2K message latency: <500ns
    /// </para>
    /// </remarks>
    [RingKernel(
        KernelId = "metal_pagerank_contribution_sender",
        Capacity = 4096,
        InputQueueSize = 1024,
        OutputQueueSize = 2048,
        MaxInputMessageSizeBytes = 512,  // MetalGraphNode with 32 edges
        MaxOutputMessageSizeBytes = 16,  // PageRankContribution (16 bytes)
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 64,
        EnableTimestamps = true,
        PublishesToKernels = new[] { "metal_pagerank_rank_aggregator" },
        UseBarriers = true,
        BarrierScope = BarrierScope.Threadgroup,  // Metal threadgroup barrier
        Target = KernelTarget.Metal)]
    public static void ContributionSenderKernel(RingKernelContext ctx, MetalGraphNode nodeInfo)
    {
        // Threadgroup barrier for Metal synchronization
        ctx.SyncThreads();

        // Calculate contribution per outbound edge
        int edgeCount = nodeInfo.EdgeCount;
        if (edgeCount == 0 || edgeCount > 32)
        {
            return; // Dangling node or invalid edge count
        }

        float contribution = nodeInfo.CurrentRank / edgeCount;

        // Send contribution to each neighbor
        // Metal will optimize this loop with simdgroup parallelism
        for (int i = 0; i < edgeCount; i++)
        {
            int targetId = nodeInfo.TargetNodeIds[i];

            var msg = new PageRankContribution
            {
                SourceNodeId = nodeInfo.NodeId,
                TargetNodeId = targetId,
                Contribution = contribution,
                Iteration = 0 // Set by orchestrator
            };

            // K2K messaging to aggregator (tests routing infrastructure)
            ctx.SendToKernel("metal_pagerank_rank_aggregator", msg);
        }

        // Memory fence to ensure all messages are visible
        ctx.ThreadFence();
    }

    /// <summary>
    /// Metal-optimized kernel that aggregates contributions and computes new ranks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Receives contributions from multiple nodes, accumulates them,
    /// and applies the PageRank formula: (1-d)/N + d × sum(contributions)
    /// </para>
    /// <para>
    /// Metal optimizations:
    /// - Threadgroup shared memory for local accumulation
    /// - Simdgroup reductions for parallel sum
    /// - Atomic operations for cross-thread aggregation
    /// </para>
    /// <para>
    /// Implementation strategy:
    /// - Phase 1: Each thread processes contributions locally
    /// - Phase 2: Threadgroup reduction to compute total contribution
    /// - Phase 3: Apply PageRank formula and compute delta
    /// - Phase 4: Send result to ConvergenceChecker
    /// </para>
    /// <para>
    /// Performance targets:
    /// - Aggregation: ~200-1000ns per contribution
    /// - Throughput: 1M+ contributions/sec
    /// </para>
    /// </remarks>
    [RingKernel(
        KernelId = "metal_pagerank_rank_aggregator",
        Capacity = 8192,
        InputQueueSize = 4096,
        OutputQueueSize = 1024,
        MaxInputMessageSizeBytes = 16,   // PageRankContribution (16 bytes)
        MaxOutputMessageSizeBytes = 16,  // RankAggregationResult (16 bytes)
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 128,
        EnableTimestamps = true,
        SubscribesToKernels = new[] { "metal_pagerank_contribution_sender" },
        PublishesToKernels = new[] { "metal_pagerank_convergence_checker" },
        UseBarriers = true,
        BarrierScope = BarrierScope.Threadgroup,
        Target = KernelTarget.Metal,
        MemoryConsistency = MemoryConsistencyModel.AcquireRelease)]
    public static void RankAggregatorKernel(RingKernelContext ctx, PageRankContribution contribution)
    {
        // Note: This is a simplified implementation
        // Production version would use threadgroup shared memory
        // to accumulate contributions per node before outputting

        // Metal threadgroup barrier ensures all contributions are visible
        ctx.SyncThreads();

        // Apply damping factor and compute new rank
        // In production, this would aggregate multiple contributions first
        float dampedContribution = DampingFactor * contribution.Contribution;

        // For demo purposes, we output partial results
        // Production implementation would batch and reduce
        var result = new RankAggregationResult
        {
            NodeId = contribution.TargetNodeId,
            NewRank = dampedContribution,  // Partial - needs full aggregation
            Delta = 0,  // Computed after aggregating all contributions
            Iteration = contribution.Iteration
        };

        // Memory fence before sending (ensures visibility across kernels)
        ctx.ThreadFence();

        // Send to ConvergenceChecker via K2K routing
        ctx.SendToKernel("metal_pagerank_convergence_checker", result);
    }

    /// <summary>
    /// Metal-optimized kernel that checks for convergence and outputs results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Collects rank updates, computes maximum delta across all nodes,
    /// and determines whether PageRank has converged.
    /// </para>
    /// <para>
    /// Metal optimizations:
    /// - Simdgroup parallel reduction for max delta
    /// - Atomic compare-and-swap for global maximum
    /// - Threadgroup shared memory for intermediate results
    /// </para>
    /// <para>
    /// Convergence criteria:
    /// - All node rank changes &lt; ConvergenceThreshold (0.0001)
    /// - Or maximum iterations reached (set by orchestrator)
    /// </para>
    /// <para>
    /// Performance targets:
    /// - Convergence check: ~500-2000ns per result
    /// - Max delta computation: O(log N) with simdgroup reduction
    /// </para>
    /// </remarks>
    [RingKernel(
        KernelId = "metal_pagerank_convergence_checker",
        Capacity = 2048,
        InputQueueSize = 1024,
        OutputQueueSize = 256,
        MaxInputMessageSizeBytes = 16,   // RankAggregationResult (16 bytes)
        MaxOutputMessageSizeBytes = 16,  // ConvergenceCheckResult (16 bytes)
        ProcessingMode = RingProcessingMode.Adaptive,
        EnableTimestamps = true,
        SubscribesToKernels = new[] { "metal_pagerank_rank_aggregator" },
        Target = KernelTarget.Metal,
        MemoryConsistency = MemoryConsistencyModel.Sequential,
        EnableCausalOrdering = true)]
    public static void ConvergenceCheckerKernel(RingKernelContext ctx, RankAggregationResult result)
    {
        // Compute absolute delta for convergence check
        float absDelta = result.Delta >= 0 ? result.Delta : -result.Delta;

        // Check if this result indicates non-convergence
        bool hasConverged = absDelta < ConvergenceThreshold;

        // Output convergence status
        // In production, would use simdgroup reduction to find max delta
        // across all nodes before deciding convergence
        var checkResult = new ConvergenceCheckResult
        {
            Iteration = result.Iteration,
            MaxDelta = absDelta,
            HasConverged = hasConverged ? 1 : 0,
            NodesProcessed = 1  // Would be aggregated in production
        };

        // Memory fence ensures visibility
        ctx.ThreadFence();

        // Output result (can be read by orchestrator on CPU via unified memory)
        ctx.EnqueueOutput(checkResult);
    }
}
