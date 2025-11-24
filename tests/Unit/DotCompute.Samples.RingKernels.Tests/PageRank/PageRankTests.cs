// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Compilation;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Samples.RingKernels.Tests.PageRank;

/// <summary>
/// Tests for PageRank ring kernel implementation.
/// Verifies kernel discovery, CUDA code generation, and result correctness.
/// </summary>
public class PageRankTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaRingKernelStubGenerator> _stubLogger;
    private readonly ILogger<RingKernelDiscovery> _discoveryLogger;

    public PageRankTests(ITestOutputHelper output)
    {
        _output = output;
        _stubLogger = NullLogger<CudaRingKernelStubGenerator>.Instance;
        _discoveryLogger = NullLogger<RingKernelDiscovery>.Instance;
    }

    /// <summary>
    /// Verifies that all PageRank kernels are discovered correctly.
    /// </summary>
    [Fact]
    public void PageRankKernels_AreDiscovered()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankKernels).Assembly });

        // Assert
        var pageRankKernels = kernels
            .Where(k => k.KernelId.StartsWith("pagerank_", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, pageRankKernels.Count);
        Assert.Contains(pageRankKernels, k => k.KernelId == "pagerank_contribution_sender");
        Assert.Contains(pageRankKernels, k => k.KernelId == "pagerank_rank_aggregator");
        Assert.Contains(pageRankKernels, k => k.KernelId == "pagerank_convergence_checker");

        _output.WriteLine($"Discovered {pageRankKernels.Count} PageRank kernels");
        foreach (var kernel in pageRankKernels)
        {
            _output.WriteLine($"  - {kernel.KernelId}: HasInlineHandler={kernel.HasInlineHandler}");
        }
    }

    /// <summary>
    /// Verifies that inline handlers are detected for kernels with RingKernelContext.
    /// </summary>
    [Fact]
    public void PageRankKernels_DetectInlineHandlers()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankKernels).Assembly });
        var contributionSender = kernels.First(k => k.KernelId == "pagerank_contribution_sender");

        // Assert - should detect inline handler because of RingKernelContext parameter
        Assert.True(contributionSender.HasInlineHandler,
            "ContributionSender should have inline handler detected");

        _output.WriteLine($"ContributionSender: HasInlineHandler={contributionSender.HasInlineHandler}");
        _output.WriteLine($"InputMessageTypeName={contributionSender.InputMessageTypeName}");
    }

    /// <summary>
    /// Verifies CUDA stub generation for PageRank kernels.
    /// </summary>
    [Fact]
    public void PageRankKernels_GenerateCudaStubs()
    {
        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);
        var generator = new CudaRingKernelStubGenerator(_stubLogger);

        // Act
        var kernels = discovery.DiscoverKernels(new[] { typeof(PageRankKernels).Assembly })
            .Where(k => k.KernelId.StartsWith("pagerank_", StringComparison.Ordinal))
            .ToList();

        foreach (var kernel in kernels)
        {
            var cudaCode = generator.GenerateKernelStub(kernel);

            // Assert
            Assert.NotEmpty(cudaCode);
            Assert.Contains("RingKernelControlBlock* control_block", cudaCode);
            Assert.Contains("__device__ bool process_", cudaCode);
            Assert.Contains($"{kernel.KernelId}_kernel", cudaCode);

            _output.WriteLine($"\n=== {kernel.KernelId} ===");
            _output.WriteLine($"Generated {cudaCode.Length} chars of CUDA code");

            // Verify K2K infrastructure for kernels that publish
            if (kernel.KernelId == "pagerank_contribution_sender")
            {
                Assert.Contains("k2k_send_queue", cudaCode);
                Assert.Contains("K2K Send Queue Declarations", cudaCode);
                Assert.Contains("pagerank_rank_aggregator", cudaCode);
                _output.WriteLine($"  - K2K send infrastructure: present");
            }

            // Verify K2K infrastructure for kernels that subscribe AND publish
            if (kernel.KernelId == "pagerank_rank_aggregator")
            {
                Assert.Contains("k2k_receive_channels", cudaCode);
                Assert.Contains("K2K Receive Channel Declarations", cudaCode);
                Assert.Contains("k2k_send_queue", cudaCode);
                Assert.Contains("K2K Send Queue Declarations", cudaCode);
                Assert.Contains("pagerank_contribution_sender", cudaCode);
                Assert.Contains("pagerank_convergence_checker", cudaCode);
                _output.WriteLine($"  - K2K receive infrastructure: present");
                _output.WriteLine($"  - K2K send infrastructure: present");
            }

            // Verify K2K infrastructure for kernels that only subscribe
            if (kernel.KernelId == "pagerank_convergence_checker")
            {
                Assert.Contains("k2k_receive_channels", cudaCode);
                Assert.Contains("K2K Receive Channel Declarations", cudaCode);
                Assert.Contains("pagerank_rank_aggregator", cudaCode);
                _output.WriteLine($"  - K2K receive infrastructure: present");
            }
        }
    }

    /// <summary>
    /// Verifies message serialization for PageRank messages.
    /// </summary>
    [Fact]
    public void PageRankMessages_Serialize()
    {
        // Arrange
        var contribution = new PageRankContribution
        {
            SourceNodeId = 1,
            TargetNodeId = 2,
            Contribution = 0.25f,
            Iteration = 5
        };

        var rankUpdate = new NodeRankUpdate
        {
            NodeId = 3,
            Rank = 0.125f,
            OutboundEdgeCount = 4,
            Iteration = 5
        };

        var result = new RankAggregationResult
        {
            NodeId = 3,
            NewRank = 0.13f,
            Delta = 0.005f,
            Iteration = 5
        };

        // Act
        var contributionBytes = MemoryPackSerializer.Serialize(contribution);
        var rankUpdateBytes = MemoryPackSerializer.Serialize(rankUpdate);
        var resultBytes = MemoryPackSerializer.Serialize(result);

        // Assert
        Assert.True(contributionBytes.Length > 0);
        Assert.True(rankUpdateBytes.Length > 0);
        Assert.True(resultBytes.Length > 0);

        // Verify roundtrip
        var contribution2 = MemoryPackSerializer.Deserialize<PageRankContribution>(contributionBytes);
        Assert.Equal(contribution.SourceNodeId, contribution2.SourceNodeId);
        Assert.Equal(contribution.TargetNodeId, contribution2.TargetNodeId);
        Assert.Equal(contribution.Contribution, contribution2.Contribution);

        _output.WriteLine($"PageRankContribution: {contributionBytes.Length} bytes");
        _output.WriteLine($"NodeRankUpdate: {rankUpdateBytes.Length} bytes");
        _output.WriteLine($"RankAggregationResult: {resultBytes.Length} bytes");
    }

    /// <summary>
    /// Computes PageRank on a simple test graph and verifies results.
    /// Uses CPU simulation to verify algorithm correctness.
    /// </summary>
    [Fact]
    public void PageRank_ComputesCorrectly_SimpleGraph()
    {
        // Arrange - Simple 4-node graph:
        // 0 -> 1, 2
        // 1 -> 2
        // 2 -> 0
        // 3 -> 0, 1, 2 (pointing to all others)
        var graph = new Dictionary<int, int[]>
        {
            { 0, new[] { 1, 2 } },
            { 1, new[] { 2 } },
            { 2, new[] { 0 } },
            { 3, new[] { 0, 1, 2 } }
        };

        int nodeCount = 4;
        float dampingFactor = 0.85f;
        int maxIterations = 100;
        float convergenceThreshold = 0.0001f;

        // Act - CPU simulation of PageRank
        var ranks = SimulatePageRank(graph, nodeCount, dampingFactor, maxIterations, convergenceThreshold);

        // Assert
        // PageRank sum should equal number of nodes (or 1 if normalized)
        float sum = ranks.Sum();
        Assert.True(Math.Abs(sum - nodeCount) < 0.01f, $"Sum of ranks should be ~{nodeCount}, got {sum}");

        // Node 2 should have highest rank (receives from 0, 1, and 3)
        Assert.True(ranks[2] > ranks[0], "Node 2 should have higher rank than Node 0");
        Assert.True(ranks[2] > ranks[1], "Node 2 should have higher rank than Node 1");

        _output.WriteLine("PageRank Results:");
        for (int i = 0; i < nodeCount; i++)
        {
            _output.WriteLine($"  Node {i}: {ranks[i]:F6}");
        }
        _output.WriteLine($"  Sum: {sum:F6}");
    }

    /// <summary>
    /// Verifies PageRank on a larger random graph.
    /// </summary>
    [Fact]
    public void PageRank_ComputesCorrectly_LargerGraph()
    {
        // Arrange - Generate random graph with 100 nodes
        var random = new Random(42); // Fixed seed for reproducibility
        int nodeCount = 100;
        var graph = new Dictionary<int, int[]>();

        for (int i = 0; i < nodeCount; i++)
        {
            // Each node has 1-10 outbound edges
            int edgeCount = random.Next(1, 11);
            var targets = new HashSet<int>();

            while (targets.Count < edgeCount)
            {
                int target = random.Next(nodeCount);
                if (target != i) // No self-loops
                {
                    targets.Add(target);
                }
            }

            graph[i] = targets.ToArray();
        }

        float dampingFactor = 0.85f;
        int maxIterations = 100;
        float convergenceThreshold = 0.0001f;

        // Act
        var ranks = SimulatePageRank(graph, nodeCount, dampingFactor, maxIterations, convergenceThreshold);

        // Assert
        float sum = ranks.Sum();
        Assert.True(Math.Abs(sum - nodeCount) < 0.1f, $"Sum should be ~{nodeCount}, got {sum}");

        // All ranks should be positive
        Assert.All(ranks, r => Assert.True(r > 0, "All ranks should be positive"));

        // Find top 5 nodes
        var topNodes = ranks
            .Select((rank, idx) => (idx, rank))
            .OrderByDescending(x => x.rank)
            .Take(5)
            .ToList();

        _output.WriteLine($"PageRank on {nodeCount}-node graph:");
        _output.WriteLine($"  Sum of ranks: {sum:F6}");
        _output.WriteLine($"  Min rank: {ranks.Min():F6}");
        _output.WriteLine($"  Max rank: {ranks.Max():F6}");
        _output.WriteLine("  Top 5 nodes:");
        foreach (var (idx, rank) in topNodes)
        {
            _output.WriteLine($"    Node {idx}: {rank:F6}");
        }
    }

    /// <summary>
    /// Simulates PageRank computation on CPU to verify algorithm correctness.
    /// This mirrors what the ring kernels would do on GPU.
    /// </summary>
    private static float[] SimulatePageRank(
        Dictionary<int, int[]> graph,
        int nodeCount,
        float dampingFactor,
        int maxIterations,
        float convergenceThreshold)
    {
        // Initialize ranks to 1.0
        var ranks = new float[nodeCount];
        var newRanks = new float[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            ranks[i] = 1.0f;
        }

        float baseProbability = (1.0f - dampingFactor);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Initialize new ranks with base probability
            for (int i = 0; i < nodeCount; i++)
            {
                newRanks[i] = baseProbability;
            }

            // Accumulate contributions from all nodes
            foreach (var (sourceNode, targets) in graph)
            {
                if (targets.Length == 0)
                {
                    continue;
                }

                float contribution = dampingFactor * ranks[sourceNode] / targets.Length;

                foreach (var targetNode in targets)
                {
                    newRanks[targetNode] += contribution;
                }
            }

            // Handle dangling nodes (distribute their rank evenly)
            float danglingSum = 0;
            foreach (var (sourceNode, targets) in graph)
            {
                if (targets.Length == 0)
                {
                    danglingSum += ranks[sourceNode];
                }
            }

            if (danglingSum > 0)
            {
                float danglingContribution = dampingFactor * danglingSum / nodeCount;
                for (int i = 0; i < nodeCount; i++)
                {
                    newRanks[i] += danglingContribution;
                }
            }

            // Check convergence
            float maxDelta = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                float delta = Math.Abs(newRanks[i] - ranks[i]);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                }
            }

            // Swap arrays
            (ranks, newRanks) = (newRanks, ranks);

            if (maxDelta < convergenceThreshold)
            {
                break;
            }
        }

        return ranks;
    }
}
