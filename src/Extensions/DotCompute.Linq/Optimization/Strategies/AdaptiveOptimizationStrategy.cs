using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Optimization;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Optimization.CostModel;

namespace DotCompute.Linq.Optimization.Strategies;

/// <summary>
/// Machine learning-based optimization strategy that adapts to execution patterns
/// and predicts optimal configurations at runtime.
/// </summary>
public sealed class AdaptiveOptimizationStrategy : ILinqOptimizationStrategy
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ExecutionCostModel _costModel;
    private readonly ConcurrentDictionary<string, ExecutionHistory> _executionHistory;
    private readonly ConcurrentDictionary<string, OptimizationModel> _models;
    private readonly ReaderWriterLockSlim _modelLock;
    private readonly Timer _adaptationTimer;
    
    // ML-based optimization parameters
    private const int MinSamplesForLearning = 10;
    private const int MaxHistorySize = 1000;
    private const double LearningRate = 0.01;
    private const TimeSpan AdaptationInterval = TimeSpan.FromMinutes(5);

    public AdaptiveOptimizationStrategy(
        IComputeOrchestrator orchestrator,
        ExecutionCostModel costModel)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _executionHistory = new ConcurrentDictionary<string, ExecutionHistory>();
        _models = new ConcurrentDictionary<string, OptimizationModel>();
        _modelLock = new ReaderWriterLockSlim();
        
        // Start adaptation timer for continuous learning
        _adaptationTimer = new Timer(AdaptModels, null, AdaptationInterval, AdaptationInterval);
    }

    public async Task<QueryPlan> OptimizeAsync(QueryPlan plan, ExecutionContext context)
    {
        var querySignature = ComputeQuerySignature(plan);
        var workloadCharacteristics = AnalyzeWorkload(plan, context);
        
        // Try to get existing model or create new one
        var model = GetOrCreateModel(querySignature, workloadCharacteristics);
        
        // Predict optimal configuration
        var optimizedPlan = await PredictOptimalConfiguration(plan, model, context);
        
        // Track execution for learning
        TrackExecution(querySignature, optimizedPlan, context);
        
        return optimizedPlan;
    }

    private string ComputeQuerySignature(QueryPlan plan)
    {
        // Create a signature based on query structure
        var signature = new List<string>();
        
        foreach (var operation in plan.Operations)
        {
            signature.Add($"{operation.Type}_{operation.InputSize}_{operation.DataType}");
        }
        
        return string.Join("|", signature);
    }

    private WorkloadCharacteristics AnalyzeWorkload(QueryPlan plan, ExecutionContext context)
    {
        return new WorkloadCharacteristics
        {
            DataSize = plan.EstimatedDataSize,
            OperationCount = plan.Operations.Count,
            ComputeIntensity = CalculateComputeIntensity(plan),
            MemoryIntensity = CalculateMemoryIntensity(plan),
            ParallelismPotential = CalculateParallelismPotential(plan),
            Hardware = context.HardwareInfo
        };
    }

    private double CalculateComputeIntensity(QueryPlan plan)
    {
        double intensity = 0.0;
        
        foreach (var operation in plan.Operations)
        {
            intensity += operation.Type switch
            {
                OperationType.Map => 1.0,
                OperationType.Filter => 0.5,
                OperationType.Reduce => 2.0,
                OperationType.GroupBy => 1.5,
                OperationType.Join => 3.0,
                OperationType.Aggregate => 2.5,
                _ => 1.0
            };
        }
        
        return intensity / plan.Operations.Count;
    }

    private double CalculateMemoryIntensity(QueryPlan plan)
    {
        return plan.EstimatedMemoryUsage / (double)plan.EstimatedDataSize;
    }

    private double CalculateParallelismPotential(QueryPlan plan)
    {
        int parallelizable = 0;
        int total = 0;
        
        foreach (var operation in plan.Operations)
        {
            total++;
            if (IsParallelizable(operation))
                parallelizable++;
        }
        
        return (double)parallelizable / total;
    }

    private bool IsParallelizable(QueryOperation operation)
    {
        return operation.Type switch
        {
            OperationType.Map => true,
            OperationType.Filter => true,
            OperationType.Reduce => operation.IsAssociative,
            OperationType.GroupBy => true,
            OperationType.Join => true,
            OperationType.Aggregate => operation.IsAssociative,
            _ => false
        };
    }

    private OptimizationModel GetOrCreateModel(string signature, WorkloadCharacteristics characteristics)
    {
        return _models.GetOrAdd(signature, _ => new OptimizationModel
        {
            Signature = signature,
            BaseCharacteristics = characteristics,
            Predictions = new Dictionary<string, double>(),
            Weights = InitializeWeights(),
            LastUpdated = DateTime.UtcNow
        });
    }

    private Dictionary<string, double> InitializeWeights()
    {
        return new Dictionary<string, double>
        {
            ["BackendPreference"] = 0.5,
            ["ParallelismFactor"] = 1.0,
            ["MemoryStrategy"] = 0.3,
            ["FusionPriority"] = 0.7,
            ["CacheStrategy"] = 0.4
        };
    }

    private async Task<QueryPlan> PredictOptimalConfiguration(
        QueryPlan plan, 
        OptimizationModel model, 
        ExecutionContext context)
    {
        var optimizedPlan = plan.Clone();
        
        // Predict backend preference
        var backendScore = PredictBackendScore(model, context);
        optimizedPlan.PreferredBackend = backendScore > 0.5 ? BackendType.GPU : BackendType.CPU;
        
        // Predict optimal parallelism
        var parallelismFactor = PredictParallelismFactor(model, context);
        optimizedPlan.ParallelismDegree = Math.Max(1, (int)(context.AvailableCores * parallelismFactor));
        
        // Predict memory strategy
        var memoryStrategy = PredictMemoryStrategy(model, context);
        optimizedPlan.MemoryStrategy = memoryStrategy;
        
        // Predict fusion opportunities
        var fusionPriority = PredictFusionPriority(model, context);
        if (fusionPriority > 0.7)
        {
            optimizedPlan = await ApplyKernelFusion(optimizedPlan);
        }
        
        // Apply cache optimization
        var cacheStrategy = PredictCacheStrategy(model, context);
        optimizedPlan.CacheStrategy = cacheStrategy;
        
        return optimizedPlan;
    }

    private double PredictBackendScore(OptimizationModel model, ExecutionContext context)
    {
        if (!model.Predictions.TryGetValue("BackendPreference", out var prediction))
        {
            // Default heuristic: GPU for large datasets, CPU for small
            return context.DataSize > 10_000 ? 0.8 : 0.2;
        }
        
        return prediction;
    }

    private double PredictParallelismFactor(OptimizationModel model, ExecutionContext context)
    {
        if (!model.Predictions.TryGetValue("ParallelismFactor", out var prediction))
        {
            // Default heuristic based on compute intensity
            var intensity = model.BaseCharacteristics.ComputeIntensity;
            return Math.Min(1.0, 0.5 + intensity * 0.5);
        }
        
        return Math.Min(1.0, prediction);
    }

    private MemoryStrategy PredictMemoryStrategy(OptimizationModel model, ExecutionContext context)
    {
        if (!model.Predictions.TryGetValue("MemoryStrategy", out var prediction))
        {
            // Default based on memory intensity
            var intensity = model.BaseCharacteristics.MemoryIntensity;
            return intensity > 0.5 ? MemoryStrategy.Streaming : MemoryStrategy.Buffered;
        }
        
        return prediction > 0.5 ? MemoryStrategy.Streaming : MemoryStrategy.Buffered;
    }

    private double PredictFusionPriority(OptimizationModel model, ExecutionContext context)
    {
        if (!model.Predictions.TryGetValue("FusionPriority", out var prediction))
        {
            // Default based on operation count and data movement cost
            var operationDensity = (double)model.BaseCharacteristics.OperationCount / 
                                   Math.Max(1, model.BaseCharacteristics.DataSize / 1000);
            return Math.Min(1.0, operationDensity * 0.3);
        }
        
        return prediction;
    }

    private CacheStrategy PredictCacheStrategy(OptimizationModel model, ExecutionContext context)
    {
        if (!model.Predictions.TryGetValue("CacheStrategy", out var prediction))
        {
            // Default based on memory access patterns
            return context.HasComplexAccessPatterns ? CacheStrategy.Aggressive : CacheStrategy.Conservative;
        }
        
        return prediction > 0.5 ? CacheStrategy.Aggressive : CacheStrategy.Conservative;
    }

    private async Task<QueryPlan> ApplyKernelFusion(QueryPlan plan)
    {
        // Implement kernel fusion logic
        var fusedPlan = plan.Clone();
        
        // Find fusable operation sequences
        var fusionCandidates = FindFusionCandidates(plan.Operations);
        
        foreach (var candidate in fusionCandidates)
        {
            if (candidate.Operations.Count > 1)
            {
                var fusedOperation = await CreateFusedOperation(candidate.Operations);
                fusedPlan.ReplaceOperations(candidate.Operations, fusedOperation);
            }
        }
        
        return fusedPlan;
    }

    private List<FusionCandidate> FindFusionCandidates(IList<QueryOperation> operations)
    {
        var candidates = new List<FusionCandidate>();
        var currentCandidate = new List<QueryOperation>();
        
        foreach (var operation in operations)
        {
            if (CanFuseWithPrevious(operation, currentCandidate.LastOrDefault()))
            {
                currentCandidate.Add(operation);
            }
            else
            {
                if (currentCandidate.Count > 1)
                {
                    candidates.Add(new FusionCandidate { Operations = new List<QueryOperation>(currentCandidate) });
                }
                currentCandidate.Clear();
                currentCandidate.Add(operation);
            }
        }
        
        if (currentCandidate.Count > 1)
        {
            candidates.Add(new FusionCandidate { Operations = currentCandidate });
        }
        
        return candidates;
    }

    private bool CanFuseWithPrevious(QueryOperation current, QueryOperation? previous)
    {
        if (previous == null) return false;
        
        // Only fuse element-wise operations
        return (current.Type == OperationType.Map || current.Type == OperationType.Filter) &&
               (previous.Type == OperationType.Map || previous.Type == OperationType.Filter);
    }

    private async Task<QueryOperation> CreateFusedOperation(List<QueryOperation> operations)
    {
        // Create a new fused operation that combines the logic of multiple operations
        var fusedOperation = new QueryOperation
        {
            Type = OperationType.FusedKernel,
            InputSize = operations.First().InputSize,
            OutputSize = operations.Last().OutputSize,
            DataType = operations.Last().DataType,
            FusedOperations = operations.ToList()
        };
        
        return fusedOperation;
    }

    private void TrackExecution(string signature, QueryPlan plan, ExecutionContext context)
    {
        var history = _executionHistory.GetOrAdd(signature, _ => new ExecutionHistory
        {
            Signature = signature,
            Executions = new List<ExecutionRecord>()
        });
        
        // This will be called after execution completes
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var record = new ExecutionRecord
            {
                Timestamp = DateTime.UtcNow,
                Configuration = ExtractConfiguration(plan),
                Context = context.Clone(),
                // Performance metrics will be filled by execution callback
            };
            
            lock (history.Executions)
            {
                history.Executions.Add(record);
                if (history.Executions.Count > MaxHistorySize)
                {
                    history.Executions.RemoveAt(0);
                }
            }
        });
    }

    private OptimizationConfiguration ExtractConfiguration(QueryPlan plan)
    {
        return new OptimizationConfiguration
        {
            BackendType = plan.PreferredBackend,
            ParallelismDegree = plan.ParallelismDegree,
            MemoryStrategy = plan.MemoryStrategy,
            CacheStrategy = plan.CacheStrategy,
            FusionEnabled = plan.Operations.Any(op => op.Type == OperationType.FusedKernel)
        };
    }

    private void AdaptModels(object? state)
    {
        try
        {
            _modelLock.EnterWriteLock();
            
            foreach (var kvp in _executionHistory)
            {
                var signature = kvp.Key;
                var history = kvp.Value;
                
                if (history.Executions.Count >= MinSamplesForLearning)
                {
                    if (_models.TryGetValue(signature, out var model))
                    {
                        UpdateModel(model, history);
                    }
                }
            }
        }
        finally
        {
            _modelLock.ExitWriteLock();
        }
    }

    private void UpdateModel(OptimizationModel model, ExecutionHistory history)
    {
        // Simple gradient descent-based learning
        var recentExecutions = history.Executions
            .Where(e => e.Timestamp > DateTime.UtcNow.AddHours(-1))
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToList();
        
        if (recentExecutions.Count < 3) return;
        
        // Find best performing configurations
        var bestExecution = recentExecutions
            .Where(e => e.PerformanceMetrics != null)
            .OrderBy(e => e.PerformanceMetrics!.ExecutionTime)
            .FirstOrDefault();
        
        if (bestExecution?.Configuration != null)
        {
            // Update predictions based on best configuration
            UpdatePrediction(model, "BackendPreference", 
                bestExecution.Configuration.BackendType == BackendType.GPU ? 1.0 : 0.0);
            
            UpdatePrediction(model, "ParallelismFactor", 
                (double)bestExecution.Configuration.ParallelismDegree / Environment.ProcessorCount);
            
            UpdatePrediction(model, "MemoryStrategy", 
                bestExecution.Configuration.MemoryStrategy == MemoryStrategy.Streaming ? 1.0 : 0.0);
            
            UpdatePrediction(model, "CacheStrategy", 
                bestExecution.Configuration.CacheStrategy == CacheStrategy.Aggressive ? 1.0 : 0.0);
            
            UpdatePrediction(model, "FusionPriority", 
                bestExecution.Configuration.FusionEnabled ? 1.0 : 0.0);
        }
        
        model.LastUpdated = DateTime.UtcNow;
    }

    private void UpdatePrediction(OptimizationModel model, string key, double target)
    {
        if (!model.Predictions.TryGetValue(key, out var current))
        {
            current = 0.5; // Default neutral prediction
        }
        
        // Simple exponential moving average
        var updated = current + LearningRate * (target - current);
        model.Predictions[key] = Math.Max(0.0, Math.Min(1.0, updated));
    }

    public void Dispose()
    {
        _adaptationTimer?.Dispose();
        _modelLock?.Dispose();
    }
}

// Supporting classes and enums
public interface ILinqOptimizationStrategy
{
    Task<QueryPlan> OptimizeAsync(QueryPlan plan, ExecutionContext context);
}

public class WorkloadCharacteristics
{
    public long DataSize { get; set; }
    public int OperationCount { get; set; }
    public double ComputeIntensity { get; set; }
    public double MemoryIntensity { get; set; }
    public double ParallelismPotential { get; set; }
    public HardwareInfo Hardware { get; set; } = new();
}

public class OptimizationModel
{
    public string Signature { get; set; } = string.Empty;
    public WorkloadCharacteristics BaseCharacteristics { get; set; } = new();
    public Dictionary<string, double> Predictions { get; set; } = new();
    public Dictionary<string, double> Weights { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class ExecutionHistory
{
    public string Signature { get; set; } = string.Empty;
    public List<ExecutionRecord> Executions { get; set; } = new();
}

public class ExecutionRecord
{
    public DateTime Timestamp { get; set; }
    public OptimizationConfiguration Configuration { get; set; } = new();
    public ExecutionContext Context { get; set; } = new();
    public PerformanceMetrics? PerformanceMetrics { get; set; }
}

public class OptimizationConfiguration
{
    public BackendType BackendType { get; set; }
    public int ParallelismDegree { get; set; }
    public MemoryStrategy MemoryStrategy { get; set; }
    public CacheStrategy CacheStrategy { get; set; }
    public bool FusionEnabled { get; set; }
}

public class FusionCandidate
{
    public List<QueryOperation> Operations { get; set; } = new();
}

public enum BackendType
{
    CPU,
    GPU
}

public enum MemoryStrategy
{
    Buffered,
    Streaming
}

public enum CacheStrategy
{
    Conservative,
    Aggressive
}

public enum OperationType
{
    Map,
    Filter,
    Reduce,
    GroupBy,
    Join,
    Aggregate,
    FusedKernel
}

public class PerformanceMetrics
{
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsage { get; set; }
    public double ThroughputMBps { get; set; }
    public double CpuUtilization { get; set; }
    public double GpuUtilization { get; set; }
}