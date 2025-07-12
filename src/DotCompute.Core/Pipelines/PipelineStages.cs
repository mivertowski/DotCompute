using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Stage that executes a single kernel.
/// </summary>
internal sealed class KernelStage : IPipelineStage
{
    private readonly ICompiledKernel _kernel;
    private readonly long[]? _globalWorkSize;
    private readonly long[]? _localWorkSize;
    private readonly Dictionary<string, string> _inputMappings;
    private readonly Dictionary<string, string> _outputMappings;
    private readonly Dictionary<string, object> _parameters;
    private readonly StageMetrics _metrics;

    public KernelStage(
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
        int priority)
    {
        Id = id;
        Name = name;
        _kernel = kernel;
        _globalWorkSize = globalWorkSize;
        _localWorkSize = localWorkSize;
        _inputMappings = inputMappings;
        _outputMappings = outputMappings;
        _parameters = parameters;
        Dependencies = dependencies;
        Metadata = metadata;
        MemoryHint = memoryHint;
        Priority = priority;
        _metrics = new StageMetrics(id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Kernel;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public MemoryHint MemoryHint { get; }
    public int Priority { get; }

    /// <inheritdoc/>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);

        try
        {
            // Prepare kernel arguments
            var arguments = PrepareArguments(context);

            // Create execution context
            var kernelContext = new KernelExecutionContext
            {
                GlobalWorkSize = _globalWorkSize ?? new[] { 1L },
                LocalWorkSize = _localWorkSize != null ? _localWorkSize : null,
                Arguments = arguments,
                Options = context.Options.EnableProfiling 
                    ? KernelExecutionOption.EnableProfiling 
                    : KernelExecutionOption.None
            };

            // Execute kernel
            await _kernel.ExecuteAsync(kernelContext, cancellationToken);

            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);

            // Prepare outputs
            var outputs = PrepareOutputs(context, arguments);

            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = Math.Max(0, endMemory - startMemory),
                PeakBytes = endMemory,
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
                    ["WorkItemsProcessed"] = CalculateWorkItemsProcessed()
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
        var kernelParams = _kernel?.Definition.Parameters ?? new List<KernelParameter>();
        var paramNames = kernelParams.Select(p => p.Name).ToHashSet();

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
    {
        var arguments = new List<object>();
        var kernelParams = _kernel.Definition.Parameters;

        foreach (var param in kernelParams)
        {
            object? value = null;

            // Check parameter overrides first
            if (_parameters.TryGetValue(param.Name, out var paramValue))
            {
                value = paramValue;
            }
            // Check input mappings
            else if (_inputMappings.TryGetValue(param.Name, out var contextKey))
            {
                if (context.Inputs.TryGetValue(contextKey, out var contextValue))
                {
                    value = contextValue;
                }
            }
            // Check direct context values
            else if (context.Inputs.TryGetValue(param.Name, out var directValue))
            {
                value = directValue;
            }

            if (value == null && !param.IsConstant)
            {
                throw new InvalidOperationException($"No value provided for required parameter '{param.Name}'");
            }

            arguments.Add(value!);
        }

        return arguments;
    }

    private Dictionary<string, object> PrepareOutputs(PipelineExecutionContext context, List<object> arguments)
    {
        var outputs = new Dictionary<string, object>();

        foreach (var mapping in _outputMappings)
        {
            var paramName = mapping.Key;
            var contextKey = mapping.Value;

            // Find parameter index
            var paramIndex = _kernel.Definition.Parameters
                .Select((p, i) => new { Parameter = p, Index = i })
                .FirstOrDefault(x => x.Parameter.Name == paramName)?.Index;

            if (paramIndex.HasValue && paramIndex.Value < arguments.Count)
            {
                outputs[contextKey] = arguments[paramIndex.Value];
            }
        }

        return outputs;
    }

    private static double CalculateComputeUtilization()
    {
        // This would typically query the device for actual utilization
        // For now, return a simulated value
        return 0.85; // 85% utilization
    }

    private static double CalculateMemoryBandwidthUtilization()
    {
        // This would typically query the device for memory bandwidth usage
        // For now, return a simulated value
        return 0.70; // 70% bandwidth utilization
    }

    private double CalculateWorkItemsProcessed()
    {
        if (_globalWorkSize == null)
            return 0;

        return _globalWorkSize.Aggregate(1L, (acc, size) => acc * size);
    }
}

/// <summary>
/// Stage that executes multiple stages in parallel.
/// </summary>
internal sealed class ParallelStage : IPipelineStage
{
    private readonly List<IPipelineStage> _parallelStages;
    private readonly int _maxDegreeOfParallelism;
    private readonly SynchronizationMode _synchronizationMode;
    private readonly bool _hasBarrier;
    private readonly StageMetrics _metrics;

    public ParallelStage(
        string id,
        string name,
        List<IPipelineStage> parallelStages,
        int maxDegreeOfParallelism,
        SynchronizationMode synchronizationMode,
        bool hasBarrier)
    {
        Id = id;
        Name = name;
        _parallelStages = parallelStages;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _synchronizationMode = synchronizationMode;
        _hasBarrier = hasBarrier;
        _metrics = new StageMetrics(id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

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
                    var tasks = _parallelStages.Select(stage => ExecuteStageAsync(stage, context, cancellationToken));
                    var stageResults = await Task.WhenAll(tasks);
                    results.AddRange(stageResults);
                    break;

                case SynchronizationMode.WaitAny:
                    var anyTask = Task.WhenAny(_parallelStages.Select(stage => ExecuteStageAsync(stage, context, cancellationToken)));
                    var completedResult = await await anyTask;
                    results.Add(completedResult);
                    break;

                case SynchronizationMode.FireAndForget:
                    // Start all tasks but don't wait
                    foreach (var stage in _parallelStages)
                    {
                        _ = Task.Run(async () => await ExecuteStageAsync(stage, context, cancellationToken), cancellationToken);
                    }
                    break;

                case SynchronizationMode.Custom:
                    // Custom synchronization would be implemented by derived classes
                    throw new NotImplementedException("Custom synchronization not implemented");
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
            var endMemory = GC.GetTotalMemory(false);

            var success = results.All(r => r.Success);
            var totalMemoryUsage = results.Sum(r => r.MemoryUsage?.AllocatedBytes ?? 0);

            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = totalMemoryUsage,
                PeakBytes = Math.Max(endMemory, startMemory + totalMemoryUsage),
                AllocationCount = results.Sum(r => r.MemoryUsage?.AllocationCount ?? 0),
                DeallocationCount = results.Sum(r => r.MemoryUsage?.DeallocationCount ?? 0)
            };

            _metrics.RecordExecution(stopwatch.Elapsed, success);
            _metrics.RecordMemoryUsage(memoryUsage.AllocatedBytes);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = success,
                Duration = stopwatch.Elapsed,
                Outputs = outputs.Count > context.Inputs.Count ? outputs : null,
                MemoryUsage = memoryUsage,
                Metrics = new Dictionary<string, double>
                {
                    ["ParallelEfficiency"] = CalculateParallelEfficiency(results),
                    ["LoadBalance"] = CalculateLoadBalance(results),
                    ["SynchronizationOverhead"] = CalculateSynchronizationOverhead(results)
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
        CancellationToken cancellationToken)
    {
        return await stage.ExecuteAsync(context, cancellationToken);
    }

    private static double CalculateParallelEfficiency(List<StageExecutionResult> results)
    {
        if (results.Count == 0)
            return 0;

        var totalTime = results.Sum(r => r.Duration.TotalMilliseconds);
        var maxTime = results.Max(r => r.Duration.TotalMilliseconds);
        
        return maxTime > 0 ? (totalTime / (results.Count * maxTime)) : 0;
    }

    private static double CalculateLoadBalance(List<StageExecutionResult> results)
    {
        if (results.Count == 0)
            return 1;

        var durations = results.Select(r => r.Duration.TotalMilliseconds).ToList();
        var mean = durations.Average();
        var variance = durations.Sum(d => Math.Pow(d - mean, 2)) / durations.Count;
        var stdDev = Math.Sqrt(variance);
        
        return mean > 0 ? Math.Max(0, 1 - (stdDev / mean)) : 1;
    }

    private static double CalculateSynchronizationOverhead(List<StageExecutionResult> results)
    {
        // This would calculate the overhead of synchronization
        // For now, return a placeholder value
        return 0.05; // 5% overhead
    }
}

/// <summary>
/// Stage that executes conditional branches.
/// </summary>
internal sealed class BranchStage : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, bool> _condition;
    private readonly List<IPipelineStage> _trueStages;
    private readonly List<IPipelineStage>? _falseStages;
    private readonly StageMetrics _metrics;

    public BranchStage(
        string id,
        Func<PipelineExecutionContext, bool> condition,
        List<IPipelineStage> trueStages,
        List<IPipelineStage>? falseStages)
    {
        Id = id;
        Name = "Branch";
        _condition = condition;
        _trueStages = trueStages;
        _falseStages = falseStages;
        _metrics = new StageMetrics(id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

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
internal sealed class LoopStage : IPipelineStage
{
    private readonly Func<PipelineExecutionContext, int, bool> _condition;
    private readonly List<IPipelineStage> _bodyStages;
    private readonly StageMetrics _metrics;

    public LoopStage(
        string id,
        Func<PipelineExecutionContext, int, bool> condition,
        List<IPipelineStage> bodyStages)
    {
        Id = id;
        Name = "Loop";
        _condition = condition;
        _bodyStages = bodyStages;
        _metrics = new StageMetrics(id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

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
internal sealed class PipelineWrapperStage : IPipelineStage
{
    private readonly IKernelPipeline _pipeline;
    private readonly StageMetrics _metrics;

    public PipelineWrapperStage(string id, string name, IKernelPipeline pipeline)
    {
        Id = id;
        Name = name;
        _pipeline = pipeline;
        _metrics = new StageMetrics(id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

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