// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using MemoryPattern = DotCompute.Abstractions.Types.MemoryAccessPattern;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Analyzes workload characteristics to guide optimization decisions.
/// </summary>
/// <remarks>
/// Phase 6: Query Optimization - Support class.
/// Provides workload analysis for:
/// - Data size estimation
/// - Compute intensity classification
/// - Memory access pattern detection
/// - Parallelism degree analysis
/// </remarks>
public sealed class WorkloadAnalyzer : IWorkloadAnalyzer
{
    private readonly ILogger<WorkloadAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkloadAnalyzer"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public WorkloadAnalyzer(ILogger<WorkloadAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes an operation graph to determine workload characteristics.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>Workload characteristics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when graph is null.</exception>
    public WorkloadCharacteristics Analyze(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _logger.LogDebug("Analyzing workload characteristics for {OperationCount} operations",
            graph.Operations.Count);

        // Estimate data size
        var dataSize = EstimateDataSize(graph);

        // Determine compute intensity
        var computeIntensity = ClassifyComputeIntensity(graph);

        // Detect memory access pattern
        var memoryPattern = DetectMemoryPattern(graph);

        // Analyze parallelism degree
        var parallelismDegree = AnalyzeParallelism(graph);

        var characteristics = new WorkloadCharacteristics
        {
            DataSize = dataSize,
            ComputeIntensity = computeIntensity,
            MemoryPattern = memoryPattern,
            ParallelismDegree = parallelismDegree
        };

        _logger.LogInformation("Workload analysis: Size={Size}, Intensity={Intensity}, " +
            "Pattern={Pattern}, Parallelism={Parallelism}",
            dataSize, computeIntensity, memoryPattern, parallelismDegree);

        return characteristics;
    }

    /// <summary>
    /// Estimates the total data size for the workload.
    /// </summary>
    private long EstimateDataSize(OperationGraph graph)
    {
        long maxSize = 0;

        foreach (var operation in graph.Operations)
        {
            if (operation.Metadata.TryGetValue("DataSize", out var sizeObj) &&
                sizeObj is long size)
            {
                maxSize = Math.Max(maxSize, size);
            }
        }

        // If no size hints, use heuristic based on operation count
        if (maxSize == 0)
        {
            maxSize = graph.Operations.Count * 1000; // Assume 1K elements per operation
        }

        return maxSize;
    }

    /// <summary>
    /// Classifies the compute intensity of the workload.
    /// </summary>
    private ComputeIntensity ClassifyComputeIntensity(OperationGraph graph)
    {
        var operations = graph.Operations.ToList();

        // Count computeintensive operations
        var intensiveCount = operations.Count(op =>
            op.Type == OperationType.Reduce ||
            op.Type == OperationType.Aggregate ||
            op.Type == OperationType.OrderBy ||
            op.Type == OperationType.Join);

        var totalOperations = operations.Count;

        if (totalOperations == 0)
        {
            return ComputeIntensity.Low;
        }

        var intensiveRatio = (double)intensiveCount / totalOperations;

        if (intensiveRatio >= 0.5)
        {
            return ComputeIntensity.High;
        }
        else if (intensiveRatio >= 0.25)
        {
            return ComputeIntensity.Medium;
        }
        else
        {
            return ComputeIntensity.Low;
        }
    }

    /// <summary>
    /// Detects the dominant memory access pattern.
    /// </summary>
    private MemoryPattern DetectMemoryPattern(OperationGraph graph)
    {
        var operations = graph.Operations.ToList();

        // Check for sequential patterns
        var hasSequential = operations.Any(op =>
            op.Type == OperationType.Map ||
            op.Type == OperationType.Scan);

        // Check for random patterns
        var hasRandom = operations.Any(op =>
            op.Type == OperationType.Join ||
            op.Type == OperationType.GroupBy);

        // Check for strided patterns
        var hasStrided = operations.Any(op =>
            op.Type == OperationType.Filter);

        // Check for gather/scatter patterns
        var hasGather = operations.Any(op =>
            op.Type == OperationType.Reduce ||
            op.Type == OperationType.Aggregate);

        // Prioritize based on impact
        if (hasRandom)
        {
            return MemoryPattern.Random;
        }
        else if (hasGather)
        {
            return MemoryPattern.ScatterGather;
        }
        else if (hasStrided)
        {
            return MemoryPattern.Strided;
        }
        else if (hasSequential)
        {
            return MemoryPattern.Sequential;
        }
        else
        {
            return MemoryPattern.Sequential; // Default
        }
    }

    /// <summary>
    /// Analyzes the degree of parallelism in the workload.
    /// </summary>
    private int AnalyzeParallelism(OperationGraph graph)
    {
        var operations = graph.Operations.ToList();

        // Count operations that can be parallelized
        var parallelizableCount = operations.Count(op =>
            op.Type == OperationType.Map ||
            op.Type == OperationType.Filter ||
            op.Type == OperationType.Reduce ||
            op.Type == OperationType.Aggregate);

        // Parallelism degree is based on independent operations
        var independentGroups = CountIndependentGroups(operations);

        // Return the maximum of parallelizable ops and independent groups
        return Math.Max(parallelizableCount, independentGroups);
    }

    /// <summary>
    /// Counts the number of independent operation groups (no dependencies).
    /// </summary>
    private int CountIndependentGroups(System.Collections.Generic.List<Operation> operations)
    {
        var dependencyMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>();

        // Build dependency map
        foreach (var op in operations)
        {
            dependencyMap.TryAdd(op.Id, new System.Collections.Generic.HashSet<string>(op.Dependencies));
        }

        // Count operations with no dependencies (can run in parallel)
        var independentCount = operations.Count(op => op.Dependencies.Count == 0);

        return Math.Max(1, independentCount);
    }
}

/// <summary>
/// Interface for workload analysis.
/// </summary>
public interface IWorkloadAnalyzer
{
    /// <summary>
    /// Analyzes an operation graph to determine workload characteristics.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>Workload characteristics.</returns>
    WorkloadCharacteristics Analyze(OperationGraph graph);
}
