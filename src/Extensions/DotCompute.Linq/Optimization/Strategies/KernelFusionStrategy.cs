using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Kernels;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Optimization.CostModel;
using DotCompute.Linq.Optimization.Models;
using DotCompute.Linq.Types;
using ExecutionContext = DotCompute.Linq.Execution.ExecutionContext;

namespace DotCompute.Linq.Optimization.Strategies;

/// <summary>
/// Advanced kernel fusion strategy that combines multiple operations into single kernels
/// to reduce memory transfers and eliminate intermediate results.
/// </summary>
public sealed class KernelFusionStrategy : ILinqOptimizationStrategy
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ExecutionCostModel _costModel;
    private readonly FusionAnalyzer _fusionAnalyzer;
    private readonly KernelCodeGenerator _codeGenerator;

    // Fusion thresholds and limits
    private const int MaxFusionDepth = 8;
    private const double MinFusionBenefit = 0.15; // 15% improvement threshold
    private const long MaxFusedKernelComplexity = 1000;

    public KernelFusionStrategy(
        IComputeOrchestrator orchestrator,
        ExecutionCostModel costModel)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _fusionAnalyzer = new FusionAnalyzer(costModel);
        _codeGenerator = new KernelCodeGenerator();
    }

    public async Task<QueryPlan> OptimizeAsync(QueryPlan plan, ExecutionContext context)
    {
        var optimizedPlan = plan.Clone();

        // Build operation dependency graph

        var dependencyGraph = BuildDependencyGraph(plan.Operations);

        // Find fusion opportunities using graph analysis

        var fusionGroups = await FindOptimalFusionGroups(dependencyGraph, context);

        // Apply fusion transformations

        foreach (var group in fusionGroups.OrderByDescending(g => g.EstimatedBenefit))
        {
            if (group.EstimatedBenefit > MinFusionBenefit)
            {
                await ApplyFusion(optimizedPlan, group, context);
            }
        }

        // Optimize memory access patterns in fused kernels

        await OptimizeMemoryAccess(optimizedPlan, context);


        return optimizedPlan;
    }

    private OperationDependencyGraph BuildDependencyGraph(IList<QueryOperation> operations)
    {
        var graph = new OperationDependencyGraph();
        var nodes = new Dictionary<int, OperationNode>();

        // Create nodes for each operation

        for (int i = 0; i < operations.Count; i++)
        {
            var node = new OperationNode
            {
                Id = i,
                Operation = operations[i],
                Dependencies = new List<int>(),
                Dependents = new List<int>()
            };
            nodes[i] = node;
            graph.Nodes.Add(node);
        }

        // Build dependencies based on data flow

        for (int i = 1; i < operations.Count; i++)
        {
            var current = operations[i];
            var previous = operations[i - 1];

            // Check if current operation depends on previous

            if (HasDataDependency(current, previous))
            {
                nodes[i].Dependencies.Add(i - 1);
                nodes[i - 1].Dependents.Add(i);
                graph.Edges.Add(new DependencyEdge
                {
                    From = i - 1,
                    To = i,
                    DataSize = EstimateDataTransferSize(previous, current)
                });
            }
        }


        return graph;
    }

    private bool HasDataDependency(QueryOperation current, QueryOperation previous)
    {
        // Check if current operation uses output from previous
        return current.InputId == previous.OutputId ||

               current.InputDataType == previous.OutputDataType;
    }

    private long EstimateDataTransferSize(QueryOperation from, QueryOperation to)
    {
        // Estimate the amount of data transferred between operations
        return Math.Min(from.OutputSize, to.InputSize) * GetTypeSize(from.DataType);
    }

    private int GetTypeSize(Type dataType)
    {
        if (dataType == typeof(byte))
        {
            return 1;
        }


        if (dataType == typeof(short))
        {
            return 2;
        }


        if (dataType == typeof(int))
        {
            return 4;
        }


        if (dataType == typeof(long))
        {
            return 8;
        }


        if (dataType == typeof(float))
        {
            return 4;
        }


        if (dataType == typeof(double))
        {
            return 8;
        }


        return 8; // Default to 8 bytes
    }

    private async Task<List<FusionGroup>> FindOptimalFusionGroups(
        OperationDependencyGraph graph,

        ExecutionContext context)
    {
        var fusionGroups = new List<FusionGroup>();
        var visitedNodes = new HashSet<int>();

        // Use topological sort to process nodes in dependency order

        var sortedNodes = TopologicalSort(graph);


        foreach (var node in sortedNodes)
        {
            if (visitedNodes.Contains(node.Id))
            {
                continue;
            }

            // Try to build fusion group starting from this node

            var group = await BuildFusionGroup(node, graph, context, visitedNodes);


            if (group.Operations.Count > 1)
            {
                // Estimate fusion benefit
                group.EstimatedBenefit = await EstimateFusionBenefit(group, context);


                if (group.EstimatedBenefit > MinFusionBenefit)
                {
                    fusionGroups.Add(group);

                    // Mark all operations in group as visited

                    foreach (var op in group.Operations)
                    {
                        visitedNodes.Add(op.Id);
                    }
                }
            }
        }


        return fusionGroups;
    }

    private List<OperationNode> TopologicalSort(OperationDependencyGraph graph)
    {
        var result = new List<OperationNode>();
        var visited = new HashSet<int>();
        var visiting = new HashSet<int>();


        foreach (var node in graph.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                TopologicalSortVisit(node, graph, visited, visiting, result);
            }
        }


        result.Reverse();
        return result;
    }

    private void TopologicalSortVisit(
        OperationNode node,

        OperationDependencyGraph graph,
        HashSet<int> visited,

        HashSet<int> visiting,

        List<OperationNode> result)
    {
        if (visiting.Contains(node.Id))
        {

            throw new InvalidOperationException("Circular dependency detected");
        }


        if (visited.Contains(node.Id))
        {
            return;
        }


        visiting.Add(node.Id);


        foreach (var dependentId in node.Dependents)
        {
            var dependent = graph.Nodes.First(n => n.Id == dependentId);
            TopologicalSortVisit(dependent, graph, visited, visiting, result);
        }


        visiting.Remove(node.Id);
        visited.Add(node.Id);
        result.Add(node);
    }

    private async Task<FusionGroup> BuildFusionGroup(
        OperationNode startNode,
        OperationDependencyGraph graph,
        ExecutionContext context,
        HashSet<int> visitedNodes)
    {
        var group = new FusionGroup
        {
            Operations = new List<OperationNode> { startNode },
            FusionComplexity = CalculateOperationComplexity(startNode.Operation)
        };


        var candidates = new Queue<OperationNode>();
        candidates.Enqueue(startNode);


        while (candidates.Count > 0 && group.Operations.Count < MaxFusionDepth)
        {
            var current = candidates.Dequeue();

            // Check each dependent for fusion eligibility

            foreach (var dependentId in current.Dependents)
            {
                if (visitedNodes.Contains(dependentId))
                {
                    continue;
                }


                var dependent = graph.Nodes.First(n => n.Id == dependentId);


                if (await CanFuseOperations(current.Operation, dependent.Operation, context))
                {
                    var newComplexity = group.FusionComplexity + CalculateOperationComplexity(dependent.Operation);


                    if (newComplexity <= MaxFusedKernelComplexity)
                    {
                        group.Operations.Add(dependent);
                        group.FusionComplexity = newComplexity;
                        candidates.Enqueue(dependent);
                    }
                }
            }
        }


        return group;
    }

    private long CalculateOperationComplexity(QueryOperation operation)
    {
        return operation.Type switch
        {
            (Models.OperationType)OperationType.Map => operation.InputSize * 2,
            (Models.OperationType)OperationType.Filter => operation.InputSize * 1,
            (Models.OperationType)OperationType.Reduce => operation.InputSize * 3,
            (Models.OperationType)OperationType.GroupBy => operation.InputSize * 5,
            (Models.OperationType)OperationType.Join => operation.InputSize * 8,
            (Models.OperationType)OperationType.Aggregate => operation.InputSize * 4,
            _ => operation.InputSize * 2
        };
    }

    private async Task<bool> CanFuseOperations(
        QueryOperation first,

        QueryOperation second,

        ExecutionContext context)
    {
        // Check compatibility for fusion
        if (!AreTypesCompatible(first, second))
        {
            return false;
        }


        if (!AreAccessPatternsCompatible(first, second))
        {
            return false;
        }


        if (!AreSideEffectsCompatible(first, second))
        {
            return false;
        }

        // Check if fusion would benefit performance

        var fusionBenefit = await _fusionAnalyzer.EstimateFusionBenefit(first, second, context);
        return fusionBenefit > MinFusionBenefit;
    }

    private bool AreTypesCompatible(QueryOperation first, QueryOperation second)
    {
        // Element-wise operations are generally fusable
        var elementWiseOps = new[] { (Models.OperationType)OperationType.Map, (Models.OperationType)OperationType.Filter };


        if (elementWiseOps.Contains(first.Type) && elementWiseOps.Contains(second.Type))
        {
            return first.DataType == second.DataType;
        }

        // Reduction operations can sometimes be fused

        if (first.Type == (Models.OperationType)OperationType.Map && second.Type == (Models.OperationType)OperationType.Reduce)
        {
            return second.IsAssociative;
        }


        return false;
    }

    private bool AreAccessPatternsCompatible(QueryOperation first, QueryOperation second)
    {
        // Check if memory access patterns allow efficient fusion
        return first.AccessPattern switch
        {
            (Models.AccessPattern)AccessPattern.Sequential when second.AccessPattern == (Models.AccessPattern)AccessPattern.Sequential => true,
            (Models.AccessPattern)AccessPattern.Random when second.AccessPattern == (Models.AccessPattern)AccessPattern.Random => false,
            (Models.AccessPattern)AccessPattern.Strided when second.AccessPattern == (Models.AccessPattern)AccessPattern.Strided =>

                first.Stride == second.Stride,
            _ => false
        };
    }

    private bool AreSideEffectsCompatible(QueryOperation first, QueryOperation second)
    {
        // Operations with side effects generally cannot be fused
        return !first.HasSideEffects && !second.HasSideEffects;
    }

    private async Task<double> EstimateFusionBenefit(FusionGroup group, ExecutionContext context)
    {
        // Calculate cost of separate execution
        double separateCost = 0;
        foreach (var operation in group.Operations)
        {
            separateCost += await _costModel.EstimateExecutionCost(operation.Operation, context);
        }

        // Calculate cost of fused execution

        var fusedOperation = CreateFusedOperation(group);
        var fusedCost = await _costModel.EstimateExecutionCost(fusedOperation, context);

        // Add memory transfer savings

        var memoryTransferSavings = CalculateMemoryTransferSavings(group);

        // Calculate relative benefit

        return (separateCost - fusedCost + memoryTransferSavings) / separateCost;
    }

    private QueryOperation CreateFusedOperation(FusionGroup group)
    {
        var firstOp = group.Operations.First().Operation;
        var lastOp = group.Operations.Last().Operation;


        return new QueryOperation
        {
            Type = (Models.OperationType)OperationType.FusedKernel,
            InputSize = firstOp.InputSize,
            OutputSize = lastOp.OutputSize,
            DataType = lastOp.DataType,
            FusedOperations = group.Operations.Select(n => n.Operation).ToList(),
            AccessPattern = (Models.AccessPattern)DetermineOptimalAccessPattern(group),
            IsAssociative = group.Operations.All(n => n.Operation.IsAssociative),
            HasSideEffects = group.Operations.Any(n => n.Operation.HasSideEffects)
        };
    }

    private AccessPattern DetermineOptimalAccessPattern(FusionGroup group)
    {
        var patterns = group.Operations.Select(n => n.Operation.AccessPattern).Distinct().ToList();


        if (patterns.Count == 1)
        {
            return (AccessPattern)patterns[0];
        }

        // If mixed patterns, choose the most restrictive

        if (patterns.Contains((Models.AccessPattern)AccessPattern.Random))
        {
            return AccessPattern.Random;
        }


        if (patterns.Contains((Models.AccessPattern)AccessPattern.Strided))
        {
            return AccessPattern.Strided;
        }


        return AccessPattern.Sequential;
    }

    private double CalculateMemoryTransferSavings(FusionGroup group)
    {
        double savings = 0;


        for (int i = 0; i < group.Operations.Count - 1; i++)
        {
            var current = group.Operations[i].Operation;
            var next = group.Operations[i + 1].Operation;

            // Calculate saved memory transfer between operations

            var transferSize = EstimateDataTransferSize(current, next);
            savings += transferSize * 2; // Read + Write cost
        }


        return savings;
    }

    private async Task ApplyFusion(QueryPlan plan, FusionGroup group, ExecutionContext context)
    {
        // Generate fused kernel code
        var fusedKernel = await _codeGenerator.GenerateFusedKernel(group, context);

        // Create fused operation

        var fusedOperation = CreateFusedOperation(group);
        fusedOperation.GeneratedKernel = ConvertToOperatorsGeneratedKernel(fusedKernel);

        // Replace original operations with fused operation

        var operationsToReplace = group.Operations.Select(n => n.Operation).ToList();
        plan.ReplaceOperations(operationsToReplace, fusedOperation);
    }

    private async Task OptimizeMemoryAccess(QueryPlan plan, ExecutionContext context)
    {
        foreach (var operation in plan.Operations.Where(op => op.Type == (Models.OperationType)OperationType.FusedKernel))
        {
            await OptimizeFusedKernelMemoryAccess(operation, context);
        }
    }

    private async Task OptimizeFusedKernelMemoryAccess(QueryOperation fusedOperation, ExecutionContext context)
    {
        if (fusedOperation.GeneratedKernel == null)
        {
            return;
        }

        // Apply memory access optimizations

        var optimizer = new MemoryAccessOptimizer();

        // Convert to the local GeneratedKernel type for optimization

        var localKernel = ConvertFromOperatorsGeneratedKernel(fusedOperation.GeneratedKernel);

        // Coalesce memory accesses

        await optimizer.CoalesceMemoryAccesses(localKernel);

        // Optimize register usage

        await optimizer.OptimizeRegisterUsage(localKernel);

        // Apply cache blocking for large datasets

        if (fusedOperation.InputSize > context.CacheSize)
        {
            await optimizer.ApplyCacheBlocking(localKernel, context.CacheSize);
        }

        // Optimize memory prefetching

        await optimizer.OptimizePrefetching(localKernel, context);

        // Convert back and update the fused operation

        fusedOperation.GeneratedKernel = ConvertToOperatorsGeneratedKernel(localKernel);
    }

    private static Operators.Generation.GeneratedKernel ConvertToOperatorsGeneratedKernel(GeneratedKernel source)
    {
        return new Operators.Generation.GeneratedKernel
        {
            Name = source.Name ?? "FusedKernel",
            Source = source.SourceCode ?? "",
            TargetBackend = source.Metadata.TryGetValue("TargetBackend", out var backend) ? backend.ToString() ?? "CPU" : "CPU",
            Language = DotCompute.Abstractions.Types.KernelLanguage.Cuda,
            Parameters = source.Parameters?.Select(p => new Operators.Generation.GeneratedKernelParameter
            {
                Name = p.Name,
                Type = p.Type,
                IsInput = !p.IsPointer || p.Name == "input",
                IsOutput = p.IsPointer && (p.Name == "output" || p.Name.Contains("result")),
                ElementType = p.Type.IsArray ? p.Type.GetElementType() : null
            }).ToArray() ?? [],
            Metadata = source.Metadata ?? new Dictionary<string, object>()
        };
    }


    private static GeneratedKernel ConvertFromOperatorsGeneratedKernel(Operators.Generation.GeneratedKernel source)
    {
        return new GeneratedKernel
        {
            Name = source.Name,
            SourceCode = source.SourceCode,
            Parameters = source.Parameters?.Select(p => new KernelParameter
            {
                Name = p.Name,
                Type = p.Type,
                IsPointer = p.IsOutput || p.Name.Contains("output") || p.Name.Contains("result")
            }).ToList() ?? new List<KernelParameter>(),
            Metadata = source.Metadata ?? new Dictionary<string, object>(),
            TargetBackend = source.TargetBackend
        };
    }
}

// Supporting classes
public class OperationDependencyGraph
{
    public List<OperationNode> Nodes { get; set; } = new();
    public List<DependencyEdge> Edges { get; set; } = new();
}

public class OperationNode
{
    public int Id { get; set; }
    public QueryOperation Operation { get; set; } = new();
    public List<int> Dependencies { get; set; } = new();
    public List<int> Dependents { get; set; } = new();
}

public class DependencyEdge
{
    public int From { get; set; }
    public int To { get; set; }
    public long DataSize { get; set; }
}

public class FusionGroup
{
    public List<OperationNode> Operations { get; set; } = new();
    public long FusionComplexity { get; set; }
    public double EstimatedBenefit { get; set; }
}

public class FusionAnalyzer
{
    private readonly ExecutionCostModel _costModel;

    public FusionAnalyzer(ExecutionCostModel costModel)
    {
        _costModel = costModel;
    }

    public async Task<double> EstimateFusionBenefit(
        QueryOperation first,

        QueryOperation second,

        ExecutionContext context)
    {
        // Estimate performance benefit of fusing two operations
        var separateCost = await _costModel.EstimateExecutionCost(first, context) +
                          await _costModel.EstimateExecutionCost(second, context);


        var fusedCost = await EstimateFusedCost(first, second, context);


        return (separateCost - fusedCost) / separateCost;
    }

    private Task<double> EstimateFusedCost(
        QueryOperation first,

        QueryOperation second,

        ExecutionContext context)
    {
        // Simplified cost model for fused operations
        var computeCost = (first.InputSize + second.InputSize) * 0.5;
        var memoryCost = first.InputSize * 0.3; // Reduced memory traffic


        return Task.FromResult(computeCost + memoryCost);
    }
}

public class KernelCodeGenerator
{
    public async Task<GeneratedKernel> GenerateFusedKernel(FusionGroup group, ExecutionContext context)
    {
        var kernel = new GeneratedKernel
        {
            Name = $"FusedKernel_{string.Join("_", group.Operations.Select(op => op.Operation.Type))}",
            SourceCode = await GenerateKernelSource(group, context),
            Parameters = ExtractParameters(group),
            Optimizations = DetermineOptimizations(group, context)
        };


        return kernel;
    }

    private async Task<string> GenerateKernelSource(FusionGroup group, ExecutionContext context)
    {
        var sourceBuilder = new List<string>();

        // Generate kernel header

        sourceBuilder.Add(GenerateKernelHeader(group));

        // Generate fused computation logic

        sourceBuilder.Add(await GenerateFusedLogic(group, context));

        // Generate kernel footer

        sourceBuilder.Add(GenerateKernelFooter());


        return string.Join("\n", sourceBuilder);
    }

    private string GenerateKernelHeader(FusionGroup group)
    {
        var firstOp = group.Operations.First().Operation;
        var lastOp = group.Operations.Last().Operation;


        return $@"
__global__ void FusedKernel(
    const {GetCudaType(firstOp.DataType)}* input,
    {GetCudaType(lastOp.DataType)}* output,
    int size
) {{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;
    
    // Shared memory for intermediate results
    __shared__ {GetCudaType(firstOp.DataType)} shared_data[256];
";
    }

    private Task<string> GenerateFusedLogic(FusionGroup group, ExecutionContext context)
    {
        var logic = new List<string>();
        var currentVar = "input[idx]";


        foreach (var node in group.Operations)
        {
            var operation = node.Operation;
            var nextVar = node == group.Operations.Last() ? "output[idx]" : $"temp_{node.Id}";


            logic.Add(GenerateOperationCode(operation, currentVar, nextVar));
            currentVar = nextVar;
        }


        return Task.FromResult(string.Join("\n    ", logic));
    }

    private string GenerateOperationCode(QueryOperation operation, string input, string output)
    {
        return operation.Type switch
        {
            (Models.OperationType)OperationType.Map => $"{output} = transform({input});",
            (Models.OperationType)OperationType.Filter => $"if (predicate({input})) {output} = {input};",
            (Models.OperationType)OperationType.Reduce => $"{output} = reduce({input}, {output});",
            _ => $"{output} = {input};"
        };
    }

    private string GenerateKernelFooter()
    {
        return "}";
    }

    private string GetCudaType(Type type)
    {
        if (type == typeof(float))
        {
            return "float";
        }


        if (type == typeof(double))
        {
            return "double";
        }


        if (type == typeof(int))
        {
            return "int";
        }


        if (type == typeof(long))
        {
            return "long long";
        }


        return "float";
    }

    private List<KernelParameter> ExtractParameters(FusionGroup group)
    {
        var parameters = new List<KernelParameter>
        {
            new() { Name = "input", Type = group.Operations.First().Operation.DataType, IsPointer = true },
            new() { Name = "output", Type = group.Operations.Last().Operation.DataType, IsPointer = true },
            new() { Name = "size", Type = typeof(int), IsPointer = false }
        };


        return parameters;
    }

    private List<string> DetermineOptimizations(FusionGroup group, ExecutionContext context)
    {
        var optimizations = new List<string>();


        if (group.Operations.All(n => n.Operation.AccessPattern == (Models.AccessPattern)AccessPattern.Sequential))
        {
            optimizations.Add("memory_coalescing");
        }


        if (group.Operations.Count > 3)
        {
            optimizations.Add("register_optimization");
        }


        if (context.TargetBackend == Types.BackendType.CUDA)
        {
            optimizations.Add("warp_optimization");
        }


        return optimizations;
    }
}

public class MemoryAccessOptimizer
{
    public async Task CoalesceMemoryAccesses(GeneratedKernel kernel)
    {
        // Implement memory coalescing optimizations
        await Task.CompletedTask;
    }

    public async Task OptimizeRegisterUsage(GeneratedKernel kernel)
    {
        // Implement register usage optimizations
        await Task.CompletedTask;
    }

    public async Task ApplyCacheBlocking(GeneratedKernel kernel, long cacheSize)
    {
        // Implement cache blocking optimizations
        await Task.CompletedTask;
    }

    public async Task OptimizePrefetching(GeneratedKernel kernel, ExecutionContext context)
    {
        // Implement prefetching optimizations
        await Task.CompletedTask;
    }
}

public record GeneratedKernel
{
    public string Name { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public List<KernelParameter> Parameters { get; init; } = new();
    public List<string> Optimizations { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string TargetBackend { get; init; } = "CPU";
}

public class KernelParameter
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(object);
    public bool IsPointer { get; set; }
}

public enum AccessPattern
{
    Sequential,
    Random,
    Strided
}