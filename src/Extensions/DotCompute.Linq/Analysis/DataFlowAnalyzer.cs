// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Pipelines.Analysis;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Analyzes data flow patterns in expression trees for optimization opportunities.
/// </summary>
public class DataFlowAnalyzer
{
    /// <summary>
    /// Builds a data flow graph from a sequence of operators.
    /// </summary>
    /// <param name="operators">The operators to analyze</param>
    /// <returns>Data flow graph</returns>
    public DataFlowGraph BuildDataFlowGraph(List<DotCompute.Linq.Pipelines.Analysis.OperatorInfo> operators)
    {
        var graph = new DataFlowGraph();
        var nodeMap = new Dictionary<DotCompute.Linq.Pipelines.Analysis.OperatorInfo, DataFlowNode>();

        // Create nodes for each operator
        foreach (var op in operators)
        {
            var node = new DataFlowNode
            {
                Id = Guid.NewGuid(),
                OperatorInfo = op,
                InputTypes = op.InputTypes?.ToList() ?? [],
                OutputType = op.OutputType
            };
            graph.Nodes.Add(node);
            nodeMap[op] = node;
        }

        // Create edges based on data dependencies
        for (var i = 1; i < operators.Count; i++)
        {
            var sourceNode = nodeMap[operators[i - 1]];
            var targetNode = nodeMap[operators[i]];


            var edge = new DataFlowEdge
            {
                Source = sourceNode,
                Target = targetNode,
                DataType = sourceNode.OutputType ?? typeof(object)
            };


            graph.Edges.Add(edge);
            sourceNode.Outputs.Add(edge);
            targetNode.Inputs.Add(edge);
        }

        return graph;
    }

    /// <summary>
    /// Identifies bottlenecks in the data flow graph.
    /// </summary>
    /// <param name="graph">The data flow graph</param>
    /// <returns>List of bottlenecks</returns>
    public List<DataFlowBottleneck> IdentifyBottlenecks(DataFlowGraph graph)
    {
        var bottlenecks = new List<DataFlowBottleneck>();

        foreach (var node in graph.Nodes)
        {
            // Check for nodes with high fan-out (many outputs)
            if (node.Outputs.Count > 3)
            {
                bottlenecks.Add(new DataFlowBottleneck
                {
                    Type = BottleneckType.HighFanOut,
                    Node = node,
                    Severity = node.Outputs.Count > 5 ? BottleneckSeverity.High : BottleneckSeverity.Medium,
                    Description = $"Node has {node.Outputs.Count} outputs which may cause memory pressure"
                });
            }

            // Check for nodes with high complexity
            if (node.OperatorInfo.ComplexityScore > 100)
            {
                bottlenecks.Add(new DataFlowBottleneck
                {
                    Type = BottleneckType.HighComplexity,
                    Node = node,
                    Severity = node.OperatorInfo.ComplexityScore > 500 ? BottleneckSeverity.High : BottleneckSeverity.Medium,
                    Description = $"Node has high computational complexity: {node.OperatorInfo.ComplexityScore}"
                });
            }

            // Check for potential memory bottlenecks
            var memoryUsage = EstimateMemoryUsage(node);
            if (memoryUsage > 100_000_000) // 100MB
            {
                bottlenecks.Add(new DataFlowBottleneck
                {
                    Type = BottleneckType.MemoryUsage,
                    Node = node,
                    Severity = memoryUsage > 1_000_000_000 ? BottleneckSeverity.High : BottleneckSeverity.Medium,
                    Description = $"Node has high estimated memory usage: {memoryUsage:N0} bytes"
                });
            }
        }

        return bottlenecks;
    }

    /// <summary>
    /// Estimates memory usage for a data flow node.
    /// </summary>
    /// <param name="node">The node to analyze</param>
    /// <returns>Estimated memory usage in bytes</returns>
    private static long EstimateMemoryUsage(DataFlowNode node)
    {
        // Simple estimation based on data types and complexity
        var baseSize = 0L;


        foreach (var inputType in node.InputTypes)
        {
            baseSize += EstimateTypeSize(inputType);
        }

        if (node.OutputType != null)
        {
            baseSize += EstimateTypeSize(node.OutputType);
        }

        // Apply complexity multiplier
        var complexityMultiplier = Math.Max(1, node.OperatorInfo.ComplexityScore / 10);


        return baseSize * complexityMultiplier;
    }

    /// <summary>
    /// Estimates the size of a type in bytes.
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>Estimated size in bytes</returns>
    private static int EstimateTypeSize(Type type)
    {
        if (type == typeof(byte) || type == typeof(sbyte))
        {
            return 1;
        }


        if (type == typeof(short) || type == typeof(ushort))
        {
            return 2;
        }


        if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
        {
            return 4;
        }


        if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
        {
            return 8;
        }


        if (type == typeof(decimal))
        {
            return 16;
        }


        if (type == typeof(bool))
        {
            return 1;
        }


        if (type == typeof(char))
        {
            return 2;
        }

        // For reference types, assume pointer size + some overhead

        if (!type.IsValueType)
        {
            return 8 + 32; // 64-bit pointer + estimated object overhead
        }

        // For other value types, assume a reasonable size

        return 8;
    }
}

/// <summary>
/// Represents a data flow graph.
/// </summary>
public class DataFlowGraph
{
    /// <summary>
    /// Gets or sets the nodes in the graph.
    /// </summary>
    public List<DataFlowNode> Nodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the edges in the graph.
    /// </summary>
    public List<DataFlowEdge> Edges { get; set; } = [];
}

/// <summary>
/// Represents a node in the data flow graph.
/// </summary>
public class DataFlowNode
{
    /// <summary>
    /// Gets or sets the unique identifier for this node.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the operator information.
    /// </summary>
    public Pipelines.Analysis.OperatorInfo OperatorInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the input types for this node.
    /// </summary>
    public List<Type> InputTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the output type for this node.
    /// </summary>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the incoming edges.
    /// </summary>
    public List<DataFlowEdge> Inputs { get; set; } = [];

    /// <summary>
    /// Gets or sets the outgoing edges.
    /// </summary>
    public List<DataFlowEdge> Outputs { get; set; } = [];
}

/// <summary>
/// Represents an edge in the data flow graph.
/// </summary>
public class DataFlowEdge
{
    /// <summary>
    /// Gets or sets the source node.
    /// </summary>
    public DataFlowNode Source { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target node.
    /// </summary>
    public DataFlowNode Target { get; set; } = null!;

    /// <summary>
    /// Gets or sets the data type flowing through this edge.
    /// </summary>
    public Type DataType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the estimated data volume.
    /// </summary>
    public long EstimatedVolume { get; set; }
}

/// <summary>
/// Represents a bottleneck in the data flow.
/// </summary>
public class DataFlowBottleneck
{
    /// <summary>
    /// Gets or sets the type of bottleneck.
    /// </summary>
    public BottleneckType Type { get; set; }

    /// <summary>
    /// Gets or sets the node causing the bottleneck.
    /// </summary>
    public DataFlowNode Node { get; set; } = null!;

    /// <summary>
    /// Gets or sets the severity of the bottleneck.
    /// </summary>
    public BottleneckSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the description of the bottleneck.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets suggested optimizations.
    /// </summary>
    public List<string> SuggestedOptimizations { get; set; } = [];
}

/// <summary>
/// Types of bottlenecks in data flow analysis.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// High fan-out causing memory pressure.
    /// </summary>
    HighFanOut,

    /// <summary>
    /// High computational complexity.
    /// </summary>
    HighComplexity,

    /// <summary>
    /// High memory usage.
    /// </summary>
    MemoryUsage,

    /// <summary>
    /// Synchronization bottleneck.
    /// </summary>
    Synchronization,

    /// <summary>
    /// I/O bottleneck.
    /// </summary>
    IO
}

/// <summary>
/// Severity levels for bottlenecks.
/// </summary>
public enum BottleneckSeverity
{
    /// <summary>
    /// Low severity, minor impact.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity, moderate impact.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity, significant impact.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity, major performance impact.
    /// </summary>
    Critical
}