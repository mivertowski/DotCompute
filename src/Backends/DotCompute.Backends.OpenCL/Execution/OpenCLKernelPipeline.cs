// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotCompute.Backends.OpenCL.Memory;
using DotCompute.Backends.OpenCL.Profiling;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Production-grade multi-kernel execution pipeline with dependency resolution,
/// automatic optimization, and comprehensive profiling.
/// </summary>
/// <remarks>
/// <para>
/// The OpenCLKernelPipeline enables complex multi-stage compute workflows with:
/// </para>
/// <list type="bullet">
/// <item><description>Automatic dependency resolution via topological sorting</description></item>
/// <item><description>Parallel execution of independent stages</description></item>
/// <item><description>Intermediate buffer management and reuse</description></item>
/// <item><description>Event-based synchronization for optimal throughput</description></item>
/// <item><description>Pipeline-level profiling with stage breakdown</description></item>
/// <item><description>Cycle detection to prevent invalid pipelines</description></item>
/// <item><description>Automatic kernel fusion opportunities identification</description></item>
/// <item><description>Memory reuse optimization across stages</description></item>
/// </list>
/// <para>
/// Example use cases:
/// - Image processing pipelines (blur → sharpen → color correct)
/// - Machine learning inference (preprocess → inference → postprocess)
/// - Scientific simulations (initialize → iterate → analyze)
/// - Data processing workflows (load → transform → reduce)
/// </para>
/// </remarks>
public sealed class OpenCLKernelPipeline : IAsyncDisposable
{
    private readonly OpenCLKernelExecutionEngine _executor;
    private readonly OpenCLMemoryPoolManager _memoryPool;
    private readonly OpenCLProfiler _profiler;
    private readonly ILogger<OpenCLKernelPipeline> _logger;
    private readonly ConcurrentDictionary<Guid, Pipeline> _pipelines;
    private readonly SemaphoreSlim _pipelineLock;
    private bool _disposed;

    // Statistics tracking
    private long _totalPipelinesExecuted;
    private long _totalStagesExecuted;
    private long _totalFusionOpportunitiesDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelPipeline"/> class.
    /// </summary>
    /// <param name="executor">Kernel execution engine for running stages.</param>
    /// <param name="memoryPool">Memory pool manager for intermediate buffers.</param>
    /// <param name="profiler">Profiler for performance metrics.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public OpenCLKernelPipeline(
        OpenCLKernelExecutionEngine executor,
        OpenCLMemoryPoolManager memoryPool,
        OpenCLProfiler profiler,
        ILogger<OpenCLKernelPipeline> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _memoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _pipelines = new ConcurrentDictionary<Guid, Pipeline>();
        _pipelineLock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("Kernel pipeline manager initialized");
    }

    /// <summary>
    /// Creates a new pipeline builder for constructing multi-kernel workflows.
    /// </summary>
    /// <param name="name">Descriptive name for the pipeline.</param>
    /// <returns>A pipeline builder for configuring stages and dependencies.</returns>
    /// <exception cref="ArgumentException">Thrown if name is null or whitespace.</exception>
    public PipelineBuilder CreatePipeline(string name)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Pipeline name cannot be null or whitespace", nameof(name));
        }

        _logger.LogDebug("Creating new pipeline: {PipelineName}", name);
        return new PipelineBuilder(name, this);
    }

    /// <summary>
    /// Executes a configured pipeline with automatic dependency resolution and optimization.
    /// </summary>
    /// <param name="pipeline">The pipeline to execute.</param>
    /// <param name="inputs">Input buffers and parameters for the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Pipeline execution result containing outputs and profiling data.</returns>
    /// <exception cref="ArgumentNullException">Thrown if pipeline or inputs is null.</exception>
    /// <exception cref="OpenCLPipelineException">Thrown if pipeline validation or execution fails.</exception>
    public async Task<PipelineResult> ExecutePipelineAsync(
        Pipeline pipeline,
        Dictionary<string, object> inputs,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(inputs);

        var overallStopwatch = Stopwatch.StartNew();

        // Start profiling session for pipeline execution
        var profilingSession = await _profiler.BeginSessionAsync(
            $"Pipeline_{pipeline.Name}",
            cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. Validate pipeline structure
            _logger.LogDebug("Validating pipeline: {PipelineName}", pipeline.Name);
            ValidatePipeline(pipeline);

            // 2. Topological sort stages for execution order
            _logger.LogDebug("Resolving execution order for {StageCount} stages", pipeline.Stages.Count);
            var executionOrder = TopologicalSort(pipeline.Stages);

            // 3. Detect optimization opportunities
            var fusionOpportunities = DetectFusionOpportunities(executionOrder);
            if (fusionOpportunities.Count > 0)
            {
                _logger.LogInformation(
                    "Detected {Count} kernel fusion opportunities in pipeline '{PipelineName}'",
                    fusionOpportunities.Count, pipeline.Name);
                Interlocked.Add(ref _totalFusionOpportunitiesDetected, fusionOpportunities.Count);
            }

            // 4. Execute stages in resolved order
            var stageResults = new List<StageResult>();
            var intermediateData = new Dictionary<string, object>(inputs);
            var activeBuffers = new Dictionary<string, OpenCLMemoryPoolManager.PooledBufferHandle>();
            var stageEvents = new Dictionary<string, OpenCLEventHandle>();

            try
            {
                foreach (var stage in executionOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Collect dependency events for synchronization
                    var waitEvents = CollectDependencyEvents(stage, stageEvents);

                    _logger.LogDebug(
                        "Executing stage '{StageName}' with {DependencyCount} dependencies",
                        stage.Name, waitEvents.Count);

                    var stageResult = await ExecuteStageAsync(
                        stage,
                        intermediateData,
                        activeBuffers,
                        waitEvents.ToArray(),
                        profilingSession,
                        cancellationToken).ConfigureAwait(false);

                    stageResults.Add(stageResult);
                    stageEvents[stage.Name] = stageResult.ExecutionEvent;

                    // Store outputs for dependent stages
                    if (stageResult.Outputs != null)
                    {
                        foreach (var (key, value) in stageResult.Outputs)
                        {
                            intermediateData[$"{stage.Name}.{key}"] = value;
                        }
                    }

                    Interlocked.Increment(ref _totalStagesExecuted);
                }

                // 5. Extract final outputs
                var outputs = ExtractOutputs(intermediateData, pipeline.OutputNames);

                overallStopwatch.Stop();
                await _profiler.EndSessionAsync(profilingSession, cancellationToken).ConfigureAwait(false);

                var result = new PipelineResult
                {
                    PipelineName = pipeline.Name,
                    StageResults = stageResults,
                    Outputs = outputs,
                    TotalTime = overallStopwatch.Elapsed,
                    FusionOpportunities = fusionOpportunities,
                    ProfilingSession = profilingSession
                };

                Interlocked.Increment(ref _totalPipelinesExecuted);

                _logger.LogInformation(
                    "Pipeline '{PipelineName}' completed in {TotalMs:F2}ms ({StageCount} stages)",
                    pipeline.Name, result.TotalTime.TotalMilliseconds, stageResults.Count);

                return result;
            }
            finally
            {
                // Release all intermediate buffers
                foreach (var buffer in activeBuffers.Values)
                {
                    await buffer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Pipeline '{PipelineName}' execution failed after {ElapsedMs:F2}ms",
                pipeline.Name, overallStopwatch.Elapsed.TotalMilliseconds);

            // Complete profiling session with error information
            if (!profilingSession.IsFinalized)
            {
                profilingSession.Complete();
            }

            throw new OpenCLPipelineException(
                $"Pipeline '{pipeline.Name}' execution failed: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Executes a single pipeline stage with proper argument preparation and synchronization.
    /// </summary>
    private async Task<StageResult> ExecuteStageAsync(
        Stage stage,
        Dictionary<string, object> data,
        Dictionary<string, OpenCLMemoryPoolManager.PooledBufferHandle> activeBuffers,
        OpenCLEventHandle[] waitEvents,
        ProfilingSession profilingSession,
        CancellationToken cancellationToken)
    {
        var stageStopwatch = Stopwatch.StartNew();

        try
        {
            // Prepare kernel arguments from stage inputs
            var arguments = await PrepareStageArgumentsAsync(
                stage,
                data,
                activeBuffers,
                cancellationToken).ConfigureAwait(false);

            // Execute kernel with dependencies
            var executionResult = await _executor.ExecuteKernelAsync(
                stage.Kernel.Handle,
                stage.Config.GlobalSize,
                stage.Config.LocalSize,
                arguments,
                waitEvents,
                cancellationToken).ConfigureAwait(false);

            // Profile stage execution
            var profiledEvent = await _profiler.ProfileKernelExecutionAsync(
                stage.Kernel,
                executionResult.Event,
                metadata: new Dictionary<string, object>
                {
                    ["StageName"] = stage.Name,
                    ["GlobalSize"] = FormatNDRange(executionResult.GlobalSize),
                    ["LocalSize"] = FormatNDRange(executionResult.LocalSize),
                    ["WaitEvents"] = waitEvents.Length
                },
                cancellationToken).ConfigureAwait(false);

            profilingSession.RecordEvent(profiledEvent);

            stageStopwatch.Stop();

            _logger.LogDebug(
                "Stage '{StageName}' completed in {DurationMs:F3}ms",
                stage.Name, stageStopwatch.Elapsed.TotalMilliseconds);

            return new StageResult
            {
                StageName = stage.Name,
                Duration = stageStopwatch.Elapsed,
                ExecutionEvent = executionResult.Event,
                Outputs = ExtractStageOutputs(stage, activeBuffers)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Stage '{StageName}' execution failed after {ElapsedMs:F2}ms",
                stage.Name, stageStopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Prepares kernel arguments from stage configuration and available data.
    /// </summary>
    private async Task<KernelArguments> PrepareStageArgumentsAsync(
        Stage stage,
        Dictionary<string, object> data,
        Dictionary<string, OpenCLMemoryPoolManager.PooledBufferHandle> activeBuffers,
        CancellationToken cancellationToken)
    {
        var arguments = new KernelArguments();

        foreach (var argSpec in stage.Config.Arguments)
        {
            if (argSpec.IsBuffer)
            {
                // Resolve buffer from inputs or create new intermediate buffer
                var bufferHandle = await ResolveBufferAsync(
                    argSpec.Name,
                    argSpec.BufferSize ?? 0,
                    argSpec.BufferFlags ?? MemoryFlags.ReadWrite,
                    data,
                    activeBuffers,
                    cancellationToken).ConfigureAwait(false);

                arguments.AddBuffer(bufferHandle.Buffer.Handle.Handle);
            }
            else if (argSpec.IsLocalMemory)
            {
                arguments.AddLocalMemory((nuint)argSpec.LocalMemorySize);
            }
            else
            {
                // Scalar argument - resolve from data
                var scalarValue = ResolveScalarValue(argSpec.Name, data);
                AddScalarArgument(arguments, scalarValue);
            }
        }

        return arguments;
    }

    /// <summary>
    /// Resolves or allocates a buffer for a stage argument.
    /// </summary>
    private async Task<OpenCLMemoryPoolManager.PooledBufferHandle> ResolveBufferAsync(
        string name,
        ulong size,
        MemoryFlags flags,
        Dictionary<string, object> data,
        Dictionary<string, OpenCLMemoryPoolManager.PooledBufferHandle> activeBuffers,
        CancellationToken cancellationToken)
    {
        // Check if buffer already exists (output from previous stage or input)
        if (activeBuffers.TryGetValue(name, out var existingBuffer))
        {
            _logger.LogTrace("Reusing existing buffer: {BufferName}", name);
            return existingBuffer;
        }

        // Check if buffer is provided in inputs
        if (data.TryGetValue(name, out var inputValue) &&
            inputValue is OpenCLMemoryPoolManager.PooledBufferHandle inputBuffer)
        {
            activeBuffers[name] = inputBuffer;
            _logger.LogTrace("Using input buffer: {BufferName}", name);
            return inputBuffer;
        }

        // Allocate new intermediate buffer from pool
        _logger.LogTrace("Allocating new intermediate buffer: {BufferName} (size={Size})", name, size);
        var buffer = await _memoryPool.AcquireBufferAsync(size, flags, cancellationToken).ConfigureAwait(false);
        activeBuffers[name] = buffer;
        return buffer;
    }

    /// <summary>
    /// Resolves a scalar value from the data dictionary.
    /// </summary>
    private static object ResolveScalarValue(string name, Dictionary<string, object> data)
    {
        if (!data.TryGetValue(name, out var value))
        {
            throw new OpenCLPipelineException($"Required scalar argument '{name}' not found in pipeline data");
        }

        return value;
    }

    /// <summary>
    /// Adds a scalar argument to the kernel arguments collection.
    /// </summary>
    private static void AddScalarArgument(KernelArguments arguments, object value)
    {
        // Type dispatch for scalar types
        switch (value)
        {
            case int intValue:
                arguments.AddScalar(intValue);
                break;
            case uint uintValue:
                arguments.AddScalar(uintValue);
                break;
            case long longValue:
                arguments.AddScalar(longValue);
                break;
            case ulong ulongValue:
                arguments.AddScalar(ulongValue);
                break;
            case float floatValue:
                arguments.AddScalar(floatValue);
                break;
            case double doubleValue:
                arguments.AddScalar(doubleValue);
                break;
            default:
                throw new OpenCLPipelineException(
                    $"Unsupported scalar type: {value.GetType().Name}");
        }
    }

    /// <summary>
    /// Collects dependency events from previous stages.
    /// </summary>
    private static List<OpenCLEventHandle> CollectDependencyEvents(
        Stage stage,
        Dictionary<string, OpenCLEventHandle> stageEvents)
    {
        var waitEvents = new List<OpenCLEventHandle>();

        if (stage.InputStages != null)
        {
            foreach (var inputStage in stage.InputStages)
            {
                if (stageEvents.TryGetValue(inputStage, out var evt))
                {
                    waitEvents.Add(evt);
                }
            }
        }

        return waitEvents;
    }

    /// <summary>
    /// Extracts outputs from stage execution.
    /// </summary>
    private static Dictionary<string, object>? ExtractStageOutputs(
        Stage stage,
        Dictionary<string, OpenCLMemoryPoolManager.PooledBufferHandle> activeBuffers)
    {
        if (stage.OutputBuffers == null || stage.OutputBuffers.Count == 0)
        {
            return null;
        }

        var outputs = new Dictionary<string, object>();
        foreach (var outputName in stage.OutputBuffers)
        {
            if (activeBuffers.TryGetValue(outputName, out var buffer))
            {
                outputs[outputName] = buffer;
            }
        }

        return outputs;
    }

    /// <summary>
    /// Extracts final pipeline outputs from intermediate data.
    /// </summary>
    private static Dictionary<string, object> ExtractOutputs(
        Dictionary<string, object> intermediateData,
        IReadOnlyCollection<string> outputNames)
    {
        var outputs = new Dictionary<string, object>();

        foreach (var outputName in outputNames)
        {
            if (intermediateData.TryGetValue(outputName, out var value))
            {
                outputs[outputName] = value;
            }
        }

        return outputs;
    }

    /// <summary>
    /// Validates pipeline structure for correctness and completeness.
    /// </summary>
    private void ValidatePipeline(Pipeline pipeline)
    {
        if (pipeline.Stages.Count == 0)
        {
            throw new OpenCLPipelineException("Pipeline must contain at least one stage");
        }

        // Validate stage names are unique
        var stageNames = new HashSet<string>();
        foreach (var stage in pipeline.Stages)
        {
            if (!stageNames.Add(stage.Name))
            {
                throw new OpenCLPipelineException($"Duplicate stage name: {stage.Name}");
            }
        }

        // Validate all input stage references exist
        foreach (var stage in pipeline.Stages)
        {
            if (stage.InputStages != null)
            {
                foreach (var inputStage in stage.InputStages)
                {
                    if (!stageNames.Contains(inputStage))
                    {
                        throw new OpenCLPipelineException(
                            $"Stage '{stage.Name}' references non-existent input stage: {inputStage}");
                    }
                }
            }
        }

        _logger.LogDebug("Pipeline validation passed: {StageName}", pipeline.Name);
    }

    /// <summary>
    /// Performs topological sort on pipeline stages using Kahn's algorithm.
    /// </summary>
    /// <param name="stages">The stages to sort.</param>
    /// <returns>Stages in valid execution order.</returns>
    /// <exception cref="OpenCLPipelineException">Thrown if a cycle is detected.</exception>
    private List<Stage> TopologicalSort(IReadOnlyCollection<Stage> stages)
    {
        var sorted = new List<Stage>();
        var inDegree = new Dictionary<string, int>();
        var graph = BuildDependencyGraph(stages);

        // Initialize in-degrees
        foreach (var stage in stages)
        {
            inDegree[stage.Name] = 0;
        }

        // Calculate in-degrees
        foreach (var stage in stages)
        {
            if (stage.OutputStages != null)
            {
                foreach (var output in stage.OutputStages)
                {
                    if (inDegree.TryGetValue(output, out var degree))
                    {
                        inDegree[output] = degree + 1;
                    }
                }
            }
        }

        // Queue stages with no dependencies
        var queue = new Queue<Stage>();
        foreach (var stage in stages)
        {
            if (inDegree[stage.Name] == 0)
            {
                queue.Enqueue(stage);
            }
        }

        // Process stages
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            // Reduce in-degree for dependent stages
            if (current.OutputStages != null)
            {
                foreach (var outputName in current.OutputStages)
                {
                    if (inDegree.TryGetValue(outputName, out var degree))
                    {
                        var newDegree = degree - 1;
                        inDegree[outputName] = newDegree;
                        if (newDegree == 0)
                        {
                            var nextStage = stages.First(s => s.Name == outputName);
                            queue.Enqueue(nextStage);
                        }
                    }
                }
            }
        }

        // Check for cycles
        if (sorted.Count != stages.Count)
        {
            var missingStages = stages.Where(s => !sorted.Contains(s))
                .Select(s => s.Name);
            throw new OpenCLPipelineException(
                $"Cycle detected in pipeline. Affected stages: {string.Join(", ", missingStages)}");
        }

        _logger.LogDebug(
            "Topological sort completed: {ExecutionOrder}",
            string.Join(" → ", sorted.Select(s => s.Name)));

        return sorted;
    }

    /// <summary>
    /// Builds a dependency graph for visualization and analysis.
    /// </summary>
    private static Dictionary<string, List<string>> BuildDependencyGraph(IReadOnlyCollection<Stage> stages)
    {
        var graph = new Dictionary<string, List<string>>();

        foreach (var stage in stages)
        {
            graph[stage.Name] = new List<string>();
            if (stage.OutputStages != null)
            {
                graph[stage.Name].AddRange(stage.OutputStages);
            }
        }

        return graph;
    }

    /// <summary>
    /// Detects opportunities for kernel fusion to reduce memory transfers.
    /// </summary>
    /// <param name="executionOrder">Stages in execution order.</param>
    /// <returns>List of fusion opportunity descriptions.</returns>
    private static List<FusionOpportunity> DetectFusionOpportunities(List<Stage> executionOrder)
    {
        var opportunities = new List<FusionOpportunity>();

        for (var i = 0; i < executionOrder.Count - 1; i++)
        {
            var current = executionOrder[i];
            var next = executionOrder[i + 1];

            // Check if stages are sequential and have compatible work sizes
            if (AreStagesFusable(current, next))
            {
                opportunities.Add(new FusionOpportunity
                {
                    Stage1 = current.Name,
                    Stage2 = next.Name,
                    EstimatedSpeedup = EstimateFusionSpeedup(current, next),
                    Reason = "Sequential stages with compatible work sizes"
                });
            }
        }

        return opportunities;
    }

    /// <summary>
    /// Determines if two stages can potentially be fused.
    /// </summary>
    private static bool AreStagesFusable(Stage stage1, Stage stage2)
    {
        // Check if stage2 depends on stage1
        if (stage2.InputStages == null || !stage2.InputStages.Contains(stage1.Name))
        {
            return false;
        }

        // Check if work sizes are compatible (same dimensions)
        if (stage1.Config.GlobalSize.Dimensions != stage2.Config.GlobalSize.Dimensions)
        {
            return false;
        }

        // Check if work sizes match (required for fusion)
        return stage1.Config.GlobalSize.X == stage2.Config.GlobalSize.X &&
               stage1.Config.GlobalSize.Y == stage2.Config.GlobalSize.Y &&
               stage1.Config.GlobalSize.Z == stage2.Config.GlobalSize.Z;
    }

    /// <summary>
    /// Estimates potential speedup from kernel fusion.
    /// </summary>
    private static double EstimateFusionSpeedup(Stage stage1, Stage stage2)
    {
        // Simplified estimation: fusion eliminates one memory round-trip
        // Typical speedup ranges from 1.2x to 2.0x depending on memory bandwidth
        return 1.5;
    }

    /// <summary>
    /// Formats NDRange for logging and reporting.
    /// </summary>
    private static string FormatNDRange(NDRange range)
    {
        return range.Dimensions switch
        {
            1 => $"{range.X}",
            2 => $"({range.X}, {range.Y})",
            3 => $"({range.X}, {range.Y}, {range.Z})",
            _ => $"Invalid[{range.Dimensions}D]"
        };
    }

    /// <summary>
    /// Gets comprehensive pipeline execution statistics.
    /// </summary>
    /// <returns>Statistics about pipeline usage and performance.</returns>
    public PipelineStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new PipelineStatistics
        {
            TotalPipelinesExecuted = Interlocked.Read(ref _totalPipelinesExecuted),
            TotalStagesExecuted = Interlocked.Read(ref _totalStagesExecuted),
            TotalFusionOpportunitiesDetected = Interlocked.Read(ref _totalFusionOpportunitiesDetected),
            ActivePipelines = _pipelines.Count
        };
    }

    /// <summary>
    /// Throws if this pipeline manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Asynchronously disposes the pipeline manager and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("Disposing kernel pipeline manager");

        try
        {
            _pipelines.Clear();
            _pipelineLock?.Dispose();

            var stats = new PipelineStatistics
            {
                TotalPipelinesExecuted = Interlocked.Read(ref _totalPipelinesExecuted),
                TotalStagesExecuted = Interlocked.Read(ref _totalStagesExecuted),
                TotalFusionOpportunitiesDetected = Interlocked.Read(ref _totalFusionOpportunitiesDetected),
                ActivePipelines = 0
            };

            _logger.LogInformation(
                "Pipeline manager disposed. Final stats: Pipelines={Pipelines}, Stages={Stages}, Fusion opportunities={Fusion}",
                stats.TotalPipelinesExecuted, stats.TotalStagesExecuted, stats.TotalFusionOpportunitiesDetected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pipeline manager disposal");
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Represents a configured pipeline ready for execution.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
#pragma warning disable CA1724 // Type name conflicts with namespace (intentional for API design)
    public sealed class Pipeline
#pragma warning restore CA1724
#pragma warning restore CA1034
    {
        /// <summary>Gets the unique identifier for this pipeline.</summary>
        public required Guid Id { get; init; }

        /// <summary>Gets the pipeline name.</summary>
        public required string Name { get; init; }

        /// <summary>Gets the stages in this pipeline.</summary>
        public required IReadOnlyCollection<Stage> Stages { get; init; }

        /// <summary>Gets the output buffer/value names.</summary>
        public required IReadOnlyCollection<string> OutputNames { get; init; }
    }

    /// <summary>
    /// Represents a single stage in a pipeline.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class Stage
#pragma warning restore CA1034
    {
        /// <summary>Gets the stage name.</summary>
        public required string Name { get; init; }

        /// <summary>Gets the compiled kernel to execute.</summary>
        public required OpenCLKernel Kernel { get; init; }

        /// <summary>Gets the execution configuration for this stage.</summary>
        public required ExecutionConfig Config { get; init; }

        /// <summary>Gets the names of stages this stage depends on (inputs).</summary>
        public IReadOnlyList<string>? InputStages { get; init; }

        /// <summary>Gets the names of stages that depend on this stage (outputs).</summary>
        public IReadOnlyList<string>? OutputStages { get; init; }

        /// <summary>Gets the output buffer names produced by this stage.</summary>
        public IReadOnlyList<string>? OutputBuffers { get; init; }
    }

    /// <summary>
    /// Configuration for stage execution.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class ExecutionConfig
#pragma warning restore CA1034
    {
        /// <summary>Gets the global work size for kernel execution.</summary>
        public required NDRange GlobalSize { get; init; }

        /// <summary>Gets the optional local work size.</summary>
        public NDRange? LocalSize { get; init; }

        /// <summary>Gets the kernel argument specifications.</summary>
        public required IReadOnlyList<ArgumentSpec> Arguments { get; init; }
    }

    /// <summary>
    /// Specification for a kernel argument.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class ArgumentSpec
#pragma warning restore CA1034
    {
        /// <summary>Gets the argument name.</summary>
        public required string Name { get; init; }

        /// <summary>Gets whether this is a buffer argument.</summary>
        public bool IsBuffer { get; init; }

        /// <summary>Gets whether this is a local memory argument.</summary>
        public bool IsLocalMemory { get; init; }

        /// <summary>Gets the buffer size (if buffer argument).</summary>
        public ulong? BufferSize { get; init; }

        /// <summary>Gets the buffer memory flags (if buffer argument).</summary>
        public MemoryFlags? BufferFlags { get; init; }

        /// <summary>Gets the local memory size (if local memory argument).</summary>
        public int LocalMemorySize { get; init; }
    }

    /// <summary>
    /// Result of pipeline execution.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class PipelineResult
#pragma warning restore CA1034
    {
        /// <summary>Gets the pipeline name.</summary>
        public required string PipelineName { get; init; }

        /// <summary>Gets the results from each stage.</summary>
        public required IReadOnlyList<StageResult> StageResults { get; init; }

        /// <summary>Gets the final pipeline outputs.</summary>
        public required Dictionary<string, object> Outputs { get; init; }

        /// <summary>Gets the total pipeline execution time.</summary>
        public required TimeSpan TotalTime { get; init; }

        /// <summary>Gets detected fusion opportunities.</summary>
        public required IReadOnlyList<FusionOpportunity> FusionOpportunities { get; init; }

        /// <summary>Gets the profiling session containing detailed metrics.</summary>
        public required ProfilingSession ProfilingSession { get; init; }
    }

    /// <summary>
    /// Result of a single stage execution.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class StageResult
#pragma warning restore CA1034
    {
        /// <summary>Gets the stage name.</summary>
        public required string StageName { get; init; }

        /// <summary>Gets the stage execution duration.</summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>Gets the execution event for synchronization.</summary>
        public required OpenCLEventHandle ExecutionEvent { get; init; }

        /// <summary>Gets the stage outputs (buffers/values).</summary>
        public Dictionary<string, object>? Outputs { get; init; }
    }

    /// <summary>
    /// Describes an opportunity for kernel fusion optimization.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class FusionOpportunity
#pragma warning restore CA1034
    {
        /// <summary>Gets the first stage name.</summary>
        public required string Stage1 { get; init; }

        /// <summary>Gets the second stage name.</summary>
        public required string Stage2 { get; init; }

        /// <summary>Gets the estimated speedup from fusion (e.g., 1.5 = 50% faster).</summary>
        public required double EstimatedSpeedup { get; init; }

        /// <summary>Gets the reason why these stages can be fused.</summary>
        public required string Reason { get; init; }

        /// <summary>Returns a string representation of this opportunity.</summary>
        public override string ToString() => $"{Stage1} + {Stage2}: {EstimatedSpeedup:F2}x speedup ({Reason})";
    }

    /// <summary>
    /// Statistics about pipeline execution.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed record PipelineStatistics
#pragma warning restore CA1034
    {
        /// <summary>Gets the total number of pipelines executed.</summary>
        public long TotalPipelinesExecuted { get; init; }

        /// <summary>Gets the total number of stages executed across all pipelines.</summary>
        public long TotalStagesExecuted { get; init; }

        /// <summary>Gets the total fusion opportunities detected.</summary>
        public long TotalFusionOpportunitiesDetected { get; init; }

        /// <summary>Gets the number of currently active pipelines.</summary>
        public int ActivePipelines { get; init; }
    }

    /// <summary>
    /// Builder for constructing pipelines with fluent API.
    /// </summary>
#pragma warning disable CA1034 // Nested type is part of fluent API design
    public sealed class PipelineBuilder
#pragma warning restore CA1034
    {
        private readonly string _name;
        private readonly OpenCLKernelPipeline _pipeline;
        private readonly List<Stage> _stages;
        private readonly List<string> _outputNames;
        private readonly Dictionary<string, List<string>> _connections;

        internal PipelineBuilder(string name, OpenCLKernelPipeline pipeline)
        {
            _name = name;
            _pipeline = pipeline;
            _stages = new List<Stage>();
            _outputNames = new List<string>();
            _connections = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Adds a stage to the pipeline.
        /// </summary>
        /// <param name="name">Stage name (must be unique).</param>
        /// <param name="kernel">Compiled kernel to execute.</param>
        /// <param name="config">Execution configuration.</param>
        /// <returns>This builder for method chaining.</returns>
        public PipelineBuilder AddStage(string name, OpenCLKernel kernel, ExecutionConfig config)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(config);

            _stages.Add(new Stage
            {
                Name = name,
                Kernel = kernel,
                Config = config
            });

            _connections[name] = new List<string>();
            return this;
        }

        /// <summary>
        /// Connects two stages, establishing a dependency relationship.
        /// </summary>
        /// <param name="from">Source stage name.</param>
        /// <param name="to">Destination stage name.</param>
        /// <returns>This builder for method chaining.</returns>
        public PipelineBuilder ConnectStages(string from, string to)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(from);
            ArgumentException.ThrowIfNullOrWhiteSpace(to);

            if (!_connections.TryGetValue(from, out var connections))
            {
                throw new ArgumentException($"Stage '{from}' does not exist", nameof(from));
            }

            connections.Add(to);
            return this;
        }

        /// <summary>
        /// Marks buffers or values as pipeline outputs.
        /// </summary>
        /// <param name="outputNames">Names of outputs to extract.</param>
        /// <returns>This builder for method chaining.</returns>
        public PipelineBuilder WithOutputs(params string[] outputNames)
        {
            ArgumentNullException.ThrowIfNull(outputNames);
            _outputNames.AddRange(outputNames);
            return this;
        }

        /// <summary>
        /// Builds the pipeline, applying connections and validating structure.
        /// </summary>
        /// <returns>A configured pipeline ready for execution.</returns>
        public Pipeline Build()
        {
            // Apply connections to stages
            foreach (var stage in _stages)
            {
                var inputs = _connections
                    .Where(kvp => kvp.Value.Contains(stage.Name))
                    .Select(kvp => kvp.Key)
                    .ToArray();

                var outputs = _connections.TryGetValue(stage.Name, out var outs)
                    ? outs.ToArray()
                    : null;

                // Update stage with connection information
                var updatedStage = new Stage
                {
                    Name = stage.Name,
                    Kernel = stage.Kernel,
                    Config = stage.Config,
                    InputStages = inputs.Length > 0 ? inputs : null,
                    OutputStages = outputs
                };

                var index = _stages.IndexOf(stage);
                _stages[index] = updatedStage;
            }

            var pipeline = new Pipeline
            {
                Id = Guid.NewGuid(),
                Name = _name,
                Stages = _stages,
                OutputNames = _outputNames
            };

            // Store pipeline
            _pipeline._pipelines[pipeline.Id] = pipeline;

            _pipeline._logger.LogInformation(
                "Built pipeline '{PipelineName}' with {StageCount} stages",
                _name, _stages.Count);

            return pipeline;
        }
    }
}

/// <summary>
/// Wrapper for OpenCL kernel handle.
/// </summary>
public readonly struct OpenCLKernel : IEquatable<OpenCLKernel>
{
    /// <summary>Gets the kernel handle.</summary>
    public nint Handle { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernel"/> struct.
    /// </summary>
    /// <param name="handle">The kernel handle.</param>
    public OpenCLKernel(nint handle)
    {
        Handle = handle;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is OpenCLKernel other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(OpenCLKernel other) => Handle == other.Handle;

    /// <inheritdoc/>
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>
    /// Compares two <see cref="OpenCLKernel"/> instances for equality.
    /// </summary>
    public static bool operator ==(OpenCLKernel left, OpenCLKernel right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="OpenCLKernel"/> instances for inequality.
    /// </summary>
    public static bool operator !=(OpenCLKernel left, OpenCLKernel right) => !left.Equals(right);
}

/// <summary>
/// Exception thrown when pipeline operations fail.
/// </summary>
public sealed class OpenCLPipelineException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPipelineException"/> class.
    /// </summary>
    public OpenCLPipelineException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPipelineException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public OpenCLPipelineException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPipelineException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public OpenCLPipelineException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
