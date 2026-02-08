// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types;

/// <summary>
/// Directed acyclic graph (DAG) representing Metal command execution dependencies.
/// </summary>
/// <remarks>
/// Enables automatic parallelization and optimization of command sequences with
/// proper dependency tracking and synchronization.
/// </remarks>
public sealed class MetalExecutionGraph
{
    public MetalExecutionGraph()
    {
        Nodes = [];
        Levels = [];
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the execution nodes in the graph.</summary>
    public IList<MetalExecutionNode> Nodes { get; }

    /// <summary>Gets the topologically sorted execution levels.</summary>
    public IList<MetalExecutionLevel> Levels { get; }

    /// <summary>Gets the total number of nodes in the graph.</summary>
    public int TotalNodes => Nodes.Count;

    /// <summary>Gets when this graph was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Adds a node to the execution graph.
    /// </summary>
    public void AddNode(MetalExecutionNode node) => Nodes.Add(node);

    /// <summary>
    /// Builds execution levels from nodes using topological sort.
    /// </summary>
    public void BuildLevels()
    {
        Levels.Clear();
        var processed = new HashSet<int>();
        var currentLevel = 0;

        while (processed.Count < Nodes.Count)
        {
            var levelNodes = Nodes
                .Where(n => !processed.Contains(n.NodeId) &&
                           n.Dependencies.All(d => processed.Contains(d)))
                .ToList();

            if (levelNodes.Count == 0)
            {
                break;
            }

            Levels.Add(new MetalExecutionLevel
            {
                LevelIndex = currentLevel++,
                Nodes = levelNodes
            });

            foreach (var node in levelNodes)
            {
                processed.Add(node.NodeId);
            }
        }
    }

    /// <summary>
    /// Builds an execution plan from the graph by sorting nodes into parallel-executable levels.
    /// </summary>
    public MetalExecutionGraph BuildExecutionPlan()
    {
        BuildLevels();
        return this;
    }
}

/// <summary>
/// Represents a single node in the Metal execution graph.
/// </summary>
public sealed class MetalExecutionNode
{
    public required int NodeId { get; init; }
    public string Id => NodeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public required Action ExecuteAction { get; init; }
    public Func<IntPtr, IntPtr, Task>? Operation { get; init; }
    public string OperationName { get; init; } = "Command";
    public MetalStreamPriority Priority { get; init; } = MetalStreamPriority.Normal;
    public required IList<int> Dependencies { get; init; }
}

/// <summary>
/// Represents a level in the execution graph where all nodes can execute in parallel.
/// </summary>
public sealed class MetalExecutionLevel
{
    public required int LevelIndex { get; init; }
    public required IList<MetalExecutionNode> Nodes { get; init; }
}

/// <summary>
/// Optimized execution plan for Metal command sequences.
/// </summary>
/// <remarks>
/// Generated from <see cref="MetalExecutionGraph"/> with optimizations applied
/// for parallel execution and resource utilization.
/// </remarks>
public sealed class MetalExecutionPlan
{
    public required MetalExecutionGraph Graph { get; init; }
    public required IList<MetalExecutionLevel> OptimizedLevels { get; init; }
    public int EstimatedParallelism { get; init; }
    public TimeSpan EstimatedExecutionTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
