using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Categorizes LINQ operations and determines parallelization strategies.
/// </summary>
/// <remarks>
/// This analyzer examines operation graphs to:
/// - Classify operations by type (Map, Filter, Reduce, etc.)
/// - Determine optimal parallelization strategies
/// - Detect kernel fusion opportunities
/// - Analyze data flow patterns and dependencies
/// </remarks>
public class OperationCategorizer
{
    private const int SmallDataThreshold = 10_000;
    private const int LargeDataThreshold = 1_000_000;

    /// <summary>
    /// Categorizes an expression by operation type.
    /// </summary>
    /// <param name="expr">The expression to categorize.</param>
    /// <returns>The operation type of the expression.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expr"/> is null.</exception>
    public OperationType Categorize(Expression expr)
    {
        ArgumentNullException.ThrowIfNull(expr);

        return expr switch
        {
            MethodCallExpression methodCall => CategorizeMethodCall(methodCall),
            UnaryExpression unary => Categorize(unary.Operand),
            BinaryExpression binary => CategorizeBinaryExpression(binary),
            LambdaExpression lambda => Categorize(lambda.Body),
            _ => OperationType.Map // Default for transformations
        };
    }

    /// <summary>
    /// Determines the optimal parallelization strategy for an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>The recommended parallelization strategy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is null.</exception>
    public ParallelizationStrategy GetStrategy(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var analysis = AnalyzeDataFlow(graph);
        var dataSize = analysis.EstimatedDataSize;
        var intensity = analysis.Intensity;
        var hasSideEffects = analysis.HasSideEffects;

        // Side effects require sequential execution
        if (hasSideEffects)
        {
            return ParallelizationStrategy.Sequential;
        }

        // OrderBy and Scan operations require sequential processing
        if (HasOrderDependencies(graph))
        {
            return ParallelizationStrategy.Sequential;
        }

        // Mixed workload with sufficient data → Hybrid approach (check before size thresholds)
        if (HasMixedWorkload(graph) && dataSize >= SmallDataThreshold)
        {
            return ParallelizationStrategy.Hybrid;
        }

        // Empty graph defaults to DataParallel
        if (graph.Operations.Count == 0)
        {
            return ParallelizationStrategy.DataParallel;
        }

        // Small data sets benefit from sequential execution (avoid overhead)
        if (dataSize < SmallDataThreshold)
        {
            return ParallelizationStrategy.Sequential;
        }

        // Very large data with high compute intensity → GPU
        if (dataSize > LargeDataThreshold && intensity >= Optimization.ComputeIntensity.High)
        {
            return ParallelizationStrategy.GpuParallel;
        }

        // Large data with medium intensity → Task-based parallelism
        if (dataSize > LargeDataThreshold && intensity >= Optimization.ComputeIntensity.Medium)
        {
            return ParallelizationStrategy.TaskParallel;
        }

        // Medium to large data with simple operations → SIMD vectorization
        return ParallelizationStrategy.DataParallel; // Default for parallelizable workloads
    }

    /// <summary>
    /// Builds a dependency graph from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <returns>A dependency graph showing operation relationships.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is null.</exception>
    public ExtendedDependencyGraph BuildDependencies(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var baseGraph = new DependencyGraph();
        var dataDependencies = new Dictionary<string, List<DataDependency>>();

        // Build direct dependencies from operation graph first
        foreach (var operation in graph.Operations)
        {
            foreach (var depId in operation.Dependencies)
            {
                baseGraph.AddDependency(operation.Id, depId);
            }
        }

        // Now analyze data dependencies (includes transitive dependencies)
        foreach (var operation in graph.Operations)
        {
            var deps = new List<DataDependency>();
            var allDeps = GetTransitiveDependencies(baseGraph, operation.Id);
            foreach (var depId in allDeps)
            {
                var depOp = graph.Operations.FirstOrDefault(o => o.Id == depId);
                if (depOp != null)
                {
                    deps.Add(AnalyzeDataDependency(operation, depOp));
                }
            }
            dataDependencies[operation.Id] = deps;
        }

        return new ExtendedDependencyGraph
        {
            BaseGraph = baseGraph,
            DataDependencies = dataDependencies,
            ParallelizableGroups = FindParallelizableGroups(graph, baseGraph)
        };
    }

    /// <summary>
    /// Analyzes data flow patterns in the operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>Data flow analysis results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is null.</exception>
    public DataFlowAnalysis AnalyzeDataFlow(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var hasSideEffects = DetectSideEffects(graph);
        var isParallelizable = !hasSideEffects && !HasOrderDependencies(graph);
        var dataSize = EstimateDataSize(graph);
        var intensity = CalculateComputeIntensity(graph);

        return new DataFlowAnalysis
        {
            HasSideEffects = hasSideEffects,
            IsDataParallelizable = isParallelizable,
            EstimatedDataSize = dataSize,
            Intensity = intensity
        };
    }

    /// <summary>
    /// Determines if operations can be fused for optimization.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>A list of detected fusion opportunities.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is null.</exception>
    public IReadOnlyList<OperationFusion> DetectFusionOpportunities(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var fusions = new List<OperationFusion>();

        // Detect common fusion patterns
        fusions.AddRange(DetectSelectWhereFusion(graph));
        fusions.AddRange(DetectWhereSelectFusion(graph));
        fusions.AddRange(DetectMapReduceFusion(graph));
        fusions.AddRange(DetectCombinedWhereFusion(graph));

        return fusions;
    }

    #region Private Helper Methods

    private static OperationType CategorizeMethodCall(MethodCallExpression methodCall)
    {
        var methodName = methodCall.Method.Name;

        return methodName switch
        {
            "Select" => OperationType.Map,
            "Where" => OperationType.Filter,
            "Aggregate" or "Sum" or "Average" or "Min" or "Max" or "Count" => OperationType.Reduce,
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" => OperationType.OrderBy,
            "Join" or "GroupJoin" => OperationType.Join,
            "GroupBy" => OperationType.GroupBy,
            "Scan" or "Accumulate" => OperationType.Scan,
            _ => OperationType.Map
        };
    }

    private static OperationType CategorizeBinaryExpression(BinaryExpression binary)
    {
        return binary.NodeType switch
        {
            ExpressionType.Add or ExpressionType.Subtract or
            ExpressionType.Multiply or ExpressionType.Divide => OperationType.Reduce,
            ExpressionType.Equal or ExpressionType.NotEqual or
            ExpressionType.LessThan or ExpressionType.GreaterThan => OperationType.Filter,
            _ => OperationType.Map
        };
    }

    private static bool HasMixedWorkload(OperationGraph graph)
    {
        var types = graph.Operations.Select(o => o.Type).Distinct().Count();
        return types >= 3; // Three or more different operation types
    }

    private static DataDependency AnalyzeDataDependency(Operation consumer, Operation producer)
    {
        // Determine dependency type based on operation patterns
        if (consumer.Type == OperationType.Reduce && producer.Type == OperationType.Map)
        {
            return new DataDependency
            {
                ProducerId = producer.Id,
                ConsumerId = consumer.Id,
                Type = DependencyType.ReadAfterWrite,
                IsBlocking = true
            };
        }

        if (consumer.Type == OperationType.Filter && producer.Type == OperationType.Map)
        {
            return new DataDependency
            {
                ProducerId = producer.Id,
                ConsumerId = consumer.Id,
                Type = DependencyType.ReadAfterWrite,
                IsBlocking = false // Can be pipelined
            };
        }

        return new DataDependency
        {
            ProducerId = producer.Id,
            ConsumerId = consumer.Id,
            Type = DependencyType.ReadAfterWrite,
            IsBlocking = true
        };
    }

    private static IReadOnlyList<IReadOnlyList<string>> FindParallelizableGroups(OperationGraph graph, DependencyGraph dependencies)
    {
        var groups = new List<IReadOnlyList<string>>();
        var visited = new HashSet<string>();

        foreach (var operation in graph.Operations)
        {
            if (visited.Contains(operation.Id))
            {
                continue;
            }

            var group = new List<string> { operation.Id };
            visited.Add(operation.Id);

            // Find operations at the same dependency level (can run in parallel)
            foreach (var other in graph.Operations)
            {
                if (visited.Contains(other.Id))
                {
                    continue;
                }

                if (CanRunInParallel(operation, other, dependencies))
                {
                    group.Add(other.Id);
                    visited.Add(other.Id);
                }
            }

            if (group.Count > 1)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    private static bool CanRunInParallel(Operation op1, Operation op2, DependencyGraph dependencies)
    {
        // Check if operations have no dependency relationship
        var op1Deps = dependencies.GetDependencies(op1.Id);
        var op2Deps = dependencies.GetDependencies(op2.Id);
        return !op1Deps.Contains(op2.Id) && !op2Deps.Contains(op1.Id);
    }

    private static bool DetectSideEffects(OperationGraph graph)
    {
        // Check metadata for side effect markers
        foreach (var operation in graph.Operations)
        {
            if (operation.Metadata.TryGetValue("HasSideEffects", out var value) && value is bool hasSideEffects && hasSideEffects)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasOrderDependencies(OperationGraph graph)
    {
        // OrderBy, Scan operations require sequential processing
        return graph.Operations.Any(o => o.Type is OperationType.OrderBy or OperationType.Scan);
    }

    private static int EstimateDataSize(OperationGraph graph)
    {
        if (graph.Metadata.TryGetValue("EstimatedDataSize", out var size) && size is int dataSize)
        {
            return dataSize;
        }

        // Conservative estimate based on operation count
        return graph.Operations.Count * 1000;
    }

    private static Optimization.ComputeIntensity CalculateComputeIntensity(OperationGraph graph)
    {
        // Handle empty graphs
        if (graph.Operations.Count == 0)
        {
            return Optimization.ComputeIntensity.Low;
        }

        var totalCost = graph.Operations.Sum(o => o.EstimatedCost);
        var avgCost = totalCost / graph.Operations.Count;

        return avgCost switch
        {
            < 1.0 => Optimization.ComputeIntensity.Low,
            < 3.0 => Optimization.ComputeIntensity.Medium,
            < 10.0 => Optimization.ComputeIntensity.High,
            _ => Optimization.ComputeIntensity.VeryHigh
        };
    }

    private static IEnumerable<OperationFusion> DetectSelectWhereFusion(OperationGraph graph)
    {
        for (int i = 0; i < graph.Operations.Count - 1; i++)
        {
            var current = graph.Operations[i];
            var next = graph.Operations[i + 1];

            if (current.Type == OperationType.Map && next.Type == OperationType.Filter &&
                next.Dependencies.Contains(current.Id))
            {
                yield return new OperationFusion
                {
                    OperationIds = new[] { current.Id, next.Id },
                    FusionType = "SelectWhere",
                    EstimatedSpeedup = 1.5
                };
            }
        }
    }

    private static IEnumerable<OperationFusion> DetectWhereSelectFusion(OperationGraph graph)
    {
        for (int i = 0; i < graph.Operations.Count - 1; i++)
        {
            var current = graph.Operations[i];
            var next = graph.Operations[i + 1];

            if (current.Type == OperationType.Filter && next.Type == OperationType.Map &&
                next.Dependencies.Contains(current.Id))
            {
                yield return new OperationFusion
                {
                    OperationIds = new[] { current.Id, next.Id },
                    FusionType = "WhereSelect",
                    EstimatedSpeedup = 1.8 // Higher speedup as filtering reduces data
                };
            }
        }
    }

    private static IEnumerable<OperationFusion> DetectMapReduceFusion(OperationGraph graph)
    {
        for (int i = 0; i < graph.Operations.Count - 1; i++)
        {
            var current = graph.Operations[i];
            var next = graph.Operations[i + 1];

            if (current.Type == OperationType.Map && next.Type == OperationType.Reduce &&
                next.Dependencies.Contains(current.Id))
            {
                yield return new OperationFusion
                {
                    OperationIds = new[] { current.Id, next.Id },
                    FusionType = "MapReduce",
                    EstimatedSpeedup = 2.0 // Significant speedup from combined operation
                };
            }
        }
    }

    private static IEnumerable<OperationFusion> DetectCombinedWhereFusion(OperationGraph graph)
    {
        var consecutiveFilters = new List<string>();

        for (int i = 0; i < graph.Operations.Count; i++)
        {
            var current = graph.Operations[i];

            if (current.Type == OperationType.Filter)
            {
                consecutiveFilters.Add(current.Id);

                // Check if next is also a filter
                if (i < graph.Operations.Count - 1 && graph.Operations[i + 1].Type != OperationType.Filter)
                {
                    if (consecutiveFilters.Count > 1)
                    {
                        yield return new OperationFusion
                        {
                            OperationIds = consecutiveFilters.ToArray(),
                            FusionType = "CombinedWhere",
                            EstimatedSpeedup = 1.3 * consecutiveFilters.Count
                        };
                    }
                    consecutiveFilters.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Gets all transitive dependencies for an operation (direct and indirect).
    /// </summary>
    private static IEnumerable<string> GetTransitiveDependencies(DependencyGraph graph, string operationId)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        // Start with direct dependencies
        foreach (var dep in graph.GetDependencies(operationId))
        {
            queue.Enqueue(dep);
        }

        // BFS to find all transitive dependencies
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Add(current))
            {
                // Add transitive dependencies
                foreach (var dep in graph.GetDependencies(current))
                {
                    if (!visited.Contains(dep))
                    {
                        queue.Enqueue(dep);
                    }
                }
            }
        }

        return visited;
    }

    #endregion
}

/// <summary>
/// Extended dependency graph with additional analysis data.
/// </summary>
public class ExtendedDependencyGraph
{
    /// <summary>
    /// Gets the underlying dependency graph.
    /// </summary>
    public DependencyGraph BaseGraph { get; init; } = new();

    /// <summary>
    /// Gets the data dependencies with detailed information.
    /// </summary>
    public Dictionary<string, List<DataDependency>> DataDependencies { get; init; } = new();

    /// <summary>
    /// Gets groups of operations that can run in parallel.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> ParallelizableGroups { get; init; } = Array.Empty<IReadOnlyList<string>>();
}

/// <summary>
/// Represents a data dependency between operations.
/// </summary>
public class DataDependency
{
    /// <summary>
    /// Gets the ID of the operation that produces data.
    /// </summary>
    public string ProducerId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ID of the operation that consumes data.
    /// </summary>
    public string ConsumerId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of dependency.
    /// </summary>
    public DependencyType Type { get; init; }

    /// <summary>
    /// Gets whether this dependency blocks parallel execution.
    /// </summary>
    public bool IsBlocking { get; init; }
}

/// <summary>
/// Defines types of data dependencies.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Read-after-write dependency (true dependency).
    /// </summary>
    ReadAfterWrite,

    /// <summary>
    /// Write-after-read dependency (anti-dependency).
    /// </summary>
    WriteAfterRead,

    /// <summary>
    /// Write-after-write dependency (output dependency).
    /// </summary>
    WriteAfterWrite
}

/// <summary>
/// Represents a data flow analysis result.
/// </summary>
public class DataFlowAnalysis
{
    /// <summary>
    /// Gets whether the operations have side effects.
    /// </summary>
    public bool HasSideEffects { get; init; }

    /// <summary>
    /// Gets whether the operations can be parallelized.
    /// </summary>
    public bool IsDataParallelizable { get; init; }

    /// <summary>
    /// Gets the estimated data size in elements.
    /// </summary>
    public int EstimatedDataSize { get; init; }

    /// <summary>
    /// Gets the computational intensity of the operations.
    /// </summary>
    public ComputeIntensity Intensity { get; init; }
}

/// <summary>
/// Represents an opportunity to fuse multiple operations.
/// </summary>
public class OperationFusion
{
    /// <summary>
    /// Gets the IDs of operations to be fused.
    /// </summary>
    public IReadOnlyList<string> OperationIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the type of fusion (e.g., "SelectWhere", "MapReduce").
    /// </summary>
    public string FusionType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the estimated performance speedup from fusion.
    /// </summary>
    public double EstimatedSpeedup { get; init; }
}
