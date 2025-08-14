using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Aot;
using DotCompute.Core.Pipelines;
using DotCompute.Tests.Shared.Kernels;
using FluentAssertions;

namespace DotCompute.Tests.Shared.Pipelines;

/// <summary>
/// Test implementation of a kernel pipeline stage.
/// </summary>
public class TestKernelStage : IPipelineStage
{
    private readonly ICompiledKernel _kernel;
    private readonly Dictionary<string, object> _metadata;
    private readonly Dictionary<string, string> _inputMappings;
    private readonly Dictionary<string, string> _outputMappings;
    private readonly Dictionary<string, object> _constantParameters;
    private readonly TestStageMetrics _metrics;
    private KernelConfiguration? _configuration;
    private int _priority;

    public TestKernelStage(string name, ICompiledKernel kernel)
    {
        Id = $"kernel_{name}_{Guid.NewGuid():N}";
        Name = name;
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _metadata = new Dictionary<string, object>();
        _inputMappings = new Dictionary<string, string>();
        _outputMappings = new Dictionary<string, string>();
        _constantParameters = new Dictionary<string, object>();
        _metrics = new TestStageMetrics(Name);
        Dependencies = Array.Empty<string>();
        Type = PipelineStageType.Kernel;
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type { get; }
    public IReadOnlyList<string> Dependencies { get; private set; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public void Configure(Action<TestKernelStageBuilder> configure)
    {
        var builder = new TestKernelStageBuilder(this);
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
                    ["ThreadsExecuted"] = _configuration?.GridDimensions.X * _configuration?.BlockDimensions.X ?? 1
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
        
        return new KernelArguments(args.ToArray());
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

    public IStageMetrics GetMetrics() => _metrics;

    internal void SetDependencies(string[] dependencies)
    {
        Dependencies = dependencies;
    }

    internal void SetConfiguration(KernelConfiguration configuration)
    {
        _configuration = configuration;
    }

    internal void AddInputMapping(string parameterName, string contextKey)
    {
        _inputMappings[parameterName] = contextKey;
    }

    internal void AddOutputMapping(string parameterName, string contextKey)
    {
        _outputMappings[parameterName] = contextKey;
    }

    internal void SetParameter<T>(string parameterName, T value)
    {
        _constantParameters[parameterName] = value!;
    }

    internal void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    internal void SetPriority(int priority)
    {
        _priority = priority;
    }
}

/// <summary>
/// Builder for configuring kernel stages.
/// </summary>
public class TestKernelStageBuilder : IKernelStageBuilder
{
    private readonly TestKernelStage _stage;
    private readonly List<string> _dependencies;
    private long[]? _globalWorkSize;
    private long[]? _localWorkSize;

    public TestKernelStageBuilder(TestKernelStage stage)
    {
        _stage = stage;
        _dependencies = new List<string>();
    }

    public IKernelStageBuilder WithName(string name)
    {
        // Name is set in constructor
        return this;
    }

    public IKernelStageBuilder WithWorkSize(params long[] globalWorkSize)
    {
        _globalWorkSize = globalWorkSize;
        UpdateConfiguration();
        return this;
    }

    public IKernelStageBuilder WithLocalWorkSize(params long[] localWorkSize)
    {
        _localWorkSize = localWorkSize;
        UpdateConfiguration();
        return this;
    }

    public IKernelStageBuilder MapInput(string parameterName, string contextKey)
    {
        _stage.AddInputMapping(parameterName, contextKey);
        return this;
    }

    public IKernelStageBuilder MapOutput(string parameterName, string contextKey)
    {
        _stage.AddOutputMapping(parameterName, contextKey);
        return this;
    }

    public IKernelStageBuilder SetParameter<T>(string parameterName, T value)
    {
        _stage.SetParameter(parameterName, value);
        return this;
    }

    public IKernelStageBuilder DependsOn(string stageId)
    {
        _dependencies.Add(stageId);
        _stage.SetDependencies(_dependencies.ToArray());
        return this;
    }

    public IKernelStageBuilder WithMetadata(string key, object value)
    {
        _stage.AddMetadata(key, value);
        return this;
    }

    public IKernelStageBuilder WithMemoryHint(MemoryHint hint)
    {
        _stage.AddMetadata("MemoryHint", hint);
        return this;
    }

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
public class TestStageMetrics : IStageMetrics
{
    private readonly string _stageName;
    private readonly object _lock = new();
    private long _executionCount;
    private long _errorCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.Zero;
    private long _totalMemoryUsage;
    private readonly Dictionary<string, double> _customMetrics;

    public TestStageMetrics(string stageName)
    {
        _stageName = stageName;
        _customMetrics = new Dictionary<string, double>();
    }

    public long ExecutionCount => _executionCount;
    
    public TimeSpan AverageExecutionTime => 
        _executionCount > 0 ? TimeSpan.FromMilliseconds(_totalExecutionTime.TotalMilliseconds / _executionCount) : TimeSpan.Zero;
    
    public TimeSpan MinExecutionTime => 
        _minExecutionTime == TimeSpan.MaxValue ? TimeSpan.Zero : _minExecutionTime;
    
    public TimeSpan MaxExecutionTime => _maxExecutionTime;
    
    public TimeSpan TotalExecutionTime => _totalExecutionTime;
    
    public long ErrorCount => _errorCount;
    
    public double SuccessRate => 
        _executionCount > 0 ? (double)(_executionCount - _errorCount) / _executionCount * 100 : 0;
    
    public long AverageMemoryUsage => 
        _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;
    
    public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

    public void RecordExecutionStart()
    {
        lock (_lock)
        {
            _executionCount++;
        }
    }

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

    public void RecordExecutionError(TimeSpan duration)
    {
        lock (_lock)
        {
            _errorCount++;
            _totalExecutionTime = _totalExecutionTime.Add(duration);
        }
    }

    public void RecordCustomMetric(string name, double value)
    {
        lock (_lock)
        {
            _customMetrics[name] = value;
        }
    }
}
