// <copyright file="DataFlowGraph.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Represents a data flow graph for analyzing dependencies in expressions.
/// </summary>
public class DataFlowGraph
{
    private readonly Dictionary<string, DataFlowNode> _nodes = [];
    private readonly List<DataFlowEdge> _edges = [];

    /// <summary>Gets all nodes in the graph.</summary>
    public IReadOnlyCollection<DataFlowNode> Nodes => _nodes.Values;

    /// <summary>Gets all edges in the graph.</summary>
    public IReadOnlyCollection<DataFlowEdge> Edges => _edges;

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public void AddNode(DataFlowNode node)
    {
        _nodes[node.Id] = node;
    }

    /// <summary>
    /// Adds an edge to the graph.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    public void AddEdge(DataFlowEdge edge)
    {
        _edges.Add(edge);
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>The node if found; otherwise, null.</returns>
    public DataFlowNode? GetNode(string nodeId)
    {
        return _nodes.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// Gets all dependencies of a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>List of dependent nodes.</returns>
    public List<DataFlowNode> GetDependencies(string nodeId)
    {
        return _edges
            .Where(e => e.ToNodeId == nodeId)
            .Select(e => _nodes[e.FromNodeId])
            .ToList();
    }

    /// <summary>
    /// Gets all dependents of a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>List of dependent nodes.</returns>
    public List<DataFlowNode> GetDependents(string nodeId)
    {
        return _edges
            .Where(e => e.FromNodeId == nodeId)
            .Select(e => _nodes[e.ToNodeId])
            .ToList();
    }

    /// <summary>
    /// Performs topological sort of the graph.
    /// </summary>
    /// <returns>Topologically sorted list of nodes.</returns>
    public List<DataFlowNode> TopologicalSort()
    {
        var result = new List<DataFlowNode>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var node in _nodes.Values)
        {
            if (!visited.Contains(node.Id))
            {
                TopologicalSortDfs(node.Id, visited, visiting, result);
            }
        }

        result.Reverse();
        return result;
    }

    private void TopologicalSortDfs(string nodeId, HashSet<string> visited, HashSet<string> visiting, List<DataFlowNode> result)
    {
        if (visiting.Contains(nodeId))
        {
            throw new InvalidOperationException("Circular dependency detected");
        }

        if (visited.Contains(nodeId))
        {
            return;
        }

        visiting.Add(nodeId);

        foreach (var dependent in GetDependents(nodeId))
        {
            TopologicalSortDfs(dependent.Id, visited, visiting, result);
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
        result.Add(_nodes[nodeId]);
    }
}

/// <summary>
/// Represents a node in the data flow graph.
/// </summary>
public record DataFlowNode
{
    /// <summary>Gets the unique node identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the node type.</summary>
    public DataFlowNodeType Type { get; init; }

    /// <summary>Gets the node value or expression.</summary>
    public object? Value { get; init; }

    /// <summary>Gets additional metadata for the node.</summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents an edge in the data flow graph.
/// </summary>
public record DataFlowEdge
{
    /// <summary>Gets the source node ID.</summary>
    public string FromNodeId { get; init; } = string.Empty;

    /// <summary>Gets the target node ID.</summary>
    public string ToNodeId { get; init; } = string.Empty;

    /// <summary>Gets the edge type.</summary>
    public DataFlowEdgeType Type { get; init; }

    /// <summary>Gets the edge weight or importance.</summary>
    public double Weight { get; init; } = 1.0;
}

// DataFlowBottleneck is defined in DataFlowBottleneck.cs to avoid duplication

/// <summary>
/// Defines types of data flow nodes.
/// </summary>
public enum DataFlowNodeType
{
    /// <summary>Input parameter or variable.</summary>
    Input,

    /// <summary>Output result.</summary>
    Output,

    /// <summary>Computation operation.</summary>
    Operation,

    /// <summary>Constant value.</summary>
    Constant,

    /// <summary>Memory access.</summary>
    MemoryAccess,

    /// <summary>Control flow.</summary>
    ControlFlow
}

/// <summary>
/// Defines types of data flow edges.
/// </summary>
public enum DataFlowEdgeType
{
    /// <summary>Data dependency.</summary>
    DataDependency,

    /// <summary>Control dependency.</summary>
    ControlDependency,

    /// <summary>Memory dependency.</summary>
    MemoryDependency,

    /// <summary>Anti-dependency.</summary>
    AntiDependency
}


// BottleneckType is defined in DataFlowBottleneck.cs to avoid duplication