// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

using System;
namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Represents a directed graph of dependencies between tasks, layers, or stages.
    /// </summary>
    /// <remarks>
    /// This graph structure is used to model dependencies in execution plans and supports
    /// topological sorting for determining safe execution ordering. It tracks various types
    /// of dependencies including data hazards, structural dependencies, and control flow.
    /// </remarks>
    public sealed class DependencyGraph
    {
        private readonly Dictionary<int, HashSet<int>> _dependencies;
        private readonly Dictionary<int, List<DependencyType>> _dependencyTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyGraph"/> class.
        /// </summary>
        public DependencyGraph()
        {
            _dependencies = [];
            _dependencyTypes = [];
        }

        /// <summary>
        /// Gets the total number of dependencies in the graph.
        /// </summary>
        /// <value>The sum of all dependency connections across all nodes.</value>
        public int TotalDependencies => _dependencies.Values.Sum(deps => deps.Count);

        /// <summary>
        /// Adds a dependency relationship between two nodes.
        /// </summary>
        /// <param name="from">The source node identifier.</param>
        /// <param name="to">The target node identifier that depends on the source.</param>
        /// <param name="type">The type of dependency relationship.</param>
        /// <remarks>
        /// This creates a directed edge from the 'from' node to the 'to' node,
        /// indicating that 'to' depends on 'from' and must execute after it.
        /// </remarks>
        public void AddDependency(int from, int to, DependencyType type)
        {
            if (!_dependencies.TryGetValue(to, out var deps))
            {
                deps = [];
                _dependencies[to] = deps;
            }

            _ = deps.Add(from);

            if (!_dependencyTypes.TryGetValue(to, out var types))
            {
                types = [];
                _dependencyTypes[to] = types;
            }

            types.Add(type);
        }

        /// <summary>
        /// Gets the list of nodes that a given node depends on.
        /// </summary>
        /// <param name="taskId">The node identifier to query dependencies for.</param>
        /// <returns>
        /// A list of node identifiers that the specified node depends on.
        /// Returns an empty list if the node has no dependencies.
        /// </returns>
        public List<int> GetDependencies(int taskId)
            => _dependencies.TryGetValue(taskId, out var deps) ? [.. deps] : [];

        /// <summary>
        /// Gets the dependency types for a given node.
        /// </summary>
        /// <param name="taskId">The node identifier to query dependency types for.</param>
        /// <returns>
        /// A list of <see cref="DependencyType"/> values representing the types of
        /// dependencies for the specified node. Returns an empty list if the node has no dependencies.
        /// </returns>
        public List<DependencyType> GetDependencyTypes(int taskId)
            => _dependencyTypes.TryGetValue(taskId, out var types) ? [.. types] : [];

        /// <summary>
        /// Determines whether a node has any dependencies.
        /// </summary>
        /// <param name="taskId">The node identifier to check.</param>
        /// <returns>
        /// <c>true</c> if the node has one or more dependencies; otherwise, <c>false</c>.
        /// </returns>
        public bool HasDependencies(int taskId)
            => _dependencies.ContainsKey(taskId) && _dependencies[taskId].Count > 0;

        /// <summary>
        /// Performs topological sort to get a safe execution order.
        /// </summary>
        /// <returns>
        /// A list of node identifiers in topological order, where each node appears
        /// after all nodes it depends on.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a circular dependency is detected in the graph.
        /// </exception>
        /// <remarks>
        /// The returned order ensures that dependencies are satisfied before execution.
        /// Nodes with no dependencies will appear earlier in the list.
        /// </remarks>
        public List<int> TopologicalSort()
        {
            var sorted = new List<int>();
            var visited = new HashSet<int>();
            var visiting = new HashSet<int>();
            var allNodes = GetAllNodes();

            foreach (var node in allNodes)
            {
                if (!visited.Contains(node))
                {
                    TopologicalSortVisit(node, visited, visiting, sorted);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Gets all nodes that have dependencies but are not dependencies of others.
        /// </summary>
        /// <returns>A list of node identifiers that represent leaf nodes in the dependency graph.</returns>
        public List<int> GetLeafNodes()
        {
            var allNodes = GetAllNodes();
            var dependencyNodes = _dependencies.Values.SelectMany(deps => deps).ToHashSet();
            return allNodes.Where(node => !dependencyNodes.Contains(node)).ToList();
        }

        /// <summary>
        /// Gets all nodes that have no dependencies (root nodes).
        /// </summary>
        /// <returns>A list of node identifiers that can be executed immediately.</returns>
        public List<int> GetRootNodes()
        {
            var allNodes = GetAllNodes();
            return allNodes.Where(node => !HasDependencies(node)).ToList();
        }

        /// <summary>
        /// Creates a deep copy of the dependency graph.
        /// </summary>
        /// <returns>A new <see cref="DependencyGraph"/> instance with the same dependencies.</returns>
        public DependencyGraph Clone()
        {
            var clone = new DependencyGraph();


            foreach (var kvp in _dependencies)
            {
                var targetNode = kvp.Key;
                var dependencies = kvp.Value;
                var dependencyTypes = _dependencyTypes[targetNode];

                for (var i = 0; i < dependencies.Count; i++)
                {
                    var sourceNode = dependencies.ElementAt(i);
                    var dependencyType = dependencyTypes[i];
                    clone.AddDependency(sourceNode, targetNode, dependencyType);
                }
            }

            return clone;
        }

        /// <summary>
        /// Recursive helper method for topological sorting using depth-first search.
        /// </summary>
        /// <param name="node">The current node being visited.</param>
        /// <param name="visited">Set of completely visited nodes.</param>
        /// <param name="visiting">Set of nodes currently being visited (for cycle detection).</param>
        /// <param name="sorted">The result list being built in reverse topological order.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a circular dependency is detected.
        /// </exception>
        private void TopologicalSortVisit(int node, HashSet<int> visited, HashSet<int> visiting, List<int> sorted)
        {
            if (visiting.Contains(node))
            {
                throw new InvalidOperationException($"Circular dependency detected involving node {node}");
            }

            if (visited.Contains(node))
            {
                return;
            }

            _ = visiting.Add(node);

            var deps = GetDependencies(node);
            foreach (var dep in deps)
            {
                TopologicalSortVisit(dep, visited, visiting, sorted);
            }

            _ = visiting.Remove(node);
            _ = visited.Add(node);
            sorted.Add(node);
        }

        /// <summary>
        /// Gets all unique node identifiers present in the graph.
        /// </summary>
        /// <returns>A set containing all node identifiers that appear in the dependency graph.</returns>
        private HashSet<int> GetAllNodes()
        {
            var nodes = new HashSet<int>();
            foreach (var kvp in _dependencies)
            {
                _ = nodes.Add(kvp.Key);
                foreach (var dep in kvp.Value)
                {
                    _ = nodes.Add(dep);
                }
            }
            return nodes;
        }
    }
}