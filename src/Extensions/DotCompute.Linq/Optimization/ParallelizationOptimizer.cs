// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Optimizes operation graphs for parallel execution across multiple cores and GPUs.
/// </summary>
/// <remarks>
/// Phase 6: Query Optimization - Support class.
/// Implements parallelization strategies:
/// - Data parallelism (split data across workers)
/// - Pipeline parallelism (overlap operations)
/// - Task parallelism (independent operations in parallel)
/// </remarks>
public sealed class ParallelizationOptimizer : IOptimizer
{
    private readonly ILogger<ParallelizationOptimizer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelizationOptimizer"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public ParallelizationOptimizer(ILogger<ParallelizationOptimizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Optimizes an operation graph for parallel execution.
    /// </summary>
    /// <param name="graph">The operation graph to optimize.</param>
    /// <returns>The optimized operation graph with parallelization hints.</returns>
    /// <exception cref="ArgumentNullException">Thrown when graph is null.</exception>
    public OperationGraph Optimize(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _logger.LogDebug("Analyzing parallelization opportunities for {OperationCount} operations",
            graph.Operations.Count);

        var operations = graph.Operations.ToList();
        var parallelizableCount = 0;

        // Analyze each operation for parallelization potential
        foreach (var operation in operations)
        {
            if (IsParallelizable(operation, operations))
            {
                // Add parallelization hints
                operation.Metadata["Parallelizable"] = true;
                operation.Metadata["ParallelizationStrategy"] = DetermineStrategy(operation);
                operation.Metadata["RecommendedThreadCount"] = EstimateThreadCount(operation);

                parallelizableCount++;

                _logger.LogTrace("Operation {OperationId} ({Type}) is parallelizable with strategy {Strategy}",
                    operation.Id, operation.Type, operation.Metadata["ParallelizationStrategy"]);
            }
        }

        _logger.LogInformation("Parallelization analysis complete: {Count}/{Total} operations can be parallelized",
            parallelizableCount, operations.Count);

        return new OperationGraph(operations) { Root = graph.Root };
    }

    /// <summary>
    /// Determines if an operation can be parallelized.
    /// </summary>
    private bool IsParallelizable(Operation operation, List<Operation> allOperations)
    {
        // Check operation type
        var isInherentlyParallel = operation.Type switch
        {
            OperationType.Map => true,          // Embarrassingly parallel
            OperationType.Filter => true,        // Embarrassingly parallel
            OperationType.Reduce => true,        // Tree reduction is parallel
            OperationType.Aggregate => true,     // Can use parallel reduction
            OperationType.Scan => false,         // Sequential dependencies
            OperationType.Join => true,          // Hash join can be parallel
            OperationType.GroupBy => true,       // Can use parallel hashing
            OperationType.OrderBy => true,       // Parallel sorting algorithms
            _ => false
        };

        if (!isInherentlyParallel)
        {
            return false;
        }

        // Check for data dependencies that prevent parallelization
        var hasCircularDependency = HasCircularDependency(operation, allOperations);
        if (hasCircularDependency)
        {
            return false;
        }

        // Check for sequential constraints in metadata
        if (operation.Metadata.TryGetValue("RequiresSequential", out var requiresSeq) &&
            requiresSeq is bool isSequential && isSequential)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines the optimal parallelization strategy for an operation.
    /// </summary>
    private string DetermineStrategy(Operation operation)
    {
        return operation.Type switch
        {
            OperationType.Map => "DataParallel",
            OperationType.Filter => "DataParallel",
            OperationType.Reduce => "TreeReduction",
            OperationType.Aggregate => "TreeReduction",
            OperationType.Join => "HashPartition",
            OperationType.GroupBy => "HashPartition",
            OperationType.OrderBy => "ParallelSort",
            _ => "DataParallel"
        };
    }

    /// <summary>
    /// Estimates the optimal thread count for an operation.
    /// </summary>
    private int EstimateThreadCount(Operation operation)
    {
        // Get data size hint if available
        if (operation.Metadata.TryGetValue("DataSize", out var dataSizeObj) &&
            dataSizeObj is long dataSize)
        {
            // Rule of thumb: 1 thread per 10K elements, capped at hardware thread count
            var hardwareThreads = Environment.ProcessorCount;
            var recommendedThreads = Math.Min((int)(dataSize / 10_000), hardwareThreads);
            return Math.Max(1, recommendedThreads);
        }

        // Default to half of hardware threads (leave some for system)
        return Math.Max(1, Environment.ProcessorCount / 2);
    }

    /// <summary>
    /// Checks if an operation has circular dependencies.
    /// </summary>
    private bool HasCircularDependency(Operation operation, List<Operation> allOperations)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        return HasCycleDFS(operation.Id, visited, recursionStack, allOperations);
    }

    /// <summary>
    /// Performs depth-first search to detect cycles in the operation graph.
    /// </summary>
    private bool HasCycleDFS(string operationId, HashSet<string> visited,
        HashSet<string> recursionStack, List<Operation> allOperations)
    {
        if (recursionStack.Contains(operationId))
        {
            return true; // Cycle detected
        }

        if (visited.Contains(operationId))
        {
            return false; // Already processed
        }

        visited.Add(operationId);
        recursionStack.Add(operationId);

        var operation = allOperations.FirstOrDefault(op => op.Id == operationId);
        if (operation != null)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (HasCycleDFS(dependency, visited, recursionStack, allOperations))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(operationId);
        return false;
    }
}

/// <summary>
/// Interface for parallelization optimization strategies.
/// </summary>
public interface IParallelizationOptimizer
{
    /// <summary>
    /// Optimizes an operation graph for parallel execution.
    /// </summary>
    /// <param name="graph">The operation graph to optimize.</param>
    /// <returns>The optimized operation graph.</returns>
    OperationGraph Optimize(OperationGraph graph);
}
