using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Core.Pipelines;
using FluentAssertions;

namespace DotCompute.Tests.Shared.Pipelines;

/// <summary>
/// Test implementation of a custom pipeline stage.
/// </summary>
public class TestCustomStage : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, CancellationToken, ValueTask<StageExecutionResult>> _executeFunc;
    private readonly Dictionary<string, object> _metadata;
    private readonly TestStageMetrics _metrics;

    public TestCustomStage(
        string name,
        Func<PipelineExecutionContext, CancellationToken, ValueTask<StageExecutionResult>> executeFunc)
    {
        Id = $"custom_{name}_{Guid.NewGuid():N}";
        Name = name;
        _executeFunc = executeFunc ?? throw new ArgumentNullException(nameof(executeFunc));
        _metadata = new Dictionary<string, object>();
        _metrics = new TestStageMetrics(Name);
        Dependencies = Array.Empty<string>();
        Type = PipelineStageType.Custom;
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type { get; }
    public IReadOnlyList<string> Dependencies { get; private set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _metrics.RecordExecutionStart();
            var result = await _executeFunc(context, cancellationToken);
            
            stopwatch.Stop();
            _metrics.RecordExecutionSuccess(stopwatch.Elapsed, 0);
            
            // Ensure result has required fields
            if(string.IsNullOrEmpty(result.StageId) || result.Duration == TimeSpan.Zero)
            {
                result = new StageExecutionResult
                {
                    StageId = string.IsNullOrEmpty(result.StageId) ? Id : result.StageId,
                    Success = result.Success,
                    Outputs = result.Outputs,
                    Duration = result.Duration == TimeSpan.Zero ? stopwatch.Elapsed : result.Duration,
                    MemoryUsage = result.MemoryUsage,
                    Error = result.Error,
                    Metrics = result.Metrics
                };
            }
            
            return result;
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

    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        
        if(_executeFunc == null)
        {
            errors.Add("Execute function is not set");
        }
        
        if(string.IsNullOrEmpty(Name))
        {
            errors.Add("Stage name is required");
        }
        
        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null
        };
    }

    public IStageMetrics GetMetrics() => _metrics;

    public void SetDependencies(params string[] dependencies)
    {
        Dependencies = dependencies;
    }

    public void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
    }
}

/// <summary>
/// Test implementation of a branch stage.
/// </summary>
public class TestBranchStage : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, bool> _condition;
    private readonly IPipelineStage? _trueBranch;
    private readonly IPipelineStage? _falseBranch;
    private readonly Dictionary<string, object> _metadata;
    private readonly TestStageMetrics _metrics;

    public TestBranchStage(
        string name,
        Func<PipelineExecutionContext, bool> condition,
        IPipelineStage? trueBranch,
        IPipelineStage? falseBranch)
    {
        Id = $"branch_{name}_{Guid.NewGuid():N}";
        Name = name;
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _trueBranch = trueBranch;
        _falseBranch = falseBranch;
        _metadata = new Dictionary<string, object>();
        _metrics = new TestStageMetrics(Name);
        Dependencies = Array.Empty<string>();
        Type = PipelineStageType.Branch;
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type { get; }
    public IReadOnlyList<string> Dependencies { get; private set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _metrics.RecordExecutionStart();
            
            var conditionResult = _condition(context);
            var selectedBranch = conditionResult ? _trueBranch : _falseBranch;
            
            StageExecutionResult? branchResult = null;
            
            if(selectedBranch != null)
            {
                branchResult = await selectedBranch.ExecuteAsync(context, cancellationToken);
            }
            
            stopwatch.Stop();
            _metrics.RecordExecutionSuccess(stopwatch.Elapsed, 0);
            
            var outputs = new Dictionary<string, object>
            {
                ["BranchTaken"] = conditionResult ? "true" : "false"
            };
            
            if(branchResult?.Outputs != null)
            {
                foreach (var (key, value) in branchResult.Outputs)
                {
                    outputs[key] = value;
                }
            }
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = branchResult?.Success ?? true,
                Outputs = outputs,
                Duration = stopwatch.Elapsed,
                Error = branchResult?.Error,
                Metrics = new Dictionary<string, double>
                {
                    ["BranchCondition"] = conditionResult ? 1.0 : 0.0,
                    ["ExecutionTimeMs"] = stopwatch.Elapsed.TotalMilliseconds
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

    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if(_condition == null)
        {
            errors.Add("Branch condition is not set");
        }
        
        if(_trueBranch == null && _falseBranch == null)
        {
            warnings.Add("Both branches are null - branch will have no effect");
        }
        
        // Validate branches
        if(_trueBranch != null)
        {
            var validation = _trueBranch.Validate();
            if(!validation.IsValid && validation.Errors != null)
            {
                errors.AddRange(validation.Errors.Select(e => $"True branch: {e}"));
            }
        }
        
        if(_falseBranch != null)
        {
            var validation = _falseBranch.Validate();
            if(!validation.IsValid && validation.Errors != null)
            {
                errors.AddRange(validation.Errors.Select(e => $"False branch: {e}"));
            }
        }
        
        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    public IStageMetrics GetMetrics() => _metrics;

    public void SetDependencies(params string[] dependencies)
    {
        Dependencies = dependencies;
    }
}

/// <summary>
/// Test implementation of a loop stage.
/// </summary>
public class TestLoopStage : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, int, bool> _condition;
    private readonly IPipelineStage _body;
    private readonly Dictionary<string, object> _metadata;
    private readonly TestStageMetrics _metrics;
    private readonly int _maxIterations;

    public TestLoopStage(
        string name,
        Func<PipelineExecutionContext, int, bool> condition,
        IPipelineStage body,
        int maxIterations = 1000)
    {
        Id = $"loop_{name}_{Guid.NewGuid():N}";
        Name = name;
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _metadata = new Dictionary<string, object>();
        _metrics = new TestStageMetrics(Name);
        Dependencies = Array.Empty<string>();
        Type = PipelineStageType.Loop;
        _maxIterations = maxIterations;
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type { get; }
    public IReadOnlyList<string> Dependencies { get; private set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _metrics.RecordExecutionStart();
            
            var outputs = new Dictionary<string, object>();
            var iterationResults = new List<StageExecutionResult>();
            int iteration = 0;
            
            while(iteration < _maxIterations && _condition(context, iteration))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Store iteration number in context
                context.State["LoopIteration"] = iteration;
                
                var result = await _body.ExecuteAsync(context, cancellationToken);
                iterationResults.Add(result);
                
                if(!result.Success && !context.Options.ContinueOnError)
                {
                    break;
                }
                
                iteration++;
            }
            
            stopwatch.Stop();
            _metrics.RecordExecutionSuccess(stopwatch.Elapsed, 0);
            
            outputs["Iterations"] = iteration;
            outputs["SuccessfulIterations"] = iterationResults.Count(r => r.Success);
            
            // Merge outputs from last iteration
            if(iterationResults.Count > 0)
            {
                var lastResult = iterationResults.Last();
                if(lastResult.Outputs != null)
                {
                    foreach (var (key, value) in lastResult.Outputs)
                    {
                        outputs[$"Last_{key}"] = value;
                    }
                }
            }
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = iterationResults.All(r => r.Success),
                Outputs = outputs,
                Duration = stopwatch.Elapsed,
                Metrics = new Dictionary<string, double>
                {
                    ["Iterations"] = iteration,
                    ["AverageIterationTimeMs"] = iteration > 0 ? stopwatch.Elapsed.TotalMilliseconds / iteration : 0,
                    ["SuccessRate"] = iteration > 0 ?(double)iterationResults.Count(r => r.Success) / iteration * 100 : 0
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

    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if(_condition == null)
        {
            errors.Add("Loop condition is not set");
        }
        
        if(_body == null)
        {
            errors.Add("Loop body is not set");
        }
        else
        {
            var validation = _body.Validate();
            if(!validation.IsValid && validation.Errors != null)
            {
                errors.AddRange(validation.Errors.Select(e => $"Loop body: {e}"));
            }
        }
        
        if(_maxIterations <= 0)
        {
            errors.Add("Max iterations must be positive");
        }
        else if(_maxIterations > 10000)
        {
            warnings.Add($"Max iterations is very high{_maxIterations}) - potential infinite loop");
        }
        
        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    public IStageMetrics GetMetrics() => _metrics;

    public void SetDependencies(params string[] dependencies)
    {
        Dependencies = dependencies;
    }
}
