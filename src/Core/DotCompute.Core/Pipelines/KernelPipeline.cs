// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using System.Diagnostics;
using DotCompute.Core.Pipelines.Exceptions;
using DotCompute.Abstractions.Models.Pipelines;
using PipelineEvent = DotCompute.Core.Pipelines.Types.PipelineEvent;
using DotCompute.Core.Validation;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines;
using AbstractionsPipelineExecutionMetrics = DotCompute.Abstractions.Pipelines.Models.PipelineExecutionMetrics;
using CorePipelineExecutionContext = DotCompute.Core.Pipelines.Models.PipelineExecutionContext;
using ValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;
using ValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;
using IPipelineMetricsInterface = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IPipelineMetrics;
using ErrorSeverity = DotCompute.Abstractions.Types.ErrorSeverity;
using MemoryUsageStats = DotCompute.Abstractions.Pipelines.Results.MemoryUsageStats;
using PipelineExecutionOptions = DotCompute.Core.Pipelines.Models.PipelineExecutionOptions;

// Additional type aliases to resolve ambiguous references
using PipelineOptimizationSettings = DotCompute.Abstractions.Pipelines.Models.PipelineOptimizationSettings;
using PipelineExecutionResult = DotCompute.Abstractions.Pipelines.Results.PipelineExecutionResult;
using ErrorHandlingResult = DotCompute.Abstractions.Pipelines.Models.ErrorHandlingResult;
// Import all Pipelines types directly
using StageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Default implementation of a kernel pipeline.
    /// </summary>
    public sealed class KernelPipeline : IKernelPipeline
    {
        private readonly List<IPipelineStage> _stages;
        private readonly Dictionary<string, object> _metadata;
        private readonly PipelineMetrics _metrics;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly List<Action<PipelineEvent>> _eventHandlers;
        private readonly Func<Exception, CorePipelineExecutionContext, ErrorHandlingResult>? _errorHandler;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the KernelPipeline class.
        /// </summary>
        internal KernelPipeline(
            string id,
            string name,
            List<IPipelineStage> stages,
            PipelineOptimizationSettings optimizationSettings,
            Dictionary<string, object> metadata,
            List<Action<PipelineEvent>> eventHandlers,
            Func<Exception, CorePipelineExecutionContext, ErrorHandlingResult>? errorHandler)
        {
            Id = id;
            Name = name;
            _stages = stages;
            OptimizationSettings = optimizationSettings;
            _metadata = metadata;
            _metrics = new PipelineMetrics(id);
            _executionSemaphore = new SemaphoreSlim(1, 1);
            _eventHandlers = eventHandlers;
            _errorHandler = errorHandler;
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IReadOnlyList<IPipelineStage> Stages => _stages;

        /// <inheritdoc/>
        public PipelineOptimizationSettings OptimizationSettings { get; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata => _metadata;

        /// <inheritdoc/>
        public async ValueTask<PipelineExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            PublishEvent(new PipelineEvent
            {
                Type = PipelineEventType.Started,
                Timestamp = startTime,
                Message = $"Pipeline '{Name}' execution started",
                Data = new Dictionary<string, object>
                {
                    ["ExecutionId"] = executionId,
                    ["InputCount"] = context.Inputs.Count
                }
            });

            var stageResults = new List<StageExecutionResult>();
            var errors = new List<PipelineError>();
            var outputs = new Dictionary<string, object>(context.Inputs);
            var memoryStats = new MemoryUsageStats
            {
                TotalAllocatedBytes = 0,
                PeakMemoryUsageBytes = 0,
                AllocationCount = 0
            };

            try
            {
                // Validate pipeline before execution
                var validationResult = Validate();
                if (!validationResult.IsValid)
                {
                    throw new PipelineValidationException(
                        "Pipeline validation failed",
                        ConvertValidationIssues(validationResult.Errors ?? []),
                        validationResult.Warnings);
                }

                // Execute stages
                foreach (var stage in _stages)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        errors.Add(new PipelineError
                        {
                            Code = "EXECUTION_CANCELLED",
                            Message = "Pipeline execution was cancelled",
                            Severity = ErrorSeverity.Warning,
                            Timestamp = DateTime.UtcNow
                        });
                        break;
                    }

                    var stageResult = await ExecuteStageAsync(
                        stage,
                        context,
                        executionId,
                        outputs,
                        cancellationToken);

                    stageResults.Add(stageResult);

                    if (!stageResult.Success)
                    {
                        var error = new PipelineError
                        {
                            Code = "STAGE_FAILED",
                            Message = $"Stage '{stage.Name}' failed: {stageResult.Error?.Message}",
                            StageId = stage.Id,
                            Severity = ErrorSeverity.Error,
                            Timestamp = DateTime.UtcNow,
                            Exception = stageResult.Error
                        };

                        errors.Add(error);

                        var options = context.Options as PipelineExecutionOptions;
                        if (!(options?.ContinueOnError ?? false))
                        {
                            break;
                        }
                    }
                    else if (stageResult.OutputData != null)
                    {
                        // Merge stage outputs
                        foreach (var (key, value) in stageResult.OutputData)
                        {
                            outputs[key] = value;
                        }
                    }

                    // Update memory stats
                    if (stageResult.MemoryUsage > 0)
                    {
                        memoryStats = new MemoryUsageStats
                        {
                            TotalAllocatedBytes = memoryStats.TotalAllocatedBytes + stageResult.MemoryUsage,
                            PeakMemoryUsageBytes = Math.Max(memoryStats.PeakMemoryUsageBytes, stageResult.MemoryUsage),
                            AllocationCount = memoryStats.AllocationCount + 1
                        };
                    }
                }

                stopwatch.Stop();
                var endTime = DateTime.UtcNow;

                var metrics = new AbstractionsPipelineExecutionMetrics
                {
                    ExecutionId = Guid.NewGuid().ToString(),
                    PipelineName = Id,
                    StartTime = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                    EndTime = DateTime.UtcNow,
                    TotalExecutionTime = stopwatch.Elapsed,
                    StageExecutions = stageResults.Count,
                    ComputationTime = TimeSpan.FromMilliseconds(stageResults.Sum(r => r.ExecutionTime.TotalMilliseconds)),
                    DataTransferTime = ExtractDataTransferTimes(stageResults).Values.Aggregate(TimeSpan.Zero, (sum, time) => sum + time),
                    ParallelExecutions = stageResults.Count(r => r.Metadata?.ContainsKey("IsParallel") == true),
                    Throughput = stageResults.Count > 0 ? stageResults.Count / stopwatch.Elapsed.TotalSeconds : 0,
                    ExecutionStatus = errors.Count == 0 ? DotCompute.Abstractions.Types.ExecutionStatus.Completed : DotCompute.Abstractions.Types.ExecutionStatus.Failed,
                    AdditionalMetrics = new Dictionary<string, object>
                    {
                        ["ComputeUtilization"] = CalculateComputeUtilization(stageResults),
                        ["MemoryBandwidthUtilization"] = CalculateMemoryBandwidthUtilization(stageResults),
                        ["MemoryUsage"] = memoryStats,
                        ["StageExecutionTimes"] = stageResults.ToDictionary(r => r.StageId, r => r.ExecutionTime.TotalMilliseconds)
                    }
                };

                _metrics.RecordExecution(metrics, errors.Count == 0);

                var executionOptions = context.Options as PipelineExecutionOptions;
                var success = errors.Count == 0 ||
                    (executionOptions?.ContinueOnError ?? false && errors.All(e => (int)e.Severity < (int)ErrorSeverity.Critical));

                PublishEvent(new PipelineEvent
                {
                    Type = PipelineEventType.Completed,
                    Timestamp = endTime,
                    Message = $"Pipeline '{Name}' execution completed",
                    Data = new Dictionary<string, object>
                    {
                        ["ExecutionId"] = executionId,
                        ["Success"] = success,
                        ["Duration"] = stopwatch.Elapsed.TotalMilliseconds,
                        ["ErrorCount"] = errors.Count
                    }
                });

                return new PipelineExecutionResult
                {
                    Success = success,
                    PipelineId = Id,
                    PipelineName = Name,
                    Outputs = outputs,
                    Metrics = ConvertToModelsPipelineExecutionMetrics(metrics),
                    Errors = errors.Count > 0 ? errors : null,
                    StageResults = stageResults,
                    StartTime = startTime,
                    EndTime = endTime
                };
            }
            catch (Exception ex)
            {
                var error = new PipelineError
                {
                    Code = "PIPELINE_EXCEPTION",
                    Message = ex.Message,
                    Severity = ErrorSeverity.Critical,
                    Timestamp = DateTime.UtcNow,
                    Exception = ex
                };

                errors.Add(error);

                PublishEvent(new PipelineEvent
                {
                    Type = PipelineEventType.Error,
                    Timestamp = DateTime.UtcNow,
                    Message = $"Pipeline '{Name}' execution failed with exception",
                    Data = new Dictionary<string, object>
                    {
                        ["ExecutionId"] = executionId,
                        ["ExceptionType"] = ex.GetType().Name,
                        ["Message"] = ex.Message
                    }
                });

                throw new PipelineExecutionException(
                    $"Pipeline execution failed: {ex.Message}",
                    Id,
                    errors,
                    null,
                    ex);
            }
        }

        /// <inheritdoc/>
        public PipelineValidationResult Validate()
        {
            var errors = new List<ValidationIssue>();
            var warnings = new List<ValidationWarning>();

            // Validate basic structure
            if (_stages.Count == 0)
            {
                errors.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Pipeline must contain at least one stage",
                    "NO_STAGES",
                    "Stages"));
            }

            // Validate stage dependencies
            var stageIds = new HashSet<string>(_stages.Select(s => s.Id));
            foreach (var stage in _stages)
            {
                var stageValidation = stage.Validate();
                if (!stageValidation.IsValid && stageValidation.Errors != null)
                {
                    foreach (var error in stageValidation.Errors)
                    {
                        errors.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            $"Stage '{stage.Name}': {error}",
                            $"STAGE_ERROR_{stage.Id}",
                            $"Stages[{stage.Id}]"));
                    }
                }

                if (stageValidation.Warnings != null)
                {
                    foreach (var warning in stageValidation.Warnings)
                    {
                        warnings.Add(new ValidationWarning
                        {
                            Code = $"STAGE_WARNING_{stage.Id}",
                            Message = $"Stage '{stage.Name}': {warning}",
                            Path = $"Stages[{stage.Id}]"
                        });
                    }
                }

                // Check dependencies exist
                foreach (var dep in stage.Dependencies)
                {
                    if (!stageIds.Contains(dep))
                    {
                        errors.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            $"Stage '{stage.Name}' depends on non-existent stage '{dep}'",
                            "INVALID_DEPENDENCY",
                            $"Stages[{stage.Id}].Dependencies",
                            dep));
                    }
                }
            }

            // Check for circular dependencies
            if (HasCircularDependencies())
            {
                errors.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Pipeline contains circular dependencies",
                    "CIRCULAR_DEPENDENCY",
                    "Stages"));
            }

            // Validate optimization settings
            if (OptimizationSettings.EnableParallelMerging && !CanMergeParallelStages())
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "PARALLEL_MERGE_INEFFECTIVE",
                    Message = "Parallel merging is enabled but no mergeable stages found",
                    Path = "OptimizationSettings.EnableParallelMerging",
                    Recommendation = "Disable parallel merging or add parallel stages"
                });
            }

            return new PipelineValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.Count > 0 ? errors.Select(e => new ValidationIssue(e.Code ?? "UNKNOWN", e.Message, DotCompute.Abstractions.Validation.ValidationSeverity.Error)).ToList() : null,
                Warnings = warnings.Count > 0 ? warnings.Select(w => new AbstractionsMemory.Validation.ValidationWarning { Code = w.Code, Message = w.Message, Severity = DotCompute.Abstractions.Validation.WarningSeverity.Medium }).ToList() : null
            };
        }

        /// <inheritdoc/>
        public IPipelineMetricsInterface GetMetrics() => _metrics;

        /// <inheritdoc/>
        public async ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer)
        {
            ThrowIfDisposed();

            var result = await optimizer.OptimizeAsync(this, OptimizationSettings.OptimizationTypes);

            PublishEvent(new PipelineEvent
            {
                Type = PipelineEventType.OptimizationApplied,
                Timestamp = DateTime.UtcNow,
                Message = $"Pipeline optimized",
                Data = new Dictionary<string, object>
                {
                    ["OptimizationApplied"] = true
                }
            });

            return result;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _executionSemaphore?.Dispose();

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

            _isDisposed = true;
        }

        private async ValueTask<StageExecutionResult> ExecuteStageAsync(
            IPipelineStage stage,
            PipelineExecutionContext context,
            string executionId,
            Dictionary<string, object> currentOutputs,
            CancellationToken cancellationToken)
        {
            var stageContext = new CorePipelineExecutionContext();
            foreach (var kvp in currentOutputs)
            {
                stageContext.Inputs[kvp.Key] = kvp.Value;
            }
            stageContext.SetMemoryManager(context.MemoryManager);
            stageContext.SetDevice(context.Device);
            stageContext.Options = context.Options;

            // Copy state
            foreach (var (key, value) in context.State)
            {
                stageContext.State[key] = value;
            }

            PublishEvent(new PipelineEvent
            {
                Type = PipelineEventType.StageStarted,
                StageId = stage.Id,
                Timestamp = DateTime.UtcNow,
                Message = $"Stage '{stage.Name}' started"
            });

            context.Profiler?.StartStageExecution(executionId, stage.Id);

            try
            {
                var result = await stage.ExecuteAsync(stageContext, cancellationToken);

                context.Profiler?.EndStageExecution(executionId, stage.Id);

                PublishEvent(new PipelineEvent
                {
                    Type = PipelineEventType.StageCompleted,
                    StageId = stage.Id,
                    Timestamp = DateTime.UtcNow,
                    Message = $"Stage '{stage.Name}' completed",
                    Data = new Dictionary<string, object>
                    {
                        ["Success"] = result.Success,
                        ["Duration"] = result.ExecutionTime.TotalMilliseconds
                    }
                });

                // Update main context state
                foreach (var (key, value) in stageContext.State)
                {
                    context.State[key] = value;
                }

                return result;
            }
            catch (Exception ex)
            {
                context.Profiler?.EndStageExecution(executionId, stage.Id);

                if (_errorHandler != null)
                {
                    var errorResult = _errorHandler(ex, ConvertToCorePipelineExecutionContext(context));

                    switch (errorResult.Action)
                    {
                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.None:
                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.Failed:
                            return new StageExecutionResult
                            {
                                StageId = stage.Id,
                                Success = false,
                                ExecutionTime = TimeSpan.Zero,
                                OutputData = [],
                                Error = ex
                            };

                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.Retry:
                            // Simple retry - in production, add backoff
                            return await ExecuteStageAsync(stage, context, executionId, currentOutputs, cancellationToken);

                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.Skip:
                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.Ignored:
                            return new StageExecutionResult
                            {
                                StageId = stage.Id,
                                Success = true,
                                ExecutionTime = TimeSpan.Zero,
                                OutputData = []
                            };

                        case DotCompute.Abstractions.Pipelines.Models.ErrorHandlingAction.Abort:
                        default:
                            throw;
                    }
                }

                throw;
            }
        }

        private void PublishEvent(PipelineEvent evt)
        {
            foreach (var handler in _eventHandlers)
            {
                try
                {
                    handler(evt);
                }
                catch
                {
                    // Ignore event handler errors
                }
            }
        }

        private bool HasCircularDependencies()
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var adjacencyList = _stages.ToDictionary(
                s => s.Id,
                s => s.Dependencies.ToList());

            foreach (var stage in _stages)
            {
                if (HasCircularDependencyDFS(stage.Id, adjacencyList, visited, recursionStack))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCircularDependencyDFS(
            string stageId,
            Dictionary<string, List<string>> adjacencyList,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            _ = visited.Add(stageId);
            _ = recursionStack.Add(stageId);

            if (adjacencyList.TryGetValue(stageId, out var dependencies))
            {
                foreach (var dep in dependencies)
                {
                    if (!visited.Contains(dep))
                    {
                        if (HasCircularDependencyDFS(dep, adjacencyList, visited, recursionStack))
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

        private bool CanMergeParallelStages() => _stages.Any(s => s.Type == PipelineStageType.ParallelExecution);

        private static double CalculateComputeUtilization(List<StageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 0;
            }

            var utilizationSum = results
                .Where(r => r.Metadata?.ContainsKey("ComputeUtilization") == true)
                .Sum(r => (double)r.Metadata!["ComputeUtilization"]);

            var count = results.Count(r => r.Metadata?.ContainsKey("ComputeUtilization") == true);

            return count > 0 ? utilizationSum / count : 0;
        }

        private static double CalculateMemoryBandwidthUtilization(List<StageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 0;
            }

            var utilizationSum = results
                .Where(r => r.Metadata?.ContainsKey("MemoryBandwidthUtilization") == true)
                .Sum(r => (double)r.Metadata!["MemoryBandwidthUtilization"]);

            var count = results.Count(r => r.Metadata?.ContainsKey("MemoryBandwidthUtilization") == true);

            return count > 0 ? utilizationSum / count : 0;
        }

        private static Dictionary<string, TimeSpan> ExtractDataTransferTimes(List<StageExecutionResult> results)
        {
            var transferTimes = new Dictionary<string, TimeSpan>();

            foreach (var result in results)
            {
                if (result.Metadata?.ContainsKey("DataTransferTime") == true)
                {
                    var duration = TimeSpan.FromMilliseconds((double)result.Metadata["DataTransferTime"]);
                    transferTimes[$"{result.StageId}_transfer"] = duration;
                }
            }

            return transferTimes;
        }

        /// <summary>
        /// Converts Models.Pipelines.PipelineExecutionContext to Core.Pipelines.Models.PipelineExecutionContext
        /// </summary>
        private static CorePipelineExecutionContext ConvertToCorePipelineExecutionContext(PipelineExecutionContext modelsContext)
        {
            return new CorePipelineExecutionContext
            {
                PipelineId = modelsContext.PipelineId,
                SessionId = modelsContext.SessionId,
                Options = modelsContext.Options,
                Profiler = modelsContext.Profiler
            };
        }

        /// <summary>
        /// Converts Pipelines.Models.PipelineExecutionMetrics to Models.Pipelines.PipelineExecutionMetrics
        /// </summary>
        private static PipelineExecutionMetrics ConvertToModelsPipelineExecutionMetrics(AbstractionsPipelineExecutionMetrics pipelineMetrics)
        {
            return new PipelineExecutionMetrics
            {
                ExecutionId = pipelineMetrics.ExecutionId,
                PipelineName = pipelineMetrics.PipelineName,
                StartTime = pipelineMetrics.StartTime,
                EndTime = pipelineMetrics.EndTime,
                TotalExecutionTime = pipelineMetrics.TotalExecutionTime,
                StageExecutions = pipelineMetrics.StageExecutions,
                ComputationTime = pipelineMetrics.ComputationTime,
                DataTransferTime = pipelineMetrics.DataTransferTime,
                ParallelExecutions = pipelineMetrics.ParallelExecutions,
                Throughput = pipelineMetrics.Throughput,
                ExecutionStatus = pipelineMetrics.ExecutionStatus,
                AdditionalMetrics = pipelineMetrics.AdditionalMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        /// <summary>
        /// Converts ValidationIssue from Abstractions to Validation namespace
        /// </summary>
        private static IReadOnlyList<ValidationIssue> ConvertValidationIssues(IReadOnlyList<ValidationIssue> issues)
        {
            return issues.Select(issue => new ValidationIssue(
                (ValidationSeverity)(int)issue.Severity,
                issue.Message,
                issue.Code,
                issue.Source
            )).ToList();
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(KernelPipeline));
    }
}
