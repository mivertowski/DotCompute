// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Stages;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Builder for creating kernel pipelines with a fluent API.
    /// </summary>
    public sealed class KernelPipelineBuilder : IKernelPipelineBuilder
    {
        private readonly List<IPipelineStage> _stages;
        private readonly Dictionary<string, object> _metadata;
        private readonly List<Action<PipelineEvent>> _eventHandlers;
        private string _name;
        private readonly PipelineOptimizationSettings _optimizationSettings;
        private Func<Exception, PipelineExecutionContext, ErrorHandlingResult>? _errorHandler;

        /// <summary>
        /// Initializes a new instance of the KernelPipelineBuilder class.
        /// </summary>
        public KernelPipelineBuilder()
        {
            _stages = [];
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
        public IKernelPipelineBuilder WithEventHandler(Action<PipelineEvent> handler)
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
                [.. _eventHandlers],
                _errorHandler);
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

        private long[]? _globalWorkSize;
        private long[]? _localWorkSize;
        private MemoryHint _memoryHint = MemoryHint.None;
        private int _priority;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IKernelStageBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

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
