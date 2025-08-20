using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Core.Pipelines;

namespace DotCompute.Tests.Implementations.Pipelines;


/// <summary>
/// Test implementation of a kernel pipeline stage.
/// </summary>
public sealed class TestKernelStage : IPipelineStage
{
    private readonly ICompiledKernel _kernel;
    private readonly Dictionary<string, object> _metadata;
    private readonly Dictionary<string, string> _inputMappings;
    private readonly Dictionary<string, string> _outputMappings;
    private readonly Dictionary<string, object> _constantParameters;
    private readonly TestStageMetrics _metrics;
    private KernelConfiguration? _configuration;
    private int _priority;


    /// <summary>
    /// Initializes a new instance of the <see cref="TestKernelStage"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="kernel">The kernel.</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    public TestKernelStage(string name, ICompiledKernel kernel)
    {
        Id = $"kernel_{name}_{Guid.NewGuid():N}";
        Name = name;
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
        _metadata = [];
        _inputMappings = [];
        _outputMappings = [];
        _constantParameters = [];
        _metrics = new TestStageMetrics(Name);
        Dependencies = [];
        Type = PipelineStageType.Kernel;
    }

    /// <summary>
    /// Gets the stage identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the stage name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the stage type.
    /// </summary>
    public PipelineStageType Type { get; }

    /// <summary>
    /// Gets the stage dependencies.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; private set; }

    /// <summary>
    /// Gets the stage metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>
    /// Configures the specified configure.
    /// </summary>
    /// <param name="configure">The configure.</param>
    public void Configure(Action<TestKernelStageBuilder> configure)
    {
        var builder = new TestKernelStageBuilder(this);
        configure(builder);
    }

    /// <summary>
    /// Executes the stage.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);

        try
        {
            _metrics.RecordExecutionStart();

            // Prepare kernel arguments from context
            var arguments = PrepareArguments(context);

            // Execute kernel
            await _kernel.ExecuteAsync(arguments, cancellationToken);

            // Store outputs back to context
            var outputs = StoreOutputs(context);

            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);

            var memoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = Math.Max(0, endMemory - startMemory),
                PeakBytes = endMemory,
                AllocationCount = 1,
                DeallocationCount = 0
            };

            _metrics.RecordExecutionSuccess(stopwatch.Elapsed, endMemory - startMemory);

            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Outputs = outputs,
                Duration = stopwatch.Elapsed,
                MemoryUsage = memoryUsage,
                Metrics = new Dictionary<string, double>
                {
                    ["ExecutionTimeMs"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["MemoryUsedBytes"] = endMemory - startMemory,
                    ["ThreadsExecuted"] = CalculateThreadCount(_configuration)
                }
            };
        }
        catch (Exception ex)
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

    private KernelArguments PrepareArguments(PipelineExecutionContext context)
    {
        var args = new List<object>();

        // Add mapped inputs
        foreach (var (paramName, contextKey) in _inputMappings)
        {
            if (context.State.TryGetValue(contextKey, out var value) ||
                context.Inputs.TryGetValue(contextKey, out value))
            {
                args.Add(value);
            }
            else
            {
                throw new InvalidOperationException($"Input '{contextKey}' not found in context for parameter '{paramName}'");
            }
        }

        // Add constant parameters
        foreach (var (_, value) in _constantParameters)
        {
            args.Add(value);
        }

        return new KernelArguments([.. args]);
    }

    private Dictionary<string, object> StoreOutputs(PipelineExecutionContext context)
    {
        var outputs = new Dictionary<string, object>();

        // Store outputs to context state
        foreach (var (paramName, contextKey) in _outputMappings)
        {
            // In a real implementation, this would extract specific outputs from the kernel result
            var outputValue = $"output_{paramName}_{Random.Shared.Next(1000, 9999)}";
            context.State[contextKey] = outputValue;
            outputs[contextKey] = outputValue;
        }

        return outputs;
    }

    private static double CalculateThreadCount(KernelConfiguration? configuration)
    {
        if (configuration == null)
            return 1;

        var gridDim = (Dim3)(configuration.Options.TryGetValue("GridDimension", out var grid) ? grid : new Dim3(1));
        var blockDim = (Dim3)(configuration.Options.TryGetValue("BlockDimension", out var block) ? block : new Dim3(256));
        
        return gridDim.X * blockDim.X;
    }

    /// <summary>
    /// Validates the stage configuration.
    /// </summary>
    /// <returns></returns>
    public StageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_kernel == null)
        {
            errors.Add("Kernel is not set");
        }

        if (string.IsNullOrEmpty(Name))
        {
            errors.Add("Stage name is required");
        }

        if (_inputMappings.Count == 0 && _constantParameters.Count == 0)
        {
            warnings.Add("No input parameters configured");
        }

        if (_outputMappings.Count == 0)
        {
            warnings.Add("No output mappings configured");
        }

        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    /// <summary>
    /// Gets performance metrics for this stage.
    /// </summary>
    /// <returns></returns>
    public IStageMetrics GetMetrics() => _metrics;

    internal void SetDependencies(string[] dependencies) => Dependencies = dependencies;

    internal void SetConfiguration(KernelConfiguration configuration) => _configuration = configuration;

    internal void AddInputMapping(string parameterName, string contextKey) => _inputMappings[parameterName] = contextKey;

    internal void AddOutputMapping(string parameterName, string contextKey) => _outputMappings[parameterName] = contextKey;

    internal void SetParameter<T>(string parameterName, T value) => _constantParameters[parameterName] = value!;

    internal void AddMetadata(string key, object value) => _metadata[key] = value;

    internal void SetPriority(int priority) => _priority = priority;
}

/// <summary>
/// Builder for configuring kernel stages.
/// </summary>
public sealed class TestKernelStageBuilder(TestKernelStage stage) : IKernelStageBuilder
{
    private readonly TestKernelStage _stage = stage;
    private readonly List<string> _dependencies = [];
    private long[]? _globalWorkSize;
    private long[]? _localWorkSize;

    /// <summary>
    /// Sets the stage name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithName(string name)
        // Name is set in constructor
        => this;

    /// <summary>
    /// Sets the work size for the kernel.
    /// </summary>
    /// <param name="globalWorkSize"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithWorkSize(params long[] globalWorkSize)
    {
        _globalWorkSize = globalWorkSize;
        UpdateConfiguration();
        return this;
    }

    /// <summary>
    /// Sets the local work size for the kernel.
    /// </summary>
    /// <param name="localWorkSize"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithLocalWorkSize(params long[] localWorkSize)
    {
        _localWorkSize = localWorkSize;
        UpdateConfiguration();
        return this;
    }

    /// <summary>
    /// Maps an input from the pipeline context.
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="contextKey"></param>
    /// <returns></returns>
    public IKernelStageBuilder MapInput(string parameterName, string contextKey)
    {
        _stage.AddInputMapping(parameterName, contextKey);
        return this;
    }

    /// <summary>
    /// Maps an output to the pipeline context.
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="contextKey"></param>
    /// <returns></returns>
    public IKernelStageBuilder MapOutput(string parameterName, string contextKey)
    {
        _stage.AddOutputMapping(parameterName, contextKey);
        return this;
    }

    /// <summary>
    /// Sets a constant parameter value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="parameterName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public IKernelStageBuilder SetParameter<T>(string parameterName, T value)
    {
        _stage.SetParameter(parameterName, value);
        return this;
    }

    /// <summary>
    /// Adds a dependency on another stage.
    /// </summary>
    /// <param name="stageId"></param>
    /// <returns></returns>
    public IKernelStageBuilder DependsOn(string stageId)
    {
        _dependencies.Add(stageId);
        _stage.SetDependencies([.. _dependencies]);
        return this;
    }

    /// <summary>
    /// Adds metadata to the stage.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithMetadata(string key, object value)
    {
        _stage.AddMetadata(key, value);
        return this;
    }

    /// <summary>
    /// Sets memory allocation hints.
    /// </summary>
    /// <param name="hint"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithMemoryHint(MemoryHint hint)
    {
        _stage.AddMetadata("MemoryHint", hint);
        return this;
    }

    /// <summary>
    /// Sets execution priority.
    /// </summary>
    /// <param name="priority"></param>
    /// <returns></returns>
    public IKernelStageBuilder WithPriority(int priority)
    {
        _stage.SetPriority(priority);
        return this;
    }

    private void UpdateConfiguration()
    {
        if (_globalWorkSize != null)
        {
            var gridDim = new Dim3(
                _globalWorkSize.Length > 0 ? (int)_globalWorkSize[0] : 1,
                _globalWorkSize.Length > 1 ? (int)_globalWorkSize[1] : 1,
                _globalWorkSize.Length > 2 ? (int)_globalWorkSize[2] : 1);

            var blockDim = new Dim3(
                _localWorkSize?.Length > 0 ? (int)_localWorkSize[0] : 1,
                _localWorkSize?.Length > 1 ? (int)_localWorkSize[1] : 1,
                _localWorkSize?.Length > 2 ? (int)_localWorkSize[2] : 1);

            _stage.SetConfiguration(new KernelConfiguration(gridDim, blockDim));
        }
    }
}

/// <summary>
/// Test implementation of stage metrics.
/// </summary>
#pragma warning disable CS9113 // Parameter is unread
public sealed class TestStageMetrics(string stageName) : IStageMetrics
#pragma warning restore CS9113
{
    private readonly Lock _lock = new();
    private long _executionCount;
    private long _errorCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.Zero;
    private long _totalMemoryUsage;
    private readonly Dictionary<string, double> _customMetrics = [];

    /// <summary>
    /// Gets the total execution count.
    /// </summary>
    public long ExecutionCount => _executionCount;

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime
        => _executionCount > 0 ? TimeSpan.FromMilliseconds(_totalExecutionTime.TotalMilliseconds / _executionCount) : TimeSpan.Zero;

    /// <summary>
    /// Gets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime
        => _minExecutionTime == TimeSpan.MaxValue ? TimeSpan.Zero : _minExecutionTime;

    /// <summary>
    /// Gets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime => _maxExecutionTime;

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime => _totalExecutionTime;

    /// <summary>
    /// Gets the error count.
    /// </summary>
    public long ErrorCount => _errorCount;

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate
        => _executionCount > 0 ? (double)(_executionCount - _errorCount) / _executionCount * 100 : 0;

    /// <summary>
    /// Gets average memory usage.
    /// </summary>
    public long AverageMemoryUsage
        => _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

    public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

    /// <summary>
    /// Records the execution start.
    /// </summary>
    public void RecordExecutionStart()
    {
        lock (_lock)
        {
            _executionCount++;
        }
    }

    /// <summary>
    /// Records the execution success.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <param name="memoryUsed">The memory used.</param>
    public void RecordExecutionSuccess(TimeSpan duration, long memoryUsed)
    {
        lock (_lock)
        {
            _totalExecutionTime = _totalExecutionTime.Add(duration);
            _totalMemoryUsage += memoryUsed;

            if (duration < _minExecutionTime)
                _minExecutionTime = duration;

            if (duration > _maxExecutionTime)
                _maxExecutionTime = duration;
        }
    }

    /// <summary>
    /// Records the execution error.
    /// </summary>
    /// <param name="duration">The duration.</param>
    public void RecordExecutionError(TimeSpan duration)
    {
        lock (_lock)
        {
            _errorCount++;
            _totalExecutionTime = _totalExecutionTime.Add(duration);
        }
    }

    /// <summary>
    /// Records the custom metric.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    public void RecordCustomMetric(string name, double value)
    {
        lock (_lock)
        {
            _customMetrics[name] = value;
        }
    }
}
