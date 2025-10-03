// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Stages;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

// Type aliases to resolve ambiguous references
using AbstractionsPipelineEvent = DotCompute.Abstractions.Pipelines.Enums.PipelineEvent;
using CorePipelineEvent = DotCompute.Core.Pipelines.Types.PipelineEvent;
using ErrorHandlingResult = DotCompute.Abstractions.Pipelines.Models.ErrorHandlingResult;
using PipelineOptimizationSettings = DotCompute.Abstractions.Pipelines.Models.PipelineOptimizationSettings;
using SynchronizationMode = DotCompute.Abstractions.Types.SynchronizationMode;
using IKernelPipeline = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline;
using PipelineExecutionContext = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext;
using MemoryHint = DotCompute.Abstractions.Pipelines.Enums.MemoryHint;
using IKernelPipelineBuilder = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipelineBuilder;
using AbstractionsPipelineEventType = DotCompute.Abstractions.Pipelines.Enums.PipelineEventType;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Builder for creating kernel pipelines with a fluent API.
    /// Implements both Core and Abstractions interfaces for maximum compatibility.
    /// </summary>
    public sealed class KernelPipelineBuilder : IKernelPipelineBuilder
    {
        private readonly List<IPipelineStage> _stages;
        private readonly List<IPipelineStage> _kernelStages;
        private readonly Dictionary<string, object> _metadata;
        private readonly List<Action<AbstractionsPipelineEvent>> _eventHandlers;
        private string _name;
        private readonly PipelineOptimizationSettings _optimizationSettings;
        private Func<Exception, PipelineExecutionContext, ErrorHandlingResult>? _errorHandler;

        /// <summary>
        /// Initializes a new instance of the KernelPipelineBuilder class.
        /// </summary>
        public KernelPipelineBuilder()
        {
            _stages = [];
            _kernelStages = [];
            _metadata = [];
            _eventHandlers = [];
            _name = $"Pipeline_{Guid.NewGuid():N}";
            _optimizationSettings = new PipelineOptimizationSettings();
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder WithName(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder AddKernel(
            string name,
            ICompiledKernel kernel,
            Action<IKernelStageBuilder>? configure = null)
        {
            var stageBuilder = new KernelStageBuilder(name, kernel);
            configure?.Invoke(stageBuilder);

            var stage = stageBuilder.Build();
            _stages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder AddParallel(Action<IParallelStageBuilder> configure)
        {
            var parallelBuilder = new ParallelStageBuilder();
            configure(parallelBuilder);

            var stage = parallelBuilder.Build();
            _stages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder AddBranch(
            Func<PipelineExecutionContext, bool> condition,
            Action<IKernelPipelineBuilder> trueBranch,
            Action<IKernelPipelineBuilder>? falseBranch = null)
        {
            var trueBuilder = new KernelPipelineBuilder();
            trueBranch(trueBuilder);

            var falseBuilder = falseBranch != null ? new KernelPipelineBuilder() : null;
            falseBranch?.Invoke(falseBuilder!);

            var stage = new BranchStage(
                $"Branch_{Guid.NewGuid():N}",
                condition,
                trueBuilder._stages,
                falseBuilder?._stages);

            _stages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder AddLoop(
            Func<PipelineExecutionContext, int, bool> condition,
            Action<IKernelPipelineBuilder> body)
        {
            var bodyBuilder = new KernelPipelineBuilder();
            body(bodyBuilder);

            var stage = new LoopStage(
                $"Loop_{Guid.NewGuid():N}",
                condition,
                bodyBuilder._stages);

            _stages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder AddStage(IPipelineStage stage)
        {
            _stages.Add(stage ?? throw new ArgumentNullException(nameof(stage)));
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder WithMetadata(string key, object value)
        {
            _metadata[key] = value;
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder WithOptimization(Action<PipelineOptimizationSettings> configure)
        {
            configure(_optimizationSettings);
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder WithErrorHandler(Func<Exception, PipelineExecutionContext, ErrorHandlingResult> handler)
        {
            _errorHandler = handler;
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipelineBuilder WithEventHandler(Action<AbstractionsPipelineEvent> handler)
        {
            _eventHandlers.Add(handler);
            return this;
        }

        /// <inheritdoc/>
        public IKernelPipeline Build()
        {
            var id = Guid.NewGuid().ToString();

            return new KernelPipeline(
                id,
                _name,
                [.. _stages],
                _optimizationSettings,
                new Dictionary<string, object>(_metadata),
                [.. ConvertEventHandlers()],
                _errorHandler);
        }

        // Note: Additional static factory methods for LINQ integration would go here
        // but are omitted in this minimal implementation to focus on compilation success

        /// <summary>
        /// Converts Abstractions PipelineEvent handlers to Core PipelineEvent handlers.
        /// </summary>
        private Action<CorePipelineEvent>[] ConvertEventHandlers()
        {
            return _eventHandlers.Select(handler =>
                new Action<CorePipelineEvent>(coreEvent =>
                {
                    // Convert Core event type to Abstractions enum value
                    var eventType = ConvertEventType(coreEvent.Type);
                    handler(eventType);
                })).ToArray();
        }

        /// <summary>
        /// Converts Core PipelineEventType to Abstractions PipelineEvent enum.
        /// </summary>
        private static AbstractionsPipelineEvent ConvertEventType(AbstractionsPipelineEventType coreType)
        {
            return coreType switch
            {
                AbstractionsPipelineEventType.Started => AbstractionsPipelineEvent.PipelineStarted,
                AbstractionsPipelineEventType.Completed => AbstractionsPipelineEvent.PipelineCompleted,
                AbstractionsPipelineEventType.Failed => AbstractionsPipelineEvent.PipelineFailed,
                AbstractionsPipelineEventType.StageStarted => AbstractionsPipelineEvent.StageStarted,
                AbstractionsPipelineEventType.StageCompleted => AbstractionsPipelineEvent.StageCompleted,
                AbstractionsPipelineEventType.StageFailed => AbstractionsPipelineEvent.StageFailed,
                _ => AbstractionsPipelineEvent.CustomEvent
            };
        }

        /// <summary>
        /// Creates a new pipeline builder.
        /// </summary>
        public static IKernelPipelineBuilder Create() => new KernelPipelineBuilder();
    }

    /// <summary>
    /// Builder for kernel stages.
    /// </summary>
    internal sealed class KernelStageBuilder(string name, ICompiledKernel kernel) : IKernelStageBuilder
    {
        private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
        private readonly ICompiledKernel _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        private readonly List<string> _dependencies = [];
        private readonly Dictionary<string, object> _metadata = [];
        private readonly Dictionary<string, string> _inputMappings = [];
        private readonly Dictionary<string, string> _outputMappings = [];
        private readonly Dictionary<string, object> _parameters = [];
        private readonly List<MemoryHint> _memoryHints = [];

        private long[]? _globalWorkSize;
        private long[]? _localWorkSize;
        private MemoryHint _memoryHint = MemoryHint.None;
        private int _priority;
        private string? _preferredBackend;
        private BackendFallbackStrategy _fallbackStrategy = BackendFallbackStrategy.Auto;
        private TimeSpan? _timeout;
        private int _maxRetries = 0;
        private RetryStrategy _retryStrategy = RetryStrategy.ExponentialBackoff;
        private string? _cacheKey;
        private TimeSpan? _cacheTtl;
        private ExecutionPriority _executionPriority = ExecutionPriority.Normal;
        private Func<object[], ValidationResult>? _inputValidator;
        private Func<object, object>? _outputTransformer;
        private bool _profilingEnabled = false;
        private readonly List<string> _customMetrics = [];
        private ResourceRequirements? _resourceRequirements;
        private Func<Exception, ErrorHandlingResult>? _errorHandler;
        private Func<PipelineExecutionMetrics, bool>? _conditionalExecution;
        private PipelineStageType _stageType = PipelineStageType.Computation;

        /// <summary>
        /// Sets the name for this stage. Note: Name is set in constructor and cannot be changed.
        /// </summary>
        /// <param name="name">The stage name (ignored in this implementation)</param>
        /// <returns>The stage builder for fluent configuration</returns>
        public IKernelStageBuilder WithName(string name)
            // Name is set in constructor and cannot be changed
            => this;

        /// <inheritdoc/>
        public IKernelStageBuilder WithWorkSize(params long[] globalWorkSize)
        {
            _globalWorkSize = globalWorkSize ?? throw new ArgumentNullException(nameof(globalWorkSize));
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithLocalWorkSize(params long[] localWorkSize)
        {
            _localWorkSize = localWorkSize;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder MapInput(string parameterName, string contextKey)
        {
            _inputMappings[parameterName] = contextKey;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder MapOutput(string parameterName, string contextKey)
        {
            _outputMappings[parameterName] = contextKey;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder SetParameter<T>(string parameterName, T value)
        {
            _parameters[parameterName] = value!;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder DependsOn(string stageId)
        {
            _dependencies.Add(stageId);
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithMetadata(string key, object value)
        {
            _metadata[key] = value;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithMemoryHint(MemoryHint hint)
        {
            _memoryHint = hint;
            return this;
        }

        /// <summary>
        /// Sets the integer priority for this stage. Use WithPriority(ExecutionPriority) for enum-based priority.
        /// </summary>
        /// <param name="priority">Numeric priority value</param>
        /// <returns>The stage builder for fluent configuration</returns>
        public IKernelStageBuilder WithNumericPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithParameters(params object[] parameters)
        {
            _parameters.Clear();
            for (var i = 0; i < parameters.Length; i++)
            {
                _parameters[$"param{i}"] = parameters[i];
            }
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder PreferBackend(string backendName, BackendFallbackStrategy fallbackStrategy = BackendFallbackStrategy.Auto)
        {
            _preferredBackend = backendName;
            _fallbackStrategy = fallbackStrategy;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithMemoryHints(params MemoryHint[] hints)
        {
            _memoryHints.Clear();
            _memoryHints.AddRange(hints);
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithRetryPolicy(int maxRetries, RetryStrategy retryStrategy = RetryStrategy.ExponentialBackoff)
        {
            _maxRetries = maxRetries;
            _retryStrategy = retryStrategy;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithCaching(string cacheKey, TimeSpan? ttl = null)
        {
            _cacheKey = cacheKey;
            _cacheTtl = ttl;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithPriority(ExecutionPriority priority)
        {
            _executionPriority = priority;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithInputValidation(Func<object[], ValidationResult> validator)
        {
            _inputValidator = validator;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithOutputTransform(Func<object, object> transformer)
        {
            _outputTransformer = transformer;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithProfiling(bool enableProfiling = true, params string[] customMetrics)
        {
            _profilingEnabled = enableProfiling;
            _customMetrics.Clear();
            _customMetrics.AddRange(customMetrics);
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithDependencies(params string[] dependencies)
        {
            _dependencies.Clear();
            _dependencies.AddRange(dependencies);
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithResourceRequirements(ResourceRequirements requirements)
        {
            _resourceRequirements = requirements;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WithErrorHandler(Func<Exception, ErrorHandlingResult> errorHandler)
        {
            _errorHandler = errorHandler;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder WhenCondition(Func<PipelineExecutionMetrics, bool> condition)
        {
            _conditionalExecution = condition;
            return this;
        }

        /// <inheritdoc/>
        public IKernelStageBuilder OfType(PipelineStageType stageType)
        {
            _stageType = stageType;
            return this;
        }
        /// <summary>
        /// Gets build.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public IPipelineStage Build()
        {
            return new KernelStage(
                $"KernelStage_{Guid.NewGuid():N}",
                _name,
                _kernel,
                _globalWorkSize,
                _localWorkSize,
                new Dictionary<string, string>(_inputMappings),
                new Dictionary<string, string>(_outputMappings),
                new Dictionary<string, object>(_parameters),
                [.. _dependencies],
                new Dictionary<string, object>(_metadata),
                _memoryHint,
                _priority);
        }
    }

    /// <summary>
    /// Builder for parallel stages.
    /// </summary>
    internal sealed class ParallelStageBuilder : IParallelStageBuilder
    {
        private readonly List<IPipelineStage> _parallelStages;
        private int _maxDegreeOfParallelism = Environment.ProcessorCount;
        private SynchronizationMode _synchronizationMode = SynchronizationMode.WaitAll;
        private bool _hasBarrier;
        private LoadBalancingStrategy _loadBalancingStrategy = LoadBalancingStrategy.RoundRobin;
        private Func<IEnumerable<object>, object>? _resultAggregator;
        private ParallelErrorStrategy _errorStrategy = ParallelErrorStrategy.FailFast;
        private bool _continueOnError = false;
        private TimeSpan? _timeout;
        private MemorySharingMode _memorySharingMode = MemorySharingMode.None;
        private Func<object, IEnumerable<object>>? _workPartitioner;
        private ExecutionPriority _priority = ExecutionPriority.Normal;
        private readonly List<string> _barrierPoints = [];
        private ResourceAllocationStrategy _resourceAllocationStrategy = ResourceAllocationStrategy.FirstFit;
        private bool _enableDetailedMetrics = false;
        private readonly List<AffinityRule> _affinityRules = [];
        private AdaptationPolicy _adaptationPolicy = AdaptationPolicy.Conservative;
        /// <summary>
        /// Initializes a new instance of the ParallelStageBuilder class.
        /// </summary>

        public ParallelStageBuilder()
        {
            _parallelStages = [];
        }

        /// <inheritdoc/>
        public IParallelStageBuilder AddKernel(
            string name,
            ICompiledKernel kernel,
            Action<IKernelStageBuilder>? configure = null)
        {
            var stageBuilder = new KernelStageBuilder(name, kernel);
            configure?.Invoke(stageBuilder);

            var stage = stageBuilder.Build();
            _parallelStages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder AddKernel(string kernelName, Action<IKernelStageBuilder>? stageBuilder = null)
            // Create a kernel stage builder for the named kernel
            // Note: This method requires a kernel instance - placeholder implementation
            => throw new NotImplementedException("AddKernel without ICompiledKernel parameter is not implemented");

        /// <inheritdoc/>
        public IParallelStageBuilder AddKernels(IEnumerable<ParallelKernelConfig> kernelConfigs)
        {
            foreach (var config in kernelConfigs)
            {
                // Create kernel stage from configuration
                // Note: This method requires a kernel instance - placeholder implementation
                throw new NotImplementedException("AddKernels with configuration is not implemented");

                // Removed - unreachable code
            }
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithLoadBalancing(LoadBalancingStrategy strategy)
        {
            _loadBalancingStrategy = strategy;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithResultAggregation(Func<IEnumerable<object>, object> aggregator)
        {
            _resultAggregator = aggregator;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithErrorHandling(ParallelErrorStrategy strategy, bool continueOnError = false)
        {
            _errorStrategy = strategy;
            _continueOnError = continueOnError;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithMemorySharing(MemorySharingMode sharingMode)
        {
            _memorySharingMode = sharingMode;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithWorkPartitioning(Func<object, IEnumerable<object>> partitioner)
        {
            _workPartitioner = partitioner;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithPriority(ExecutionPriority priority)
        {
            _priority = priority;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithBarriers(params string[] barrierPoints)
        {
            _barrierPoints.Clear();
            _barrierPoints.AddRange(barrierPoints);
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithResourceAllocation(ResourceAllocationStrategy strategy)
        {
            _resourceAllocationStrategy = strategy;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithMonitoring(bool enableDetailedMetrics = true)
        {
            _enableDetailedMetrics = enableDetailedMetrics;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithAffinity(IEnumerable<AffinityRule> affinityRules)
        {
            _affinityRules.Clear();
            _affinityRules.AddRange(affinityRules);
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithAdaptiveExecution(AdaptationPolicy adaptationPolicy)
        {
            _adaptationPolicy = adaptationPolicy;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder AddPipeline(
            string name,
            Action<IKernelPipelineBuilder> configure)
        {
            var pipelineBuilder = new KernelPipelineBuilder();
            configure(pipelineBuilder);

            var pipeline = pipelineBuilder.Build();
            var stage = new PipelineWrapperStage(
                $"Pipeline_{Guid.NewGuid():N}",
                name,
                pipeline);

            _parallelStages.Add(stage);

            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithMaxDegreeOfParallelism(int maxDegree)
        {
            _maxDegreeOfParallelism = maxDegree;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithSynchronization(SynchronizationMode mode)
        {
            _synchronizationMode = mode;
            return this;
        }

        /// <inheritdoc/>
        public IParallelStageBuilder WithBarrier()
        {
            _hasBarrier = true;
            return this;
        }
        /// <summary>
        /// Gets build.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public IPipelineStage Build()
        {
            return new ParallelStage(
                $"Parallel_{Guid.NewGuid():N}",
                "Parallel Execution",
                [.. _parallelStages],
                _maxDegreeOfParallelism,
                _synchronizationMode,
                _hasBarrier);
        }
    }
}
