// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Directed acyclic graph (DAG) for CUDA command execution with dependencies.
/// </summary>
/// <remarks>
/// Enables CUDA graph-like optimizations with automatic parallelization and
/// dependency resolution for complex execution patterns.
/// </remarks>
public sealed class CudaExecutionGraph
{
    private readonly List<CudaExecutionNode> _nodes = [];
    private readonly List<CudaExecutionLevel> _levels = [];

    public CudaExecutionGraph()
    {
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets execution nodes in the graph.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Mutable list needed for graph construction")]
    public List<CudaExecutionNode> Nodes => _nodes;

    /// <summary>Gets topologically sorted execution levels.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Mutable list needed for graph construction")]
    public List<CudaExecutionLevel> Levels => _levels;

    /// <summary>Gets when this graph was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Adds a node to the execution graph.</summary>
    public void AddNode(CudaExecutionNode node) => Nodes.Add(node);

    /// <summary>
    /// Builds execution levels using topological sort for parallel execution.
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

            Levels.Add(new CudaExecutionLevel
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
    /// Builds an optimized execution plan from this graph.
    /// </summary>
    public CudaExecutionPlan BuildExecutionPlan()
    {
        BuildLevels();

        return new CudaExecutionPlan
        {
            Graph = this,
            OptimizedLevels = Levels.ToList(),
            EstimatedParallelism = Levels.Count > 0 ? Levels.Max(l => l.Nodes.Count) : 0,
            EstimatedExecutionTime = TimeSpan.FromTicks(Nodes.Sum(n => n.EstimatedDuration.Ticks)),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Represents a single node in the CUDA execution graph.
/// </summary>
public sealed class CudaExecutionNode
{
    public required int NodeId { get; init; }
    public required Action ExecuteAction { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Mutable list needed for dependency tracking")]
    public required List<int> Dependencies { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public IntPtr? AssignedStream { get; set; }
}

/// <summary>
/// Execution level where all nodes can execute in parallel.
/// </summary>
public sealed class CudaExecutionLevel
{
    public required int LevelIndex { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Mutable list needed for level construction")]
    public required List<CudaExecutionNode> Nodes { get; init; }
}

/// <summary>
/// Optimized execution plan for CUDA command sequences.
/// </summary>
/// <remarks>
/// Generated from <see cref="CudaExecutionGraph"/> with RTX 2000 optimizations
/// for stream assignment and parallel execution.
/// </remarks>
public sealed class CudaExecutionPlan
{
    public required CudaExecutionGraph Graph { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Mutable list needed for execution plan")]
    public required List<CudaExecutionLevel> OptimizedLevels { get; init; }
    public int EstimatedParallelism { get; init; }
    public TimeSpan EstimatedExecutionTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
