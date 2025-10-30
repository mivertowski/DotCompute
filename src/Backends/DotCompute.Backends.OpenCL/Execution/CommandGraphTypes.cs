// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Represents a complete command graph with nodes and dependencies.
/// </summary>
public sealed class Graph
{
    /// <summary>
    /// Gets or initializes the unique name of the graph.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the list of nodes in the graph.
    /// </summary>
    public required IReadOnlyList<OpenCLCommandGraph.Node> Nodes { get; init; }

    /// <summary>
    /// Gets or sets the number of optimizations applied to this graph.
    /// </summary>
    public int OptimizationCount { get; set; }

    /// <summary>
    /// Gets or initializes optional metadata for the graph.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Clones the graph structure with a new name.
    /// </summary>
    /// <param name="newName">The name for the cloned graph.</param>
    /// <returns>A new graph instance with cloned structure.</returns>
    public Graph Clone(string newName)
    {
        return new Graph
        {
            Name = newName,
            Nodes = new List<OpenCLCommandGraph.Node>(Nodes),
            OptimizationCount = OptimizationCount,
            Metadata = Metadata != null ? new Dictionary<string, object>(Metadata) : null
        };
    }
}

/// <summary>
/// Provides a fluent API for building command graphs.
/// </summary>
public sealed class GraphBuilder
{
    private readonly string _name;
    private readonly List<OpenCLCommandGraph.Node> _nodes;
    private readonly Dictionary<string, OpenCLCommandGraph.Node> _nodeMap;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphBuilder"/> class.
    /// </summary>
    /// <param name="name">The name of the graph being built.</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    internal GraphBuilder(string name, ILogger logger)
    {
        _name = name;
        _nodes = new List<OpenCLCommandGraph.Node>();
        _nodeMap = new Dictionary<string, OpenCLCommandGraph.Node>();
        _logger = logger;
    }

    /// <summary>
    /// Adds a kernel execution node to the graph.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="config">The execution configuration.</param>
    /// <param name="defaultArguments">Optional default arguments for the kernel.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a node with the same name exists.</exception>
    public GraphBuilder AddKernelExecution(
        string name,
        object kernel,
        ExecutionConfig config,
        object? defaultArguments = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(config);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.KernelExecution,
            Operation = new KernelOperation
            {
                Kernel = kernel,
                Config = config,
                DefaultArguments = defaultArguments
            }
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added kernel execution node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a memory write operation node to the graph.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>The builder for method chaining.</returns>
    public GraphBuilder AddMemoryWrite(string name, IUnifiedMemoryBuffer buffer, object data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(data);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.MemoryWrite,
            Operation = new MemoryOperation
            {
                Buffer = buffer,
                Data = data,
                OperationType = MemoryOperationType.Write
            }
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added memory write node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a memory read operation node to the graph.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <param name="buffer">The buffer to read from.</param>
    /// <returns>The builder for method chaining.</returns>
    public GraphBuilder AddMemoryRead(string name, IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(buffer);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.MemoryRead,
            Operation = new MemoryOperation
            {
                Buffer = buffer,
                OperationType = MemoryOperationType.Read
            }
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added memory read node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a memory copy operation node to the graph.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <returns>The builder for method chaining.</returns>
    public GraphBuilder AddMemoryCopy(string name, IUnifiedMemoryBuffer source, IUnifiedMemoryBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.MemoryCopy,
            Operation = new MemoryOperation
            {
                Buffer = source,
                DestinationBuffer = destination,
                OperationType = MemoryOperationType.Copy
            }
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added memory copy node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a synchronization barrier node to the graph.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <returns>The builder for method chaining.</returns>
    public GraphBuilder AddBarrier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.Barrier,
            Operation = new BarrierOperation()
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added barrier node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a marker node for timing or debugging purposes.
    /// </summary>
    /// <param name="name">The unique name for this node.</param>
    /// <returns>The builder for method chaining.</returns>
    public GraphBuilder AddMarker(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_nodeMap.ContainsKey(name))
        {
            throw new InvalidOperationException($"Node with name '{name}' already exists");
        }

        var node = new OpenCLCommandGraph.Node
        {
            Name = name,
            Type = OpenCLCommandGraph.NodeType.Marker,
            Operation = new MarkerOperation()
        };

        _nodes.Add(node);
        _nodeMap[name] = node;

        _logger.LogDebug("Added marker node: {NodeName}", name);

        return this;
    }

    /// <summary>
    /// Adds a dependency between two nodes.
    /// </summary>
    /// <param name="fromName">The name of the dependent node.</param>
    /// <param name="toName">The name of the node that must complete first.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when a node is not found.</exception>
    public GraphBuilder AddDependency(string fromName, string toName)
    {
        if (!_nodeMap.TryGetValue(fromName, out var fromNode))
        {
            throw new ArgumentException($"Node '{fromName}' not found", nameof(fromName));
        }

        if (!_nodeMap.TryGetValue(toName, out var toNode))
        {
            throw new ArgumentException($"Node '{toName}' not found", nameof(toName));
        }

        if (!fromNode.Dependencies.Contains(toNode))
        {
            fromNode.Dependencies.Add(toNode);
            _logger.LogDebug("Added dependency: {From} -> {To}", fromName, toName);
        }

        return this;
    }

    /// <summary>
    /// Builds the final graph from the accumulated nodes and dependencies.
    /// </summary>
    /// <returns>A complete graph ready for execution.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph is empty.</exception>
    public Graph Build()
    {
        if (_nodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot build an empty graph");
        }

        _logger.LogInformation("Built command graph: {GraphName} with {NodeCount} nodes",
            _name, _nodes.Count);

        return new Graph
        {
            Name = _name,
            Nodes = _nodes
        };
    }
}

/// <summary>
/// Represents parameters for graph execution.
/// </summary>
public sealed class GraphParameters
{
    /// <summary>
    /// Gets or initializes kernel arguments by node name.
    /// </summary>
    public Dictionary<string, object> KernelArguments { get; init; } = new();

    /// <summary>
    /// Gets or initializes buffer data by node name.
    /// </summary>
    public Dictionary<string, object> BufferData { get; init; } = new();

    /// <summary>
    /// Gets or initializes optional execution hints.
    /// </summary>
    public Dictionary<string, object>? ExecutionHints { get; init; }
}

/// <summary>
/// Represents the result of a graph execution.
/// </summary>
public sealed class GraphExecutionResult
{
    /// <summary>
    /// Gets or initializes the name of the executed graph.
    /// </summary>
    public required string GraphName { get; init; }

    /// <summary>
    /// Gets or initializes the results of individual node executions.
    /// </summary>
    public required IReadOnlyList<NodeExecutionResult> NodeResults { get; init; }

    /// <summary>
    /// Gets or initializes the total execution time.
    /// </summary>
    public required TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Gets or initializes the parallel efficiency (0.0 to 1.0).
    /// </summary>
    public required double ParallelEfficiency { get; init; }

    /// <summary>
    /// Gets or initializes the number of schedule levels.
    /// </summary>
    public required int ScheduleLevels { get; init; }

    /// <summary>
    /// Gets or initializes the number of optimizations applied.
    /// </summary>
    public required int OptimizationsApplied { get; init; }

    /// <summary>
    /// Gets a value indicating whether all nodes executed successfully.
    /// </summary>
    public bool Success => NodeResults.All(r => r.Success);
}

/// <summary>
/// Represents the result of executing a single node.
/// </summary>
public sealed class NodeExecutionResult
{
    /// <summary>
    /// Gets or initializes the name of the executed node.
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// Gets or initializes the type of the node.
    /// </summary>
    public required OpenCLCommandGraph.NodeType NodeType { get; init; }

    /// <summary>
    /// Gets or initializes the execution duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets optional result data from the execution.
    /// </summary>
    public object? ResultData { get; set; }
}

/// <summary>
/// Represents an execution schedule with parallel levels.
/// </summary>
internal sealed class ExecutionSchedule
{
    /// <summary>
    /// Gets or initializes the levels of nodes that can execute in parallel.
    /// </summary>
    public required List<List<OpenCLCommandGraph.Node>> Levels { get; init; }

    /// <summary>
    /// Gets or initializes the total number of nodes in the schedule.
    /// </summary>
    public required int TotalNodes { get; init; }

    /// <summary>
    /// Gets or initializes the maximum parallelism (largest level size).
    /// </summary>
    public required int MaxParallelism { get; init; }
}

/// <summary>
/// Represents execution configuration for a kernel.
/// </summary>
public sealed class ExecutionConfig
{
    /// <summary>
    /// Gets or initializes the global work size.
    /// </summary>
    public required IReadOnlyList<int> GlobalWorkSize { get; init; }

    /// <summary>
    /// Gets or initializes the local work size.
    /// </summary>
    public IReadOnlyList<int>? LocalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the work dimension (1D, 2D, or 3D).
    /// </summary>
    public int WorkDimension { get; set; } = 1;
}

/// <summary>
/// Represents a kernel operation.
/// </summary>
internal sealed class KernelOperation
{
    public required object Kernel { get; init; }
    public required ExecutionConfig Config { get; init; }
    public object? DefaultArguments { get; init; }
}

/// <summary>
/// Represents a memory operation.
/// </summary>
internal sealed class MemoryOperation
{
    public required IUnifiedMemoryBuffer Buffer { get; init; }
    public IUnifiedMemoryBuffer? DestinationBuffer { get; init; }
    public object? Data { get; init; }
    public required MemoryOperationType OperationType { get; init; }
}

/// <summary>
/// Represents a batched memory operation.
/// </summary>
internal sealed class BatchedMemoryOperation
{
    public required List<MemoryOperation> Operations { get; init; }
}

/// <summary>
/// Represents a barrier operation.
/// </summary>
internal sealed class BarrierOperation
{
    // Marker class for barrier operations
}

/// <summary>
/// Represents a marker operation.
/// </summary>
internal sealed class MarkerOperation
{
    // Marker class for timing/debugging
}

/// <summary>
/// Defines the types of memory operations.
/// </summary>
internal enum MemoryOperationType
{
    /// <summary>Write data to buffer.</summary>
    Write,

    /// <summary>Read data from buffer.</summary>
    Read,

    /// <summary>Copy data between buffers.</summary>
    Copy
}
