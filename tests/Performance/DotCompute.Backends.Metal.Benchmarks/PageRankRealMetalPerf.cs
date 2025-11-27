// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Native;
using DotCompute.Samples.RingKernels.PageRank.Metal;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Real Metal Ring Kernel performance measurement on Apple Silicon.
/// Tests actual GPU performance of PageRank implementation.
/// </summary>
public static class PageRankRealMetalPerf
{
    public static async Task Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  PageRank Metal Ring Kernel - Real Performance Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        // Create Metal device
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            Console.WriteLine("❌ ERROR: No Metal device available!");
            return;
        }

        Console.WriteLine("✅ Metal device created successfully");

        var commandQueue = MetalNative.CreateCommandQueue(device);
        Console.WriteLine("✅ Command queue created\n");

        var logger = NullLogger<MetalPageRankOrchestrator>.Instance;
        var orchestrator = new MetalPageRankOrchestrator(device, commandQueue, logger);

        try
        {
            // Initialize orchestrator (compiles kernels, sets up routing)
            Console.WriteLine("Initializing Metal PageRank orchestrator...");
            var initSw = Stopwatch.StartNew();
            await orchestrator.InitializeAsync();
            initSw.Stop();
            Console.WriteLine($"✅ Initialization complete: {initSw.Elapsed.TotalMilliseconds:F2} ms\n");

            // Test 1: Small graph (100 nodes)
            await TestGraphSize(orchestrator, 100, "Small");

            // Test 2: Medium graph (1000 nodes)
            await TestGraphSize(orchestrator, 1000, "Medium");

            // Test 3: Large graph (10000 nodes)
            await TestGraphSize(orchestrator, 10000, "Large");
        }
        finally
        {
            await orchestrator.DisposeAsync();
            if (commandQueue != IntPtr.Zero)
            {
                MetalNative.ReleaseCommandQueue(commandQueue);
            }
            if (device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  Performance Test Complete");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    }

    private static async Task TestGraphSize(
        MetalPageRankOrchestrator orchestrator,
        int nodeCount,
        string sizeName)
    {
        Console.WriteLine($"─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"  Test: {sizeName} Graph ({nodeCount:N0} nodes)");
        Console.WriteLine($"─────────────────────────────────────────────────────────────────\n");

        // Generate scale-free web graph
        var graph = GenerateWebGraph(nodeCount);
        Console.WriteLine($"Generated {sizeName.ToUpperInvariant()} scale-free graph: {nodeCount:N0} nodes, ~{graph.Sum(kvp => kvp.Value.Length):N0} edges");

        // Warmup run
        Console.WriteLine("Warming up GPU...");
        await orchestrator.ComputePageRankAsync(
            graph,
            maxIterations: 10,
            convergenceThreshold: 0.0001f,
            dampingFactor: 0.85f);

        // Measure 5 runs
        var times = new List<double>();
        Console.WriteLine($"Running 5 timed iterations...");

        for (int run = 1; run <= 5; run++)
        {
            var sw = Stopwatch.StartNew();
            var ranks = await orchestrator.ComputePageRankAsync(
                graph,
                maxIterations: 100,
                convergenceThreshold: 0.0001f,
                dampingFactor: 0.85f);
            sw.Stop();

            times.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"  Run {run}: {sw.Elapsed.TotalMilliseconds:F2} ms ({ranks.Count:N0} nodes ranked)");
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        var maxMs = times.Max();
        var stdDev = Math.Sqrt(times.Select(t => Math.Pow(t - avgMs, 2)).Average());

        Console.WriteLine($"\nResults for {sizeName} Graph ({nodeCount:N0} nodes):");
        Console.WriteLine($"  Average: {avgMs:F2} ms");
        Console.WriteLine($"  Min:     {minMs:F2} ms");
        Console.WriteLine($"  Max:     {maxMs:F2} ms");
        Console.WriteLine($"  StdDev:  {stdDev:F2} ms");
        Console.WriteLine($"  Throughput: {nodeCount / (avgMs / 1000.0):F0} nodes/sec\n");
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
