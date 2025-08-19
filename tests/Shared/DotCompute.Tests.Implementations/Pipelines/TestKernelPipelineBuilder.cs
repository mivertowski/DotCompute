using DotCompute.Abstractions;
using DotCompute.Core.Pipelines;

namespace DotCompute.Tests.Implementations.Pipelines;


/// <summary>
/// Test implementation of a kernel pipeline builder.
/// </summary>
public sealed class TestKernelPipelineBuilder : IKernelPipelineBuilder
{
    private readonly TestKernelPipeline _pipeline;
    private readonly List<string> _stageOrder;

    public TestKernelPipelineBuilder()
    {
        _pipeline = new TestKernelPipeline("TestPipeline");
        _stageOrder = [];
    }

    public IKernelPipelineBuilder WithName(string name)
    {
        // Name is set in constructor, but we can update it via reflection or property if needed
        // For simplicity, we'll store it as metadata
        _pipeline.AddMetadata("CustomName", name);
        return this;
    }

    public IKernelPipelineBuilder AddKernel(
        string name,
        ICompiledKernel kernel,
        Action<IKernelStageBuilder>? configure = null)
    {
        var stage = new TestKernelStage(name, kernel);

        if (configure != null)
        {
            var builder = new TestKernelStageBuilder(stage);
            configure(builder);
        }

        _pipeline.AddStage(stage);
        _stageOrder.Add(stage.Id);

        return this;
    }

    public IKernelPipelineBuilder AddParallel(Action<IParallelStageBuilder> configure)
    {
        var stage = new TestParallelStage($"Parallel_{_stageOrder.Count}");
        var builder = new TestParallelStageBuilder(stage);
        configure(builder);

        _pipeline.AddStage(stage);
        _stageOrder.Add(stage.Id);

        return this;
    }

    public IKernelPipelineBuilder AddBranch(
        Func<PipelineExecutionContext, bool> condition,
        Action<IKernelPipelineBuilder> trueBranch,
        Action<IKernelPipelineBuilder>? falseBranch = null)
    {
        IPipelineStage? trueStage = null;
        IPipelineStage? falseStage = null;

        if (trueBranch != null)
        {
            var trueBuilder = new TestKernelPipelineBuilder();
            trueBranch(trueBuilder);
            var truePipeline = trueBuilder.Build();

            trueStage = new TestCustomStage(
                "TrueBranch",
                async (ctx, ct) =>
                {
                    var result = await truePipeline.ExecuteAsync(ctx, ct);
                    return new StageExecutionResult
                    {
                        StageId = truePipeline.Id,
                        Success = result.Success,
                        Outputs = result.Outputs,
                        Duration = result.Metrics.Duration,
                        Error = result.Errors?.Count > 0 ? result.Errors[0].Exception : null
                    };
                });
        }

        if (falseBranch != null)
        {
            var falseBuilder = new TestKernelPipelineBuilder();
            falseBranch(falseBuilder);
            var falsePipeline = falseBuilder.Build();

            falseStage = new TestCustomStage(
                "FalseBranch",
                async (ctx, ct) =>
                {
                    var result = await falsePipeline.ExecuteAsync(ctx, ct);
                    return new StageExecutionResult
                    {
                        StageId = falsePipeline.Id,
                        Success = result.Success,
                        Outputs = result.Outputs,
                        Duration = result.Metrics.Duration,
                        Error = result.Errors?.Count > 0 ? result.Errors[0].Exception : null
                    };
                });
        }

        var branchStage = new TestBranchStage(
            $"Branch_{_stageOrder.Count}",
            condition,
            trueStage,
            falseStage);

        _pipeline.AddStage(branchStage);
        _stageOrder.Add(branchStage.Id);

        return this;
    }

    public IKernelPipelineBuilder AddLoop(
        Func<PipelineExecutionContext, int, bool> condition,
        Action<IKernelPipelineBuilder> body)
    {
        var bodyBuilder = new TestKernelPipelineBuilder();
        body(bodyBuilder);
        var bodyPipeline = bodyBuilder.Build();

        var bodyStage = new TestCustomStage(
            "LoopBody",
            async (ctx, ct) =>
            {
                var result = await bodyPipeline.ExecuteAsync(ctx, ct);
                return new StageExecutionResult
                {
                    StageId = bodyPipeline.Id,
                    Success = result.Success,
                    Outputs = result.Outputs,
                    Duration = result.Metrics.Duration,
                    Error = result.Errors?.Count > 0 ? result.Errors[0].Exception : null
                };
            });

        var loopStage = new TestLoopStage(
            $"Loop_{_stageOrder.Count}",
            condition,
            bodyStage);

        _pipeline.AddStage(loopStage);
        _stageOrder.Add(loopStage.Id);

        return this;
    }

    public IKernelPipelineBuilder AddStage(IPipelineStage stage)
    {
        _pipeline.AddStage(stage);
        _stageOrder.Add(stage.Id);
        return this;
    }

    public IKernelPipelineBuilder WithMetadata(string key, object value)
    {
        _pipeline.AddMetadata(key, value);
        return this;
    }

    public IKernelPipelineBuilder WithOptimization(Action<PipelineOptimizationSettings> configure)
    {
        configure(_pipeline.OptimizationSettings);
        return this;
    }

    public IKernelPipelineBuilder WithErrorHandler(Func<Exception, PipelineExecutionContext, ErrorHandlingResult> handler)
    {
        _pipeline.SetErrorHandler(handler);
        return this;
    }

    public IKernelPipelineBuilder WithEventHandler(Action<PipelineEvent> handler)
    {
        _pipeline.SetEventHandler(handler);
        return this;
    }

    public IKernelPipeline Build()
    {
        // Set up dependencies based on order(unless already specified)
        for (var i = 1; i < _stageOrder.Count; i++)
        {
            var currentStageId = _stageOrder[i];
            var previousStageId = _stageOrder[i - 1];

            var currentStage = _pipeline.Stages.FirstOrDefault(s => s.Id == currentStageId);

            // Only add dependency if stage doesn't already have dependencies
            if (currentStage != null && currentStage.Dependencies.Count == 0)
            {
                if (currentStage is TestKernelStage kernelStage)
                {
                    kernelStage.SetDependencies([previousStageId]);
                }
                else if (currentStage is TestParallelStage parallelStage)
                {
                    parallelStage.SetDependencies([previousStageId]);
                }
                else if (currentStage is TestCustomStage customStage)
                {
                    customStage.SetDependencies(previousStageId);
                }
                else if (currentStage is TestBranchStage branchStage)
                {
                    branchStage.SetDependencies(previousStageId);
                }
                else if (currentStage is TestLoopStage loopStage)
                {
                    loopStage.SetDependencies(previousStageId);
                }
            }
        }

        return _pipeline;
    }
}

/// <summary>
/// Test implementation of pipeline metrics.
/// </summary>
public sealed class TestPipelineMetrics(string pipelineId) : IPipelineMetrics
{
    private readonly string _pipelineId = pipelineId;
    private readonly Lock _lock = new();
    private long _executionCount;
    private long _successCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.Zero;
    private long _totalMemoryUsage;
    private long _peakMemoryUsage;
    private readonly Dictionary<string, double> _customMetrics = [];
    private readonly List<TimeSeriesMetric> _timeSeries = [];

    public string PipelineId => _pipelineId;

    public long ExecutionCount => _executionCount;

    public long SuccessfulExecutionCount => _successCount;

    public long FailedExecutionCount => _executionCount - _successCount;

    public TimeSpan AverageExecutionTime
        => _executionCount > 0 ? TimeSpan.FromMilliseconds(_totalExecutionTime.TotalMilliseconds / _executionCount) : TimeSpan.Zero;

    public TimeSpan MinExecutionTime
        => _minExecutionTime == TimeSpan.MaxValue ? TimeSpan.Zero : _minExecutionTime;

    public TimeSpan MaxExecutionTime => _maxExecutionTime;

    public TimeSpan TotalExecutionTime => _totalExecutionTime;

    public double Throughput
        => _totalExecutionTime.TotalSeconds > 0 ? _executionCount / _totalExecutionTime.TotalSeconds : 0;

    public double SuccessRate
        => _executionCount > 0 ? (double)_successCount / _executionCount * 100 : 0;

    public long AverageMemoryUsage
        => _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

    public long PeakMemoryUsage => _peakMemoryUsage;

    public IReadOnlyDictionary<string, IStageMetrics> StageMetrics { get; } = new Dictionary<string, IStageMetrics>();

    public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

    public IReadOnlyList<TimeSeriesMetric> TimeSeries => _timeSeries.AsReadOnly();

    public void Reset()
    {
        lock (_lock)
        {
            _executionCount = 0;
            _successCount = 0;
            _totalExecutionTime = TimeSpan.Zero;
            _minExecutionTime = TimeSpan.MaxValue;
            _maxExecutionTime = TimeSpan.Zero;
            _totalMemoryUsage = 0;
            _peakMemoryUsage = 0;
            _customMetrics.Clear();
            _timeSeries.Clear();
        }
    }

    public string Export(MetricsExportFormat format)
    {
        lock (_lock)
        {
            return format switch
            {
                MetricsExportFormat.Json => $"{{\"pipelineId\":\"{_pipelineId}\",\"executionCount\":{_executionCount},\"successRate\":{SuccessRate}}}",
                MetricsExportFormat.Csv => $"PipelineId,ExecutionCount,SuccessRate\n{_pipelineId},{_executionCount},{SuccessRate}",
                MetricsExportFormat.Prometheus => $"pipeline_executions_total{{pipeline=\"{_pipelineId}\"}} {_executionCount}\npipeline_success_rate{{pipeline=\"{_pipelineId}\"}} {SuccessRate}",
                _ => string.Empty
            };
        }
    }

    public void RecordExecutionStart()
    {
        lock (_lock)
        {
            _executionCount++;
        }
    }

    public void RecordExecutionComplete(TimeSpan duration, bool success)
    {
        lock (_lock)
        {
            if (success)
                _successCount++;

            _totalExecutionTime = _totalExecutionTime.Add(duration);

            if (duration < _minExecutionTime)
                _minExecutionTime = duration;

            if (duration > _maxExecutionTime)
                _maxExecutionTime = duration;

            var memoryUsage = GC.GetTotalMemory(false);
            _totalMemoryUsage += memoryUsage;
            if (memoryUsage > _peakMemoryUsage)
                _peakMemoryUsage = memoryUsage;

            _timeSeries.Add(new TimeSeriesMetric
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "ExecutionTime",
                Value = duration.TotalMilliseconds
            });
        }
    }
}

/// <summary>
/// Test pipeline execution metrics.
/// </summary>
public sealed class TestPipelineExecutionMetrics
{
    public required TimeSpan TotalDuration { get; init; }
    public required IReadOnlyDictionary<string, IStageMetrics> StageMetrics { get; init; }
    public required long MemoryUsage { get; init; }
    public required double ThroughputMBps { get; init; }
}



/// <summary>
/// Test implementation of IPipelineProfiler.
/// </summary>
public interface IPipelineProfiler
{
    public void StartStage(string stageId);
    public void EndStage(string stageId);
    public void RecordMetric(string name, double value);
}
