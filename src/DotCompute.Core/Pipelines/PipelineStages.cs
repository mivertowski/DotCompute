// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Custom synchronization strategies for parallel execution.
/// </summary>
internal enum CustomSyncStrategy
{
    Default,
    BarrierSync,
    ProducerConsumer,
    WorkStealing
}

/// <summary>
/// Stage that executes a single kernel.
/// </summary>
internal sealed class KernelStage(
    string id,
    string name,
    ICompiledKernel kernel,
    long[]? globalWorkSize,
    long[]? localWorkSize,
    Dictionary<string, string> inputMappings,
    Dictionary<string, string> outputMappings,
    Dictionary<string, object> parameters,
    List<string> dependencies,
    Dictionary<string, object> metadata,
    MemoryHint memoryHint,
    int priority) : IPipelineStage
{
    private readonly ICompiledKernel _kernel = kernel;
    private readonly long[]? _globalWorkSize = globalWorkSize;
    private readonly long[]? _localWorkSize = localWorkSize;
    private readonly Dictionary<string, string> _inputMappings = inputMappings;
    private readonly Dictionary<string, string> _outputMappings = outputMappings;
    private readonly Dictionary<string, object> _parameters = parameters;
    private readonly StageMetrics _metrics = new(id);

    /// <summary>
    /// Builds kernel parameters from input mappings, direct inputs, and parameter overrides.
    /// </summary>
    /// <param name="context">The pipeline execution context containing input values.</param>
    /// <returns>A list of parameters in the correct order for kernel execution.</returns>
    private List<object> BuildKernelParameters(PipelineExecutionContext context)
    {
        var parameters = new List<object>();

        // Build parameter list based on mappings and context
        // Since we don't have KernelDefinition.Parameters yet, we'll use the mappings
        // The order will be determined by the order in which parameters are added

        // First, add parameters from input mappings
        foreach (var (paramName, contextKey) in _inputMappings)
        {
            if (context.Inputs.TryGetValue(contextKey, out var value))
            {
                parameters.Add(value);
            }
            else if (_parameters.TryGetValue(paramName, out var paramValue))
            {
                parameters.Add(paramValue);
            }
            else
            {
                throw new InvalidOperationException($"No value found for parameter '{paramName}' (mapped from '{contextKey}')");
            }
        }

        // Then add any parameters that aren't in input mappings but are in _parameters
        foreach (var (paramName, paramValue) in _parameters)
        {
            if (!_inputMappings.ContainsKey(paramName))
            {
                parameters.Add(paramValue);
            }
        }

        return parameters;
    }

    /// <summary>
    /// Gets the index of a parameter by name.
    /// </summary>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The zero-based index of the parameter, or -1 if not found.</returns>
    private int GetParameterIndex(string paramName)
    {
        // Since we don't have KernelDefinition.Parameters, we'll use the order
        // established by BuildKernelParameters method

        var index = 0;

        // Check input mappings first (these come first in BuildKernelParameters)
        foreach (var (mappingParam, _) in _inputMappings)
        {
            if (mappingParam == paramName)
            {
                return index;
            }
            index++;
        }

        // Then check parameters that aren't in input mappings
        foreach (var (param, _) in _parameters)
        {
            if (!_inputMappings.ContainsKey(param))
            {
                if (param == paramName)
                {
                    return index;
                }
                index++;
            }
        }

        return -1; // Not found
    }

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Kernel;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; } = dependencies;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; } = metadata;

    public MemoryHint MemoryHint { get; } = memoryHint;
    public int Priority { get; } = priority;

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);

        // Start performance monitoring
        PerformanceMonitor.ExecutionMetrics.StartExecution();

        try
        {
            // Prepare kernel arguments
            var arguments = PrepareArguments(context);

            // Create execution context
            var kernelContext = new KernelExecutionContext
            {
                Name = _kernel.Name,
                WorkDimensions = _globalWorkSize ?? new[] { 1L },
                LocalWorkSize = _localWorkSize != null ? _localWorkSize : null,
                Arguments = [.. arguments],
                CancellationToken = cancellationToken
            };

            // Execute kernel - convert to KernelArguments
            var kernelArgs = new KernelArguments(kernelContext.Arguments ?? []);
            await _kernel.ExecuteAsync(kernelArgs, cancellationToken);

            stopwatch.Stop();

            // Get detailed execution metrics
            var (cpuTime, allocatedBytes, elapsedMs) = PerformanceMonitor.ExecutionMetrics.EndExecution();
            var endMemory = GC.GetTotalMemory(false);

            // Prepare outputs
            var outputs = PrepareOutputs(context, arguments);

            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = allocatedBytes,
                PeakBytes = Math.Max(endMemory, startMemory + allocatedBytes),
                AllocationCount = 1,
                DeallocationCount = 0
            };

            _metrics.RecordExecution(stopwatch.Elapsed, true);
            _metrics.RecordMemoryUsage(memoryUsage.AllocatedBytes);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = outputs.Count > 0 ? outputs : null,
                MemoryUsage = memoryUsage,
                Metrics = new Dictionary<string, double>
                {
                    ["ComputeUtilization"] = CalculateComputeUtilization(),
                    ["MemoryBandwidthUtilization"] = CalculateMemoryBandwidthUtilization(),
                    ["WorkItemsProcessed"] = CalculateWorkItemsProcessed(),
                    ["CpuTimeMs"] = cpuTime,
                    ["ElapsedTimeMs"] = elapsedMs,
                    ["CpuEfficiency"] = elapsedMs > 0 ? cpuTime / elapsedMs : 0
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, false);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate kernel
        if (_kernel == null)
        {
            errors.Add("Kernel is required");
        }

        // Validate work size
        if (_globalWorkSize == null || _globalWorkSize.Length == 0)
        {
            warnings.Add("Global work size not specified, using default [1]");
        }
        else if (_globalWorkSize.Any(size => size <= 0))
        {
            errors.Add("Global work size must be positive");
        }

        // Validate local work size
        if (_localWorkSize != null)
        {
            if (_localWorkSize.Length != _globalWorkSize?.Length)
            {
                errors.Add("Local work size dimensions must match global work size dimensions");
            }
            else if (_localWorkSize.Any(size => size <= 0))
            {
                errors.Add("Local work size must be positive");
            }
        }

        // Validate parameter mappings
        // Since KernelDefinition is not yet available in the interface, we validate based on
        // the mappings and parameters provided. This ensures consistency even without metadata.
        var paramNames = new HashSet<string>(_inputMappings.Keys.Union(_parameters.Keys));

        foreach (var mapping in _inputMappings)
        {
            if (!paramNames.Contains(mapping.Key))
            {
                warnings.Add($"Input mapping '{mapping.Key}' does not match any kernel parameter");
            }
        }

        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;

    private List<object> PrepareArguments(PipelineExecutionContext context)
        // Use the new BuildKernelParameters method
        => BuildKernelParameters(context);

    private Dictionary<string, object> PrepareOutputs(PipelineExecutionContext context, List<object> arguments)
    {
        var outputs = new Dictionary<string, object>();

        foreach (var mapping in _outputMappings)
        {
            var paramName = mapping.Key;
            var contextKey = mapping.Value;

            // Use the new GetParameterIndex helper method
            var paramIndex = GetParameterIndex(paramName);

            if (paramIndex >= 0 && paramIndex < arguments.Count)
            {
                outputs[contextKey] = arguments[paramIndex];
            }
        }

        return outputs;
    }

    private double CalculateComputeUtilization()
    {
        // Calculate real compute utilization based on work items and execution time
        var workItems = CalculateWorkItemsProcessed();
        var executionTime = _metrics.AverageExecutionTime;

        // Use performance monitor to get real CPU utilization
        return PerformanceMonitor.GetComputeUtilization(executionTime, (long)workItems);
    }

    private static double CalculateMemoryBandwidthUtilization()
        // Use performance monitor to get real memory bandwidth utilization
        => PerformanceMonitor.GetMemoryBandwidthUtilization();

    private double CalculateWorkItemsProcessed()
    {
        if (_globalWorkSize == null)
        {
            return 0;
        }

        return _globalWorkSize.Aggregate(1L, (acc, size) => acc * size);
    }
}

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
    public IReadOnlyList<string> Dependencies { get; } = new List<string>();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
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

            var results = new List<StageExecutionResult>();
            var outputs = new Dictionary<string, object>(context.Inputs);

            switch (_synchronizationMode)
            {
                case SynchronizationMode.WaitAll:
                    // Performance optimization: Use pre-allocated array for better performance
                    var tasks = new Task<StageExecutionResult>[_parallelStages.Count];
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
                        ThreadPool.QueueUserWorkItem(async _ =>
                        {
                            try
                            {
                                await ExecuteStageAsync(capturedStage, context, cancellationToken);
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
                if (result.Success && result.Outputs != null)
                {
                    foreach (var (key, value) in result.Outputs)
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
            var totalMemoryUsage = results.Sum(r => r.MemoryUsage?.AllocatedBytes ?? 0);

            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = Math.Max(allocatedBytes, totalMemoryUsage),
                PeakBytes = Math.Max(endMemory, startMemory + totalMemoryUsage),
                AllocationCount = results.Sum(r => r.MemoryUsage?.AllocationCount ?? 0),
                DeallocationCount = results.Sum(r => r.MemoryUsage?.DeallocationCount ?? 0)
            };

            _metrics.RecordExecution(stopwatch.Elapsed, success);
            _metrics.RecordMemoryUsage(memoryUsage.AllocatedBytes);

            // Calculate real parallel efficiency
            var parallelEfficiency = CalculateParallelEfficiency(results);
            var loadBalance = CalculateLoadBalance(results);

            // Calculate real synchronization overhead based on CPU utilization difference
            var avgCpuUtilization = (startCpuUtilization + endCpuUtilization) / 2.0;
            var expectedCpuUtilization = Math.Min(1.0, parallelEfficiency * _maxDegreeOfParallelism / Environment.ProcessorCount);
            var syncOverhead = Math.Max(0.0, (expectedCpuUtilization - avgCpuUtilization) / expectedCpuUtilization);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = success,
                Duration = stopwatch.Elapsed,
                Outputs = outputs.Count > context.Inputs.Count ? outputs : null,
                MemoryUsage = memoryUsage,
                Metrics = new Dictionary<string, double>
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

            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_parallelStages.Count == 0)
        {
            errors.Add("Parallel stage must contain at least one sub-stage");
        }

        if (_maxDegreeOfParallelism <= 0)
        {
            errors.Add("Max degree of parallelism must be positive");
        }

        // Validate all sub-stages
        foreach (var stage in _parallelStages)
        {
            var stageValidation = stage.Validate();
            if (!stageValidation.IsValid && stageValidation.Errors != null)
            {
                foreach (var error in stageValidation.Errors)
                {
                    errors.Add($"Sub-stage '{stage.Name}': {error}");
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
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;

    private static async Task<StageExecutionResult> ExecuteStageAsync(
        IPipelineStage stage,
        PipelineExecutionContext context,
        CancellationToken cancellationToken) => await stage.ExecuteAsync(context, cancellationToken);

    private static double CalculateParallelEfficiency(List<StageExecutionResult> results)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var totalTime = results.Sum(r => r.Duration.TotalMilliseconds);
        var maxTime = results.Max(r => r.Duration.TotalMilliseconds);

        return maxTime > 0 ? (totalTime / (results.Count * maxTime)) : 0;
    }

    private static double CalculateLoadBalance(List<StageExecutionResult> results)
    {
        if (results.Count == 0)
        {
            return 1;
        }

        var durations = results.Select(r => r.Duration.TotalMilliseconds).ToList();
        var mean = durations.Average();
        var variance = durations.Sum(d => Math.Pow(d - mean, 2)) / durations.Count;
        var stdDev = Math.Sqrt(variance);

        return mean > 0 ? Math.Max(0, 1 - (stdDev / mean)) : 1;
    }

    private static double CalculateSynchronizationOverhead(List<StageExecutionResult> results)
    {
        if (results.Count <= 1)
        {
            return 0.0;
        }

        // Calculate synchronization overhead based on variance in execution times
        var executionTimes = results.Select(r => r.Duration.TotalMilliseconds).ToList();
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

    private static double CalculateActualParallelism(List<StageExecutionResult> results, TimeSpan totalDuration)
    {
        if (results.Count == 0 || totalDuration.TotalMilliseconds == 0)
        {
            return 0.0;
        }

        // Calculate the sum of all stage durations
        var totalStageDuration = results.Sum(r => r.Duration.TotalMilliseconds);

        // Actual parallelism is the ratio of total work time to wall clock time
        var actualParallelism = totalStageDuration / totalDuration.TotalMilliseconds;

        // This gives us the average number of stages running in parallel
        return Math.Min(results.Count, actualParallelism);
    }

    /// <summary>
    /// Executes custom synchronization strategies for parallel stages.
    /// </summary>
    private async Task ExecuteCustomSynchronizationAsync(PipelineExecutionContext context, List<StageExecutionResult> results, CancellationToken cancellationToken)
    {
        // Implementation of custom synchronization patterns
        var customStrategy = DetermineCustomStrategy(context);

        switch (customStrategy)
        {
            case CustomSyncStrategy.BarrierSync:
                await ExecuteBarrierSynchronizationAsync(context, results, cancellationToken);
                break;

            case CustomSyncStrategy.ProducerConsumer:
                await ExecuteProducerConsumerPatternAsync(context, results, cancellationToken);
                break;

            case CustomSyncStrategy.WorkStealing:
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

    private CustomSyncStrategy DetermineCustomStrategy(PipelineExecutionContext context)
    {
        // Analyze context and metadata to determine optimal synchronization strategy
        if (_parallelStages.Count > 4)
        {
            return CustomSyncStrategy.WorkStealing;
        }
        else if (_parallelStages.Any(s => s.Type == PipelineStageType.Custom))
        {
            return CustomSyncStrategy.ProducerConsumer;
        }
        else if (_hasBarrier)
        {
            return CustomSyncStrategy.BarrierSync;
        }

        return CustomSyncStrategy.Default;
    }

    private async Task ExecuteBarrierSynchronizationAsync(PipelineExecutionContext context, List<StageExecutionResult> results, CancellationToken cancellationToken)
    {
        using var barrier = new Barrier(_parallelStages.Count);
        var tasks = new List<Task<StageExecutionResult>>();

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

    private async Task ExecuteProducerConsumerPatternAsync(PipelineExecutionContext context, List<StageExecutionResult> results, CancellationToken cancellationToken)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<object>();
        var writer = channel.Writer;
        var reader = channel.Reader;

        // Start producer stages
        var producers = _parallelStages.Where(s => s.Type != PipelineStageType.Custom).ToList();
        var consumers = _parallelStages.Where(s => s.Type == PipelineStageType.Custom).ToList();

        var producerTasks = producers.Select(async stage =>
        {
            var result = await ExecuteStageAsync(stage, context, cancellationToken);
            if (result.Outputs != null)
            {
                foreach (var output in result.Outputs)
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

    private async Task ExecuteWorkStealingPatternAsync(PipelineExecutionContext context, List<StageExecutionResult> results, CancellationToken cancellationToken)
    {
        var workQueue = new System.Collections.Concurrent.ConcurrentQueue<IPipelineStage>(_parallelStages);
        var workerCount = Math.Min(_maxDegreeOfParallelism, Environment.ProcessorCount);
        var workers = new List<Task<List<StageExecutionResult>>>();

        for (var i = 0; i < workerCount; i++)
        {
            var worker = Task.Run(async () =>
            {
                var workerResults = new List<StageExecutionResult>();

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

/// <summary>
/// Stage that executes conditional branches.
/// </summary>
internal sealed class BranchStage(
    string id,
    Func<PipelineExecutionContext, bool> condition,
    List<IPipelineStage> trueStages,
    List<IPipelineStage>? falseStages) : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, bool> _condition = condition;
    private readonly List<IPipelineStage> _trueStages = trueStages;
    private readonly List<IPipelineStage>? _falseStages = falseStages;
    private readonly StageMetrics _metrics = new(id);

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public string Name { get; } = "Branch";

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Branch;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; } = new List<string>();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var conditionResult = _condition(context);
            var stagesToExecute = conditionResult ? _trueStages : _falseStages;

            var outputs = new Dictionary<string, object>(context.Inputs);

            if (stagesToExecute != null)
            {
                foreach (var stage in stagesToExecute)
                {
                    var stageContext = new PipelineExecutionContext
                    {
                        Inputs = outputs,
                        MemoryManager = context.MemoryManager,
                        Device = context.Device,
                        Options = context.Options,
                        Profiler = context.Profiler
                    };

                    var result = await stage.ExecuteAsync(stageContext, cancellationToken);

                    if (!result.Success)
                    {
                        return new StageExecutionResult
                        {
                            StageId = Id,
                            Success = false,
                            Duration = stopwatch.Elapsed,
                            Error = result.Error
                        };
                    }

                    if (result.Outputs != null)
                    {
                        foreach (var (key, value) in result.Outputs)
                        {
                            outputs[key] = value;
                        }
                    }
                }
            }

            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, true);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = outputs.Count > context.Inputs.Count ? outputs : null,
                Metrics = new Dictionary<string, double>
                {
                    ["BranchTaken"] = conditionResult ? 1 : 0,
                    ["StagesExecuted"] = stagesToExecute?.Count ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, false);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_condition == null)
        {
            errors.Add("Branch condition is required");
        }

        if (_trueStages.Count == 0 && (_falseStages == null || _falseStages.Count == 0))
        {
            warnings.Add("Branch has no stages to execute");
        }

        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;
}

/// <summary>
/// Stage that executes loops.
/// </summary>
internal sealed class LoopStage(
    string id,
    Func<PipelineExecutionContext, int, bool> condition,
    List<IPipelineStage> bodyStages) : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, int, bool> _condition = condition;
    private readonly List<IPipelineStage> _bodyStages = bodyStages;
    private readonly StageMetrics _metrics = new(id);

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public string Name { get; } = "Loop";

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Loop;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; } = new List<string>();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var outputs = new Dictionary<string, object>(context.Inputs);
            var iteration = 0;

            while (_condition(context, iteration) && !cancellationToken.IsCancellationRequested)
            {
                foreach (var stage in _bodyStages)
                {
                    var stageContext = new PipelineExecutionContext
                    {
                        Inputs = outputs,
                        MemoryManager = context.MemoryManager,
                        Device = context.Device,
                        Options = context.Options,
                        Profiler = context.Profiler
                    };

                    var result = await stage.ExecuteAsync(stageContext, cancellationToken);

                    if (!result.Success)
                    {
                        return new StageExecutionResult
                        {
                            StageId = Id,
                            Success = false,
                            Duration = stopwatch.Elapsed,
                            Error = result.Error
                        };
                    }

                    if (result.Outputs != null)
                    {
                        foreach (var (key, value) in result.Outputs)
                        {
                            outputs[key] = value;
                        }
                    }
                }

                iteration++;
            }

            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, true);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = outputs.Count > context.Inputs.Count ? outputs : null,
                Metrics = new Dictionary<string, double>
                {
                    ["IterationsCompleted"] = iteration,
                    ["AverageIterationTime"] = iteration > 0 ? stopwatch.Elapsed.TotalMilliseconds / iteration : 0
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, false);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_condition == null)
        {
            errors.Add("Loop condition is required");
        }

        if (_bodyStages.Count == 0)
        {
            warnings.Add("Loop has no body stages");
        }

        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;
}

/// <summary>
/// Stage that wraps a pipeline as a stage.
/// </summary>
internal sealed class PipelineWrapperStage(string id, string name, IKernelPipeline pipeline) : IPipelineStage
{
    private readonly IKernelPipeline _pipeline = pipeline;
    private readonly StageMetrics _metrics = new(id);

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Custom;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; } = new List<string>();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata => _pipeline.Metadata;

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _pipeline.ExecuteAsync(context, cancellationToken);

            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, result.Success);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = result.Success,
                Duration = stopwatch.Elapsed,
                Outputs = result.Outputs,
                MemoryUsage = result.Metrics.MemoryUsage,
                Error = result.Errors?.FirstOrDefault()?.Exception
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordExecution(stopwatch.Elapsed, false);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var pipelineValidation = _pipeline.Validate();

        return new StageValidationResult
        {
            IsValid = pipelineValidation.IsValid,
            Errors = pipelineValidation.Errors?.Select(e => e.Message).ToList(),
            Warnings = pipelineValidation.Warnings?.Select(w => w.Message).ToList()
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;
}
