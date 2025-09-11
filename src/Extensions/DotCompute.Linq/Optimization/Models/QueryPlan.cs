// <copyright file="QueryPlan.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Linq.Optimization.Models;

/// <summary>
/// Represents a complete query execution plan consisting of multiple operations.
/// </summary>
public class QueryPlan
{
    /// <summary>Gets or sets the list of operations in execution order.</summary>
    public List<QueryOperation> Operations { get; set; } = new();

    /// <summary>Gets or sets the plan identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the plan name or description.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the estimated total execution cost.</summary>
    public double EstimatedCost { get; set; }

    /// <summary>Gets or sets the estimated total execution time.</summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>Gets or sets the estimated peak memory usage.</summary>
    public long EstimatedPeakMemoryUsage { get; set; }

    /// <summary>Gets or sets plan-specific metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Gets or sets whether this plan supports parallel execution.</summary>
    public bool SupportsParallelExecution { get; set; } = true;

    /// <summary>Gets or sets the target backend for execution.</summary>
    public string TargetBackend { get; set; } = "Auto";

    /// <summary>Gets or sets optimization hints for this plan.</summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>Gets or sets the plan creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the plan last modified timestamp.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the total input size across all operations.
    /// </summary>
    public long TotalInputSize => Operations.Sum(op => op.InputSize);

    /// <summary>
    /// Gets the total output size across all operations.
    /// </summary>
    public long TotalOutputSize => Operations.Sum(op => op.OutputSize);

    /// <summary>
    /// Gets the number of operations in this plan.
    /// </summary>
    public int OperationCount => Operations.Count;

    /// <summary>
    /// Gets the maximum parallelization degree for this plan.
    /// </summary>
    public int MaxParallelizationDegree { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Adds an operation to the plan.
    /// </summary>
    /// <param name="operation">The operation to add.</param>
    public void AddOperation(QueryOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Operations.Add(operation);
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes an operation from the plan.
    /// </summary>
    /// <param name="operationId">The ID of the operation to remove.</param>
    /// <returns>True if the operation was found and removed; otherwise, false.</returns>
    public bool RemoveOperation(string operationId)
    {
        var operation = Operations.FirstOrDefault(op => op.Id == operationId);
        if (operation != null)
        {
            Operations.Remove(operation);
            LastModified = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets an operation by its ID.
    /// </summary>
    /// <param name="operationId">The operation ID.</param>
    /// <returns>The operation if found; otherwise, null.</returns>
    public QueryOperation? GetOperation(string operationId)
    {
        return Operations.FirstOrDefault(op => op.Id == operationId);
    }

    /// <summary>
    /// Creates a deep copy of this query plan.
    /// </summary>
    /// <returns>A new QueryPlan instance with the same properties.</returns>
    public QueryPlan Clone()
    {
        return new QueryPlan
        {
            Operations = Operations.Select(op => op.Clone()).ToList(),
            Id = Guid.NewGuid().ToString(), // Generate new ID for clone
            Name = $"{Name}_Clone",
            EstimatedCost = EstimatedCost,
            EstimatedExecutionTime = EstimatedExecutionTime,
            EstimatedPeakMemoryUsage = EstimatedPeakMemoryUsage,
            Metadata = new Dictionary<string, object>(Metadata),
            SupportsParallelExecution = SupportsParallelExecution,
            TargetBackend = TargetBackend,
            OptimizationHints = new List<string>(OptimizationHints),
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            MaxParallelizationDegree = MaxParallelizationDegree
        };
    }

    /// <summary>
    /// Validates the query plan for consistency.
    /// </summary>
    /// <returns>A list of validation errors, empty if plan is valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Check for circular dependencies
        if (HasCircularDependencies())
        {
            errors.Add("Query plan contains circular dependencies");
        }

        // Check for missing dependencies
        var missingDeps = GetMissingDependencies();
        if (missingDeps.Any())
        {
            errors.Add($"Missing dependencies: {string.Join(", ", missingDeps)}");
        }

        // Check for empty operations
        if (!Operations.Any())
        {
            errors.Add("Query plan contains no operations");
        }

        return errors;
    }

    /// <summary>
    /// Checks if the plan has circular dependencies.
    /// </summary>
    /// <returns>True if circular dependencies exist; otherwise, false.</returns>
    private bool HasCircularDependencies()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var operation in Operations)
        {
            if (HasCircularDependencyDfs(operation.Id, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Depth-first search to detect circular dependencies.
    /// </summary>
    private bool HasCircularDependencyDfs(string operationId, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(operationId))
        {
            return true; // Circular dependency found
        }

        if (visited.Contains(operationId))
        {
            return false; // Already processed
        }

        visited.Add(operationId);
        recursionStack.Add(operationId);

        var operation = GetOperation(operationId);
        if (operation != null)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (HasCircularDependencyDfs(dependency, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(operationId);
        return false;
    }

    /// <summary>
    /// Gets missing dependencies in the plan.
    /// </summary>
    /// <returns>A list of missing dependency IDs.</returns>
    private List<string> GetMissingDependencies()
    {
        var operationIds = new HashSet<string>(Operations.Select(op => op.Id));
        var missingDeps = new List<string>();

        foreach (var operation in Operations)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (!operationIds.Contains(dependency))
                {
                    missingDeps.Add(dependency);
                }
            }
        }

        return missingDeps.Distinct().ToList();
    }
}