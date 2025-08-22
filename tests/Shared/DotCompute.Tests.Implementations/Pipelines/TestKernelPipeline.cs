using System.Diagnostics;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Types;
using FluentAssertions;

namespace DotCompute.Tests.Implementations.Pipelines;


/// <summary>
/// Test implementation of a kernel pipeline.
/// </summary>
public sealed class TestKernelPipeline : IKernelPipeline
{
    private readonly List<IPipelineStage> _stages;
    private readonly Dictionary<string, object> _metadata;
    private readonly TestPipelineMetrics _metrics;
    private Func<Exception, PipelineExecutionContext, ErrorHandlingResult>? _errorHandler;
    private Action<PipelineEvent>? _eventHandler;
    private bool _disposed;

    public TestKernelPipeline(string name)
    {
        Id = $"pipeline_{name}_{Guid.NewGuid():N}";
        Name = name;
        _stages = [];
        _metadata = [];
        _metrics = new TestPipelineMetrics(Id);
        OptimizationSettings = new PipelineOptimizationSettings();
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<IPipelineStage> Stages => _stages.AsReadOnly();
    public PipelineOptimizationSettings OptimizationSettings { get; internal set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public async ValueTask<PipelineExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<Core.Pipelines.Models.PipelineError>();
        var stageResults = new List<StageExecutionResult>();
        var outputs = new Dictionary<string, object>();

        try
        {
            _metrics.RecordExecutionStart();

            // Fire pipeline started event
            FireEvent(new PipelineEvent
            {
                Type = PipelineEventType.Started,
                Timestamp = DateTime.UtcNow,
                Message = $"Pipeline '{Name}' started"
            });

            // Execute stages in order(respecting dependencies)
            var executedStages = new HashSet<string>();

            while (executedStages.Count < _stages.Count)
            {
                var stagesToExecute = _stages
                    .Where(s => !executedStages.Contains(s.Id) &&
                               s.Dependencies.All(d => executedStages.Contains(d)))
                    .ToList();

                if (stagesToExecute.Count == 0)
                {
                    // No stages can be executed - circular dependency or invalid configuration
                    throw new InvalidOperationException("Pipeline has circular dependencies or invalid stage configuration");
                }

                // Execute stages that are ready
                var tasks = new List<Task<StageExecutionResult>>();

                foreach (var stage in stagesToExecute)
                {
                    FireEvent(new PipelineEvent
                    {
                        Type = PipelineEventType.StageStarted,
                        Timestamp = DateTime.UtcNow,
                        StageId = stage.Id,
                        Message = $"Stage '{stage.Name}' started"
                    });

                    tasks.Add(ExecuteStageAsync(stage, context, cancellationToken));
                }

                var results = await Task.WhenAll(tasks);

                foreach (var (stage, result) in stagesToExecute.Zip(results))
                {
                    _ = executedStages.Add(stage.Id);
                    stageResults.Add(result);

                    if (result.Success)
                    {
                        FireEvent(new PipelineEvent
                        {
                            Type = PipelineEventType.StageCompleted,
                            Timestamp = DateTime.UtcNow,
                            StageId = stage.Id,
                            Message = $"Stage '{stage.Name}' completed successfully"
                        });

                        // Merge outputs
                        if (result.Outputs != null)
                        {
                            foreach (var (key, value) in result.Outputs)
                            {
                                outputs[key] = value;
                            }
                        }
                    }
                    else
                    {
                        var error = new Core.Pipelines.Models.PipelineError
                        {
                            Code = "STAGE_EXECUTION_FAILED",
                            Message = result.Error?.Message ?? "Stage execution failed",
                            StageId = stage.Id,
                            Severity = Core.Pipelines.Types.ErrorSeverity.Error,
                            Exception = result.Error,
                            Timestamp = DateTime.UtcNow
                        };

                        errors.Add(error);

                        FireEvent(new PipelineEvent
                        {
                            Type = PipelineEventType.Error,
                            Timestamp = DateTime.UtcNow,
                            StageId = stage.Id,
                            Message = error.Message
                        });

                        if (!context.Options.ContinueOnError)
                        {
                            break;
                        }
                    }
                }

                if (errors.Count > 0 && !context.Options.ContinueOnError)
                {
                    break;
                }
            }

            stopwatch.Stop();

            var success = errors.Count == 0;
            _metrics.RecordExecutionComplete(stopwatch.Elapsed, success);

            FireEvent(new PipelineEvent
            {
                Type = PipelineEventType.Completed,
                Timestamp = DateTime.UtcNow,
                Message = $"Pipeline '{Name}' completed {(success ? "successfully" : "with errors")}"
            });

            return new PipelineExecutionResult
            {
                Success = success,
                Outputs = outputs,
                Metrics = new PipelineExecutionMetrics
                {
                    ExecutionId = $"exec-{Guid.NewGuid():N}",
                    StartTime = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                    EndTime = DateTime.UtcNow,
                    Duration = stopwatch.Elapsed,
                    MemoryUsage = new MemoryUsageStats
                    {
                        AllocatedBytes = CalculateTotalMemoryUsage(stageResults),
                        PeakBytes = CalculateTotalMemoryUsage(stageResults),
                        AllocationCount = stageResults.Count,
                        DeallocationCount = 0
                    },
                    ComputeUtilization = 0.75, // Default test value
                    MemoryBandwidthUtilization = 0.6, // Default test value
                    StageExecutionTimes = stageResults.ToDictionary(
                        r => r.StageId,
                        r => r.Duration),
                    DataTransferTimes = new Dictionary<string, TimeSpan>()
                },
                Errors = errors.Count > 0 ? errors : null,
                StageResults = stageResults
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecutionComplete(stopwatch.Elapsed, false);

            var error = new Core.Pipelines.Models.PipelineError
            {
                Code = "PIPELINE_EXECUTION_FAILED",
                Message = ex.Message,
                Severity = Core.Pipelines.Types.ErrorSeverity.Critical,
                Exception = ex,
                Timestamp = DateTime.UtcNow
            };

            errors.Add(error);

            FireEvent(new PipelineEvent
            {
                Type = PipelineEventType.Error,
                Timestamp = DateTime.UtcNow,
                Message = $"Pipeline '{Name}' failed: {ex.Message}"
            });

            return new PipelineExecutionResult
            {
                Success = false,
                Outputs = outputs,
                Metrics = new PipelineExecutionMetrics
                {
                    ExecutionId = $"exec-{Guid.NewGuid():N}",
                    StartTime = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                    EndTime = DateTime.UtcNow,
                    Duration = stopwatch.Elapsed,
                    MemoryUsage = new MemoryUsageStats
                    {
                        AllocatedBytes = 0,
                        PeakBytes = 0,
                        AllocationCount = 0,
                        DeallocationCount = 0
                    },
                    ComputeUtilization = 0.0, // Failed execution
                    MemoryBandwidthUtilization = 0.0, // Failed execution
                    StageExecutionTimes = new Dictionary<string, TimeSpan>(),
                    DataTransferTimes = new Dictionary<string, TimeSpan>()
                },
                Errors = errors,
                StageResults = stageResults
            };
        }
    }

    private async Task<StageExecutionResult> ExecuteStageAsync(
        IPipelineStage stage,
        PipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await stage.ExecuteAsync(context, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            if (_errorHandler != null)
            {
                var handlingResult = _errorHandler(ex, context);

                switch (handlingResult)
                {
                    case ErrorHandlingResult.Retry:
                        // Retry once
                        return await stage.ExecuteAsync(context, cancellationToken);

                    case ErrorHandlingResult.Skip:
                        // Return success to skip
                        return new StageExecutionResult
                        {
                            StageId = stage.Id,
                            Success = true,
                            Duration = TimeSpan.Zero,
                            Outputs = new Dictionary<string, object>()
                        };

                    case ErrorHandlingResult.Abort:
                        throw;

                    case ErrorHandlingResult.Continue:
                    default:
                        // Return failure but continue
                        return new StageExecutionResult
                        {
                            StageId = stage.Id,
                            Success = false,
                            Duration = TimeSpan.Zero,
                            Error = ex
                        };
                }
            }

            throw;
        }
    }

    private static long CalculateTotalMemoryUsage(IReadOnlyList<StageExecutionResult> stageResults)
    {
        return stageResults
            .Where(r => r.MemoryUsage != null)
            .Sum(r => r.MemoryUsage!.AllocatedBytes);
    }

    private static double CalculateThroughput(IReadOnlyList<StageExecutionResult> stageResults, TimeSpan totalDuration)
    {
        if (totalDuration.TotalSeconds == 0)
            return 0;

        var totalBytes = CalculateTotalMemoryUsage(stageResults);
        var totalMB = totalBytes / (1024.0 * 1024.0);

        return totalMB / totalDuration.TotalSeconds;
    }

    private void FireEvent(PipelineEvent evt) => _eventHandler?.Invoke(evt);

    public PipelineValidationResult Validate()
    {
        var errors = new List<Core.Pipelines.Models.ValidationError>();
        var warnings = new List<Core.Pipelines.Models.ValidationWarning>();

        // Validate stages
        foreach (var stage in _stages)
        {
            var validation = stage.Validate();
            if (!validation.IsValid && validation.Errors != null)
            {
                errors.AddRange(validation.Errors.Select(e => new Core.Pipelines.Models.ValidationError
                {
                    Code = "STAGE_VALIDATION_ERROR",
                    Message = e,
                    Path = $"stage.{stage.Id}"
                }));
            }

            if (validation.Warnings != null)
            {
                warnings.AddRange(validation.Warnings.Select(w => new Core.Pipelines.Models.ValidationWarning
                {
                    Code = "STAGE_VALIDATION_WARNING",
                    Message = w,
                    Path = $"stage.{stage.Id}"
                }));
            }
        }

        // Check for circular dependencies
        if (HasCircularDependencies())
        {
            errors.Add(new Core.Pipelines.Models.ValidationError
            {
                Code = "CIRCULAR_DEPENDENCIES",
                Message = "Pipeline contains circular dependencies",
                Path = "pipeline.dependencies"
            });
        }

        // Check for orphaned dependencies
        var stageIds = new HashSet<string>(_stages.Select(s => s.Id));
        foreach (var stage in _stages)
        {
            foreach (var dep in stage.Dependencies)
            {
                if (!stageIds.Contains(dep))
                {
                    errors.Add(new Core.Pipelines.Models.ValidationError
                    {
                        Code = "MISSING_DEPENDENCY",
                        Message = $"Dependency '{dep}' not found in pipeline",
                        Path = $"stage.{stage.Id}.dependencies.{dep}"
                    });
                }
            }
        }

        return new PipelineValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors.Cast<Core.Pipelines.Models.ValidationError>().ToList() : null,
            Warnings = warnings.Count > 0 ? warnings.Cast<Core.Pipelines.Models.ValidationWarning>().ToList() : null
        };
    }

    private bool HasCircularDependencies()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var stage in _stages)
        {
            if (HasCircularDependencyDFS(stage.Id, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCircularDependencyDFS(string stageId, HashSet<string> visited, HashSet<string> recursionStack)
    {
        _ = visited.Add(stageId);
        _ = recursionStack.Add(stageId);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage != null)
        {
            foreach (var dep in stage.Dependencies)
            {
                if (!visited.Contains(dep))
                {
                    if (HasCircularDependencyDFS(dep, visited, recursionStack))
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

        _ = recursionStack.Remove(stageId);
        return false;
    }

    public IPipelineMetrics GetMetrics() => _metrics;

    public async ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer)
    {
        var optimized = await optimizer.OptimizeAsync(this, OptimizationSettings);
        return optimized.Pipeline;
    }

    internal void AddStage(IPipelineStage stage) => _stages.Add(stage);

    internal void AddMetadata(string key, object value) => _metadata[key] = value;

    internal void SetErrorHandler(Func<Exception, PipelineExecutionContext, ErrorHandlingResult> handler) => _errorHandler = handler;

    internal void SetEventHandler(Action<PipelineEvent> handler) => _eventHandler = handler;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose any disposable stages
        foreach (var stage in _stages)
        {
            if (stage is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (stage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _stages.Clear();
        GC.SuppressFinalize(this);
    }
}
