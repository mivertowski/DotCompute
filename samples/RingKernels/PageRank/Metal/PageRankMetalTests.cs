// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Compilation;
using DotCompute.Backends.Metal.RingKernels;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// Tests for Metal PageRank ring kernel implementation.
/// </summary>
/// <remarks>
/// Validates end-to-end Metal backend pipeline:
/// - RingKernel discovery from C# source
/// - C# → MSL transpilation
/// - MemoryPack serialization auto-discovery
/// - K2K message routing infrastructure
/// - Barrier synchronization
/// </remarks>
public class PageRankMetalTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalRingKernelStubGenerator> _stubLogger;
    private readonly ILogger<RingKernelDiscovery> _discoveryLogger;

    public PageRankMetalTests(ITestOutputHelper output)
    {
        _output = output;
        _stubLogger = NullLogger<MetalRingKernelStubGenerator>.Instance;
        _discoveryLogger = NullLogger<RingKernelDiscovery>.Instance;
    }

    /// <summary>
    /// Phase 3.2: Verifies that all 3 Metal PageRank kernels are discovered.
    /// </summary>
    [Fact]
    public void MetalPageRankKernels_AreDiscovered()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankMetalKernels).Assembly });

        // Assert
        var metalPageRankKernels = kernels
            .Where(k => k.KernelId.StartsWith("metal_pagerank_", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, metalPageRankKernels.Count);
        Assert.Contains(metalPageRankKernels, k => k.KernelId == "metal_pagerank_contribution_sender");
        Assert.Contains(metalPageRankKernels, k => k.KernelId == "metal_pagerank_rank_aggregator");
        Assert.Contains(metalPageRankKernels, k => k.KernelId == "metal_pagerank_convergence_checker");

        _output.WriteLine($"✅ Discovered {metalPageRankKernels.Count} Metal PageRank kernels");
        foreach (var kernel in metalPageRankKernels)
        {
            _output.WriteLine($"  - {kernel.KernelId}:");
            _output.WriteLine($"    HasInlineHandler: {kernel.HasInlineHandler}");
            _output.WriteLine($"    Target: {kernel.Target}");
            _output.WriteLine($"    InputMessageType: {kernel.InputMessageTypeName}");
            _output.WriteLine($"    OutputMessageType: {kernel.OutputMessageTypeName}");
        }
    }

    /// <summary>
    /// Phase 3.2: Verifies that inline handlers are detected for Metal kernels.
    /// </summary>
    [Fact]
    public void MetalPageRankKernels_DetectInlineHandlers()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankMetalKernels).Assembly });
        var contributionSender = kernels.First(k => k.KernelId == "metal_pagerank_contribution_sender");
        var rankAggregator = kernels.First(k => k.KernelId == "metal_pagerank_rank_aggregator");
        var convergenceChecker = kernels.First(k => k.KernelId == "metal_pagerank_convergence_checker");

        // Assert - all should have inline handlers (RingKernelContext parameter)
        Assert.True(contributionSender.HasInlineHandler,
            "ContributionSender should have inline handler");
        Assert.True(rankAggregator.HasInlineHandler,
            "RankAggregator should have inline handler");
        Assert.True(convergenceChecker.HasInlineHandler,
            "ConvergenceChecker should have inline handler");

        // Verify target is Metal
        Assert.Equal(KernelTarget.Metal, contributionSender.Target);
        Assert.Equal(KernelTarget.Metal, rankAggregator.Target);
        Assert.Equal(KernelTarget.Metal, convergenceChecker.Target);

        _output.WriteLine("✅ All 3 kernels have inline handlers and Metal target");
        _output.WriteLine($"  ContributionSender: {contributionSender.InputMessageTypeName} → Metal");
        _output.WriteLine($"  RankAggregator: {rankAggregator.InputMessageTypeName} → Metal");
        _output.WriteLine($"  ConvergenceChecker: {convergenceChecker.InputMessageTypeName} → Metal");
    }

    /// <summary>
    /// Phase 3.2: Verifies Metal MSL stub generation for PageRank kernels.
    /// Tests C# → MSL transpilation pipeline.
    /// </summary>
    [Fact]
    public void MetalPageRankKernels_GenerateMslStubs()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);
        var generator = new MetalRingKernelStubGenerator(_stubLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankMetalKernels).Assembly })
            .Where(k => k.KernelId.StartsWith("metal_pagerank_", StringComparison.Ordinal))
            .ToList();

        foreach (var kernel in kernels)
        {
            var mslCode = generator.GenerateKernelStub(kernel);

            // Assert - verify MSL code structure
            Assert.NotEmpty(mslCode);
            Assert.Contains("ring_kernel_control_block* control_block", mslCode);
            Assert.Contains($"{kernel.KernelId}_kernel", mslCode);
            Assert.Contains("kernel void", mslCode);
            Assert.Contains("[[buffer(", mslCode);

            _output.WriteLine($"\n=== {kernel.KernelId} ===");
            _output.WriteLine($"✅ Generated {mslCode.Length} chars of MSL code");

            // Verify threadgroup barrier translation
            if (kernel.UseBarriers == true)
            {
                Assert.Contains("threadgroup_barrier", mslCode);
                _output.WriteLine("  - Threadgroup barriers: present");
            }

            // Verify K2K infrastructure for ContributionSender
            if (kernel.KernelId == "metal_pagerank_contribution_sender")
            {
                Assert.Contains("k2k_send_queue", mslCode);
                Assert.Contains("metal_pagerank_rank_aggregator", mslCode);
                _output.WriteLine("  - K2K send infrastructure: present");
                _output.WriteLine("  - Routes to: metal_pagerank_rank_aggregator");
            }

            // Verify K2K infrastructure for RankAggregator (both send and receive)
            if (kernel.KernelId == "metal_pagerank_rank_aggregator")
            {
                Assert.Contains("k2k_receive_channels", mslCode);
                Assert.Contains("k2k_send_queue", mslCode);
                Assert.Contains("metal_pagerank_contribution_sender", mslCode);
                Assert.Contains("metal_pagerank_convergence_checker", mslCode);
                _output.WriteLine("  - K2K receive infrastructure: present");
                _output.WriteLine("  - K2K send infrastructure: present");
                _output.WriteLine("  - Receives from: metal_pagerank_contribution_sender");
                _output.WriteLine("  - Routes to: metal_pagerank_convergence_checker");
            }

            // Verify K2K infrastructure for ConvergenceChecker (receive only)
            if (kernel.KernelId == "metal_pagerank_convergence_checker")
            {
                Assert.Contains("k2k_receive_channels", mslCode);
                Assert.Contains("metal_pagerank_rank_aggregator", mslCode);
                _output.WriteLine("  - K2K receive infrastructure: present");
                _output.WriteLine("  - Receives from: metal_pagerank_rank_aggregator");
            }
        }
    }

    /// <summary>
    /// Phase 3.2: Verifies MemoryPack serialization for Metal PageRank messages.
    /// Tests message serialization infrastructure.
    /// </summary>
    [Fact]
    public void MetalPageRankMessages_Serialize()
    {
        // Arrange
        var contribution = new PageRankContribution
        {
            SourceNodeId = 1,
            TargetNodeId = 2,
            Contribution = 0.25f,
            Iteration = 5
        };

        var result = new RankAggregationResult
        {
            NodeId = 3,
            NewRank = 0.13f,
            Delta = 0.005f,
            Iteration = 5
        };

        var convergence = new ConvergenceCheckResult
        {
            Iteration = 5,
            MaxDelta = 0.005f,
            HasConverged = 0,
            NodesProcessed = 100
        };

        var graphNode = MetalGraphNode.Create(
            nodeId: 1,
            initialRank: 1.0f,
            edges: new[] { 2, 3, 4 });

        // Act
        var contributionBytes = MemoryPackSerializer.Serialize(contribution);
        var resultBytes = MemoryPackSerializer.Serialize(result);
        var convergenceBytes = MemoryPackSerializer.Serialize(convergence);
        var graphNodeBytes = MemoryPackSerializer.Serialize(graphNode);

        // Assert
        Assert.True(contributionBytes.Length > 0);
        Assert.True(resultBytes.Length > 0);
        Assert.True(convergenceBytes.Length > 0);
        Assert.True(graphNodeBytes.Length > 0);

        // Verify 16-byte alignment for fixed messages
        Assert.Equal(16, contributionBytes.Length);
        Assert.Equal(16, resultBytes.Length);
        Assert.Equal(16, convergenceBytes.Length);

        // Verify roundtrip
        var contribution2 = MemoryPackSerializer.Deserialize<PageRankContribution>(contributionBytes);
        Assert.Equal(contribution.SourceNodeId, contribution2.SourceNodeId);
        Assert.Equal(contribution.TargetNodeId, contribution2.TargetNodeId);
        Assert.Equal(contribution.Contribution, contribution2.Contribution, precision: 5);
        Assert.Equal(contribution.Iteration, contribution2.Iteration);

        var graphNode2 = MemoryPackSerializer.Deserialize<MetalGraphNode>(graphNodeBytes);
        Assert.Equal(graphNode.NodeId, graphNode2.NodeId);
        Assert.Equal(graphNode.CurrentRank, graphNode2.CurrentRank, precision: 5);
        Assert.Equal(graphNode.EdgeCount, graphNode2.EdgeCount);

        _output.WriteLine("✅ Message serialization verified:");
        _output.WriteLine($"  PageRankContribution: {contributionBytes.Length} bytes (16-byte aligned)");
        _output.WriteLine($"  RankAggregationResult: {resultBytes.Length} bytes (16-byte aligned)");
        _output.WriteLine($"  ConvergenceCheckResult: {convergenceBytes.Length} bytes (16-byte aligned)");
        _output.WriteLine($"  MetalGraphNode: {graphNodeBytes.Length} bytes (variable size)");
    }

    /// <summary>
    /// Phase 3.3: Validates K2K message graph for Metal PageRank.
    /// Tests that kernel dependencies are correctly configured.
    /// </summary>
    [Fact]
    public void MetalPageRankKernels_ValidateK2KMessageGraph()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankMetalKernels).Assembly })
            .Where(k => k.KernelId.StartsWith("metal_pagerank_", StringComparison.Ordinal))
            .ToDictionary(k => k.KernelId);

        // Act & Assert - ContributionSender
        var contributionSender = kernels["metal_pagerank_contribution_sender"];
        Assert.Empty(contributionSender.SubscribesToKernels ?? Array.Empty<string>());
        Assert.Contains("metal_pagerank_rank_aggregator", contributionSender.PublishesToKernels ?? Array.Empty<string>());

        // Act & Assert - RankAggregator
        var rankAggregator = kernels["metal_pagerank_rank_aggregator"];
        Assert.Contains("metal_pagerank_contribution_sender", rankAggregator.SubscribesToKernels ?? Array.Empty<string>());
        Assert.Contains("metal_pagerank_convergence_checker", rankAggregator.PublishesToKernels ?? Array.Empty<string>());

        // Act & Assert - ConvergenceChecker
        var convergenceChecker = kernels["metal_pagerank_convergence_checker"];
        Assert.Contains("metal_pagerank_rank_aggregator", convergenceChecker.SubscribesToKernels ?? Array.Empty<string>());
        Assert.Empty(convergenceChecker.PublishesToKernels ?? Array.Empty<string>());

        _output.WriteLine("✅ K2K message graph validated:");
        _output.WriteLine("  ContributionSender → RankAggregator");
        _output.WriteLine("  RankAggregator → ConvergenceChecker");
        _output.WriteLine("  Message flow: 3-stage pipeline");
    }

    /// <summary>
    /// Phase 3.3: Simulates PageRank on CPU to establish baseline for Metal validation.
    /// </summary>
    [Fact]
    public void MetalPageRank_CpuBaselineComputation()
    {
        // Arrange - Simple 4-node graph
        var graph = new Dictionary<int, int[]>
        {
            { 0, new[] { 1, 2 } },
            { 1, new[] { 2 } },
            { 2, new[] { 0 } },
            { 3, new[] { 0, 1, 2 } }
        };

        int nodeCount = 4;
        float dampingFactor = PageRankMetalKernels.DampingFactor;
        int maxIterations = 100;
        float convergenceThreshold = PageRankMetalKernels.ConvergenceThreshold;

        // Act - CPU simulation
        var ranks = SimulatePageRank(graph, nodeCount, dampingFactor, maxIterations, convergenceThreshold);

        // Assert
        float sum = ranks.Sum();
        Assert.True(Math.Abs(sum - nodeCount) < 0.01f, $"Sum should be ~{nodeCount}, got {sum}");

        // Node 2 should have highest rank (receives from 0, 1, and 3)
        Assert.True(ranks[2] > ranks[0]);
        Assert.True(ranks[2] > ranks[1]);

        _output.WriteLine("✅ CPU baseline PageRank:");
        for (int i = 0; i < nodeCount; i++)
        {
            _output.WriteLine($"  Node {i}: {ranks[i]:F6}");
        }
        _output.WriteLine($"  Sum: {sum:F6}");
        _output.WriteLine($"  Damping: {dampingFactor}");
        _output.WriteLine($"  Threshold: {convergenceThreshold}");
    }

    /// <summary>
    /// CPU PageRank simulation for validation.
    /// </summary>
    private static float[] SimulatePageRank(
        Dictionary<int, int[]> graph,
        int nodeCount,
        float dampingFactor,
        int maxIterations,
        float convergenceThreshold)
    {
        var ranks = new float[nodeCount];
        var newRanks = new float[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            ranks[i] = 1.0f;
        }

        float baseProbability = (1.0f - dampingFactor);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                newRanks[i] = baseProbability;
            }

            foreach (var (sourceNode, targets) in graph)
            {
                if (targets.Length == 0) continue;
                float contribution = dampingFactor * ranks[sourceNode] / targets.Length;
                foreach (var targetNode in targets)
                {
                    newRanks[targetNode] += contribution;
                }
            }

            float maxDelta = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                float delta = Math.Abs(newRanks[i] - ranks[i]);
                if (delta > maxDelta) maxDelta = delta;
            }

            (ranks, newRanks) = (newRanks, ranks);

            if (maxDelta < convergenceThreshold) break;
        }

        return ranks;
    }
}
