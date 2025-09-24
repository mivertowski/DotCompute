// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Telemetry;
using DotCompute.Core.Models;
using DotCompute.Core.Pipelines.Types;
// using DotCompute.Core.Pipelines.Models; // Removed to avoid ambiguity - using alias instead

// Type aliases to resolve ambiguous references
using PipelineExecutionContext = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext;
using SynchronizationMode = DotCompute.Abstractions.Types.SynchronizationMode;
using DotCompute.Abstractions.Validation;
using ValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;
using AbsStageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;
using SynchronizationStrategyEnum = DotCompute.Core.Pipelines.Types.SynchronizationStrategy;
using CoreStageExecutionResult = DotCompute.Core.Pipelines.Models.StageExecutionResult;
using PipelineStageType = DotCompute.Abstractions.Pipelines.Enums.PipelineStageType;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;
using StageValidationResult = DotCompute.Abstractions.Models.Pipelines.StageValidationResult;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes multiple stages in parallel.
    /// </summary>
    internal sealed class ParallelStage(
        string id,
        string name,
        List<IPipelineStage> parallelStages,
        int maxDegreeOfParallelism,
        SynchronizationMode synchronizationMode,
        bool hasBarrier) : IPipelineStage
    {
        private readonly List<IPipelineStage> _parallelStages = parallelStages;
        private readonly int _maxDegreeOfParallelism = maxDegreeOfParallelism;
        private readonly SynchronizationMode _synchronizationMode = synchronizationMode;
        private readonly bool _hasBarrier = hasBarrier;
        private readonly StageMetrics _metrics = new(id);

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = name;

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Parallel;

        /// <inheritdoc/>
        public IReadOnlyList<string> Dependencies { get; } = [];

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        /// <inheritdoc/>
        public async ValueTask<AbsStageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var startMemory = GC.GetTotalMemory(false);

            // Start performance monitoring
            PerformanceMonitor.ExecutionMetrics.StartExecution();
            var startCpuUtilization = PerformanceMonitor.GetCpuUtilization();

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };

                var results = new List<AbsStageExecutionResult>();
                var outputs = new Dictionary<string, object>(context.Inputs);

                switch (_synchronizationMode)
                {
                    case SynchronizationMode.WaitAll:
                        // Performance optimization: Use pre-allocated array for better performance
                        var tasks = new Task<AbsStageExecutionResult>[_parallelStages.Count];
                        for (var i = 0; i < _parallelStages.Count; i++)
                        {
                            var stage = _parallelStages[i];
                            tasks[i] = ExecuteStageAsync(stage, context, cancellationToken);
                        }
                        var stageResults = await Task.WhenAll(tasks);
                        results.AddRange(stageResults);
                        break;

                    case SynchronizationMode.WaitAny:
                        var anyTasks = _parallelStages.Select(stage => ExecuteStageAsync(stage, context, cancellationToken)).ToArray();
                        var completedTask = await Task.WhenAny(anyTasks);
                        var completedResult = await completedTask;
                        results.Add(completedResult);
                        break;

                    case SynchronizationMode.FireAndForget:
                        // Performance optimization: Use ThreadPool directly for fire-and-forget
                        foreach (var stage in _parallelStages)
                        {
                            var capturedStage = stage;
                            _ = ThreadPool.QueueUserWorkItem(async _ =>
                            {
                                try
                                {
                                    _ = await ExecuteStageAsync(capturedStage, context, cancellationToken);
                                }
                                catch
                                {
                                    // Fire and forget - ignore errors
                                }
                            });
                        }
                        break;

                    case SynchronizationMode.Custom:
                        // Custom synchronization implementation using configurable strategies
                        await ExecuteCustomSynchronizationAsync(context, results, cancellationToken);
                        break;
                }

                // Merge outputs from all stages
                foreach (var result in results)
                {
                    if (result.Success && result.OutputData != null)
                    {
                        foreach (var (key, value) in result.OutputData)
                        {
                            outputs[key] = value;
                        }
                    }
                }

                stopwatch.Stop();

                // Get detailed execution metrics
                var (cpuTime, allocatedBytes, elapsedMs) = PerformanceMonitor.ExecutionMetrics.EndExecution();
                var endCpuUtilization = PerformanceMonitor.GetCpuUtilization();
                var endMemory = GC.GetTotalMemory(false);

                var success = results.All(r => r.Success);
                var totalMemoryUsage = results.Sum(r => r.MemoryUsed);

                var memoryUsed = Math.Max(allocatedBytes, totalMemoryUsage);

                _metrics.RecordExecution(stopwatch.Elapsed, success);
                _metrics.RecordMemoryUsage(memoryUsed);

                // Calculate real parallel efficiency
                var parallelEfficiency = CalculateParallelEfficiency(results);
                var loadBalance = CalculateLoadBalance(results);

                // Calculate real synchronization overhead based on CPU utilization difference
                var avgCpuUtilization = (startCpuUtilization + endCpuUtilization) / 2.0;
                var expectedCpuUtilization = Math.Min(1.0, parallelEfficiency * _maxDegreeOfParallelism / Environment.ProcessorCount);
                var syncOverhead = Math.Max(0.0, (expectedCpuUtilization - avgCpuUtilization) / expectedCpuUtilization);

                return new AbsStageExecutionResult
                {
                    StageId = Id,
                    Success = success,
                    ExecutionTime = stopwatch.Elapsed,
                    OutputData = outputs.Count > context.Inputs.Count ? outputs : [],
                    MemoryUsed = memoryUsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ParallelEfficiency"] = parallelEfficiency,
                        ["LoadBalance"] = loadBalance,
                        ["SynchronizationOverhead"] = syncOverhead,
                        ["CpuUtilization"] = avgCpuUtilization,
                        ["ThreadPoolUtilization"] = GetThreadPoolUtilization(),
                        ["ActualParallelism"] = CalculateActualParallelism(results, stopwatch.Elapsed)
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, false);

                return new AbsStageExecutionResult
                {
                    StageId = Id,
                    Success = false,
                    ExecutionTime = stopwatch.Elapsed,
                    OutputData = [],
                    MemoryUsed = 0,
                    Error = ex
                };
            }
        }

        /// <inheritdoc/>
        public StageValidationResult Validate()
        {
            var errors = new List<ValidationIssue>();
            var warnings = new List<string>();

            if (_parallelStages.Count == 0)
            {
                errors.Add(new ValidationIssue("PARALLEL_001", "Parallel stage must contain at least one sub-stage", ValidationSeverity.Error));
            }

            if (_maxDegreeOfParallelism <= 0)
            {
                errors.Add(new ValidationIssue("PARALLEL_002", "Max degree of parallelism must be positive", ValidationSeverity.Error));
            }

            // Validate all sub-stages
            foreach (var stage in _parallelStages)
            {
                var stageValidation = stage.Validate();
                if (!stageValidation.IsValid && stageValidation.Issues != null)
                {
                    foreach (var error in stageValidation.Issues)
                    {
                        errors.Add(new ValidationIssue(ValidationSeverity.Error, $"Sub-stage '{stage.Name}': {error.Message}", "PARALLEL_003"));
                    }
                }

                if (stageValidation.Warnings != null)
                {
                    foreach (var warning in stageValidation.Warnings)
                    {
                        warnings.Add($"Sub-stage '{stage.Name}': {warning}");
                    }
                }
            }

            return new StageValidationResult
            {
                IsValid = errors.Count == 0,
                Issues = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;

        private static async Task<AbsStageExecutionResult> ExecuteStageAsync(
            IPipelineStage stage,
            PipelineExecutionContext context,
            CancellationToken cancellationToken) => await stage.ExecuteAsync(context, cancellationToken);

        private static double CalculateParallelEfficiency(List<AbsStageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 0;
            }

            var totalTime = results.Sum(r => r.ExecutionTime.TotalMilliseconds);
            var maxTime = results.Max(r => r.ExecutionTime.TotalMilliseconds);

            return maxTime > 0 ? (totalTime / (results.Count * maxTime)) : 0;
        }

        private static double CalculateLoadBalance(List<AbsStageExecutionResult> results)
        {
            if (results.Count == 0)
            {
                return 1;
            }

            var durations = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToList();
            var mean = durations.Average();
            var variance = durations.Sum(d => Math.Pow(d - mean, 2)) / durations.Count;
            var stdDev = Math.Sqrt(variance);

            return mean > 0 ? Math.Max(0, 1 - (stdDev / mean)) : 1;
        }

        private static double CalculateSynchronizationOverhead(List<AbsStageExecutionResult> results)
        {
            if (results.Count <= 1)
            {
                return 0.0;
            }

            // Calculate synchronization overhead based on variance in execution times
            var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToList();
            var mean = executionTimes.Average();
            var variance = executionTimes.Sum(t => Math.Pow(t - mean, 2)) / executionTimes.Count;
            var stdDev = Math.Sqrt(variance);

            // Higher variance indicates more synchronization overhead
            var coefficientOfVariation = mean > 0 ? stdDev / mean : 0;

            // Convert to overhead percentage (clamped between 0% and 20%)
            return Math.Min(0.20, Math.Max(0.0, coefficientOfVariation * 0.1));
        }

        private static double GetThreadPoolUtilization()
        {
            var (activeWorkers, activeCompletionPorts, availableWorkers, availableCompletionPorts) =
                PerformanceMonitor.GetThreadPoolStats();

            var totalWorkers = activeWorkers + availableWorkers;
            var totalCompletionPorts = activeCompletionPorts + availableCompletionPorts;

            if (totalWorkers > 0)
            {
                var workerUtilization = (double)activeWorkers / totalWorkers;
                var completionPortUtilization = totalCompletionPorts > 0 ?
                    (double)activeCompletionPorts / totalCompletionPorts : 0;

                // Average of worker and completion port utilization
                return (workerUtilization + completionPortUtilization) / 2.0;
            }

            return 0.0;
        }

        private static double CalculateActualParallelism(List<AbsStageExecutionResult> results, TimeSpan totalDuration)
        {
            if (results.Count == 0 || totalDuration.TotalMilliseconds == 0)
            {
                return 0.0;
            }

            // Calculate the sum of all stage durations
            var totalStageDuration = results.Sum(r => r.ExecutionTime.TotalMilliseconds);

            // Actual parallelism is the ratio of total work time to wall clock time
            var actualParallelism = totalStageDuration / totalDuration.TotalMilliseconds;

            // This gives us the average number of stages running in parallel
            return Math.Min(results.Count, actualParallelism);
        }

        /// <summary>
        /// Executes custom synchronization strategies for parallel stages.
        /// </summary>
        private async Task ExecuteCustomSynchronizationAsync(PipelineExecutionContext context, List<AbsStageExecutionResult> results, CancellationToken cancellationToken)
        {
            // Implementation of custom synchronization patterns
            var customStrategy = DetermineCustomStrategy(context);

            switch (customStrategy)
            {
                case SynchronizationStrategyEnum.BarrierSync:
                    await ExecuteBarrierSynchronizationAsync(context, results, cancellationToken);
                    break;

                case SynchronizationStrategyEnum.ProducerConsumer:
                    await ExecuteProducerConsumerPatternAsync(context, results, cancellationToken);
                    break;

                case SynchronizationStrategyEnum.WorkStealing:
                    await ExecuteWorkStealingPatternAsync(context, results, cancellationToken);
                    break;

                default:
                    // Fallback to WaitAll if no custom strategy is determined
                    var tasks = _parallelStages.Select(stage => ExecuteStageAsync(stage, context, cancellationToken));
                    var stageResults = await Task.WhenAll(tasks);
                    results.AddRange(stageResults);
                    break;
            }
        }

        private SynchronizationStrategyEnum DetermineCustomStrategy(PipelineExecutionContext context)
        {
            // Analyze context and metadata to determine optimal synchronization strategy
            if (_parallelStages.Count > 4)
            {
                return SynchronizationStrategyEnum.WorkStealing;
            }
            else if (_parallelStages.Any(s => s.Type == PipelineStageType.Custom))
            {
                return SynchronizationStrategyEnum.ProducerConsumer;
            }
            else if (_hasBarrier)
            {
                return SynchronizationStrategyEnum.BarrierSync;
            }

            return SynchronizationStrategyEnum.Default;
        }

        private async Task ExecuteBarrierSynchronizationAsync(PipelineExecutionContext context, List<AbsStageExecutionResult> results, CancellationToken cancellationToken)
        {
            using var barrier = new Barrier(_parallelStages.Count);
            var tasks = new List<Task<AbsStageExecutionResult>>();

            foreach (var stage in _parallelStages)
            {
                var task = Task.Run(async () =>
                {
                    var result = await ExecuteStageAsync(stage, context, cancellationToken);
                    barrier.SignalAndWait(cancellationToken);
                    return result;
                }, cancellationToken);

                tasks.Add(task);
            }

            var stageResults = await Task.WhenAll(tasks);
            results.AddRange(stageResults);
        }

        private async Task ExecuteProducerConsumerPatternAsync(PipelineExecutionContext context, List<AbsStageExecutionResult> results, CancellationToken cancellationToken)
        {
            var channel = global::System.Threading.Channels.Channel.CreateUnbounded<object>();
            var writer = channel.Writer;
            var reader = channel.Reader;

            // Start producer stages
            var producers = _parallelStages.Where(s => s.Type != PipelineStageType.Custom).ToList();
            var consumers = _parallelStages.Where(s => s.Type == PipelineStageType.Custom).ToList();

            var producerTasks = producers.Select(async stage =>
            {
                var result = await ExecuteStageAsync(stage, context, cancellationToken);
                if (result.OutputData != null)
                {
                    foreach (var output in result.OutputData)
                    {
                        await writer.WriteAsync(output, cancellationToken);
                    }
                }
                return result;
            });

            var consumerTasks = consumers.Select(async stage =>
            {
                await foreach (var item in reader.ReadAllAsync(cancellationToken))
                {
                    // Process consumed items
                }
                return await ExecuteStageAsync(stage, context, cancellationToken);
            });

            var allTasks = producerTasks.Concat(consumerTasks);
            var stageResults = await Task.WhenAll(allTasks);
            results.AddRange(stageResults);

            writer.Complete();
        }

        private async Task ExecuteWorkStealingPatternAsync(PipelineExecutionContext context, List<AbsStageExecutionResult> results, CancellationToken cancellationToken)
        {
            var workQueue = new global::System.Collections.Concurrent.ConcurrentQueue<IPipelineStage>(_parallelStages);
            var workerCount = Math.Min(_maxDegreeOfParallelism, Environment.ProcessorCount);
            var workers = new List<Task<List<AbsStageExecutionResult>>>();

            for (var i = 0; i < workerCount; i++)
            {
                var worker = Task.Run(async () =>
                {
                    var workerResults = new List<AbsStageExecutionResult>();

                    while (workQueue.TryDequeue(out var stage))
                    {
                        var result = await ExecuteStageAsync(stage, context, cancellationToken);
                        workerResults.Add(result);
                    }

                    return workerResults;
                }, cancellationToken);

                workers.Add(worker);
            }

            var allWorkerResults = await Task.WhenAll(workers);
            foreach (var workerResults in allWorkerResults)
            {
                results.AddRange(workerResults);
            }
        }
    }
}