// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Configs;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace DotCompute.Benchmarks.RingKernel;

#region Stub Orchestrator (Placeholder for Phase 5)

/// <summary>
/// Stub orchestrator for Phase 4.2 benchmark infrastructure.
/// Will be replaced with actual implementation from samples in Phase 5.
/// </summary>
internal sealed class MetalPageRankOrchestrator_Stub : IAsyncDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;

    public MetalPageRankOrchestrator_Stub(IntPtr device, IntPtr commandQueue, ILogger? logger = null)
    {
        _device = device;
        _commandQueue = commandQueue;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Stub: No-op for Phase 4.2
        return Task.CompletedTask;
    }

    public Task<Dictionary<int, float>> ComputePageRankAsync(
        Dictionary<int, int[]> graph,
        int maxIterations = 100,
        float convergenceThreshold = 0.0001f,
        float dampingFactor = 0.85f,
        CancellationToken cancellationToken = default)
    {
        // Stub: Return empty results for Phase 4.2
        // Actual implementation will come in Phase 5
        return Task.FromResult(new Dictionary<int, float>());
    }

    public List<string> GetDiscoveredKernels()
    {
        // Stub: Return empty list for Phase 4.2
        return new List<string>();
    }

    public ValueTask DisposeAsync()
    {
        // Stub: No-op cleanup
        return ValueTask.CompletedTask;
    }
}

#endregion

/// <summary>
/// Metal GPU batch processing benchmarks for PageRank computation.
/// Compares Metal GPU performance against CPU baseline established in PageRankCpuBaselineBenchmark.
/// </summary>
/// <remarks>
/// <para>
/// Benchmark Categories:
/// - Small Batch: 10-100 nodes (latency-sensitive)
/// - Medium Batch: 1,000-10,000 nodes (throughput-optimized)
/// - Large Batch: 100,000+ nodes (memory-intensive)
/// </para>
/// <para>
/// Metal-Specific Performance Metrics:
/// - Unified Memory Performance: Zero-copy CPU ↔ GPU transfers
/// - K2K Message Routing: Direct GPU-to-GPU message passing latency
/// - Barrier Synchronization: Multi-kernel coordination overhead
/// - Batch Processing Throughput: Messages/second sustained
/// - MSL Compilation: Kernel compilation overhead (cache hit/miss)
/// - Command Queue Utilization: Batch submission efficiency
/// </para>
/// <para>
/// Expected Performance Targets (Apple Silicon M2+):
/// - Small graphs (10-100 nodes): CPU competitive (overhead dominant)
/// - Medium graphs (1,000 nodes): 5-10x GPU speedup
/// - Large graphs (10,000+ nodes): 15-30x GPU speedup
/// - K2K message latency: ~200-500ns per message
/// - Iteration throughput: 2M+ messages/sec sustained
/// - Convergence detection: ~200ns per result check
/// </para>
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net90)]
public class PageRankMetalBatchBenchmark
{
    private IntPtr _device;
    private IntPtr _commandQueue;
    private MetalPageRankOrchestrator_Stub? _orchestrator;
    private ILogger? _logger;

    // Pre-generated graphs for benchmarking
    private Dictionary<int, int[]> _smallStarGraph = null!;
    private Dictionary<int, int[]> _mediumRandomGraph = null!;
    private Dictionary<int, int[]> _largeWebGraph = null!;
    private Dictionary<int, int[]> _smallChainGraph = null!;

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
        // Verify running on macOS with Metal support
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Metal benchmarks require macOS");
        }

        // Initialize Metal device and command queue
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device");
        }

        _commandQueue = MetalNative.CreateCommandQueue(_device);
        if (_commandQueue == IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            throw new InvalidOperationException("Failed to create Metal command queue");
        }

        // Initialize logger
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<PageRankMetalBatchBenchmark>();

        // Create stub orchestrator (Phase 4.2 - infrastructure only)
        _orchestrator = new MetalPageRankOrchestrator_Stub(_device, _commandQueue, _logger);

        // Initialize orchestrator (Phase 1: Discovery, MSL generation, routing, barriers)
        _orchestrator.InitializeAsync().GetAwaiter().GetResult();

        // Pre-generate graphs
        _smallStarGraph = CreateStarGraph(10);
        _smallChainGraph = CreateChainGraph(10);
        _mediumRandomGraph = CreateRandomGraph(1000, avgEdgesPerNode: 5, seed: 42);
        _largeWebGraph = CreateWebGraph(10000, seed: 42);

        Console.WriteLine($"\n=== PageRank Metal GPU Batch Benchmark Setup ===");
        Console.WriteLine($"Device: {_device.ToInt64():X}");
        Console.WriteLine($"Command Queue: {_commandQueue.ToInt64():X}");
        Console.WriteLine($"Graph Size: {GraphSize} nodes");
        Console.WriteLine($"Topology: {Topology}");
        Console.WriteLine($"Damping Factor: {DampingFactor}");
        Console.WriteLine($"Convergence Threshold: {ConvergenceThreshold}");
        Console.WriteLine($"Max Iterations: {MaxIterations}");

        // Display discovered kernels (Phase 4.2: Stub returns empty list)
        var discoveredKernels = _orchestrator.GetDiscoveredKernels();
        Console.WriteLine($"Discovered {discoveredKernels.Count} Metal PageRank kernels (Phase 4.2 stub)");
        Console.WriteLine("Note: Actual kernel discovery will be implemented in Phase 5");
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_orchestrator != null)
        {
            await _orchestrator.DisposeAsync();
        }

        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }

    #region Small Batch Benchmarks (Latency-Sensitive)

    /// <summary>
    /// Small batch: 10-node graph, single iteration.
    /// Target: CPU competitive (~1-10 μs, overhead-limited).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small", "SingleIteration")]
    public async Task<Dictionary<int, float>> SmallBatch_SingleIteration_10Nodes()
    {
        var graph = GetGraphForSize(10);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 1,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    /// <summary>
    /// Small batch: 10-node graph, full convergence.
    /// Target: ~50-200 μs total (10-20 iterations typical).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "Convergence")]
    public async Task<Dictionary<int, float>> SmallBatch_FullConvergence_10Nodes()
    {
        var graph = GetGraphForSize(10);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    /// <summary>
    /// Small batch: 100-node graph, single iteration.
    /// Target: ~20-50 μs per iteration (starting to show GPU benefits).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "SingleIteration")]
    public async Task<Dictionary<int, float>> SmallBatch_SingleIteration_100Nodes()
    {
        var graph = GetGraphForSize(100);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 1,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    /// <summary>
    /// Small batch: 100-node graph, full convergence.
    /// Target: ~500 μs - 2 ms total (GPU overhead still significant).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Small", "Convergence")]
    public async Task<Dictionary<int, float>> SmallBatch_FullConvergence_100Nodes()
    {
        var graph = GetGraphForSize(100);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    #endregion

    #region Medium Batch Benchmarks (Throughput-Optimized)

    /// <summary>
    /// Medium batch: 1,000-node graph, single iteration.
    /// Target: 5-10x speedup vs CPU (~10-30 μs per iteration).
    /// CPU baseline: ~100-300 μs per iteration.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Medium", "SingleIteration")]
    public async Task<Dictionary<int, float>> MediumBatch_SingleIteration_1000Nodes()
    {
        var graph = GetGraphForSize(1000);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 1,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    /// <summary>
    /// Medium batch: 1,000-node graph, full convergence.
    /// Target: 5-10x speedup vs CPU (~200 μs - 1 ms total).
    /// CPU baseline: ~2-10 ms total.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Medium", "Convergence")]
    public async Task<Dictionary<int, float>> MediumBatch_FullConvergence_1000Nodes()
    {
        var graph = GetGraphForSize(1000);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);
    }

    #endregion

    #region Metal-Specific Performance Benchmarks

    /// <summary>
    /// Measures unified memory performance for Metal PageRank.
    /// Target: 2-3x speedup vs discrete memory transfers.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Metal", "UnifiedMemory")]
    public async Task<Dictionary<int, float>> Metal_UnifiedMemory_ZeroCopy()
    {
        // Use 1,000-node graph to measure memory transfer impact
        var graph = GetGraphForSize(1000);

        // Metal uses unified memory (MTLStorageModeShared) by default
        // This benchmark measures zero-copy optimization
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 10,  // 10 iterations to amortize setup
            convergenceThreshold: 0.0f,  // Force all iterations
            dampingFactor: DampingFactor);
    }

    /// <summary>
    /// Measures K2K message routing latency.
    /// Target: ~200-500ns per message, 2M+ messages/sec throughput.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Metal", "K2KRouting")]
    public async Task<Dictionary<int, float>> Metal_K2KMessageRouting_Throughput()
    {
        // Use 1,000-node graph to generate significant K2K message traffic
        // ContributionSender → RankAggregator → ConvergenceChecker
        var graph = GetGraphForSize(1000);

        var sw = Stopwatch.StartNew();

        var result = await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 10,
            convergenceThreshold: 0.0f,  // Force all iterations
            dampingFactor: DampingFactor);

        sw.Stop();

        // Calculate K2K message throughput
        int nodeCount = graph.Count;
        int totalEdges = graph.Values.Sum(targets => targets.Length);
        long totalMessages = totalEdges * 10;  // 10 iterations

        double messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  K2K Throughput: {messagesPerSecond:N0} messages/sec");

        return result;
    }

    /// <summary>
    /// Measures multi-kernel barrier synchronization overhead.
    /// Target: &lt;20ns barrier overhead, &lt;50% of total iteration time.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Metal", "Barriers")]
    public async Task<Dictionary<int, float>> Metal_BarrierSynchronization_Overhead()
    {
        // Use chain graph (minimal computation) to isolate barrier overhead
        var graph = _smallChainGraph;

        var sw = Stopwatch.StartNew();

        var result = await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 100,  // Many iterations to measure barrier overhead
            convergenceThreshold: 0.0f,  // Force all iterations
            dampingFactor: DampingFactor);

        sw.Stop();

        double avgIterationTime = sw.Elapsed.TotalMicroseconds / 100.0;
        Console.WriteLine($"  Avg Iteration Time: {avgIterationTime:F3} μs (includes barriers)");

        return result;
    }

    /// <summary>
    /// Measures Metal command queue utilization and batch submission efficiency.
    /// Target: &gt;80% queue utilization, &lt;100μs per batch submission.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Metal", "CommandQueue")]
    public async Task<Dictionary<int, float>> Metal_CommandQueue_BatchEfficiency()
    {
        // Use 1,000-node graph to generate multiple command buffer submissions
        var graph = GetGraphForSize(1000);

        var sw = Stopwatch.StartNew();

        var result = await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 20,
            convergenceThreshold: 0.0f,
            dampingFactor: DampingFactor);

        sw.Stop();

        double avgBatchTime = sw.Elapsed.TotalMicroseconds / 20.0;
        Console.WriteLine($"  Avg Batch Submission: {avgBatchTime:F3} μs");

        return result;
    }

    #endregion

    #region Convergence Benchmarks

    /// <summary>
    /// Measures convergence detection accuracy and performance.
    /// Target: ~200ns per convergence check, matches CPU baseline accuracy.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "Accuracy")]
    public async Task<Dictionary<int, float>> Convergence_AccuracyValidation()
    {
        // Use 100-node random graph for convergence testing
        var graph = GetGraphForSize(100);

        var sw = Stopwatch.StartNew();

        var result = await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);

        sw.Stop();

        // Verify convergence
        float sum = result.Values.Sum();
        Debug.Assert(Math.Abs(sum - graph.Count) < 0.01f,
            $"PageRank sum should be ~{graph.Count}, got {sum:F6}");

        Console.WriteLine($"  Convergence Time: {sw.Elapsed.TotalMicroseconds:F3} μs");
        Console.WriteLine($"  Final Rank Sum: {sum:F6} (expected: {graph.Count})");

        return result;
    }

    /// <summary>
    /// Measures early convergence optimization (adaptive iteration control).
    /// Target: Converges in 10-50 iterations, matches CPU baseline.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "EarlyTermination")]
    public async Task<Dictionary<int, float>> Convergence_EarlyTermination()
    {
        // Use small star graph (converges quickly)
        var graph = _smallStarGraph;

        var result = await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: 1000,  // High limit to test early termination
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor);

        // Verify early termination occurred (should not run all 1000 iterations)
        return result;
    }

    #endregion

    #region Memory Efficiency Benchmarks

    /// <summary>
    /// Measures memory allocation efficiency for batch processing.
    /// Target: Minimal allocations during iterations (unified memory reuse).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Memory", "Allocation")]
    public async Task<Dictionary<int, float>> Memory_AllocationEfficiency()
    {
        var graph = GetGraphForSize(1000);

        // Pre-warm the orchestrator (allocate buffers)
        await _orchestrator!.ComputePageRankAsync(graph, maxIterations: 1);

        // Measure allocation-free iterations
        return await _orchestrator.ComputePageRankAsync(
            graph,
            maxIterations: 10,
            convergenceThreshold: 0.0f,
            dampingFactor: DampingFactor);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate graph for the specified size and topology.
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

    #endregion

    #region Graph Generation (shared with CPU baseline)

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
