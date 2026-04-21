// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Numerics;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotCompute.Samples.PageRank.Cpu;

/// <summary>
/// CPU PageRank - the v1.0 CPU backend running dense PageRank on a 1000-node synthetic
/// graph. Designed to produce numerically comparable output to the CUDA sample at
/// samples/RingKernels/PageRank/Cuda/SimpleExample/ so developers can validate
/// equivalence across backends.
/// </summary>
internal static class Program
{
    private const int NodeCount = 1000;
    private const int MaxIterations = 100;
    private const float DampingFactor = 0.85f;
    private const float ConvergenceThreshold = 1e-4f;
    private const int Seed = 42;

    private static async Task<int> Main()
    {
        Console.WriteLine("===========================================================");
        Console.WriteLine("  DotCompute CPU PageRank - SIMD-Accelerated Sample");
        Console.WriteLine("===========================================================");
        Console.WriteLine();

        Console.WriteLine($"Generating synthetic graph: {NodeCount} nodes, seeded ({Seed})");
        var edges = BuildGraph(NodeCount, averageOutDegree: 8, seed: Seed);
        var edgeCount = edges.Sum(e => e.Length);
        Console.WriteLine($"Graph built: {edgeCount:N0} edges, avg out-degree {(double)edgeCount / NodeCount:F2}");
        Console.WriteLine();

        // CPU backend initialization - always available, no capability gating required.
        var acceleratorOptions = Options.Create(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true,
            MaxWorkGroupSize = Environment.ProcessorCount,
        });
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount,
        });

        await using var accelerator = new CpuAccelerator(
            acceleratorOptions,
            threadPoolOptions,
            NullLogger<CpuAccelerator>.Instance);

        Console.WriteLine($"Accelerator: {accelerator.Info.Name}");
        Console.WriteLine($"  Compute units   : {accelerator.Info.ComputeUnits}");
        Console.WriteLine($"  Unified memory  : {accelerator.Info.IsUnifiedMemory}");
        if (accelerator.Info.Capabilities?.TryGetValue("SimdWidth", out var simd) == true)
        {
            Console.WriteLine($"  SIMD width      : {simd} bits");
        }
        if (accelerator.Info.Capabilities?.TryGetValue("SimdInstructionSets", out var sets) == true
            && sets is IReadOnlySet<string> isaSets)
        {
            Console.WriteLine($"  SIMD ISA        : {string.Join(", ", isaSets)}");
        }
        Console.WriteLine();

        Console.WriteLine($"Running PageRank: max {MaxIterations} iterations, threshold {ConvergenceThreshold}");
        var ranks = new float[NodeCount];
        Array.Fill(ranks, 1.0f / NodeCount);
        var nextRanks = new float[NodeCount];
        var outDegree = edges.Select(e => e.Length).ToArray();

        var stopwatch = Stopwatch.StartNew();
        var iterations = 0;
        var finalDelta = 0.0f;
        for (iterations = 0; iterations < MaxIterations; iterations++)
        {
            VectorFill(nextRanks, (1.0f - DampingFactor) / NodeCount);
            for (var src = 0; src < NodeCount; src++)
            {
                if (outDegree[src] == 0)
                {
                    continue;
                }
                var contribution = DampingFactor * ranks[src] / outDegree[src];
                var targets = edges[src];
                foreach (var dst in targets)
                {
                    nextRanks[dst] += contribution;
                }
            }

            finalDelta = VectorMaxAbsDelta(ranks, nextRanks);
            (ranks, nextRanks) = (nextRanks, ranks);
            if (finalDelta < ConvergenceThreshold)
            {
                iterations++;
                break;
            }
        }
        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"Converged in {iterations} iterations ({stopwatch.Elapsed.TotalMilliseconds:F2} ms)");
        Console.WriteLine($"Final max delta: {finalDelta:E3}");
        Console.WriteLine($"Rank sum       : {ranks.Sum():F6}  (expected ~1.0)");
        Console.WriteLine();

        Console.WriteLine("Top 10 nodes:");
        Console.WriteLine("  Rank | Node   | Score");
        Console.WriteLine("  -----+--------+----------");
        var topN = Enumerable.Range(0, NodeCount)
            .Select(i => (Node: i, Score: ranks[i]))
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToArray();
        for (var i = 0; i < topN.Length; i++)
        {
            Console.WriteLine($"  #{i + 1,-3} | {topN[i].Node,6} | {topN[i].Score:F6}");
        }
        Console.WriteLine();
        Console.WriteLine("Done. Compare with Cuda/SimpleExample for cross-backend validation");
        Console.WriteLine("(numeric tolerance: ~1e-4 across backends, rank ordering should match).");
        return 0;
    }

    /// <summary>
    /// Builds a reproducible directed graph with the given average out-degree.
    /// Used by the CPU and CUDA samples with an identical seed so output ranks are comparable.
    /// </summary>
    private static int[][] BuildGraph(int nodes, int averageOutDegree, int seed)
    {
#pragma warning disable CA5394
        var rng = new Random(seed);
        var graph = new int[nodes][];
        for (var i = 0; i < nodes; i++)
        {
            var degree = Math.Max(1, (int)(rng.NextDouble() * averageOutDegree * 2));
            var targets = new HashSet<int>();
            while (targets.Count < degree)
            {
                var candidate = rng.Next(nodes);
                if (candidate != i)
                {
                    _ = targets.Add(candidate);
                }
            }
            graph[i] = [.. targets];
        }
        return graph;
#pragma warning restore CA5394
    }

    private static void VectorFill(float[] array, float value)
    {
        var width = Vector<float>.Count;
        var broadcast = new Vector<float>(value);
        var i = 0;
        for (; i <= array.Length - width; i += width)
        {
            broadcast.CopyTo(array, i);
        }
        for (; i < array.Length; i++)
        {
            array[i] = value;
        }
    }

    private static float VectorMaxAbsDelta(float[] a, float[] b)
    {
        var n = a.Length;
        var width = Vector<float>.Count;
        var max = Vector<float>.Zero;
        var i = 0;
        for (; i <= n - width; i += width)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, i);
            max = Vector.Max(max, Vector.Abs(va - vb));
        }
        var best = 0.0f;
        for (var k = 0; k < width; k++)
        {
            best = MathF.Max(best, max[k]);
        }
        for (; i < n; i++)
        {
            best = MathF.Max(best, MathF.Abs(a[i] - b[i]));
        }
        return best;
    }
}
