// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;

namespace DotCompute.Benchmarks.RingKernel;

/// <summary>
/// Benchmarks Metal native actor performance for PageRank using persistent Ring Kernels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 4.3: GPU Native Actor Pattern</b>
/// Tests the persistent Ring Kernel actor model where GPU kernels run continuously
/// as message-driven actors, communicating via K2K (kernel-to-kernel) messaging.
/// </para>
/// <para>
/// <b>Comparison: Batch vs Native Actor</b>
/// - Phase 4.2 (Batch): Traditional GPU execution (launch → process → return → repeat)
/// - Phase 4.3 (Actor): Persistent actors (launch once → process messages → converge → shutdown)
/// </para>
/// <para>
/// <b>Actor Architecture (3 persistent kernels):</b>
/// 1. ContributionSender: Receives MetalGraphNode → sends MetalRankContribution
/// 2. RankAggregator: Collects contributions → computes ranks → sends ConvergenceCheckResult
/// 3. ConvergenceChecker: Monitors convergence → signals completion
/// </para>
/// <para>
/// <b>Performance Targets (Apple Silicon M2+):</b>
/// - Actor launch latency: &lt;500μs (3 kernels)
/// - Message throughput: &gt;2M messages/sec
/// - K2K routing overhead: &lt;100ns per message
/// - Barrier synchronization: &lt;20ns per sync
/// - Actor shutdown latency: &lt;200μs
/// - End-to-end convergence: &lt;10ms for 1000-node graph
/// </para>
/// <para>
/// <b>Benchmark Categories:</b>
/// - ActorLifecycle: Launch, message processing, shutdown
/// - K2KMessaging: Direct GPU-to-GPU message routing
/// - Synchronization: Multi-kernel barriers
/// - Convergence: Iterative PageRank convergence
/// - Comparison: Batch vs Actor performance
/// </para>
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net90)]
public class PageRankMetalNativeActorBenchmark
{
    private const float DampingFactor = 0.85f;
    private const float ConvergenceThreshold = 0.0001f;
    private const int MaxIterations = 100;

    // Metal device infrastructure (placeholder for Phase 5)
    private IntPtr _device;
    private IntPtr _commandQueue;
    private MetalPageRankActorOrchestrator_Stub? _orchestrator;

    // Test graphs
    private Dictionary<int, int[]> _smallGraph = null!;    // 10 nodes
    private Dictionary<int, int[]> _mediumGraph = null!;   // 100 nodes
    private Dictionary<int, int[]> _largeGraph = null!;    // 1000 nodes

    /// <summary>
    /// Graph topology for parameterized benchmarks.
    /// </summary>
    public enum GraphTopology
    {
        Star,      // One central node, all others point to it
        Chain,     // Linear chain A→B→C→...
        Complete,  // Fully connected (all nodes link to all others)
        Random,    // Erdős-Rényi random graph
        Web        // Scale-free power-law distribution (realistic web graph)
    }

    [Params(GraphTopology.Star, GraphTopology.Random, GraphTopology.Web)]
    public GraphTopology Topology { get; set; }

    [Params(10, 100, 1000)]
    public int GraphSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Phase 5 TODO: Initialize actual Metal device and command queue
        // For Phase 4.3, use placeholder values
        _device = IntPtr.Zero;
        _commandQueue = IntPtr.Zero;

        // Create stub orchestrator for actor pattern testing
        _orchestrator = new MetalPageRankActorOrchestrator_Stub(_device, _commandQueue);

        // Generate test graphs
        _smallGraph = GenerateGraph(10, GraphTopology.Star);
        _mediumGraph = GenerateGraph(100, GraphTopology.Random);
        _largeGraph = GenerateGraph(1000, GraphTopology.Web);

        Console.WriteLine($"[Setup] Metal Native Actor Benchmark initialized");
        Console.WriteLine($"  Small Graph: {_smallGraph.Count} nodes, {_smallGraph.Sum(kvp => kvp.Value.Length)} edges");
        Console.WriteLine($"  Medium Graph: {_mediumGraph.Count} nodes, {_mediumGraph.Sum(kvp => kvp.Value.Length)} edges");
        Console.WriteLine($"  Large Graph: {_largeGraph.Count} nodes, {_largeGraph.Sum(kvp => kvp.Value.Length)} edges");
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_orchestrator != null)
        {
            await _orchestrator.DisposeAsync();
        }

        Console.WriteLine("[Cleanup] Metal Native Actor Benchmark disposed");
    }

    #region Actor Lifecycle Benchmarks

    /// <summary>
    /// Benchmark: Actor launch latency (all 3 kernels).
    /// Target: &lt;500μs for 3 persistent kernels.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ActorLifecycle", "Launch")]
    public async Task ActorLaunch_ThreeKernels()
    {
        await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Single message processing latency.
    /// Target: &lt;1μs per message (GPU-resident processing).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("ActorLifecycle", "MessageProcessing")]
    public async Task ActorMessageProcessing_SingleMessage()
    {
        var graph = GetGraphForSize(10);
        await _orchestrator!.ProcessSingleMessageAsync(graph, CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Actor shutdown latency (all 3 kernels).
    /// Target: &lt;200μs for graceful shutdown.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("ActorLifecycle", "Shutdown")]
    public async Task ActorShutdown_ThreeKernels()
    {
        await _orchestrator!.ShutdownActorsAsync(CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Full actor lifecycle (launch → process → shutdown).
    /// Target: &lt;1ms for 10-node graph.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("ActorLifecycle", "FullCycle")]
    public async Task ActorFullLifecycle_SmallGraph()
    {
        await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
        var graph = GetGraphForSize(10);
        await _orchestrator.ProcessGraphAsync(graph, 1, CancellationToken.None);
        await _orchestrator.ShutdownActorsAsync(CancellationToken.None);
    }

    #endregion

    #region K2K Messaging Benchmarks

    /// <summary>
    /// Benchmark: K2K message routing overhead (ContributionSender → RankAggregator).
    /// Target: &lt;100ns per message.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("K2KMessaging", "Routing")]
    public async Task<long> K2KRouting_MessageThroughput()
    {
        return await _orchestrator!.MeasureK2KRoutingLatencyAsync(1000, CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Sustained message throughput (messages/sec).
    /// Target: &gt;2M messages/sec.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("K2KMessaging", "Throughput")]
    public async Task<long> K2KMessaging_SustainedThroughput()
    {
        return await _orchestrator!.MeasureSustainedThroughputAsync(
            durationMs: 1000,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Multi-hop message routing (3-kernel pipeline).
    /// Target: &lt;300ns for full pipeline.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("K2KMessaging", "Pipeline")]
    public async Task<long> K2KMessaging_ThreeKernelPipeline()
    {
        return await _orchestrator!.MeasureThreeKernelPipelineLatencyAsync(
            messageCount: 100,
            CancellationToken.None);
    }

    #endregion

    #region Synchronization Benchmarks

    /// <summary>
    /// Benchmark: Multi-kernel barrier synchronization (3 participants).
    /// Target: &lt;20ns per sync.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Synchronization", "Barrier")]
    public async Task<long> Barrier_ThreeKernelSync()
    {
        return await _orchestrator!.MeasureBarrierSyncLatencyAsync(
            participantCount: 3,
            syncCount: 100,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Named barrier coordination (per-iteration sync).
    /// Target: &lt;50ns per iteration.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Synchronization", "NamedBarrier")]
    public async Task<long> NamedBarrier_IterationSync()
    {
        return await _orchestrator!.MeasureNamedBarrierLatencyAsync(
            barrierName: "iteration_barrier",
            syncCount: 100,
            CancellationToken.None);
    }

    #endregion

    #region Convergence Benchmarks

    /// <summary>
    /// Benchmark: PageRank convergence with native actors (10 nodes).
    /// Target: &lt;1ms for star topology.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "Small")]
    public async Task<Dictionary<int, float>> Convergence_SmallGraph_10Nodes()
    {
        var graph = GetGraphForSize(10);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: PageRank convergence with native actors (100 nodes).
    /// Target: &lt;5ms for random topology.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "Medium")]
    public async Task<Dictionary<int, float>> Convergence_MediumGraph_100Nodes()
    {
        var graph = GetGraphForSize(100);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: PageRank convergence with native actors (1000 nodes).
    /// Target: &lt;10ms for web topology.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "Large")]
    public async Task<Dictionary<int, float>> Convergence_LargeGraph_1000Nodes()
    {
        var graph = GetGraphForSize(1000);
        return await _orchestrator!.ComputePageRankAsync(
            graph,
            maxIterations: MaxIterations,
            convergenceThreshold: ConvergenceThreshold,
            dampingFactor: DampingFactor,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Early convergence detection latency.
    /// Target: &lt;200ns per convergence check.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Convergence", "Detection")]
    public async Task<long> Convergence_DetectionLatency()
    {
        return await _orchestrator!.MeasureConvergenceDetectionLatencyAsync(
            checkCount: 1000,
            CancellationToken.None);
    }

    #endregion

    #region Comparison Benchmarks (Batch vs Actor)

    /// <summary>
    /// Benchmark: Compare batch vs actor for 100-node graph.
    /// Expected: Actor should be 2-5x faster due to persistence.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Comparison", "BatchVsActor")]
    public async Task<(long batchTimeNs, long actorTimeNs)> Comparison_BatchVsActor_100Nodes()
    {
        var graph = GetGraphForSize(100);

        // Batch mode: Launch → Process → Shutdown per iteration
        var batchStart = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < 10; i++)
        {
            await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
            await _orchestrator.ProcessGraphAsync(graph, 1, CancellationToken.None);
            await _orchestrator.ShutdownActorsAsync(CancellationToken.None);
        }
        var batchEnd = System.Diagnostics.Stopwatch.GetTimestamp();
        var batchTimeNs = (batchEnd - batchStart) * 1_000_000_000 / System.Diagnostics.Stopwatch.Frequency;

        // Actor mode: Launch once → Process iterations → Shutdown once
        var actorStart = System.Diagnostics.Stopwatch.GetTimestamp();
        await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
        await _orchestrator.ProcessGraphAsync(graph, 10, CancellationToken.None);
        await _orchestrator.ShutdownActorsAsync(CancellationToken.None);
        var actorEnd = System.Diagnostics.Stopwatch.GetTimestamp();
        var actorTimeNs = (actorEnd - actorStart) * 1_000_000_000 / System.Diagnostics.Stopwatch.Frequency;

        return (batchTimeNs, actorTimeNs);
    }

    /// <summary>
    /// Benchmark: Parameterized batch vs actor comparison.
    /// Tests across different graph sizes and topologies.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Comparison", "Parameterized")]
    public async Task<double> Comparison_ActorSpeedup()
    {
        var graph = GenerateGraph(GraphSize, Topology);

        // Measure batch mode
        var batchStart = System.Diagnostics.Stopwatch.GetTimestamp();
        await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
        await _orchestrator.ProcessGraphAsync(graph, 1, CancellationToken.None);
        await _orchestrator.ShutdownActorsAsync(CancellationToken.None);
        var batchEnd = System.Diagnostics.Stopwatch.GetTimestamp();

        // Measure actor mode
        var actorStart = System.Diagnostics.Stopwatch.GetTimestamp();
        await _orchestrator!.LaunchActorsAsync(CancellationToken.None);
        await _orchestrator.ProcessGraphAsync(graph, 1, CancellationToken.None);
        await _orchestrator.ShutdownActorsAsync(CancellationToken.None);
        var actorEnd = System.Diagnostics.Stopwatch.GetTimestamp();

        var batchTime = (batchEnd - batchStart) * 1.0 / System.Diagnostics.Stopwatch.Frequency;
        var actorTime = (actorEnd - actorStart) * 1.0 / System.Diagnostics.Stopwatch.Frequency;

        return batchTime / actorTime; // Speedup factor
    }

    #endregion

    #region Helper Methods

    private Dictionary<int, int[]> GetGraphForSize(int size)
    {
        return size switch
        {
            10 => _smallGraph,
            100 => _mediumGraph,
            1000 => _largeGraph,
            _ => GenerateGraph(size, GraphTopology.Random)
        };
    }

    private Dictionary<int, int[]> GenerateGraph(int nodeCount, GraphTopology topology)
    {
        var graph = new Dictionary<int, int[]>();
        var random = new Random(42); // Fixed seed for reproducibility

        switch (topology)
        {
            case GraphTopology.Star:
                // Central node (0) receives links from all others
                for (int i = 1; i < nodeCount; i++)
                {
                    graph[i] = new[] { 0 };
                }
                graph[0] = Enumerable.Range(1, nodeCount - 1).ToArray();
                break;

            case GraphTopology.Chain:
                // Linear chain: 0→1→2→...→N-1→0
                for (int i = 0; i < nodeCount; i++)
                {
                    graph[i] = new[] { (i + 1) % nodeCount };
                }
                break;

            case GraphTopology.Complete:
                // Fully connected graph
                for (int i = 0; i < nodeCount; i++)
                {
                    graph[i] = Enumerable.Range(0, nodeCount).Where(j => j != i).ToArray();
                }
                break;

            case GraphTopology.Random:
                // Erdős-Rényi random graph (p = 0.1)
                for (int i = 0; i < nodeCount; i++)
                {
                    var targets = new List<int>();
                    for (int j = 0; j < nodeCount; j++)
                    {
                        if (i != j && random.NextDouble() < 0.1)
                        {
                            targets.Add(j);
                        }
                    }
                    graph[i] = targets.Count > 0 ? targets.ToArray() : new[] { (i + 1) % nodeCount };
                }
                break;

            case GraphTopology.Web:
                // Scale-free power-law distribution (Barabási-Albert model approximation)
                graph[0] = new[] { 1 };
                graph[1] = new[] { 0 };

                for (int i = 2; i < nodeCount; i++)
                {
                    var targets = new HashSet<int>();
                    int edgesToAdd = Math.Min(3, i); // m = 3 (preferential attachment)

                    while (targets.Count < edgesToAdd)
                    {
                        // Preferential attachment: higher degree = higher probability
                        int target = random.Next(i);
                        targets.Add(target);
                    }

                    graph[i] = targets.ToArray();
                }
                break;
        }

        return graph;
    }

    #endregion
}

#region Stub Orchestrator (Placeholder for Phase 5)

/// <summary>
/// Stub orchestrator for Metal native actor pattern testing.
/// </summary>
/// <remarks>
/// This stub provides the API surface for Phase 4.3 benchmark infrastructure.
/// Phase 5 will replace this with the actual MetalPageRankOrchestrator implementation
/// that includes:
/// - Real Metal device/queue initialization
/// - Persistent Ring Kernel actor launch
/// - K2K message routing via Metal buffers
/// - Multi-kernel barrier synchronization
/// - Convergence detection and result collection
/// </remarks>
internal sealed class MetalPageRankActorOrchestrator_Stub : IAsyncDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private bool _actorsLaunched;

    public MetalPageRankActorOrchestrator_Stub(IntPtr device, IntPtr commandQueue, ILogger? logger = null)
    {
        _device = device;
        _commandQueue = commandQueue;
        _actorsLaunched = false;
    }

    public Task LaunchActorsAsync(CancellationToken cancellationToken)
    {
        _actorsLaunched = true;
        // Stub: Simulate 500μs launch latency
        return Task.Delay(TimeSpan.FromMicroseconds(500), cancellationToken);
    }

    public Task ProcessSingleMessageAsync(Dictionary<int, int[]> graph, CancellationToken cancellationToken)
    {
        // Stub: Simulate 1μs message processing
        return Task.Delay(TimeSpan.FromMicroseconds(1), cancellationToken);
    }

    public Task ShutdownActorsAsync(CancellationToken cancellationToken)
    {
        _actorsLaunched = false;
        // Stub: Simulate 200μs shutdown latency
        return Task.Delay(TimeSpan.FromMicroseconds(200), cancellationToken);
    }

    public async Task ProcessGraphAsync(Dictionary<int, int[]> graph, int iterations, CancellationToken cancellationToken)
    {
        // Stub: Simulate graph processing
        for (int i = 0; i < iterations; i++)
        {
            await Task.Delay(TimeSpan.FromMicroseconds(100), cancellationToken);
        }
    }

    public Task<long> MeasureK2KRoutingLatencyAsync(int messageCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 100ns per message
        long totalNs = messageCount * 100L;
        return Task.FromResult(totalNs);
    }

    public Task<long> MeasureSustainedThroughputAsync(int durationMs, CancellationToken cancellationToken)
    {
        // Stub: Simulate 2M messages/sec
        long messagesPerSec = 2_000_000;
        long totalMessages = messagesPerSec * durationMs / 1000;
        return Task.FromResult(totalMessages);
    }

    public Task<long> MeasureThreeKernelPipelineLatencyAsync(int messageCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 300ns for 3-kernel pipeline
        long totalNs = messageCount * 300L;
        return Task.FromResult(totalNs);
    }

    public Task<long> MeasureBarrierSyncLatencyAsync(int participantCount, int syncCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 20ns per sync
        long totalNs = syncCount * 20L;
        return Task.FromResult(totalNs);
    }

    public Task<long> MeasureNamedBarrierLatencyAsync(string barrierName, int syncCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 50ns per named barrier sync
        long totalNs = syncCount * 50L;
        return Task.FromResult(totalNs);
    }

    public Task<Dictionary<int, float>> ComputePageRankAsync(
        Dictionary<int, int[]> graph,
        int maxIterations,
        float convergenceThreshold,
        float dampingFactor,
        CancellationToken cancellationToken)
    {
        // Stub: Return empty results for Phase 4.3
        return Task.FromResult(new Dictionary<int, float>());
    }

    public Task<long> MeasureConvergenceDetectionLatencyAsync(int checkCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 200ns per convergence check
        long totalNs = checkCount * 200L;
        return Task.FromResult(totalNs);
    }

    public ValueTask DisposeAsync()
    {
        if (_actorsLaunched)
        {
            _actorsLaunched = false;
        }
        return ValueTask.CompletedTask;
    }
}

#endregion
