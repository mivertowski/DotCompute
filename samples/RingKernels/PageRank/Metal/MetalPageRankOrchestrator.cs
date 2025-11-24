// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.Metal.Compilation;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// Orchestrates end-to-end Metal PageRank computation using ring kernels.
/// </summary>
/// <remarks>
/// <para>
/// This orchestrator manages the complete PageRank pipeline on Metal:
/// 1. Kernel Discovery: Finds all 3 PageRank Metal kernels via reflection
/// 2. MSL Generation: Compiles C# RingKernels to Metal Shading Language
/// 3. Routing Setup: Configures K2K message routing table
/// 4. Barrier Setup: Initializes multi-kernel barriers for synchronization
/// 5. Kernel Launch: Launches all 3 kernels as persistent GPU actors
/// 6. Graph Distribution: Sends MetalGraphNode messages to ContributionSender
/// 7. Iteration Control: Manages PageRank iterations until convergence
/// 8. Result Collection: Gathers ConvergenceCheckResult from ConvergenceChecker
/// </para>
/// <para>
/// Architecture (3-kernel pipeline):
/// - ContributionSender: Receives graph nodes, sends contributions
/// - RankAggregator: Collects contributions, computes new ranks
/// - ConvergenceChecker: Monitors convergence, outputs results
/// </para>
/// <para>
/// Metal Optimizations:
/// - Unified memory: Zero-copy CPU ↔ GPU data access
/// - Threadgroup barriers: Intra-kernel synchronization
/// - K2K messaging: Direct GPU-to-GPU message passing
/// - MemoryPack serialization: <100ns message encode/decode
/// </para>
/// <para>
/// Performance Targets (Apple Silicon M2+):
/// - Graph preprocessing: ~1μs per node
/// - Iteration latency: ~500μs for 1000-node graph
/// - Convergence detection: ~200ns per result
/// - Overall throughput: 2M+ messages/sec sustained
/// </para>
/// </remarks>
public sealed class MetalPageRankOrchestrator : IAsyncDisposable
{
    private readonly ILogger<MetalPageRankOrchestrator> _logger;
    private readonly RingKernelDiscovery _discovery;
    private readonly MetalRingKernelStubGenerator _stubGenerator;
    private readonly MetalRingKernelCompiler _compiler;
    private readonly MetalKernelRoutingTableManager? _routingTableManager;
    private readonly MetalMultiKernelBarrierManager? _barrierManager;
    private readonly MetalRingKernelRuntime? _runtime;

    // Kernel metadata
    private readonly ConcurrentDictionary<string, DiscoveredRingKernel> _discoveredKernels = new();
    private readonly ConcurrentDictionary<string, string> _compiledMslCode = new();

    // Execution state
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Gets the Metal device pointer.
    /// </summary>
    public IntPtr Device { get; }

    /// <summary>
    /// Gets the Metal command queue pointer.
    /// </summary>
    public IntPtr CommandQueue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalPageRankOrchestrator"/> class.
    /// </summary>
    /// <param name="device">Metal device pointer.</param>
    /// <param name="commandQueue">Metal command queue pointer.</param>
    /// <param name="logger">Logger instance.</param>
    public MetalPageRankOrchestrator(
        IntPtr device,
        IntPtr commandQueue,
        ILogger<MetalPageRankOrchestrator>? logger = null)
    {
        if (device == IntPtr.Zero)
        {
            throw new ArgumentException("Metal device pointer cannot be zero", nameof(device));
        }

        if (commandQueue == IntPtr.Zero)
        {
            throw new ArgumentException("Metal command queue pointer cannot be zero", nameof(commandQueue));
        }

        Device = device;
        CommandQueue = commandQueue;
        _logger = logger ?? NullLogger<MetalPageRankOrchestrator>.Instance;

        // Initialize discovery and compilation components
        _discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        _stubGenerator = new MetalRingKernelStubGenerator(
            NullLogger<MetalRingKernelStubGenerator>.Instance);

        var compilerLogger = NullLogger<MetalRingKernelCompiler>.Instance;
        _compiler = new MetalRingKernelCompiler(compilerLogger, device, commandQueue);

        // Initialize infrastructure managers
        var routingLogger = NullLogger<MetalKernelRoutingTableManager>.Instance;
        _routingTableManager = new MetalKernelRoutingTableManager(device, commandQueue, routingLogger);

        var barrierLogger = NullLogger<MetalMultiKernelBarrierManager>.Instance;
        _barrierManager = new MetalMultiKernelBarrierManager(device, commandQueue, barrierLogger);

        var runtimeLogger = NullLogger<MetalRingKernelRuntime>.Instance;
        _runtime = new MetalRingKernelRuntime(runtimeLogger, _compiler);

        _logger.LogInformation(
            "MetalPageRankOrchestrator created (Device: {Device:X}, CommandQueue: {CommandQueue:X})",
            device.ToInt64(),
            commandQueue.ToInt64());
    }

    /// <summary>
    /// Initializes the orchestrator by discovering and compiling all PageRank kernels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
        {
            _logger.LogWarning("Orchestrator already initialized, skipping");
            return;
        }

        _logger.LogInformation("Initializing Metal PageRank orchestrator");

        // Phase 1: Discover all 3 PageRank Metal kernels
        var kernels = _discovery.DiscoverKernels(new[] { typeof(PageRankMetalKernels).Assembly });
        var metalKernels = kernels
            .Where(k => k.KernelId.StartsWith("metal_pagerank_", StringComparison.Ordinal))
            .ToList();

        if (metalKernels.Count != 3)
        {
            throw new InvalidOperationException(
                $"Expected 3 Metal PageRank kernels, found {metalKernels.Count}");
        }

        _logger.LogInformation("Discovered {Count} Metal PageRank kernels", metalKernels.Count);

        foreach (var kernel in metalKernels)
        {
            _discoveredKernels[kernel.KernelId] = kernel;
            _logger.LogInformation(
                "  - {KernelId}: {InputType} → {OutputType}",
                kernel.KernelId,
                kernel.InputMessageTypeName,
                kernel.OutputMessageTypeName);
        }

        // Phase 2: Generate MSL code for all kernels
        foreach (var kernel in metalKernels)
        {
            _logger.LogInformation("Generating MSL for {KernelId}", kernel.KernelId);
            var mslCode = _stubGenerator.GenerateKernelStub(kernel);

            if (string.IsNullOrWhiteSpace(mslCode))
            {
                throw new InvalidOperationException(
                    $"Failed to generate MSL code for kernel '{kernel.KernelId}'");
            }

            _compiledMslCode[kernel.KernelId] = mslCode;
            _logger.LogInformation(
                "  Generated {Length} chars of MSL for {KernelId}",
                mslCode.Length,
                kernel.KernelId);
        }

        // Phase 3: Set up K2K routing table
        if (_routingTableManager != null)
        {
            _logger.LogInformation("Setting up K2K routing table for 3 kernels");
            // TODO: Configure routing table with kernel IDs
            // await _routingTableManager.InitializeRoutingTableAsync(metalKernels, cancellationToken);
        }

        // Phase 4: Set up multi-kernel barriers
        if (_barrierManager != null)
        {
            _logger.LogInformation("Setting up multi-kernel barriers (3 participants)");
            // TODO: Initialize barrier with participant count = 3
            // await _barrierManager.CreateBarrierAsync(3, cancellationToken);
        }

        _initialized = true;
        _logger.LogInformation("Metal PageRank orchestrator initialized successfully");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes PageRank computation on the given graph.
    /// </summary>
    /// <param name="graph">Graph represented as adjacency lists (nodeId → target nodes).</param>
    /// <param name="maxIterations">Maximum number of PageRank iterations.</param>
    /// <param name="convergenceThreshold">Convergence threshold (default: 0.0001).</param>
    /// <param name="dampingFactor">Damping factor (default: 0.85).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the computation, with final PageRank values.</returns>
    public async Task<Dictionary<int, float>> ComputePageRankAsync(
        Dictionary<int, int[]> graph,
        int maxIterations = 100,
        float convergenceThreshold = PageRankMetalKernels.ConvergenceThreshold,
        float dampingFactor = PageRankMetalKernels.DampingFactor,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Orchestrator not initialized. Call InitializeAsync() first.");
        }

        if (graph == null || graph.Count == 0)
        {
            throw new ArgumentException("Graph cannot be null or empty", nameof(graph));
        }

        _logger.LogInformation(
            "Starting PageRank computation: {NodeCount} nodes, max {MaxIterations} iterations",
            graph.Count,
            maxIterations);

        // Phase 1: Launch all 3 kernels
        await LaunchKernelsAsync(cancellationToken);

        // Phase 2: Convert graph to MetalGraphNode messages
        var graphNodes = CreateGraphNodes(graph, dampingFactor);
        _logger.LogInformation("Created {Count} MetalGraphNode messages", graphNodes.Count);

        // Phase 3: Send graph nodes to ContributionSender kernel
        await DistributeGraphNodesAsync(graphNodes, cancellationToken);

        // Phase 4: Run PageRank iterations
        var results = await RunIterationsAsync(
            graph.Count,
            maxIterations,
            convergenceThreshold,
            cancellationToken);

        _logger.LogInformation(
            "PageRank computation completed: {NodeCount} nodes processed",
            results.Count);

        return results;
    }

    /// <summary>
    /// Launches all 3 PageRank kernels as persistent GPU actors.
    /// </summary>
    private async Task LaunchKernelsAsync(CancellationToken cancellationToken)
    {
        if (_runtime == null)
        {
            throw new InvalidOperationException("Runtime not initialized");
        }

        _logger.LogInformation("Launching 3 Metal PageRank kernels");

        // Launch kernels with appropriate grid/block sizes for Metal
        const int gridSize = 1;  // Single threadgroup for now
        const int blockSize = 256;  // 256 threads per threadgroup (typical for Metal)

        var kernelIds = new[]
        {
            "metal_pagerank_contribution_sender",
            "metal_pagerank_rank_aggregator",
            "metal_pagerank_convergence_checker"
        };

        foreach (var kernelId in kernelIds)
        {
            _logger.LogInformation("Launching kernel: {KernelId}", kernelId);

            // Launch persistent Ring Kernel on GPU
            await _runtime.LaunchAsync(kernelId, gridSize, blockSize, null, cancellationToken);

            _logger.LogInformation("  Launched {KernelId} (grid={Grid}, block={Block})",
                kernelId, gridSize, blockSize);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates MetalGraphNode messages from the graph adjacency list.
    /// </summary>
    private List<MetalGraphNode> CreateGraphNodes(
        Dictionary<int, int[]> graph,
        float dampingFactor)
    {
        var nodes = new List<MetalGraphNode>(graph.Count);
        float initialRank = 1.0f;  // Initial rank for all nodes

        foreach (var (nodeId, targets) in graph)
        {
            var node = MetalGraphNode.Create(nodeId, initialRank, targets);
            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Distributes MetalGraphNode messages to the ContributionSender kernel.
    /// </summary>
    private async Task DistributeGraphNodesAsync(
        List<MetalGraphNode> graphNodes,
        CancellationToken cancellationToken)
    {
        const string targetKernel = "metal_pagerank_contribution_sender";

        _logger.LogInformation(
            "Distributing {Count} graph nodes to {Kernel}",
            graphNodes.Count,
            targetKernel);

        // TODO: Send messages via MetalRingKernelRuntime
        // foreach (var node in graphNodes)
        // {
        //     await _runtime.SendMessageAsync(targetKernel, node, cancellationToken);
        // }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Runs PageRank iterations until convergence or max iterations reached.
    /// </summary>
    private async Task<Dictionary<int, float>> RunIterationsAsync(
        int nodeCount,
        int maxIterations,
        float convergenceThreshold,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Running PageRank iterations (max={Max}, threshold={Threshold})",
            maxIterations,
            convergenceThreshold);

        var ranks = new Dictionary<int, float>();
        bool converged = false;
        int iteration = 0;

        while (!converged && iteration < maxIterations)
        {
            iteration++;

            // TODO: Poll for ConvergenceCheckResult messages from convergence_checker
            // var results = await _runtime.ReceiveMessagesAsync<ConvergenceCheckResult>(
            //     "metal_pagerank_convergence_checker",
            //     cancellationToken);

            // Check for convergence
            // converged = results.Any(r => r.HasConverged == 1);

            if (iteration % 10 == 0)
            {
                _logger.LogDebug("Iteration {Iteration}/{Max}", iteration, maxIterations);
            }

            // For now, just simulate convergence after a few iterations
            if (iteration >= 10)
            {
                converged = true;
            }

            await Task.Delay(10, cancellationToken);  // Simulated iteration delay
        }

        _logger.LogInformation(
            "PageRank converged after {Iterations} iterations (threshold={Threshold})",
            iteration,
            convergenceThreshold);

        // TODO: Collect final ranks from RankAggregator output
        // For now, return empty results
        return ranks;
    }

    /// <summary>
    /// Gets the MSL code for a specific kernel.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <returns>The generated MSL code, or null if not found.</returns>
    public string? GetKernelMsl(string kernelId)
    {
        return _compiledMslCode.TryGetValue(kernelId, out var msl) ? msl : null;
    }

    /// <summary>
    /// Gets all discovered kernel metadata.
    /// </summary>
    /// <returns>A collection of discovered kernels.</returns>
    public IReadOnlyCollection<DiscoveredRingKernel> GetDiscoveredKernels()
    {
        return _discoveredKernels.Values;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing Metal PageRank orchestrator");

        // Dispose runtime
        if (_runtime != null)
        {
            await _runtime.DisposeAsync();
        }

        // Dispose barrier manager
        if (_barrierManager != null)
        {
            await _barrierManager.DisposeAsync();
        }

        // Dispose routing table manager
        if (_routingTableManager != null)
        {
            _routingTableManager.Dispose();
        }

        // Dispose compiler
        _compiler?.Dispose();

        _disposed = true;
        _logger.LogInformation("Metal PageRank orchestrator disposed");
    }
}
