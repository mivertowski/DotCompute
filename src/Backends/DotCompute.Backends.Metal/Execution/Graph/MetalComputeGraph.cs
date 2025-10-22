// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using DotCompute.Backends.Metal.Execution.Graph.Nodes;
using DotCompute.Backends.Metal.Execution.Graph.Configuration;
using DotCompute.Backends.Metal.Execution.Graph.Statistics;
using DotCompute.Core.Execution.Analysis;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph;

/// <summary>
/// Represents a Metal computational graph that can be constructed, optimized, and executed efficiently.
/// Provides high-performance execution through kernel fusion and optimization for Apple Silicon architecture.
/// </summary>
/// <remarks>
/// Metal compute graphs enable efficient execution of complex computation pipelines by reducing
/// CPU-GPU synchronization overhead and enabling advanced optimizations like kernel fusion,
/// memory coalescing, and command buffer batching.
/// </remarks>
public sealed class MetalComputeGraph : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentBag<MetalGraphNode> _nodes = [];
    private readonly DependencyGraph _dependencyGraph = new();
    private readonly ILogger<MetalComputeGraph>? _logger;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalComputeGraph"/> class.
    /// </summary>
    /// <param name="name">The human-readable name for this graph.</param>
    /// <param name="logger">Optional logger instance for debugging and monitoring.</param>
    /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
    public MetalComputeGraph(string name, ILogger<MetalComputeGraph>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Graph name cannot be null or empty.", nameof(name));
        }

        Name = name;
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTimeOffset.UtcNow;
        Configuration = new MetalGraphConfiguration();
        Statistics = new MetalGraphStatistics { Name = name };
        _logger = logger;

        _logger?.LogDebug("Created Metal compute graph '{Name}' with ID {Id}", Name, Id);
    }

    #region Properties

    /// <summary>
    /// Gets the unique identifier for this Metal compute graph.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable name for this Metal compute graph.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the timestamp when this graph was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the configuration options for this graph.
    /// </summary>
    public MetalGraphConfiguration Configuration { get; }

    /// <summary>
    /// Gets the current statistics for this graph.
    /// </summary>
    public MetalGraphStatistics Statistics { get; }

    /// <summary>
    /// Gets the thread-safe collection of graph nodes that comprise this Metal compute graph.
    /// </summary>
    public IReadOnlyList<MetalGraphNode> Nodes
    {
        get
        {
            lock (_lock)
            {
                return _nodes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the current count of nodes in the graph.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Gets a value indicating whether the graph is empty (contains no nodes).
    /// </summary>
    public bool IsEmpty => _nodes.IsEmpty;

    /// <summary>
    /// Gets a value indicating whether the graph has been built and is ready for execution.
    /// </summary>
    public bool IsBuilt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the graph has been optimized for execution.
    /// </summary>
    public bool IsOptimized { get; private set; }

    /// <summary>
    /// Gets the total estimated memory footprint of this graph in bytes.
    /// </summary>
    public long EstimatedMemoryFootprint
    {
        get
        {
            return _nodes.Sum(node => node.EstimatedMemoryUsage);
        }
    }

    #endregion

    #region Node Management

    /// <summary>
    /// Adds a kernel computation node to the graph.
    /// </summary>
    /// <param name="kernel">The compiled Metal kernel to execute.</param>
    /// <param name="threadgroupsPerGrid">The number of threadgroups to dispatch.</param>
    /// <param name="threadsPerThreadgroup">The number of threads per threadgroup.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="dependencies">Optional array of nodes this kernel depends on.</param>
    /// <returns>The created kernel node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when kernel is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
    public MetalGraphNode AddKernelNode(
        ICompiledKernel kernel,
        MTLSize threadgroupsPerGrid,
        MTLSize threadsPerThreadgroup,
        object[] arguments,
        MetalGraphNode[]? dependencies = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(arguments);

        var nodeId = GenerateNodeId();
        var node = new MetalGraphNode(nodeId, MetalNodeType.Kernel)
        {
            Kernel = kernel,
            ThreadgroupsPerGrid = threadgroupsPerGrid,
            ThreadsPerThreadgroup = threadsPerThreadgroup,
            Arguments = arguments,
            EstimatedMemoryUsage = EstimateKernelMemoryUsage(kernel, arguments)
        };

        if (dependencies != null)
        {
            foreach (var dep in dependencies)
            {
                node.AddDependency(dep);
            }
        }

        return AddNodeInternal(node);
    }

    /// <summary>
    /// Adds a memory copy node to the graph.
    /// </summary>
    /// <param name="sourceBuffer">The source Metal buffer.</param>
    /// <param name="destinationBuffer">The destination Metal buffer.</param>
    /// <param name="size">The number of bytes to copy.</param>
    /// <param name="dependencies">Optional array of nodes this copy depends on.</param>
    /// <returns>The created memory copy node.</returns>
    public MetalGraphNode AddMemoryCopyNode(
        IntPtr sourceBuffer,
        IntPtr destinationBuffer,
        long size,
        MetalGraphNode[]? dependencies = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var nodeId = GenerateNodeId();
        var node = new MetalGraphNode(nodeId, MetalNodeType.MemoryCopy)
        {
            SourceBuffer = sourceBuffer,
            DestinationBuffer = destinationBuffer,
            CopySize = size,
            EstimatedMemoryUsage = size
        };

        if (dependencies != null)
        {
            foreach (var dep in dependencies)
            {
                node.AddDependency(dep);
            }
        }

        return AddNodeInternal(node);
    }

    /// <summary>
    /// Adds a memory set node to the graph.
    /// </summary>
    /// <param name="buffer">The Metal buffer to fill.</param>
    /// <param name="value">The value to fill with.</param>
    /// <param name="size">The number of bytes to fill.</param>
    /// <param name="dependencies">Optional array of nodes this operation depends on.</param>
    /// <returns>The created memory set node.</returns>
    public MetalGraphNode AddMemorySetNode(
        IntPtr buffer,
        byte value,
        long size,
        MetalGraphNode[]? dependencies = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var nodeId = GenerateNodeId();
        var node = new MetalGraphNode(nodeId, MetalNodeType.MemorySet)
        {
            DestinationBuffer = buffer,
            FillValue = value,
            CopySize = size,
            EstimatedMemoryUsage = size
        };

        if (dependencies != null)
        {
            foreach (var dep in dependencies)
            {
                node.AddDependency(dep);
            }
        }

        return AddNodeInternal(node);
    }

    /// <summary>
    /// Adds a synchronization barrier node to the graph.
    /// </summary>
    /// <param name="dependencies">The nodes that must complete before subsequent operations.</param>
    /// <returns>The created barrier node.</returns>
    public MetalGraphNode AddBarrierNode(MetalGraphNode[] dependencies)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(dependencies);

        var nodeId = GenerateNodeId();
        var node = new MetalGraphNode(nodeId, MetalNodeType.Barrier)
        {
            EstimatedMemoryUsage = 0 // Barriers don't consume additional memory
        };

        foreach (var dep in dependencies)
        {
            node.AddDependency(dep);
        }

        return AddNodeInternal(node);
    }

    /// <summary>
    /// Removes a node from the graph by its ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to remove.</param>
    /// <returns><c>true</c> if the node was found and removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when nodeId is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
    public bool RemoveNode(string nodeId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));
        }

        lock (_lock)
        {
            var nodeToRemove = _nodes.FirstOrDefault(n => n.Id == nodeId);
            if (nodeToRemove == null)
            {
                return false;
            }

            // Remove from dependency graph
            var dependentNodes = _nodes.Where(n => n.Dependencies.Any(d => d.Id == nodeId)).ToList();
            foreach (var dependent in dependentNodes)
            {
                var toRemove = dependent.Dependencies.Where(d => d.Id == nodeId).ToList();
                foreach (var dep in toRemove)
                {
                    dependent.Dependencies.Remove(dep);
                }
            }

            // Rebuild the concurrent bag without the removed node
            var remainingNodes = _nodes.Where(n => n.Id != nodeId).ToList();
            while (!_nodes.IsEmpty)
            {
                _ = _nodes.TryTake(out _);
            }

            foreach (var node in remainingNodes)
            {
                _nodes.Add(node);
            }

            Statistics.NodeCount = _nodes.Count;
            _logger?.LogDebug("Removed node {NodeId} from graph '{Name}'", nodeId, Name);
            return true;
        }
    }

    /// <summary>
    /// Finds a node in the graph by its ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to find.</param>
    /// <returns>The node with the specified ID, or <c>null</c> if not found.</returns>
    public MetalGraphNode? FindNode(string nodeId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));
        }

        return _nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>
    /// Clears all nodes from the graph.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            while (!_nodes.IsEmpty)
            {
                _ = _nodes.TryTake(out _);
            }

            _dependencyGraph.Clear();
            Statistics.NodeCount = 0;
            IsBuilt = false;
            IsOptimized = false;


            _logger?.LogDebug("Cleared all nodes from graph '{Name}'", Name);
        }
    }

    #endregion

    #region Graph Analysis and Optimization

    /// <summary>
    /// Builds the graph by analyzing dependencies and preparing for execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when circular dependencies are detected.</exception>
    public void Build()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _logger?.LogDebug("Building graph '{Name}' with {NodeCount} nodes", Name, _nodes.Count);

            // Clear and rebuild dependency graph

            _dependencyGraph.Clear();

            // Build dependency relationships

            var nodeDict = _nodes.ToDictionary(n => n.Id, n => n);
            var nodeIndex = 0;
            var nodeToIndex = new Dictionary<string, int>();

            foreach (var node in _nodes)
            {
                nodeToIndex[node.Id] = nodeIndex++;
            }

            foreach (var node in _nodes)
            {
                var nodeIdx = nodeToIndex[node.Id];
                foreach (var dependency in node.Dependencies)
                {
                    if (nodeToIndex.TryGetValue(dependency.Id, out var depIdx))
                    {
                        _dependencyGraph.AddDependency(depIdx, nodeIdx, Core.Execution.Types.DependencyType.DataHazard);
                    }
                }
            }

            // Validate no circular dependencies
            try
            {
                var executionOrder = _dependencyGraph.TopologicalSort();
                Statistics.CriticalPathLength = CalculateCriticalPathLength();
                Statistics.ParallelismOpportunities = CalculateParallelismOpportunities();


                IsBuilt = true;
                _logger?.LogInformation("Successfully built graph '{Name}' - Critical path: {CriticalPath}, Parallelism opportunities: {Parallelism}",

                    Name, Statistics.CriticalPathLength, Statistics.ParallelismOpportunities);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(ex, "Failed to build graph '{Name}' due to circular dependencies", Name);
                throw new InvalidOperationException($"Graph '{Name}' contains circular dependencies.", ex);
            }
        }
    }

    /// <summary>
    /// Gets the execution order of nodes using topological sorting.
    /// </summary>
    /// <returns>A list of nodes in execution order.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph hasn't been built.</exception>
    public IReadOnlyList<MetalGraphNode> GetExecutionOrder()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (!IsBuilt)
        {
            throw new InvalidOperationException("Graph must be built before getting execution order.");
        }

        var executionOrder = _dependencyGraph.TopologicalSort();
        var nodeArray = _nodes.ToArray();


        return executionOrder.Select(index => nodeArray[index]).ToList().AsReadOnly();
    }

    /// <summary>
    /// Analyzes the graph for optimization opportunities.
    /// </summary>
    /// <returns>Analysis results containing optimization information.</returns>
    public MetalGraphAnalysis AnalyzeOptimizationOpportunities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var analysis = new MetalGraphAnalysis
        {
            NodeCount = _nodes.Count,
            EstimatedMemoryFootprint = EstimatedMemoryFootprint,
            CriticalPathLength = CalculateCriticalPathLength(),
            ParallelismOpportunities = CalculateParallelismOpportunities(),
            FusionOpportunities = AnalyzeFusionOpportunities(),
            MemoryCoalescingOpportunities = AnalyzeMemoryCoalescingOpportunities(),
            CommandBufferBatchingOpportunities = AnalyzeCommandBufferBatching()
        };

        return analysis;
    }

    #endregion

    #region Internal Helper Methods

    private MetalGraphNode AddNodeInternal(MetalGraphNode node)
    {
        lock (_lock)
        {
            // Validate no duplicate IDs
            if (_nodes.Any(n => n.Id == node.Id))
            {
                throw new InvalidOperationException($"A node with ID '{node.Id}' already exists in the graph.");
            }

            // Validate dependencies exist in the graph
            foreach (var dependency in node.Dependencies)
            {
                if (!_nodes.Any(n => n.Id == dependency.Id))
                {
                    throw new InvalidOperationException($"Dependency node with ID '{dependency.Id}' does not exist in the graph.");
                }
            }

            _nodes.Add(node);
            Statistics.NodeCount = _nodes.Count;
            IsBuilt = false; // Graph needs to be rebuilt
            IsOptimized = false;

            _logger?.LogDebug("Added {NodeType} node '{NodeId}' to graph '{Name}'", node.Type, node.Id, Name);
            return node;
        }
    }

    private static string GenerateNodeId() => $"node_{Guid.NewGuid():N}";

    private static long EstimateKernelMemoryUsage(ICompiledKernel kernel, object[] arguments)
    {
        // Estimate based on argument sizes and typical kernel memory usage
        long estimatedUsage = 1024 * 1024; // Base 1MB for kernel overhead

        foreach (var arg in arguments)
        {
            estimatedUsage += arg switch
            {
                IntPtr => 8, // Pointer size
                float => 4,
                double => 8,
                int => 4,
                long => 8,
                Array array => array.Length * GetElementSize(array.GetType().GetElementType()!),
                _ => 8 // Default pointer size
            };
        }

        return estimatedUsage;
    }

    private static int GetElementSize(Type elementType)
    {
        return elementType.Name switch
        {
            nameof(Single) => 4,
            nameof(Double) => 8,
            nameof(Int32) => 4,
            nameof(Int64) => 8,
            nameof(Byte) => 1,
            _ => 4 // Default to 4 bytes
        };
    }

    private int CalculateCriticalPathLength()
    {
        if (_nodes.IsEmpty)
        {
            return 0;
        }

        var pathLengths = new Dictionary<string, int>();
        var visited = new HashSet<string>();

        int CalculateNodePath(MetalGraphNode node)
        {
            if (visited.Contains(node.Id))
            {
                return pathLengths.GetValueOrDefault(node.Id, 0);
            }

            _ = visited.Add(node.Id);

            var maxDependencyPath = node.Dependencies.Count > 0
                ? node.Dependencies.Max(dep => CalculateNodePath(dep))
                : 0;

            pathLengths[node.Id] = maxDependencyPath + 1;
            return pathLengths[node.Id];
        }

        return _nodes.Max(CalculateNodePath);
    }

    private int CalculateParallelismOpportunities()
    {
        var levels = new Dictionary<MetalGraphNode, int>();


        foreach (var node in _nodes)
        {
            var level = node.Dependencies.Count > 0
                ? node.Dependencies.Max(dep => levels.GetValueOrDefault(dep, 0)) + 1
                : 0;
            levels[node] = level;
        }

        return levels.GroupBy(kvp => kvp.Value)
                   .Where(group => group.Count() > 1)
                   .Sum(group => group.Count() - 1);
    }

    private int AnalyzeFusionOpportunities()
    {
        var kernelNodes = _nodes.Where(n => n.Type == MetalNodeType.Kernel).ToList();
        var fusionOpportunities = 0;

        for (var i = 0; i < kernelNodes.Count - 1; i++)
        {
            for (var j = i + 1; j < kernelNodes.Count; j++)
            {
                if (CanFuseKernels(kernelNodes[i], kernelNodes[j]))
                {
                    fusionOpportunities++;
                }
            }
        }

        return fusionOpportunities;
    }

    private static bool CanFuseKernels(MetalGraphNode kernel1, MetalGraphNode kernel2)
    {
        // Kernels can potentially be fused if:
        // 1. One depends directly on the other
        // 2. They have compatible threadgroup configurations
        // 3. Combined resource usage is within Metal limits

        _ = kernel1.Dependencies.Contains(kernel2) ||
                                 kernel2.Dependencies.Contains(kernel1);

        // Check threadgroup compatibility (simplified check)


        // Check threadgroup compatibility (simplified check)
        var compatible = kernel1.ThreadgroupsPerGrid.Width <= 1024 &&
                        kernel2.ThreadgroupsPerGrid.Width <= 1024 &&
                        kernel1.ThreadsPerThreadgroup.Width * kernel1.ThreadsPerThreadgroup.Height * kernel1.ThreadsPerThreadgroup.Depth <= 1024 &&
                        kernel2.ThreadsPerThreadgroup.Width * kernel2.ThreadsPerThreadgroup.Height * kernel2.ThreadsPerThreadgroup.Depth <= 1024;

        return compatible;
    }

    private int AnalyzeMemoryCoalescingOpportunities()
    {
        var memoryNodes = _nodes.Where(n => n.Type == MetalNodeType.MemoryCopy).ToList();
        var coalescingOpportunities = 0;

        // Look for adjacent memory operations that can be coalesced
        for (var i = 0; i < memoryNodes.Count - 1; i++)
        {
            for (var j = i + 1; j < memoryNodes.Count; j++)
            {
                if (CanCoalesceMemoryOperations(memoryNodes[i], memoryNodes[j]))
                {
                    coalescingOpportunities++;
                }
            }
        }

        return coalescingOpportunities;
    }

    private static bool CanCoalesceMemoryOperations(MetalGraphNode mem1, MetalGraphNode mem2)
        // Memory operations can be coalesced if they're adjacent and have no dependencies between them

        => !mem1.Dependencies.Contains(mem2) && !mem2.Dependencies.Contains(mem1);

    private int AnalyzeCommandBufferBatching()
    {
        // Analyze opportunities to batch operations into fewer command buffers
        var independentGroups = new List<List<MetalGraphNode>>();
        var processed = new HashSet<string>();

        foreach (var node in _nodes)
        {
            if (processed.Contains(node.Id))
            {
                continue;
            }


            var group = new List<MetalGraphNode> { node };
            _ = processed.Add(node.Id);

            // Find nodes that can be batched with this one
            foreach (var other in _nodes)
            {
                if (processed.Contains(other.Id))
                {
                    continue;
                }


                if (CanBatchInSameCommandBuffer(node, other))
                {
                    group.Add(other);
                    _ = processed.Add(other.Id);
                }
            }

            independentGroups.Add(group);
        }

        // Return the number of command buffers that can be saved
        return Math.Max(0, _nodes.Count - independentGroups.Count);
    }

    private static bool CanBatchInSameCommandBuffer(MetalGraphNode node1, MetalGraphNode node2)
        // Nodes can be batched if they don't depend on each other

        => !node1.Dependencies.Contains(node2) && !node2.Dependencies.Contains(node1);

    #endregion

    #region Disposal

    /// <summary>
    /// Releases all resources used by the Metal compute graph.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                Clear();
                _logger?.LogDebug("Disposed Metal compute graph '{Name}'", Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during disposal of graph '{Name}'", Name);
            }
            finally
            {
                _disposed = true;
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for the MetalComputeGraph class.
    /// </summary>
    ~MetalComputeGraph()
    {
        Dispose();
    }

    #endregion
}