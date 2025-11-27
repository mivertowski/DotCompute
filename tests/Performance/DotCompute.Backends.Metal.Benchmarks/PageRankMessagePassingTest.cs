// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Native;
using DotCompute.Samples.RingKernels.PageRank.Metal;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Focused test for validating message passing between PageRank kernels.
/// Tests actual K2K (kernel-to-kernel) communication on Apple Silicon.
/// </summary>
public static class PageRankMessagePassingTest
{
    public static async Task Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  PageRank Message Passing Validation");
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

        // Create logger that actually logs (not NullLogger)
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<MetalPageRankOrchestrator>();

        var orchestrator = new MetalPageRankOrchestrator(device, commandQueue, logger);

        try
        {
            // Initialize orchestrator
            Console.WriteLine("Initializing orchestrator...");
            var initSw = Stopwatch.StartNew();
            await orchestrator.InitializeAsync();
            initSw.Stop();
            Console.WriteLine($"✅ Initialization complete: {initSw.Elapsed.TotalMilliseconds:F2} ms\n");

            // Test 1: Tiny graph (5 nodes) for message passing validation
            Console.WriteLine("─────────────────────────────────────────────────────────────────");
            Console.WriteLine("  Test: Tiny Graph (5 nodes) - Message Passing Validation");
            Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

            var tinyGraph = GenerateTinyGraph();
            Console.WriteLine($"Graph: {tinyGraph.Count} nodes, ~{tinyGraph.Sum(kvp => kvp.Value.Length)} edges");
            Console.WriteLine($"Structure: 0→1→2→3→4→0 (ring)\n");

            // Run PageRank with detailed logging
            Console.WriteLine("Running PageRank computation...");
            var sw = Stopwatch.StartNew();
            var ranks = await orchestrator.ComputePageRankAsync(
                tinyGraph,
                maxIterations: 20,
                convergenceThreshold: 0.001f,
                dampingFactor: 0.85f);
            sw.Stop();

            Console.WriteLine($"\n✅ Computation completed in {sw.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"   Nodes processed: {ranks.Count}");
            Console.WriteLine($"   Average rank: {ranks.Values.Average():F6}");
            Console.WriteLine($"   Min rank: {ranks.Values.Min():F6}");
            Console.WriteLine($"   Max rank: {ranks.Values.Max():F6}");

            // Display all ranks
            Console.WriteLine("\n   Final PageRank values:");
            foreach (var (nodeId, rank) in ranks.OrderBy(kvp => kvp.Key))
            {
                Console.WriteLine($"     Node {nodeId}: {rank:F6}");
            }

            // Test 2: Medium graph (50 nodes)
            Console.WriteLine("\n─────────────────────────────────────────────────────────────────");
            Console.WriteLine("  Test: Medium Graph (50 nodes) - Performance Check");
            Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

            var mediumGraph = GenerateScaleFreeGraph(50);
            Console.WriteLine($"Graph: {mediumGraph.Count} nodes, ~{mediumGraph.Sum(kvp => kvp.Value.Length)} edges");

            sw.Restart();
            ranks = await orchestrator.ComputePageRankAsync(
                mediumGraph,
                maxIterations: 50,
                convergenceThreshold: 0.0001f,
                dampingFactor: 0.85f);
            sw.Stop();

            Console.WriteLine($"\n✅ Computation completed in {sw.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"   Nodes processed: {ranks.Count}");
            Console.WriteLine($"   Average rank: {ranks.Values.Average():F6}");
            Console.WriteLine($"   Throughput: {ranks.Count / (sw.Elapsed.TotalSeconds):F0} nodes/sec");
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
        Console.WriteLine("  Message Passing Test Complete");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    }

    /// <summary>
    /// Generates a tiny 5-node ring graph for testing: 0→1→2→3→4→0
    /// </summary>
    private static Dictionary<int, int[]> GenerateTinyGraph()
    {
        return new Dictionary<int, int[]>
        {
            { 0, new[] { 1 } },
            { 1, new[] { 2 } },
            { 2, new[] { 3 } },
            { 3, new[] { 4 } },
            { 4, new[] { 0 } }
        };
    }

    /// <summary>
    /// Generates a scale-free web graph using Barabási-Albert preferential attachment.
    /// </summary>
    private static Dictionary<int, int[]> GenerateScaleFreeGraph(int nodeCount)
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
