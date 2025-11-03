// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Represents dependencies between operations in a LINQ query.
/// </summary>
/// <remarks>
/// The dependency graph tracks relationships between operation nodes and provides
/// algorithms for topological sorting and cycle detection to ensure correct execution order.
/// </remarks>
public sealed class DependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _dependencies;
    private readonly Dictionary<string, HashSet<string>> _dependents;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyGraph"/> class.
    /// </summary>
    public DependencyGraph()
    {
        _dependencies = new Dictionary<string, HashSet<string>>();
        _dependents = new Dictionary<string, HashSet<string>>();
    }

    /// <summary>
    /// Adds a dependency relationship between two nodes.
    /// </summary>
    /// <param name="nodeId">The identifier of the dependent node.</param>
    /// <param name="dependsOn">The identifier of the node that is depended upon.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodeId"/> or <paramref name="dependsOn"/> is null.
    /// </exception>
    /// <remarks>
    /// This establishes that <paramref name="nodeId"/> must execute after <paramref name="dependsOn"/>.
    /// </remarks>
    public void AddDependency(string nodeId, string dependsOn)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(dependsOn);

        // Add to forward dependencies (node -> dependencies)
        if (!_dependencies.TryGetValue(nodeId, out var deps))
        {
            deps = new HashSet<string>();
            _dependencies[nodeId] = deps;
        }
        deps.Add(dependsOn);

        // Add to reverse dependencies (node -> dependents)
        if (!_dependents.TryGetValue(dependsOn, out var dependentsList))
        {
            dependentsList = new HashSet<string>();
            _dependents[dependsOn] = dependentsList;
        }
        dependentsList.Add(nodeId);
    }

    /// <summary>
    /// Gets the direct dependencies for a node.
    /// </summary>
    /// <param name="nodeId">The identifier of the node to get dependencies for.</param>
    /// <returns>
    /// A read-only list of node identifiers that the specified node depends on.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodeId"/> is null.</exception>
    public IReadOnlyList<string> GetDependencies(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);

        return _dependencies.TryGetValue(nodeId, out var deps)
            ? new Collection<string>(deps.ToList())
            : new Collection<string>();
    }

    /// <summary>
    /// Gets the direct dependents for a node (nodes that depend on this one).
    /// </summary>
    /// <param name="nodeId">The identifier of the node to get dependents for.</param>
    /// <returns>
    /// A read-only list of node identifiers that depend on the specified node.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodeId"/> is null.</exception>
    public IReadOnlyList<string> GetDependents(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);

        return _dependents.TryGetValue(nodeId, out var deps)
            ? new Collection<string>(deps.ToList())
            : new Collection<string>();
    }

    /// <summary>
    /// Performs a topological sort on the dependency graph.
    /// </summary>
    /// <returns>
    /// A read-only list of node identifiers in topological order (dependencies before dependents).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the graph contains cycles and cannot be sorted topologically.
    /// </exception>
    /// <remarks>
    /// The topological sort ensures that operations are executed in the correct order,
    /// with all dependencies satisfied before an operation is executed.
    /// </remarks>
    public IReadOnlyList<string> TopologicalSort()
    {
        if (HasCycle())
        {
            throw new InvalidOperationException(
                "Cannot perform topological sort on a graph with cycles.");
        }

        var result = new List<string>();
        var visited = new HashSet<string>();
        var allNodes = _dependencies.Keys.Union(_dependents.Keys).ToHashSet();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                TopologicalSortVisit(node, visited, result);
            }
        }

        result.Reverse();
        return new Collection<string>(result);
    }

    /// <summary>
    /// Determines whether the dependency graph contains a cycle.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the graph contains at least one cycle; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Cycles in the dependency graph indicate circular dependencies,
    /// which would make execution impossible without breaking the cycle.
    /// </remarks>
    public bool HasCycle()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var allNodes = _dependencies.Keys.Union(_dependents.Keys).ToHashSet();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                if (HasCycleHelper(node, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void TopologicalSortVisit(string node, HashSet<string> visited, List<string> result)
    {
        visited.Add(node);

        if (_dependencies.TryGetValue(node, out var deps))
        {
            foreach (var dep in deps)
            {
                if (!visited.Contains(dep))
                {
                    TopologicalSortVisit(dep, visited, result);
                }
            }
        }

        result.Add(node);
    }

    private bool HasCycleHelper(string node, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(node);
        recursionStack.Add(node);

        if (_dependencies.TryGetValue(node, out var deps))
        {
            foreach (var dep in deps)
            {
                if (!visited.Contains(dep))
                {
                    if (HasCycleHelper(dep, visited, recursionStack))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(dep))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(node);
        return false;
    }
}
