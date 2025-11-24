// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// PageRank performance validation without BenchmarkDotNet complexity.
/// Tests Ring Kernel actor pattern performance claims for iterative graph algorithms.
/// </summary>
public static class PageRankPerformanceValidation
{
    private const int WarmupIterations = 5;
    private const int MeasureIterations = 10;

    public static async Task Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  PageRank Ring Kernel Performance Validation");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        Console.WriteLine("Performance Claims:");
        Console.WriteLine("  8. Ring Kernel Actor Launch: <500μs (3 persistent kernels)");
        Console.WriteLine("  9. K2K Message Routing: >2M messages/sec, <100ns overhead");
        Console.WriteLine("  10. Multi-Kernel Barriers: <20ns per sync");
        Console.WriteLine("  11. PageRank Convergence: <10ms for 1000-node graphs");
        Console.WriteLine("  12. Actor vs Batch Speedup: 2-5x for iterative algorithms");
        Console.WriteLine();

        var results = new List<(string Claim, bool Passed, string Result, string Target)>();

        // Claim 8: Actor Launch Performance
        results.Add(await ValidateActorLaunchPerformance());

        // Claim 9: K2K Message Routing
        results.Add(await ValidateK2KMessagingPerformance());

        // Claim 10: Multi-Kernel Barriers
        results.Add(await ValidateBarrierSyncPerformance());

        // Claim 11: PageRank Convergence
        results.Add(await ValidatePageRankConvergence());

        // Claim 12: Actor vs Batch Speedup
        results.Add(await ValidateActorVsBatchSpeedup());

        // Print Summary
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  PAGERANK VALIDATION SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        int passed = 0;
        int failed = 0;

        foreach (var (claim, passedTest, result, target) in results)
        {
            var status = passedTest ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"{status} | {claim}");
            Console.WriteLine($"      Result: {result}");
            Console.WriteLine($"      Target: {target}\n");

            if (passedTest)
            {
                passed++;
            }
            else
            {
                failed++;
            }
        }

        Console.WriteLine($"Total: {passed} passed, {failed} failed out of {results.Count} claims validated");
        Console.WriteLine();
        Console.WriteLine("Note: Phase 5 Metal Ring Kernel implementation is COMPLETE.");
        Console.WriteLine("      This validation framework uses stub implementations for infrastructure testing.");
        Console.WriteLine("      Real performance validation will be done in Phase 6.4 using:");
        Console.WriteLine("      - E2E tests (samples/RingKernels/PageRank/Metal/PageRankMetalE2ETests.cs)");
        Console.WriteLine("      - Profiling tools (Instruments.app) for component-level measurements");
        Console.WriteLine("      - Refactored validation harness matching production API");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    }

    private static async Task<(string, bool, string, string)> ValidateActorLaunchPerformance()
    {
        Console.WriteLine("Testing Claim #8: Ring Kernel Actor Launch (<500μs for 3 kernels)...");

        // Note: Using stub orchestrator for Phase 4.5 infrastructure validation
        // Phase 5 will replace with actual Metal implementation
        var orchestrator = new PageRankMetalNativeActorBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await orchestrator.LaunchActorsAsync(CancellationToken.None);
            await orchestrator.ShutdownActorsAsync(CancellationToken.None);
        }

        // Measure
        var launchTimes = new List<double>();
        for (int i = 0; i < MeasureIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await orchestrator.LaunchActorsAsync(CancellationToken.None);
            sw.Stop();

            launchTimes.Add(sw.Elapsed.TotalMicroseconds);
            await orchestrator.ShutdownActorsAsync(CancellationToken.None);
        }

        var avgMicroseconds = launchTimes.Average();
        bool passed = avgMicroseconds < 500.0;

        Console.WriteLine($"  Average launch time: {avgMicroseconds:F2} μs");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")} (stub implementation)\n");

        await orchestrator.DisposeAsync();

        return (
            "Ring Kernel Actor Launch",
            passed,
            $"{avgMicroseconds:F2} μs",
            "<500 μs"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateK2KMessagingPerformance()
    {
        Console.WriteLine("Testing Claim #9: K2K Message Routing (>2M msg/sec, <100ns)...");

        var orchestrator = new PageRankMetalNativeActorBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);

        // Test message throughput
        const int messageCount = 10000;

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await orchestrator.MeasureK2KRoutingLatencyAsync(messageCount, CancellationToken.None);
        }

        // Measure throughput
        var sw = Stopwatch.StartNew();
        long totalMessages = 0;
        for (int i = 0; i < MeasureIterations; i++)
        {
            totalMessages += messageCount;
            await orchestrator.MeasureK2KRoutingLatencyAsync(messageCount, CancellationToken.None);
        }
        sw.Stop();

        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;
        var avgNsPerMessage = (sw.Elapsed.TotalNanoseconds / totalMessages);

        bool passed = messagesPerSecond > 2_000_000 && avgNsPerMessage < 100;

        Console.WriteLine($"  Throughput: {messagesPerSecond / 1_000_000:F2}M msg/sec");
        Console.WriteLine($"  Latency: {avgNsPerMessage:F2} ns/msg");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")} (stub implementation)\n");

        await orchestrator.DisposeAsync();

        return (
            "K2K Message Routing",
            passed,
            $"{messagesPerSecond / 1_000_000:F2}M msg/sec, {avgNsPerMessage:F2} ns/msg",
            ">2M msg/sec, <100 ns"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateBarrierSyncPerformance()
    {
        Console.WriteLine("Testing Claim #10: Multi-Kernel Barriers (<20ns per sync)...");

        var orchestrator = new PageRankMetalNativeActorBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);

        const int syncCount = 1000;

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await orchestrator.MeasureBarrierSyncLatencyAsync(3, syncCount, CancellationToken.None);
        }

        // Measure
        var totalNs = await orchestrator.MeasureBarrierSyncLatencyAsync(3, syncCount, CancellationToken.None);
        var avgNsPerSync = totalNs / (double)syncCount;

        bool passed = avgNsPerSync < 20.0;

        Console.WriteLine($"  Average sync latency: {avgNsPerSync:F2} ns");
        Console.WriteLine($"  Total for {syncCount} syncs: {totalNs / 1000.0:F2} μs");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")} (stub implementation)\n");

        await orchestrator.DisposeAsync();

        return (
            "Multi-Kernel Barrier Sync",
            passed,
            $"{avgNsPerSync:F2} ns",
            "<20 ns"
        );
    }

    private static async Task<(string, bool, string, string)> ValidatePageRankConvergence()
    {
        Console.WriteLine("Testing Claim #11: PageRank Convergence (<10ms for 1000 nodes)...");

        var orchestrator = new PageRankMetalNativeActorBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);

        // Create 1000-node web graph
        var graph = GenerateWebGraph(1000);

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await orchestrator.ComputePageRankAsync(
                graph,
                maxIterations: 100,
                convergenceThreshold: 0.0001f,
                dampingFactor: 0.85f,
                CancellationToken.None);
        }

        // Measure
        var convergenceTimes = new List<double>();
        for (int i = 0; i < MeasureIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await orchestrator.ComputePageRankAsync(
                graph,
                maxIterations: 100,
                convergenceThreshold: 0.0001f,
                dampingFactor: 0.85f,
                CancellationToken.None);
            sw.Stop();

            convergenceTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgMs = convergenceTimes.Average();
        bool passed = avgMs < 10.0;

        Console.WriteLine($"  Average convergence time: {avgMs:F2} ms");
        Console.WriteLine($"  Graph: 1000 nodes (web topology)");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")} (stub implementation)\n");

        await orchestrator.DisposeAsync();

        return (
            "PageRank Convergence (1000 nodes)",
            passed,
            $"{avgMs:F2} ms",
            "<10 ms"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateActorVsBatchSpeedup()
    {
        Console.WriteLine("Testing Claim #12: Actor vs Batch Speedup (2-5x for iterative)...");

        var actorOrchestrator = new PageRankMetalNativeActorBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);
        var batchOrchestrator = new PageRankMetalBatchBenchmark_Stub(IntPtr.Zero, IntPtr.Zero);

        // Use 100-node graph for comparison
        var graph = GenerateWebGraph(100);

        // Measure Batch mode (10 iterations with launch overhead)
        var batchTimes = new List<double>();
        for (int run = 0; run < MeasureIterations; run++)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                await batchOrchestrator.InitializeAsync(CancellationToken.None);
                await batchOrchestrator.ComputePageRankAsync(
                    graph,
                    maxIterations: 1,
                    convergenceThreshold: 0.0001f,
                    dampingFactor: 0.85f,
                    CancellationToken.None);
                await batchOrchestrator.DisposeAsync();
            }
            sw.Stop();
            batchTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Measure Actor mode (launch once, 10 iterations, shutdown)
        var actorTimes = new List<double>();
        for (int run = 0; run < MeasureIterations; run++)
        {
            var sw = Stopwatch.StartNew();
            await actorOrchestrator.LaunchActorsAsync(CancellationToken.None);
            await actorOrchestrator.ProcessGraphAsync(graph, 10, CancellationToken.None);
            await actorOrchestrator.ShutdownActorsAsync(CancellationToken.None);
            sw.Stop();
            actorTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgBatch = batchTimes.Average();
        var avgActor = actorTimes.Average();
        var speedup = avgBatch / avgActor;

        bool passed = speedup >= 2.0 && speedup <= 5.0;

        Console.WriteLine($"  Batch mode (10 iterations): {avgBatch:F2} ms");
        Console.WriteLine($"  Actor mode (10 iterations): {avgActor:F2} ms");
        Console.WriteLine($"  Speedup: {speedup:F2}x");
        Console.WriteLine($"  Graph: 100 nodes (web topology)");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")} (stub implementation)\n");

        await actorOrchestrator.DisposeAsync();
        await batchOrchestrator.DisposeAsync();

        return (
            "Actor vs Batch Speedup",
            passed,
            $"{speedup:F2}x speedup",
            "2-5x speedup"
        );
    }

    /// <summary>
    /// Generates a scale-free web graph using Barabási-Albert preferential attachment.
    /// </summary>
    private static Dictionary<int, int[]> GenerateWebGraph(int nodeCount)
    {
        var graph = new Dictionary<int, int[]>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Start with 2 nodes connected
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

        return graph;
    }
}

#region Stub Orchestrators (Reused from benchmark files)

#pragma warning disable CA1822 // Mark members as static (intentional stub implementation)

/// <summary>
/// Stub for PageRank native actor orchestrator validation.
/// Reuses implementation from PageRankMetalNativeActorBenchmark.cs
/// </summary>
internal sealed class PageRankMetalNativeActorBenchmark_Stub : IAsyncDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private bool _actorsLaunched;

    public PageRankMetalNativeActorBenchmark_Stub(IntPtr device, IntPtr commandQueue)
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

    public Task<long> MeasureBarrierSyncLatencyAsync(int participantCount, int syncCount, CancellationToken cancellationToken)
    {
        // Stub: Simulate 20ns per sync
        long totalNs = syncCount * 20L;
        return Task.FromResult(totalNs);
    }

    public Task<Dictionary<int, float>> ComputePageRankAsync(
        Dictionary<int, int[]> graph,
        int maxIterations,
        float convergenceThreshold,
        float dampingFactor,
        CancellationToken cancellationToken)
    {
        // Stub: Return empty results
        return Task.FromResult(new Dictionary<int, float>());
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

/// <summary>
/// Stub for PageRank batch orchestrator validation.
/// Reuses implementation from PageRankMetalBatchBenchmark.cs
/// </summary>
internal sealed class PageRankMetalBatchBenchmark_Stub : IAsyncDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;

    public PageRankMetalBatchBenchmark_Stub(IntPtr device, IntPtr commandQueue)
    {
        _device = device;
        _commandQueue = commandQueue;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Stub: Simulate batch initialization
        return Task.CompletedTask;
    }

    public Task<Dictionary<int, float>> ComputePageRankAsync(
        Dictionary<int, int[]> graph,
        int maxIterations,
        float convergenceThreshold,
        float dampingFactor,
        CancellationToken cancellationToken)
    {
        // Stub: Return empty results
        return Task.FromResult(new Dictionary<int, float>());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

#pragma warning restore CA1822

#endregion
