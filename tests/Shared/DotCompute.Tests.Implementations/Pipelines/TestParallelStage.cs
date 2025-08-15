using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Aot;
using DotCompute.Core.Pipelines;
using FluentAssertions;

namespace DotCompute.Tests.Shared.Pipelines;

/// <summary>
/// Test implementation of a parallel pipeline stage.
/// </summary>
public class TestParallelStage : IPipelineStage
{
    private readonly List<IPipelineStage> _stages;
    private readonly Dictionary<string, object> _metadata;
    private readonly TestStageMetrics _metrics;
    private int _maxDegreeOfParallelism;
    private SynchronizationMode _synchronizationMode;
    private bool _useBarrier;

    public TestParallelStage(string name)
    {
        Id = $"parallel_{name}_{Guid.NewGuid():N}";
        Name = name;
        _stages = new List<IPipelineStage>();
        _metadata = new Dictionary<string, object>();
        _metrics = new TestStageMetrics(Name);
        Dependencies = Array.Empty<string>();
        Type = PipelineStageType.Parallel;
        _maxDegreeOfParallelism = Environment.ProcessorCount;
        _synchronizationMode = SynchronizationMode.WaitAll;
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type { get; }
    public IReadOnlyList<string> Dependencies { get; private set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;
    public IReadOnlyList<IPipelineStage> Stages => _stages.AsReadOnly();

    public void Configure(Action<TestParallelStageBuilder> configure)
    {
        var builder = new TestParallelStageBuilder(this);
        configure(builder);
    }

    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);
        
        try
        {
            _metrics.RecordExecutionStart();
            
            var outputs = new Dictionary<string, object>();
            var stageResults = new List<StageExecutionResult>();
            
            switch(_synchronizationMode)
            {
                case SynchronizationMode.WaitAll:
                    stageResults = await ExecuteWaitAll(context, cancellationToken);
                    break;
                    
                case SynchronizationMode.WaitAny:
                    stageResults = await ExecuteWaitAny(context, cancellationToken);
                    break;
                    
                case SynchronizationMode.FireAndForget:
                    ExecuteFireAndForget(context, cancellationToken);
                    break;
                    
                case SynchronizationMode.Custom:
                    stageResults = await ExecuteCustom(context, cancellationToken);
                    break;
            }
            
            // Merge outputs from all stages
            foreach (var result in stageResults)
            {
                if(result.Outputs != null)
                {
                    foreach (var (key, value) in result.Outputs)
                    {
                        outputs[$"{result.StageId}_{key}"] = value;
                    }
                }
            }
            
            if(_useBarrier)
            {
                await Task.Yield(); // Simulate barrier synchronization
            }
            
            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);
            
            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = Math.Max(0, endMemory - startMemory),
                PeakBytes = endMemory,
                AllocationCount = _stages.Count,
                DeallocationCount = 0
            };
            
            _metrics.RecordExecutionSuccess(stopwatch.Elapsed, endMemory - startMemory);
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = stageResults.All(r => r.Success),
                Outputs = outputs,
                Duration = stopwatch.Elapsed,
                MemoryUsage = memoryUsage,
                Metrics = new Dictionary<string, double>
                {
                    ["ExecutionTimeMs"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["ParallelStages"] = _stages.Count,
                    ["SuccessfulStages"] = stageResults.Count(r => r.Success),
                    ["MaxDegreeOfParallelism"] = _maxDegreeOfParallelism
                }
            };
        }
        catch(Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecutionError(stopwatch.Elapsed);
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    private async Task<List<StageExecutionResult>> ExecuteWaitAll(
        PipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);
        var tasks = new List<Task<StageExecutionResult>>();
        
        foreach (var stage in _stages)
        {
            tasks.Add(ExecuteStageWithSemaphore(stage, context, semaphore, cancellationToken));
        }
        
        var results = await Task.WhenAll(tasks);
        semaphore.Dispose();
        
        return results.ToList();
    }

    private async Task<List<StageExecutionResult>> ExecuteWaitAny(
        PipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        var tasks = _stages.Select(stage => 
            stage.ExecuteAsync(context, cancellationToken).AsTask()).ToList();
        
        var results = new List<StageExecutionResult>();
        
        while(tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            results.Add(await completedTask);
            tasks.Remove(completedTask);
            
            // For WaitAny, we might want to cancel remaining tasks after first completion
            if(results.Count == 1 && _metadata.ContainsKey("CancelOthersOnFirstCompletion"))
            {
                break;
            }
        }
        
        return results;
    }

    private void ExecuteFireAndForget(
        PipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var stage in _stages)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await stage.ExecuteAsync(context, cancellationToken);
                }
                catch
                {
                    // Fire and forget - ignore errors
                }
            }, cancellationToken);
        }
    }

    private async Task<List<StageExecutionResult>> ExecuteCustom(
        PipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Custom synchronization - execute in batches
        var results = new List<StageExecutionResult>();
        var batchSize = Math.Max(1, _maxDegreeOfParallelism / 2);
        
        for(int i = 0; i < _stages.Count; i += batchSize)
        {
            var batch = _stages.Skip(i).Take(batchSize);
            var batchTasks = batch.Select(stage => 
                stage.ExecuteAsync(context, cancellationToken).AsTask());
            
            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults);
        }
        
        return results;
    }

    private async Task<StageExecutionResult> ExecuteStageWithSemaphore(
        IPipelineStage stage,
        PipelineExecutionContext context,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await stage.ExecuteAsync(context, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if(_stages.Count == 0)
        {
            errors.Add("Parallel stage has no child stages");
        }
        
        foreach (var stage in _stages)
        {
            var validation = stage.Validate();
            if(!validation.IsValid && validation.Errors != null)
            {
                errors.AddRange(validation.Errors.Select(e => $"{stage.Name}: {e}"));
            }
            if(validation.Warnings != null)
            {
                warnings.AddRange(validation.Warnings.Select(w => $"{stage.Name}: {w}"));
            }
        }
        
        if(_maxDegreeOfParallelism < 1)
        {
            errors.Add("MaxDegreeOfParallelism must be at least 1");
        }
        
        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    public IStageMetrics GetMetrics() => _metrics;

    internal void AddStage(IPipelineStage stage)
    {
        _stages.Add(stage);
    }

    internal void SetMaxDegreeOfParallelism(int maxDegree)
    {
        _maxDegreeOfParallelism = maxDegree;
    }

    internal void SetSynchronizationMode(SynchronizationMode mode)
    {
        _synchronizationMode = mode;
    }

    internal void SetBarrier(bool useBarrier)
    {
        _useBarrier = useBarrier;
    }

    internal void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    internal void SetDependencies(string[] dependencies)
    {
        Dependencies = dependencies;
    }
}

/// <summary>
/// Builder for configuring parallel stages.
/// </summary>
public class TestParallelStageBuilder : IParallelStageBuilder
{
    private readonly TestParallelStage _stage;

    public TestParallelStageBuilder(TestParallelStage stage)
    {
        _stage = stage;
    }

    public IParallelStageBuilder AddKernel(
        string name,
        ICompiledKernel kernel,
        Action<IKernelStageBuilder>? configure = null)
    {
        var kernelStage = new TestKernelStage(name, kernel);
        
        if(configure != null)
        {
            var builder = new TestKernelStageBuilder(kernelStage);
            configure(builder);
        }
        
        _stage.AddStage(kernelStage);
        return this;
    }

    public IParallelStageBuilder AddPipeline(
        string name,
        Action<IKernelPipelineBuilder> configure)
    {
        var pipelineBuilder = new TestKernelPipelineBuilder();
        pipelineBuilder.WithName(name);
        configure(pipelineBuilder);
        
        var pipeline = pipelineBuilder.Build();
        
        // Wrap pipeline as a custom stage
        var pipelineStage = new TestCustomStage(
            name,
            async(context, ct) =>
            {
                var result = await pipeline.ExecuteAsync(context, ct);
                return new StageExecutionResult
                {
                    StageId = pipeline.Id,
                    Success = result.Success,
                    Outputs = result.Outputs,
                    Duration = result.Metrics.Duration,
                    Error = result.Errors?.FirstOrDefault()?.Exception
                };
            });
        
        _stage.AddStage(pipelineStage);
        return this;
    }

    public IParallelStageBuilder WithMaxDegreeOfParallelism(int maxDegree)
    {
        _stage.SetMaxDegreeOfParallelism(maxDegree);
        return this;
    }

    public IParallelStageBuilder WithSynchronization(SynchronizationMode mode)
    {
        _stage.SetSynchronizationMode(mode);
        return this;
    }

    public IParallelStageBuilder WithBarrier()
    {
        _stage.SetBarrier(true);
        return this;
    }
}
