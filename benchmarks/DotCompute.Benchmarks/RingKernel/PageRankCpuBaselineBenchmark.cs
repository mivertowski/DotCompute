// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;

namespace DotCompute.Benchmarks.RingKernel;

/// <summary>
/// CPU baseline benchmarks for PageRank computation.
/// Establishes performance baselines for comparison with Metal GPU implementation.
/// </summary>
/// <remarks>
/// <para>
/// Benchmark Categories:
/// - Small Graphs: 10-100 nodes
/// - Medium Graphs: 1,000-10,000 nodes
/// - Large Graphs: 100,000+ nodes
/// </para>
/// <para>
/// Performance Metrics:
/// - Iteration latency (μs per iteration)
/// - Convergence time (total time to convergence)
/// - Throughput (iterations/second)
/// - Memory usage
/// - Cache efficiency
/// </para>
/// <para>
/// Graph Topologies Tested:
/// - Star: Central hub with spokes
/// - Chain: Linear sequence
/// - Complete: Fully connected
/// - Random: Erdős-Rényi random graph
/// - Web: Scale-free power-law distribution
/// </para>
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[SimpleJob(RuntimeMoniker.Net90, baseline: true)]
public class PageRankCpuBaselineBenchmark
{
    private Dictionary<int, int[]> _smallStarGraph = null!;
    private Dictionary<int, int[]> _mediumRandomGraph = null!;
    private Dictionary<int, int[]> _largeWebGraph = null!;

    private Dictionary<int, int[]> _smallChainGraph = null!;
    private Dictionary<int, int[]> _mediumCompleteGraph = null!;

    private const float DampingFactor = 0.85f;
    private const float ConvergenceThreshold = 0.0001f;
    private const int MaxIterations = 100;

    [Params(10, 100, 1000)]
    public int GraphSize { get; set; }

    [Params(GraphTopology.Star, GraphTopology.Random, GraphTopology.Chain)]
    public GraphTopology Topology { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Small graphs (10-100 nodes)
        _smallStarGraph = CreateStarGraph(10);
        _smallChainGraph = CreateChainGraph(10);

        // Medium graphs (1,000 nodes)
        _mediumRandomGraph = CreateRandomGraph(1000, avgEdgesPerNode: 5, seed: 42);
        _mediumCompleteGraph = CreateCompleteGraph(50);  // K50

        // Large graphs (10,000+ nodes)
        _largeWebGraph = CreateWebGraph(10000, seed: 42);

        Console.WriteLine($"\n=== PageRank CPU Baseline Benchmark Setup ===");
        Console.WriteLine($"Graph Size: {GraphSize} nodes");
        Console.WriteLine($"Topology: {Topology}");
        Console.WriteLine($"Damping Factor: {DampingFactor}");
        Console.WriteLine($"Convergence Threshold: {ConvergenceThreshold}");
        Console.WriteLine($"Max Iterations: {MaxIterations}");
    }

    #region Small Graph Benchmarks

    /// <summary>
    /// Baseline: 10-node star graph (single iteration).
    /// Target: ~1-5 μs per iteration.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small", "SingleIteration")]
    public float[] SmallGraph_SingleIteration()
    {
        var graph = GetGraphForSize(10);
        return RunSingleIteration(graph, 10);
    }

    /// <summary>
    /// Small graph: Full convergence benchmark.
    /// Target: ~10-50 μs total (10-20 iterations typical).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "Convergence")]
    public float[] SmallGraph_FullConvergence()
    {
        var graph = GetGraphForSize(10);
        return ComputePageRank(graph, 10);
    }

    /// <summary>
    /// Small graph: 100 nodes, single iteration.
    /// Target: ~10-20 μs per iteration.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "SingleIteration")]
    public float[] SmallGraph100_SingleIteration()
    {
        var graph = GetGraphForSize(100);
        return RunSingleIteration(graph, 100);
    }

    /// <summary>
    /// Small graph: 100 nodes, full convergence.
    /// Target: ~100-500 μs total.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "Convergence")]
    public float[] SmallGraph100_FullConvergence()
    {
        var graph = GetGraphForSize(100);
        return ComputePageRank(graph, 100);
    }

    #endregion

    #region Medium Graph Benchmarks

    /// <summary>
    /// Medium graph: 1,000 nodes, single iteration.
    /// Target: ~100-300 μs per iteration.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Medium", "SingleIteration")]
    public float[] MediumGraph_SingleIteration()
    {
        var graph = GetGraphForSize(1000);
        return RunSingleIteration(graph, 1000);
    }

    /// <summary>
    /// Medium graph: 1,000 nodes, full convergence.
    /// Target: ~2-10 ms total (20-40 iterations typical).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Medium", "Convergence")]
    public float[] MediumGraph_FullConvergence()
    {
        var graph = GetGraphForSize(1000);
        return ComputePageRank(graph, 1000);
    }

    #endregion

    #region Memory Efficiency Benchmarks

    /// <summary>
    /// Tests memory allocation patterns for 1,000-node graph.
    /// Target: Minimal allocations during iterations (pre-allocated buffers).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Memory", "Allocation")]
    public float[] MemoryEfficient_PreallocatedBuffers()
    {
        var graph = GetGraphForSize(1000);
        return ComputePageRankOptimized(graph, 1000);
    }

    #endregion

    #region Cache Efficiency Benchmarks

    /// <summary>
    /// Sequential access pattern (cache-friendly).
    /// Target: Better cache hit rate than random access.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Cache", "Sequential")]
    public float[] CacheEfficient_SequentialAccess()
    {
        var graph = _smallChainGraph;  // Linear access pattern
        return ComputePageRank(graph, 10);
    }

    /// <summary>
    /// Random access pattern (cache-unfriendly).
    /// Target: Baseline for cache impact comparison.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Cache", "Random")]
    public float[] CacheInefficient_RandomAccess()
    {
        var graph = _mediumRandomGraph;  // Random access pattern
        return ComputePageRank(graph, 1000);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate graph for the specified size.
    /// </summary>
    private Dictionary<int, int[]> GetGraphForSize(int size)
    {
        return Topology switch
        {
            GraphTopology.Star => size switch
            {
                10 => _smallStarGraph,
                100 => CreateStarGraph(100),
                1000 => CreateStarGraph(1000),
                _ => CreateStarGraph(size)
            },
            GraphTopology.Chain => size switch
            {
                10 => _smallChainGraph,
                100 => CreateChainGraph(100),
                1000 => CreateChainGraph(1000),
                _ => CreateChainGraph(size)
            },
            GraphTopology.Random => size switch
            {
                10 => CreateRandomGraph(10, 3, 42),
                100 => CreateRandomGraph(100, 5, 42),
                1000 => _mediumRandomGraph,
                _ => CreateRandomGraph(size, 5, 42)
            },
            GraphTopology.Complete => size switch
            {
                10 => CreateCompleteGraph(10),
                50 => _mediumCompleteGraph,
                _ => CreateCompleteGraph(Math.Min(size, 100))  // Complete graphs scale poorly
            },
            GraphTopology.Web => size switch
            {
                10 => CreateWebGraph(10, 42),
                100 => CreateWebGraph(100, 42),
                1000 => CreateWebGraph(1000, 42),
                _ => _largeWebGraph
            },
            _ => CreateRandomGraph(size, 5, 42)
        };
    }

    /// <summary>
    /// Runs a single PageRank iteration (for micro-benchmarking).
    /// </summary>
    private static float[] RunSingleIteration(Dictionary<int, int[]> graph, int nodeCount)
    {
        var ranks = new float[nodeCount];
        var newRanks = new float[nodeCount];

        // Initialize ranks
        for (int i = 0; i < nodeCount; i++)
        {
            ranks[i] = 1.0f;
        }

        // Single iteration
        float baseProbability = 1.0f - DampingFactor;
        for (int i = 0; i < nodeCount; i++)
        {
            newRanks[i] = baseProbability;
        }

        foreach (var (sourceNode, targets) in graph)
        {
            if (targets.Length == 0)
            {
                continue;
            }

            float contribution = DampingFactor * ranks[sourceNode] / targets.Length;
            foreach (var targetNode in targets)
            {
                newRanks[targetNode] += contribution;
            }
        }

        return newRanks;
    }

    /// <summary>
    /// Standard PageRank computation (allocates arrays per call).
    /// </summary>
    private static float[] ComputePageRank(Dictionary<int, int[]> graph, int nodeCount)
    {
        var ranks = new float[nodeCount];
        var newRanks = new float[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            ranks[i] = 1.0f;
        }

        float baseProbability = 1.0f - DampingFactor;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                newRanks[i] = baseProbability;
            }

            foreach (var (sourceNode, targets) in graph)
            {
                if (targets.Length == 0)
                {
                    continue;
                }

                float contribution = DampingFactor * ranks[sourceNode] / targets.Length;
                foreach (var targetNode in targets)
                {
                    newRanks[targetNode] += contribution;
                }
            }

            float maxDelta = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                float delta = Math.Abs(newRanks[i] - ranks[i]);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                }
            }

            (ranks, newRanks) = (newRanks, ranks);

            if (maxDelta < ConvergenceThreshold)
            {
                break;
            }
        }

        return ranks;
    }

    /// <summary>
    /// Optimized PageRank with pre-allocated buffers (simulates GPU approach).
    /// </summary>
    private static float[] ComputePageRankOptimized(Dictionary<int, int[]> graph, int nodeCount)
    {
        // Pre-allocate buffers (amortize allocation cost)
        var ranks = new float[nodeCount];
        var newRanks = new float[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            ranks[i] = 1.0f;
        }

        float baseProbability = 1.0f - DampingFactor;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            // Optimized: Use array operations where possible
            Array.Fill(newRanks, baseProbability);

            foreach (var (sourceNode, targets) in graph)
            {
                if (targets.Length == 0)
                {
                    continue;
                }
                float contribution = DampingFactor * ranks[sourceNode] / targets.Length;

                // Optimized: Minimize branching in tight loop
                for (int i = 0; i < targets.Length; i++)
                {
                    newRanks[targets[i]] += contribution;
                }
            }

            float maxDelta = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                float delta = Math.Abs(newRanks[i] - ranks[i]);
                maxDelta = Math.Max(maxDelta, delta);
            }

            (ranks, newRanks) = (newRanks, ranks);

            if (maxDelta < ConvergenceThreshold)
            {
                break;
            }
        }

        return ranks;
    }

    #endregion

    #region Graph Generation

    /// <summary>
    /// Creates a star graph: central hub connected to all other nodes.
    /// </summary>
    private static Dictionary<int, int[]> CreateStarGraph(int nodeCount)
    {
        var graph = new Dictionary<int, int[]>();

        // Central hub (node 0) connected to all others
        var hubTargets = new int[nodeCount - 1];
        for (int i = 0; i < nodeCount - 1; i++)
        {
            hubTargets[i] = i + 1;
        }
        graph[0] = hubTargets;

        // Peripheral nodes point back to hub
        for (int i = 1; i < nodeCount; i++)
        {
            graph[i] = new[] { 0 };
        }

        return graph;
    }

    /// <summary>
    /// Creates a chain graph: linear sequence 0 → 1 → 2 → ... → n-1.
    /// </summary>
    private static Dictionary<int, int[]> CreateChainGraph(int nodeCount)
    {
        var graph = new Dictionary<int, int[]>();

        for (int i = 0; i < nodeCount - 1; i++)
        {
            graph[i] = new[] { i + 1 };
        }
        graph[nodeCount - 1] = Array.Empty<int>();  // Sink node

        return graph;
    }

    /// <summary>
    /// Creates a complete graph: every node connected to every other node.
    /// </summary>
    private static Dictionary<int, int[]> CreateCompleteGraph(int nodeCount)
    {
        var graph = new Dictionary<int, int[]>();

        for (int i = 0; i < nodeCount; i++)
        {
            var targets = new List<int>();
            for (int j = 0; j < nodeCount; j++)
            {
                if (i != j)
                {
                    targets.Add(j);
                }
            }
            graph[i] = targets.ToArray();
        }

        return graph;
    }

    /// <summary>
    /// Creates a random Erdős-Rényi graph.
    /// </summary>
    private static Dictionary<int, int[]> CreateRandomGraph(int nodeCount, int avgEdgesPerNode, int seed)
    {
        var random = new Random(seed);
        var graph = new Dictionary<int, int[]>();

        for (int i = 0; i < nodeCount; i++)
        {
            int edgeCount = Math.Max(1, random.Next(avgEdgesPerNode * 2));
            var targets = new HashSet<int>();

            while (targets.Count < edgeCount && targets.Count < nodeCount - 1)
            {
                int target = random.Next(nodeCount);
                if (target != i)  // No self-loops
                {
                    targets.Add(target);
                }
            }

            graph[i] = targets.ToArray();
        }

        return graph;
    }

    /// <summary>
    /// Creates a scale-free web graph (power-law degree distribution).
    /// </summary>
    private static Dictionary<int, int[]> CreateWebGraph(int nodeCount, int seed)
    {
        var random = new Random(seed);
        var graph = new Dictionary<int, int[]>();

        // Preferential attachment model (simplified)
        var degrees = new int[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            var targets = new HashSet<int>();

            // Power-law: few nodes have many edges, most have few
            int edgeCount = random.NextDouble() < 0.1
                ? random.Next(10, 30)  // 10% hub nodes
                : random.Next(1, 5);   // 90% regular nodes

            while (targets.Count < edgeCount && targets.Count < i)
            {
                // Preferential attachment: higher degree nodes more likely
                int target = SelectNodeByDegree(degrees, i, random);
                if (target != i && target >= 0)
                {
                    targets.Add(target);
                    degrees[target]++;
                }
            }

            graph[i] = targets.ToArray();
        }

        return graph;
    }

    /// <summary>
    /// Selects a node with probability proportional to its degree.
    /// </summary>
    private static int SelectNodeByDegree(int[] degrees, int maxNode, Random random)
    {
        if (maxNode == 0)
        {
            return -1;
        }

        int totalDegree = 0;
        for (int i = 0; i < maxNode; i++)
        {
            totalDegree += Math.Max(1, degrees[i]);  // Ensure non-zero
        }

        int targetDegree = random.Next(totalDegree);
        int cumulativeDegree = 0;

        for (int i = 0; i < maxNode; i++)
        {
            cumulativeDegree += Math.Max(1, degrees[i]);
            if (cumulativeDegree > targetDegree)
            {
                return i;
            }
        }

        return maxNode - 1;
    }

    #endregion
}

/// <summary>
/// Graph topology types for benchmarking.
/// </summary>
public enum GraphTopology
{
    /// <summary>Star topology: central hub + spokes.</summary>
    Star,

    /// <summary>Chain topology: linear sequence.</summary>
    Chain,

    /// <summary>Complete topology: fully connected.</summary>
    Complete,

    /// <summary>Random topology: Erdős-Rényi.</summary>
    Random,

    /// <summary>Web topology: scale-free power-law.</summary>
    Web
}
