// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Core.Pipelines.Errors;
using DotCompute.Core.Pipelines.Exceptions;
using DotCompute.Core.Pipelines.Stages;
using DotCompute.Core.Pipelines.Types;
using DotCompute.Core.Pipelines.Validation;

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
        private readonly Func<Exception, PipelineExecutionContext, ErrorHandlingResult>? _errorHandler;
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
            Func<Exception, PipelineExecutionContext, ErrorHandlingResult>? errorHandler)
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
                AllocatedBytes = 0,
                PeakBytes = 0,
                AllocationCount = 0,
                DeallocationCount = 0
            };

            try
            {
                // Validate pipeline before execution
                var validationResult = Validate();
                if (!validationResult.IsValid)
                {
                    throw new PipelineValidationException(
                        "Pipeline validation failed",
                        validationResult.Errors ?? new List<ValidationError>(),
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

                        if (!context.Options.ContinueOnError)
                        {
                            break;
                        }
                    }
                    else if (stageResult.Outputs != null)
                    {
                        // Merge stage outputs
                        foreach (var (key, value) in stageResult.Outputs)
                        {
                            outputs[key] = value;
                        }
                    }

                    // Update memory stats
                    if (stageResult.MemoryUsage != null)
                    {
                        memoryStats = new MemoryUsageStats
                        {
                            AllocatedBytes = memoryStats.AllocatedBytes + stageResult.MemoryUsage.AllocatedBytes,
                            PeakBytes = Math.Max(memoryStats.PeakBytes, stageResult.MemoryUsage.PeakBytes),
                            AllocationCount = memoryStats.AllocationCount + stageResult.MemoryUsage.AllocationCount,
                            DeallocationCount = memoryStats.DeallocationCount + stageResult.MemoryUsage.DeallocationCount
                        };
                    }
                }

                stopwatch.Stop();
                var endTime = DateTime.UtcNow;

                var metrics = new PipelineExecutionMetrics
                {
                    ExecutionId = executionId,
                    StartTime = startTime,
                    EndTime = endTime,
                    Duration = stopwatch.Elapsed,
                    MemoryUsage = memoryStats,
                    ComputeUtilization = CalculateComputeUtilization(stageResults),
                    MemoryBandwidthUtilization = CalculateMemoryBandwidthUtilization(stageResults),
                    StageExecutionTimes = stageResults.ToDictionary(r => r.StageId, r => r.Duration),
                    DataTransferTimes = ExtractDataTransferTimes(stageResults)
                };

                _metrics.RecordExecution(metrics, errors.Count == 0);

                var success = errors.Count == 0 ||
                    (context.Options.ContinueOnError && errors.All(e => e.Severity < ErrorSeverity.Critical));

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
                    Outputs = outputs,
                    Metrics = metrics,
                    Errors = errors.Count > 0 ? errors : null,
                    StageResults = stageResults
                };
            }
            catch (Exception ex)
            {
                var error = new PipelineError
                {
                    Code = "PIPELINE_EXCEPTION",
                    Message = ex.Message,
                    Severity = ErrorSeverity.Fatal,
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
            var errors = new List<ValidationError>();
            var warnings = new List<ValidationWarning>();

            // Validate basic structure
            if (_stages.Count == 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "NO_STAGES",
                    Message = "Pipeline must contain at least one stage",
                    Path = "Stages"
                });
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
                        errors.Add(new ValidationError
                        {
                            Code = $"STAGE_ERROR_{stage.Id}",
                            Message = $"Stage '{stage.Name}': {error}",
                            Path = $"Stages[{stage.Id}]"
                        });
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
                        errors.Add(new ValidationError
                        {
                            Code = "INVALID_DEPENDENCY",
                            Message = $"Stage '{stage.Name}' depends on non-existent stage '{dep}'",
                            Path = $"Stages[{stage.Id}].Dependencies",
                            InvalidValue = dep
                        });
                    }
                }
            }

            // Check for circular dependencies
            if (HasCircularDependencies())
            {
                errors.Add(new ValidationError
                {
                    Code = "CIRCULAR_DEPENDENCY",
                    Message = "Pipeline contains circular dependencies",
                    Path = "Stages"
                });
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
                Errors = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        /// <inheritdoc/>
        public IPipelineMetrics GetMetrics() => _metrics;

        /// <inheritdoc/>
        public async ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer)
        {
            ThrowIfDisposed();

            var result = await optimizer.OptimizeAsync(this, OptimizationSettings);

            PublishEvent(new PipelineEvent
            {
                Type = PipelineEventType.OptimizationApplied,
                Timestamp = DateTime.UtcNow,
                Message = $"Pipeline optimized with {result.AppliedOptimizations.Count} optimizations",
                Data = new Dictionary<string, object>
                {
                    ["EstimatedSpeedup"] = result.EstimatedSpeedup,
                    ["EstimatedMemorySavings"] = result.EstimatedMemorySavings,
                    ["OptimizationCount"] = result.AppliedOptimizations.Count
                }
            });

            return result.Pipeline;
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
            var stageContext = new PipelineExecutionContext
            {
                Inputs = currentOutputs,
                MemoryManager = context.MemoryManager,
                Device = context.Device,
                Options = context.Options,
                Profiler = context.Profiler
            };

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
                        ["Duration"] = result.Duration.TotalMilliseconds
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
                    var errorResult = _errorHandler(ex, context);

                    switch (errorResult)
                    {
                        case ErrorHandlingResult.Continue:
                            return new StageExecutionResult
                            {
                                StageId = stage.Id,
                                Success = false,
                                Duration = TimeSpan.Zero,
                                Error = ex
                            };

                        case ErrorHandlingResult.Retry:
                            // Simple retry - in production, add backoff
                            return await ExecuteStageAsync(stage, context, executionId, currentOutputs, cancellationToken);

                        case ErrorHandlingResult.Skip:
                            return new StageExecutionResult
                            {
                                StageId = stage.Id,
                                Success = true,
                                Duration = TimeSpan.Zero
                            };

                        case ErrorHandlingResult.Abort:
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

        private bool CanMergeParallelStages() => _stages.Any(s => s.Type == PipelineStageType.Parallel);

        private static double CalculateComputeUtilization(List<StageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 0;
            }

            var utilizationSum = results
                .Where(r => r.Metrics?.ContainsKey("ComputeUtilization") == true)
                .Sum(r => r.Metrics!["ComputeUtilization"]);

            var count = results.Count(r => r.Metrics?.ContainsKey("ComputeUtilization") == true);

            return count > 0 ? utilizationSum / count : 0;
        }

        private static double CalculateMemoryBandwidthUtilization(List<StageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 0;
            }

            var utilizationSum = results
                .Where(r => r.Metrics?.ContainsKey("MemoryBandwidthUtilization") == true)
                .Sum(r => r.Metrics!["MemoryBandwidthUtilization"]);

            var count = results.Count(r => r.Metrics?.ContainsKey("MemoryBandwidthUtilization") == true);

            return count > 0 ? utilizationSum / count : 0;
        }

        private static Dictionary<string, TimeSpan> ExtractDataTransferTimes(List<StageExecutionResult> results)
        {
            var transferTimes = new Dictionary<string, TimeSpan>();

            foreach (var result in results)
            {
                if (result.Metrics?.ContainsKey("DataTransferTime") == true)
                {
                    var duration = TimeSpan.FromMilliseconds(result.Metrics["DataTransferTime"]);
                    transferTimes[$"{result.StageId}_transfer"] = duration;
                }
            }

            return transferTimes;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(KernelPipeline));
    }
}
