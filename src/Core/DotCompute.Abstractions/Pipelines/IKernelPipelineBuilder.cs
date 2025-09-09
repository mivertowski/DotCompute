// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Builder for constructing kernel pipelines with fluent syntax and comprehensive configuration options.
/// Provides multiple entry points for pipeline creation from various data sources and execution patterns.
/// </summary>
public interface IKernelPipelineBuilder
{
    #region Pipeline Creation from Data Sources
    
    /// <summary>
    /// Creates a new pipeline starting with input data array.
    /// </summary>
    /// <typeparam name="T">The element type of the input data</typeparam>
    /// <param name="inputData">The input data array to process</param>
    /// <param name="options">Optional configuration for data handling</param>
    /// <returns>A new pipeline initialized with the input data</returns>
    IKernelPipeline FromData<T>(T[] inputData, DataPipelineOptions? options = null) where T : unmanaged;
    
    /// <summary>
    /// Creates a pipeline starting with an async data stream for large dataset processing.
    /// </summary>
    /// <typeparam name="T">The element type of the input stream</typeparam>
    /// <param name="inputStream">The async enumerable input stream</param>
    /// <param name="options">Optional streaming configuration</param>
    /// <returns>A new streaming pipeline</returns>
    IKernelPipeline FromStream<T>(IAsyncEnumerable<T> inputStream, StreamPipelineOptions? options = null) where T : unmanaged;
    
    /// <summary>
    /// Creates a pipeline starting with a unified memory buffer for zero-copy processing.
    /// </summary>
    /// <typeparam name="T">The element type of the buffer</typeparam>
    /// <param name="buffer">The unified memory buffer to process</param>
    /// <param name="options">Optional buffer handling configuration</param>
    /// <returns>A new pipeline using the memory buffer</returns>
    IKernelPipeline FromBuffer<T>(IUnifiedMemoryBuffer<T> buffer, BufferPipelineOptions? options = null) where T : unmanaged;
    
    /// <summary>
    /// Creates a pipeline from multiple data sources with automatic data fusion.
    /// </summary>
    /// <param name="dataSources">Multiple data sources to combine</param>
    /// <param name="fusionStrategy">Strategy for combining the data sources</param>
    /// <returns>A new pipeline with fused data inputs</returns>
    IKernelPipeline FromMultipleSources(
        IEnumerable<IDataSource> dataSources, 
        IDataFusionStrategy fusionStrategy);
    
    /// <summary>
    /// Creates a pipeline from a data provider that can dynamically supply data.
    /// </summary>
    /// <typeparam name="T">The data type provided</typeparam>
    /// <param name="dataProvider">The data provider implementation</param>
    /// <param name="options">Provider-specific configuration</param>
    /// <returns>A new pipeline with dynamic data provision</returns>
    IKernelPipeline FromProvider<T>(IDataProvider<T> dataProvider, ProviderPipelineOptions? options = null) where T : unmanaged;
    
    #endregion

    #region Pipeline Creation from Kernels
    
    /// <summary>
    /// Creates a pipeline starting with a specific kernel execution.
    /// </summary>
    /// <typeparam name="TOutput">The output type of the starting kernel</typeparam>
    /// <param name="kernelName">The name of the kernel to execute first</param>
    /// <param name="parameters">Parameters for the initial kernel</param>
    /// <param name="options">Optional kernel execution configuration</param>
    /// <returns>A new pipeline starting with the specified kernel</returns>
    IKernelPipeline StartWith<TOutput>(
        string kernelName, 
        object[] parameters,
        KernelStageOptions? options = null) where TOutput : unmanaged;
        
    /// <summary>
    /// Creates a pipeline starting with a pre-configured kernel stage.
    /// </summary>
    /// <typeparam name="TOutput">The output type of the starting stage</typeparam>
    /// <param name="stage">The pre-configured kernel stage</param>
    /// <returns>A new pipeline starting with the specified stage</returns>
    IKernelPipeline StartWith<TOutput>(IKernelStage<TOutput> stage) where TOutput : unmanaged;
    
    /// <summary>
    /// Creates a pipeline from multiple parallel starting kernels.
    /// </summary>
    /// <param name="parallelStages">Multiple kernels to start with in parallel</param>
    /// <param name="aggregationStrategy">How to combine the parallel results</param>
    /// <returns>A new pipeline with parallel starting execution</returns>
    IKernelPipeline StartWithParallel(
        IEnumerable<IKernelStage> parallelStages,
        IAggregationStrategy aggregationStrategy);
    
    #endregion

    #region Pipeline Creation from Templates and Patterns
    
    /// <summary>
    /// Creates an empty pipeline for manual construction and configuration.
    /// </summary>
    /// <param name="configuration">Optional initial pipeline configuration</param>
    /// <returns>An empty pipeline ready for stage addition</returns>
    IKernelPipeline Create(IPipelineConfiguration? configuration = null);
    
    /// <summary>
    /// Creates a pipeline from a predefined template with parameter substitution.
    /// </summary>
    /// <param name="templateName">The name of the pipeline template to use</param>
    /// <param name="parameters">Parameters to substitute in the template</param>
    /// <param name="options">Template instantiation options</param>
    /// <returns>A new pipeline instantiated from the template</returns>
    IKernelPipeline FromTemplate(
        string templateName,
        Dictionary<string, object>? parameters = null,
        TemplateOptions? options = null);
        
    /// <summary>
    /// Creates a pipeline from a LINQ expression tree for declarative pipeline construction.
    /// </summary>
    /// <typeparam name="T">The data type being processed</typeparam>
    /// <param name="expression">LINQ expression defining the pipeline operations</param>
    /// <returns>A new pipeline based on the LINQ expression</returns>
    IKernelPipeline FromExpression<T>(
        Expression<Func<IQueryable<T>, IQueryable<T>>> expression) where T : unmanaged;
    
    /// <summary>
    /// Creates a pipeline from a workflow definition (e.g., YAML, JSON configuration).
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to parse</param>
    /// <param name="format">The format of the workflow definition</param>
    /// <returns>A new pipeline based on the workflow definition</returns>
    IKernelPipeline FromWorkflow(
        string workflowDefinition, 
        WorkflowFormat format = WorkflowFormat.Auto);
    
    /// <summary>
    /// Creates a pipeline using a fluent builder pattern with method chaining.
    /// </summary>
    /// <param name="builderAction">Action to configure the pipeline using fluent syntax</param>
    /// <returns>A new pipeline configured by the builder action</returns>
    IKernelPipeline Build(Action<IFluentPipelineBuilder> builderAction);
    
    #endregion

    #region Advanced Pipeline Creation
    
    /// <summary>
    /// Creates a conditional pipeline that adapts its structure based on runtime conditions.
    /// </summary>
    /// <param name="conditionEvaluator">Evaluates conditions to determine pipeline structure</param>
    /// <param name="pipelineSelector">Selects appropriate pipeline based on conditions</param>
    /// <returns>A new adaptive pipeline</returns>
    IKernelPipeline CreateAdaptive(
        IConditionEvaluator conditionEvaluator,
        Func<IConditionResult, IKernelPipeline> pipelineSelector);
    
    /// <summary>
    /// Creates a self-optimizing pipeline that learns and adapts from execution patterns.
    /// </summary>
    /// <param name="learningStrategy">Strategy for learning from execution history</param>
    /// <param name="optimizationCriteria">Criteria for pipeline optimization</param>
    /// <returns>A new self-optimizing pipeline</returns>
    IKernelPipeline CreateSelfOptimizing(
        ILearningStrategy learningStrategy,
        IOptimizationCriteria optimizationCriteria);
    
    /// <summary>
    /// Creates a distributed pipeline that can execute across multiple compute nodes.
    /// </summary>
    /// <param name="distributionStrategy">Strategy for distributing pipeline execution</param>
    /// <param name="nodeConfiguration">Configuration for compute nodes</param>
    /// <returns>A new distributed pipeline</returns>
    IKernelPipeline CreateDistributed(
        IDistributionStrategy distributionStrategy,
        INodeConfiguration nodeConfiguration);
    
    /// <summary>
    /// Creates a pipeline with advanced fault tolerance and recovery capabilities.
    /// </summary>
    /// <param name="resilience">Resilience configuration for the pipeline</param>
    /// <returns>A new fault-tolerant pipeline</returns>
    IKernelPipeline CreateResilient(IResilienceConfiguration resilience);
    
    #endregion

    #region Pipeline Composition and Reuse
    
    /// <summary>
    /// Composes multiple existing pipelines into a larger composite pipeline.
    /// </summary>
    /// <param name="pipelines">The pipelines to compose together</param>
    /// <param name="compositionStrategy">Strategy for composing the pipelines</param>
    /// <returns>A new composite pipeline</returns>
    IKernelPipeline Compose(
        IEnumerable<IKernelPipeline> pipelines,
        IPipelineCompositionStrategy compositionStrategy);
    
    /// <summary>
    /// Creates a pipeline that can be reused with different input types through type adaptation.
    /// </summary>
    /// <typeparam name="TInput">The primary input type</typeparam>
    /// <param name="basePipeline">The base pipeline to make reusable</param>
    /// <param name="adaptationRules">Rules for adapting different input types</param>
    /// <returns>A new reusable pipeline with type adaptation</returns>
    IKernelPipeline CreateReusable<TInput>(
        IKernelPipeline basePipeline,
        ITypeAdaptationRules<TInput> adaptationRules) where TInput : unmanaged;
    
    /// <summary>
    /// Creates a pipeline library entry that can be reused across multiple contexts.
    /// </summary>
    /// <param name="libraryName">Name for the pipeline in the library</param>
    /// <param name="pipeline">The pipeline to add to the library</param>
    /// <param name="metadata">Metadata describing the pipeline capabilities</param>
    /// <returns>A library reference for the registered pipeline</returns>
    IPipelineLibraryEntry CreateLibraryEntry(
        string libraryName,
        IKernelPipeline pipeline,
        IPipelineMetadata metadata);
    
    #endregion

    #region Pipeline Analysis and Optimization
    
    /// <summary>
    /// Analyzes a pipeline for potential optimizations before execution.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze</param>
    /// <returns>Analysis results with optimization recommendations</returns>
    Task<IPipelineAnalysisResult> AnalyzeAsync(IKernelPipeline pipeline);
    
    /// <summary>
    /// Optimizes a pipeline structure for better performance characteristics.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="optimizationGoals">Specific optimization objectives</param>
    /// <returns>An optimized version of the pipeline</returns>
    Task<IKernelPipeline> OptimizeAsync(
        IKernelPipeline pipeline, 
        IOptimizationGoals optimizationGoals);
    
    /// <summary>
    /// Validates a pipeline configuration for correctness and compatibility.
    /// </summary>
    /// <param name="pipeline">The pipeline to validate</param>
    /// <returns>Validation results with any issues found</returns>
    Task<IPipelineValidationResult> ValidateAsync(IKernelPipeline pipeline);
    
    #endregion

    #region Configuration and Customization
    
    /// <summary>
    /// Configures global settings for all pipelines created by this builder.
    /// </summary>
    /// <param name="configuration">Global pipeline configuration</param>
    /// <returns>The builder instance for method chaining</returns>
    IKernelPipelineBuilder WithGlobalConfiguration(IGlobalPipelineConfiguration configuration);
    
    /// <summary>
    /// Adds a custom pipeline stage factory for creating specialized stages.
    /// </summary>
    /// <param name="stageFactory">Factory for creating custom pipeline stages</param>
    /// <returns>The builder instance for method chaining</returns>
    IKernelPipelineBuilder WithStageFactory(IPipelineStageFactory stageFactory);
    
    /// <summary>
    /// Configures the resource manager for pipeline execution.
    /// </summary>
    /// <param name="resourceManager">Custom resource manager implementation</param>
    /// <returns>The builder instance for method chaining</returns>
    IKernelPipelineBuilder WithResourceManager(IPipelineResourceManager resourceManager);
    
    /// <summary>
    /// Adds custom middleware to all pipelines created by this builder.
    /// </summary>
    /// <param name="middleware">Pipeline middleware to add</param>
    /// <returns>The builder instance for method chaining</returns>
    IKernelPipelineBuilder WithMiddleware(IPipelineMiddleware middleware);
    
    #endregion

    #region Specialized Pipeline Types
    
    /// <summary>
    /// Creates a real-time pipeline optimized for low-latency stream processing.
    /// </summary>
    /// <typeparam name="T">The stream element type</typeparam>
    /// <param name="latencyRequirements">Latency constraints for the pipeline</param>
    /// <returns>A new real-time optimized pipeline</returns>
    IKernelPipeline CreateRealTime<T>(ILatencyRequirements latencyRequirements) where T : unmanaged;
    
    /// <summary>
    /// Creates a batch processing pipeline optimized for high-throughput workloads.
    /// </summary>
    /// <typeparam name="T">The batch element type</typeparam>
    /// <param name="throughputRequirements">Throughput objectives for the pipeline</param>
    /// <returns>A new batch processing pipeline</returns>
    IKernelPipeline CreateBatch<T>(IThroughputRequirements throughputRequirements) where T : unmanaged;
    
    /// <summary>
    /// Creates a machine learning pipeline with specialized ML operations and optimizations.
    /// </summary>
    /// <param name="modelConfiguration">Configuration for ML model integration</param>
    /// <returns>A new machine learning optimized pipeline</returns>
    IKernelPipeline CreateMachineLearning(IMLModelConfiguration modelConfiguration);
    
    /// <summary>
    /// Creates a scientific computing pipeline with high-precision numerical operations.
    /// </summary>
    /// <param name="precisionRequirements">Numerical precision requirements</param>
    /// <returns>A new scientific computing pipeline</returns>
    IKernelPipeline CreateScientificComputing(IPrecisionRequirements precisionRequirements);
    
    /// <summary>
    /// Creates an image processing pipeline with specialized computer vision operations.
    /// </summary>
    /// <param name="imageFormat">Expected image format and characteristics</param>
    /// <returns>A new image processing pipeline</returns>
    IKernelPipeline CreateImageProcessing(IImageFormatConfiguration imageFormat);
    
    #endregion
}

/// <summary>
/// Fluent builder interface for constructing pipelines with method chaining.
/// </summary>
public interface IFluentPipelineBuilder
{
    /// <summary>
    /// Adds a kernel stage to the pipeline being built.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to add</param>
    /// <param name="parameters">Parameters for the kernel</param>
    /// <returns>The fluent builder for continued configuration</returns>
    IFluentPipelineBuilder AddStage(string kernelName, params object[] parameters);
    
    /// <summary>
    /// Adds parallel execution of multiple stages.
    /// </summary>
    /// <param name="parallelStages">Stages to execute in parallel</param>
    /// <returns>The fluent builder for continued configuration</returns>
    IFluentPipelineBuilder AddParallel(params (string kernelName, object[] parameters)[] parallelStages);
    
    /// <summary>
    /// Adds conditional branching to the pipeline.
    /// </summary>
    /// <param name="condition">Condition for branching</param>
    /// <param name="trueBuilder">Builder for the true branch</param>
    /// <param name="falseBuilder">Builder for the false branch</param>
    /// <returns>The fluent builder for continued configuration</returns>
    IFluentPipelineBuilder AddBranch(
        Func<object, bool> condition,
        Action<IFluentPipelineBuilder> trueBuilder,
        Action<IFluentPipelineBuilder> falseBuilder);
    
    /// <summary>
    /// Configures caching for the current pipeline segment.
    /// </summary>
    /// <param name="cacheKey">Key for caching results</param>
    /// <param name="policy">Cache policy configuration</param>
    /// <returns>The fluent builder for continued configuration</returns>
    IFluentPipelineBuilder WithCaching(string cacheKey, CachePolicy policy);
    
    /// <summary>
    /// Configures error handling for the pipeline.
    /// </summary>
    /// <param name="errorHandler">Error handling strategy</param>
    /// <returns>The fluent builder for continued configuration</returns>
    IFluentPipelineBuilder WithErrorHandling(Func<Exception, object, Task<object>> errorHandler);
}