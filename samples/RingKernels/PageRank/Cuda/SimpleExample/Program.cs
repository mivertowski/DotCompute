// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Samples.PageRank.Cuda;

/// <summary>
/// CUDA PageRank — the v1.0 CUDA backend running dense PageRank on a 1000-node
/// synthetic graph using real CUDA kernels compiled at runtime via NVRTC.
///
/// <para>
/// This sample is the GPU counterpart to the CPU sample at
/// <c>samples/RingKernels/PageRank/Cpu/SimpleExample/</c>. Both use the same seeded
/// graph so the rank results should match to ~1e-4 — developers can run both and diff
/// the top-10 rankings to validate cross-backend equivalence.
/// </para>
///
/// <para>
/// The sample gracefully skips with an informative message when CUDA hardware is not
/// available, mirroring the gating pattern used by the hardware test suite.
/// </para>
/// </summary>
internal static class Program
{
    private const int NodeCount = 1000;
    private const int MaxIterations = 100;
    private const float DampingFactor = 0.85f;
    private const float ConvergenceThreshold = 1e-4f;
    private const int Seed = 42;

    // Three kernels: initialize ranks with a broadcast value, distribute contributions
    // via CSR scatter, and reduce per-block max |delta|.
    // Written to be CC 5.0+ compatible so it runs unmodified on any Maxwell-or-newer GPU.
    private const string PageRankKernelSource = """
        extern "C" __global__ void pagerank_init(float* ranks, int n, float value) {
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < n) { ranks[idx] = value; }
        }

        extern "C" __global__ void pagerank_iterate(
            const int* rowOffsets,  // size n+1, CSR row offsets into edgeTargets
            const int* edgeTargets, // size edgeCount
            const int* outDegree,   // size n
            const float* ranks,     // current ranks, size n
            float* nextRanks,       // updated ranks, size n
            int n,
            float damping)
        {
            int src = blockIdx.x * blockDim.x + threadIdx.x;
            if (src >= n) { return; }
            if (outDegree[src] == 0) { return; }
            float contribution = damping * ranks[src] / (float)outDegree[src];
            int start = rowOffsets[src];
            int end = rowOffsets[src + 1];
            for (int e = start; e < end; e++) {
                atomicAdd(&nextRanks[edgeTargets[e]], contribution);
            }
        }

        extern "C" __global__ void pagerank_max_delta(
            const float* a, const float* b, float* out, int n)
        {
            __shared__ float sdata[256];
            int tid = threadIdx.x;
            int i = blockIdx.x * blockDim.x + threadIdx.x;
            float local = (i < n) ? fabsf(a[i] - b[i]) : 0.0f;
            sdata[tid] = local;
            __syncthreads();
            for (int s = blockDim.x / 2; s > 0; s >>= 1) {
                if (tid < s) { sdata[tid] = fmaxf(sdata[tid], sdata[tid + s]); }
                __syncthreads();
            }
            if (tid == 0) { out[blockIdx.x] = sdata[0]; }
        }
        """;

    private static async Task<int> Main()
    {
        Console.WriteLine("===========================================================");
        Console.WriteLine("  DotCompute CUDA PageRank — GPU Accelerated Sample");
        Console.WriteLine("===========================================================");
        Console.WriteLine();

        // 1. Gate on CUDA hardware availability. We do NOT throw — the sample is
        //    explicitly designed so `dotnet run` works on a box without a GPU (and
        //    exits cleanly), which matches the skip pattern used by the hardware tests.
        using var factory = new CudaAcceleratorFactory();
        if (!factory.IsAvailable())
        {
            Console.WriteLine("CUDA hardware not available on this system.");
            Console.WriteLine();
            Console.WriteLine("This sample requires:");
            Console.WriteLine("  - An NVIDIA GPU with Compute Capability 5.0 or newer");
            Console.WriteLine("  - CUDA Toolkit 12.0+ installed");
            Console.WriteLine("  - On WSL2, set LD_LIBRARY_PATH=/usr/lib/wsl/lib:$LD_LIBRARY_PATH");
            Console.WriteLine();
            Console.WriteLine("To verify, run:  /usr/local/cuda/bin/nvcc --version");
            Console.WriteLine();
            Console.WriteLine("Skipping CUDA path; try the CPU sample for the same graph on CPU:");
            Console.WriteLine("  dotnet run --project samples/RingKernels/PageRank/Cpu/SimpleExample");
            return 0;
        }

        // 2. Create the CUDA accelerator. Using CudaAccelerator directly is the simplest
        //    public-API path for a single-device sample. Production apps typically
        //    register this via services.AddDotComputeRuntime().
        await using var accelerator = new CudaAccelerator(
            deviceId: 0, logger: NullLogger<CudaAccelerator>.Instance);

        Console.WriteLine($"Accelerator: {accelerator.Info.Name}");
        Console.WriteLine($"  Device ID       : {accelerator.DeviceId}");
        Console.WriteLine($"  Compute cap.    : {accelerator.Device.ComputeCapability.Major}.{accelerator.Device.ComputeCapability.Minor}");
        Console.WriteLine($"  Global memory   : {accelerator.Info.GlobalMemorySize / (1024.0 * 1024.0 * 1024.0):F2} GB");
        Console.WriteLine();

        // 3. Build the same synthetic graph as the CPU sample, in CSR form for GPU upload.
        Console.WriteLine($"Generating synthetic graph: {NodeCount} nodes, seeded ({Seed})");
        var (rowOffsets, edgeTargets, outDegree) = BuildCsrGraph(NodeCount, 8, Seed);
        Console.WriteLine($"Graph built: {edgeTargets.Length:N0} edges, avg out-degree {(double)edgeTargets.Length / NodeCount:F2}");
        Console.WriteLine();

        // 4. Allocate device buffers via the unified memory API.
        await using var rowOffsetsBuf = await accelerator.Memory.AllocateAsync<int>(rowOffsets.Length);
        await using var edgeTargetsBuf = await accelerator.Memory.AllocateAsync<int>(edgeTargets.Length);
        await using var outDegreeBuf = await accelerator.Memory.AllocateAsync<int>(outDegree.Length);
        await using var ranksBuf = await accelerator.Memory.AllocateAsync<float>(NodeCount);
        await using var nextRanksBuf = await accelerator.Memory.AllocateAsync<float>(NodeCount);
        const int BlockSize = 256;
        var blockCount = (NodeCount + BlockSize - 1) / BlockSize;
        await using var deltaBuf = await accelerator.Memory.AllocateAsync<float>(blockCount);

        await rowOffsetsBuf.CopyFromAsync(rowOffsets.AsMemory());
        await edgeTargetsBuf.CopyFromAsync(edgeTargets.AsMemory());
        await outDegreeBuf.CopyFromAsync(outDegree.AsMemory());

        // 5. Compile the three kernels via NVRTC.
        Console.WriteLine("Compiling CUDA kernels via NVRTC...");
        var initKernel = await accelerator.CompileKernelAsync(
            new KernelDefinition("pagerank_init", PageRankKernelSource, "pagerank_init"));
        var iterateKernel = await accelerator.CompileKernelAsync(
            new KernelDefinition("pagerank_iterate", PageRankKernelSource, "pagerank_iterate"));
        var maxDeltaKernel = await accelerator.CompileKernelAsync(
            new KernelDefinition("pagerank_max_delta", PageRankKernelSource, "pagerank_max_delta"));
        Console.WriteLine("Kernels compiled successfully.");
        Console.WriteLine();

        // 6. Run the iteration loop on the GPU.
        Console.WriteLine($"Running PageRank: max {MaxIterations} iterations, threshold {ConvergenceThreshold}");
        var launchConfig = new LaunchConfiguration
        {
            GridSize = new Dim3(blockCount),
            BlockSize = new Dim3(BlockSize),
        };
        await initKernel.LaunchAsync<float>(launchConfig, ranksBuf, NodeCount, 1.0f / NodeCount);

        var stopwatch = Stopwatch.StartNew();
        var iterations = 0;
        var finalDelta = 0.0f;
        var deltas = new float[blockCount];

        // Alias the two buffers with local variables so we can swap them each iteration
        // without a device-side copy — reusing the same storage is the standard PageRank
        // optimization on GPU.
        var current = ranksBuf;
        var next = nextRanksBuf;
        for (iterations = 0; iterations < MaxIterations; iterations++)
        {
            var teleport = (1.0f - DampingFactor) / NodeCount;
            await initKernel.LaunchAsync<float>(launchConfig, next, NodeCount, teleport);
            await iterateKernel.LaunchAsync<float>(
                launchConfig,
                rowOffsetsBuf, edgeTargetsBuf, outDegreeBuf,
                current, next, NodeCount, DampingFactor);

            await maxDeltaKernel.LaunchAsync<float>(launchConfig, current, next, deltaBuf, NodeCount);
            await accelerator.SynchronizeAsync();
            await deltaBuf.CopyToAsync(deltas.AsMemory());
            finalDelta = deltas.Max();

            // Swap buffer handles — no device-side copy, the iteration just reads from
            // the other buffer on the next pass.
            (current, next) = (next, current);
            if (finalDelta < ConvergenceThreshold)
            {
                iterations++;
                break;
            }
        }
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // 7. Read results and report. After the iteration loop, `current` holds the
        //    final ranks (we swap before the convergence check so `current` is next-ranks).
        var readBack = new float[NodeCount];
        await current.CopyToAsync(readBack.AsMemory());

        Console.WriteLine();
        Console.WriteLine($"Converged in {iterations} iterations ({stopwatch.Elapsed.TotalMilliseconds:F2} ms)");
        Console.WriteLine($"Final max delta: {finalDelta:E3}");
        Console.WriteLine($"Rank sum       : {readBack.Sum():F6}  (expected ~1.0)");
        Console.WriteLine();
        Console.WriteLine("Top 10 nodes:");
        Console.WriteLine("  Rank | Node   | Score");
        Console.WriteLine("  -----+--------+----------");
        var topN = Enumerable.Range(0, NodeCount)
            .Select(i => (Node: i, Score: readBack[i]))
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToArray();
        for (var i = 0; i < topN.Length; i++)
        {
            Console.WriteLine($"  #{i + 1,-3} | {topN[i].Node,6} | {topN[i].Score:F6}");
        }
        Console.WriteLine();
        Console.WriteLine("Done. Top rankings should match the CPU sample to ~1e-4 tolerance.");
        return 0;
    }

    /// <summary>
    /// Builds the same synthetic directed graph as the CPU sample, returned in CSR
    /// (compressed sparse row) form so it can be uploaded efficiently to the GPU.
    /// </summary>
    private static (int[] rowOffsets, int[] edgeTargets, int[] outDegree) BuildCsrGraph(
        int nodes, int averageOutDegree, int seed)
    {
#pragma warning disable CA5394 // Deterministic PRNG — sample needs reproducible output.
        var rng = new Random(seed);
        var adjacency = new int[nodes][];
        var totalEdges = 0;
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
            adjacency[i] = [.. targets];
            totalEdges += adjacency[i].Length;
        }

        var rowOffsets = new int[nodes + 1];
        var edgeTargets = new int[totalEdges];
        var outDegree = new int[nodes];
        var cursor = 0;
        for (var i = 0; i < nodes; i++)
        {
            rowOffsets[i] = cursor;
            outDegree[i] = adjacency[i].Length;
            adjacency[i].CopyTo(edgeTargets, cursor);
            cursor += adjacency[i].Length;
        }
        rowOffsets[nodes] = cursor;
        return (rowOffsets, edgeTargets, outDegree);
#pragma warning restore CA5394
    }
}
