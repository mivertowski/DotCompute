// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Dependency resolution system for complex pipeline topologies with advanced analysis and optimization.
/// Manages stage dependencies, execution ordering, and parallel execution opportunities.
/// </summary>
public interface IPipelineDependencyResolver : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this dependency resolver instance.
    /// </summary>
    Guid ResolverId { get; }
    
    /// <summary>
    /// Gets current dependency resolution metrics.
    /// </summary>
    IDependencyResolutionMetrics Metrics { get; }

    #region Dependency Analysis
    
    /// <summary>
    /// Analyzes pipeline dependencies and creates a comprehensive execution graph.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze for dependencies</param>
    /// <returns>Complete execution graph with dependency relationships</returns>
    Task<IPipelineExecutionGraph> AnalyzeDependenciesAsync(IKernelPipeline pipeline);
    
    /// <summary>
    /// Analyzes dependencies for a group of related pipelines with shared resources.
    /// </summary>
    /// <param name="pipelineGroup">Group of pipelines to analyze together</param>
    /// <returns>Multi-pipeline execution graph with cross-pipeline dependencies</returns>
    Task<IMultiPipelineExecutionGraph> AnalyzeMultiPipelineDependenciesAsync(
        IPipelineGroup pipelineGroup);
    
    /// <summary>
    /// Performs incremental dependency analysis for dynamically changing pipelines.
    /// </summary>
    /// <param name="executionGraph">Existing execution graph to update</param>
    /// <param name="changes">Changes to apply to the pipeline structure</param>
    /// <returns>Updated execution graph reflecting the changes</returns>
    Task<IPipelineExecutionGraph> UpdateDependenciesAsync(
        IPipelineExecutionGraph executionGraph,
        IEnumerable<IPipelineChange> changes);
    
    /// <summary>
    /// Analyzes data dependencies between pipeline stages based on input/output types.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze for data dependencies</param>
    /// <returns>Data dependency graph showing data flow relationships</returns>
    Task<IDataDependencyGraph> AnalyzeDataDependenciesAsync(IKernelPipeline pipeline);
    
    #endregion

    #region Execution Planning
    
    /// <summary>
    /// Creates an optimized execution plan based on dependencies and resource constraints.
    /// </summary>
    /// <param name="graph">The execution graph to create a plan for</param>
    /// <param name="criteria">Optimization criteria for the execution plan</param>
    /// <returns>Optimized execution plan with stage ordering and parallelization</returns>
    Task<IExecutionPlan> CreateExecutionPlanAsync(
        IPipelineExecutionGraph graph,
        OptimizationCriteria criteria);
    
    /// <summary>
    /// Creates execution plans for streaming pipelines with continuous dependency resolution.
    /// </summary>
    /// <param name="streamingGraph">Execution graph for streaming pipeline</param>
    /// <param name="streamingCriteria">Streaming-specific optimization criteria</param>
    /// <returns>Streaming execution plan with flow-based scheduling</returns>
    Task<IStreamingExecutionPlan> CreateStreamingExecutionPlanAsync(
        IPipelineExecutionGraph streamingGraph,
        StreamingOptimizationCriteria streamingCriteria);
    
    /// <summary>
    /// Optimizes an existing execution plan based on runtime performance feedback.
    /// </summary>
    /// <param name="currentPlan">The current execution plan to optimize</param>
    /// <param name="performanceFeedback">Performance data from previous executions</param>
    /// <param name="optimizationGoals">Specific goals for optimization</param>
    /// <returns>Optimized execution plan with improved characteristics</returns>
    Task<IExecutionPlan> OptimizeExecutionPlanAsync(
        IExecutionPlan currentPlan,
        IPerformanceFeedback performanceFeedback,
        ExecutionOptimizationGoals optimizationGoals);
    
    /// <summary>
    /// Creates conditional execution plans that adapt based on runtime conditions.
    /// </summary>
    /// <param name="graph">The execution graph with conditional branches</param>
    /// <param name="conditionEvaluators">Evaluators for runtime conditions</param>
    /// <returns>Adaptive execution plan with conditional paths</returns>
    Task<IAdaptiveExecutionPlan> CreateAdaptiveExecutionPlanAsync(
        IPipelineExecutionGraph graph,
        IEnumerable<IConditionEvaluator> conditionEvaluators);
    
    #endregion

    #region Validation and Analysis
    
    /// <summary>
    /// Detects circular dependencies and other topology issues in pipelines.
    /// </summary>
    /// <param name="pipeline">The pipeline to validate for dependency issues</param>
    /// <returns>Validation result with any detected issues and recommendations</returns>
    Task<IDependencyValidationResult> ValidateAsync(IKernelPipeline pipeline);
    
    /// <summary>
    /// Performs comprehensive validation of complex multi-pipeline dependencies.
    /// </summary>
    /// <param name="pipelineGroup">Group of pipelines to validate together</param>
    /// <returns>Multi-pipeline validation result with cross-pipeline issue detection</returns>
    Task<IMultiPipelineValidationResult> ValidateMultiPipelineAsync(IPipelineGroup pipelineGroup);
    
    /// <summary>
    /// Analyzes pipeline structure for potential deadlock conditions.
    /// </summary>
    /// <param name="graph">Execution graph to analyze for deadlocks</param>
    /// <returns>Deadlock analysis result with potential issues and mitigation strategies</returns>
    Task<IDeadlockAnalysisResult> AnalyzeDeadlockRiskAsync(IPipelineExecutionGraph graph);
    
    /// <summary>
    /// Validates execution plan feasibility given resource constraints.
    /// </summary>
    /// <param name="plan">Execution plan to validate</param>
    /// <param name="resourceConstraints">Available resource constraints</param>
    /// <returns>Plan feasibility analysis with resource requirement validation</returns>
    Task<IPlanFeasibilityResult> ValidateExecutionPlanFeasibilityAsync(
        IExecutionPlan plan,
        ResourceConstraints resourceConstraints);
    
    #endregion

    #region Dynamic Dependency Resolution
    
    /// <summary>
    /// Resolves dynamic dependencies at runtime based on execution context.
    /// </summary>
    /// <param name="context">Current pipeline execution context</param>
    /// <param name="query">Query for dynamic dependency resolution</param>
    /// <returns>Dynamic dependency resolution result</returns>
    Task<IDynamicDependencyResolutionResult> ResolveDynamicDependenciesAsync(
        IPipelineContext context,
        DependencyQuery query);
    
    /// <summary>
    /// Handles runtime dependency changes during pipeline execution.
    /// </summary>
    /// <param name="executionGraph">Currently executing graph</param>
    /// <param name="runtimeChange">Runtime change to handle</param>
    /// <returns>Updated execution strategy for the runtime change</returns>
    Task<IRuntimeDependencyUpdateResult> HandleRuntimeDependencyChangeAsync(
        IPipelineExecutionGraph executionGraph,
        RuntimeDependencyChange runtimeChange);
    
    /// <summary>
    /// Creates dependency resolvers for plugin-based pipeline extensions.
    /// </summary>
    /// <param name="pluginDependencies">Plugin dependency specifications</param>
    /// <returns>Plugin-aware dependency resolver</returns>
    Task<IPluginDependencyResolver> CreatePluginDependencyResolverAsync(
        IEnumerable<IPluginDependencySpec> pluginDependencies);
    
    #endregion

    #region Parallelization Analysis
    
    /// <summary>
    /// Identifies opportunities for parallel execution within pipeline stages.
    /// </summary>
    /// <param name="graph">Execution graph to analyze for parallelization</param>
    /// <param name="parallelizationConstraints">Constraints on parallel execution</param>
    /// <returns>Parallelization analysis with optimization recommendations</returns>
    Task<IParallelizationAnalysis> AnalyzeParallelizationOpportunitiesAsync(
        IPipelineExecutionGraph graph,
        ParallelizationConstraints parallelizationConstraints);
    
    /// <summary>
    /// Creates parallel execution groups for independent pipeline segments.
    /// </summary>
    /// <param name="graph">Execution graph to create parallel groups from</param>
    /// <param name="resourceLimits">Resource limits for parallel execution</param>
    /// <returns>Parallel execution groups with resource allocation</returns>
    Task<IParallelExecutionGroups> CreateParallelExecutionGroupsAsync(
        IPipelineExecutionGraph graph,
        ParallelResourceLimits resourceLimits);
    
    /// <summary>
    /// Optimizes parallel execution based on hardware characteristics.
    /// </summary>
    /// <param name="parallelGroups">Current parallel execution groups</param>
    /// <param name="hardwareProfile">Target hardware characteristics</param>
    /// <returns>Hardware-optimized parallel execution strategy</returns>
    Task<IHardwareOptimizedParallelStrategy> OptimizeForHardwareAsync(
        IParallelExecutionGroups parallelGroups,
        IHardwareProfile hardwareProfile);
    
    #endregion

    #region Dependency Monitoring and Events
    
    /// <summary>
    /// Gets an observable stream of dependency resolution events.
    /// </summary>
    IObservable<DependencyResolutionEvent> DependencyEvents { get; }
    
    /// <summary>
    /// Monitors dependency resolution performance in real-time.
    /// </summary>
    /// <param name="monitoringOptions">Configuration for dependency monitoring</param>
    /// <returns>Dependency monitor for real-time analysis</returns>
    Task<IDependencyMonitor> StartDependencyMonitoringAsync(
        DependencyMonitoringOptions monitoringOptions);
    
    /// <summary>
    /// Analyzes dependency resolution patterns for optimization opportunities.
    /// </summary>
    /// <param name="analysisOptions">Configuration for dependency pattern analysis</param>
    /// <returns>Dependency pattern analysis with optimization recommendations</returns>
    Task<IDependencyPatternAnalysis> AnalyzeDependencyPatternsAsync(
        DependencyAnalysisOptions analysisOptions);
    
    #endregion

    #region Configuration and Customization
    
    /// <summary>
    /// Configures custom dependency resolution strategies.
    /// </summary>
    /// <param name="strategy">Custom dependency resolution strategy</param>
    /// <returns>Dependency resolver with custom strategy</returns>
    Task<ICustomDependencyResolver> ConfigureCustomResolutionAsync(
        IDependencyResolutionStrategy strategy);
    
    /// <summary>
    /// Adds custom dependency types for application-specific scenarios.
    /// </summary>
    /// <param name="dependencyTypes">Custom dependency type definitions</param>
    /// <returns>Extended dependency resolver supporting custom types</returns>
    Task<IExtendedDependencyResolver> AddCustomDependencyTypesAsync(
        IEnumerable<ICustomDependencyType> dependencyTypes);
    
    /// <summary>
    /// Configures dependency caching for improved resolution performance.
    /// </summary>
    /// <param name="cachingConfiguration">Configuration for dependency caching</param>
    /// <returns>Caching-enabled dependency resolver</returns>
    Task<ICachingDependencyResolver> ConfigureDependencyCachingAsync(
        DependencyCachingConfiguration cachingConfiguration);
    
    #endregion
}

/// <summary>
/// Represents a complete execution graph for a pipeline with dependency relationships.
/// </summary>
public interface IPipelineExecutionGraph
{
    /// <summary>Unique identifier for this execution graph.</summary>
    Guid GraphId { get; }
    
    /// <summary>Pipeline this graph represents.</summary>
    IKernelPipeline Pipeline { get; }
    
    /// <summary>All nodes in the execution graph (pipeline stages).</summary>
    IReadOnlyList<IExecutionNode> Nodes { get; }
    
    /// <summary>All edges in the graph representing dependencies.</summary>
    IReadOnlyList<IExecutionEdge> Edges { get; }
    
    /// <summary>Root nodes with no dependencies (entry points).</summary>
    IReadOnlyList<IExecutionNode> RootNodes { get; }
    
    /// <summary>Leaf nodes with no dependents (exit points).</summary>
    IReadOnlyList<IExecutionNode> LeafNodes { get; }
    
    /// <summary>Critical path through the execution graph.</summary>
    ICriticalPath CriticalPath { get; }
    
    /// <summary>Parallel execution opportunities identified in the graph.</summary>
    IReadOnlyList<IParallelExecutionGroup> ParallelGroups { get; }
    
    /// <summary>Gets all direct dependencies for a specific node.</summary>
    IReadOnlyList<IExecutionNode> GetDependencies(IExecutionNode node);
    
    /// <summary>Gets all nodes that depend on a specific node.</summary>
    IReadOnlyList<IExecutionNode> GetDependents(IExecutionNode node);
    
    /// <summary>Checks if the graph contains cycles (circular dependencies).</summary>
    Task<ICycleDetectionResult> DetectCyclesAsync();
    
    /// <summary>Gets the topological sort order for execution.</summary>
    IReadOnlyList<IExecutionNode> GetTopologicalOrder();
    
    /// <summary>Creates a subgraph containing only specified nodes and their connections.</summary>
    IPipelineExecutionGraph CreateSubgraph(IEnumerable<IExecutionNode> nodes);
}

/// <summary>
/// Represents a node in the pipeline execution graph (a pipeline stage).
/// </summary>
public interface IExecutionNode
{
    /// <summary>Unique identifier for this execution node.</summary>
    Guid NodeId { get; }
    
    /// <summary>Name of the pipeline stage this node represents.</summary>
    string StageName { get; }
    
    /// <summary>Kernel name to be executed at this node.</summary>
    string KernelName { get; }
    
    /// <summary>Input types expected by this node.</summary>
    IReadOnlyList<Type> InputTypes { get; }
    
    /// <summary>Output types produced by this node.</summary>
    IReadOnlyList<Type> OutputTypes { get; }
    
    /// <summary>Estimated execution time for this node.</summary>
    TimeSpan EstimatedExecutionTime { get; }
    
    /// <summary>Resource requirements for this node.</summary>
    INodeResourceRequirements ResourceRequirements { get; }
    
    /// <summary>Stage configuration options.</summary>
    PipelineStageOptions StageOptions { get; }
    
    /// <summary>Whether this node can be executed in parallel with others.</summary>
    bool SupportsParallelExecution { get; }
    
    /// <summary>Custom metadata for this execution node.</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Represents an edge in the execution graph (a dependency relationship).
/// </summary>
public interface IExecutionEdge
{
    /// <summary>Unique identifier for this execution edge.</summary>
    Guid EdgeId { get; }
    
    /// <summary>Source node of the dependency.</summary>
    IExecutionNode Source { get; }
    
    /// <summary>Target node that depends on the source.</summary>
    IExecutionNode Target { get; }
    
    /// <summary>Type of dependency relationship.</summary>
    DependencyType DependencyType { get; }
    
    /// <summary>Data being passed along this edge.</summary>
    IDataFlowInfo DataFlow { get; }
    
    /// <summary>Strength of the dependency (0.0 to 1.0).</summary>
    double DependencyStrength { get; }
    
    /// <summary>Whether this dependency can be satisfied asynchronously.</summary>
    bool SupportsAsyncDependency { get; }
    
    /// <summary>Custom conditions for this dependency.</summary>
    IReadOnlyList<IDependencyCondition> Conditions { get; }
}

/// <summary>
/// Types of dependency relationships between pipeline stages.
/// </summary>
public enum DependencyType
{
    /// <summary>Data dependency - output from source is input to target.</summary>
    DataDependency,
    
    /// <summary>Control dependency - target must wait for source completion.</summary>
    ControlDependency,
    
    /// <summary>Resource dependency - shared resource usage constraint.</summary>
    ResourceDependency,
    
    /// <summary>Ordering dependency - execution order requirement.</summary>
    OrderingDependency,
    
    /// <summary>Conditional dependency - dependency based on runtime condition.</summary>
    ConditionalDependency,
    
    /// <summary>Custom dependency - application-specific dependency type.</summary>
    CustomDependency
}

/// <summary>
/// Optimized execution plan with stage ordering and parallelization strategy.
/// </summary>
public interface IExecutionPlan
{
    /// <summary>Unique identifier for this execution plan.</summary>
    Guid PlanId { get; }
    
    /// <summary>Execution graph this plan was created from.</summary>
    IPipelineExecutionGraph ExecutionGraph { get; }
    
    /// <summary>Ordered execution phases with parallel groups.</summary>
    IReadOnlyList<IExecutionPhase> ExecutionPhases { get; }
    
    /// <summary>Resource allocation plan for the execution.</summary>
    IResourceAllocationPlan ResourceAllocation { get; }
    
    /// <summary>Estimated total execution time.</summary>
    TimeSpan EstimatedExecutionTime { get; }
    
    /// <summary>Critical path information for the plan.</summary>
    ICriticalPath CriticalPath { get; }
    
    /// <summary>Parallelization strategy used in the plan.</summary>
    IParallelizationStrategy ParallelizationStrategy { get; }
    
    /// <summary>Performance characteristics estimated for this plan.</summary>
    IPlanPerformanceEstimate PerformanceEstimate { get; }
    
    /// <summary>Validation results for the execution plan.</summary>
    IPlanValidationResult ValidationResult { get; }
    
    /// <summary>Executes this plan with the given execution context.</summary>
    Task<IPlanExecutionResult> ExecuteAsync(IPipelineExecutionContext context);
}

/// <summary>
/// Represents an execution phase containing stages that can run in parallel.
/// </summary>
public interface IExecutionPhase
{
    /// <summary>Phase identifier within the execution plan.</summary>
    int PhaseId { get; }
    
    /// <summary>Stages to execute in this phase.</summary>
    IReadOnlyList<IExecutionNode> Stages { get; }
    
    /// <summary>Whether all stages in this phase can run in parallel.</summary>
    bool IsParallelPhase { get; }
    
    /// <summary>Resource requirements for this phase.</summary>
    IPhaseResourceRequirements ResourceRequirements { get; }
    
    /// <summary>Estimated execution time for this phase.</summary>
    TimeSpan EstimatedPhaseTime { get; }
    
    /// <summary>Dependencies that must be satisfied before this phase.</summary>
    IReadOnlyList<IExecutionPhase> PhaseDependencies { get; }
}

/// <summary>
/// Validation result for pipeline dependency analysis.
/// </summary>
public interface IDependencyValidationResult
{
    /// <summary>Whether the dependency analysis passed validation.</summary>
    bool IsValid { get; }
    
    /// <summary>Validation errors found during analysis.</summary>
    IReadOnlyList<DependencyValidationError> Errors { get; }
    
    /// <summary>Validation warnings that don't prevent execution.</summary>
    IReadOnlyList<DependencyValidationWarning> Warnings { get; }
    
    /// <summary>Circular dependencies detected in the pipeline.</summary>
    IReadOnlyList<ICircularDependency> CircularDependencies { get; }
    
    /// <summary>Unreachable nodes in the pipeline graph.</summary>
    IReadOnlyList<IExecutionNode> UnreachableNodes { get; }
    
    /// <summary>Missing dependencies that could not be resolved.</summary>
    IReadOnlyList<IMissingDependency> MissingDependencies { get; }
    
    /// <summary>Recommendations for fixing validation issues.</summary>
    IReadOnlyList<DependencyValidationRecommendation> Recommendations { get; }
    
    /// <summary>Performance implications of the current dependency structure.</summary>
    IDependencyPerformanceAnalysis PerformanceAnalysis { get; }
}

/// <summary>
/// Information about a circular dependency in the pipeline.
/// </summary>
public interface ICircularDependency
{
    /// <summary>Nodes involved in the circular dependency.</summary>
    IReadOnlyList<IExecutionNode> InvolvedNodes { get; }
    
    /// <summary>Path of dependencies forming the cycle.</summary>
    IReadOnlyList<IExecutionEdge> DependencyPath { get; }
    
    /// <summary>Severity of the circular dependency.</summary>
    CircularDependencySeverity Severity { get; }
    
    /// <summary>Suggested strategies for breaking the cycle.</summary>
    IReadOnlyList<CycleBreakingStrategy> BreakingStrategies { get; }
}

/// <summary>
/// Severity levels for circular dependencies.
/// </summary>
public enum CircularDependencySeverity
{
    /// <summary>Warning - cycle exists but may not prevent execution.</summary>
    Warning,
    
    /// <summary>Error - cycle prevents proper execution ordering.</summary>
    Error,
    
    /// <summary>Critical - cycle causes deadlock conditions.</summary>
    Critical
}

/// <summary>
/// Strategies for breaking circular dependencies.
/// </summary>
public enum CycleBreakingStrategy
{
    /// <summary>Remove the weakest dependency in the cycle.</summary>
    RemoveWeakestDependency,
    
    /// <summary>Introduce intermediate buffering stage.</summary>
    IntroduceBuffering,
    
    /// <summary>Split one stage into multiple stages.</summary>
    SplitStage,
    
    /// <summary>Change dependency type to conditional.</summary>
    MakeConditional,
    
    /// <summary>Reorder pipeline stages to eliminate cycle.</summary>
    ReorderStages,
    
    /// <summary>Use async dependency resolution.</summary>
    UseAsyncResolution
}

/// <summary>
/// Query for dynamic dependency resolution at runtime.
/// </summary>
public class DependencyQuery
{
    /// <summary>Stage requesting dependency resolution.</summary>
    public string RequestingStage { get; set; } = string.Empty;
    
    /// <summary>Type of dependency being queried.</summary>
    public DependencyType DependencyType { get; set; }
    
    /// <summary>Data type requirements for the dependency.</summary>
    public Type? RequiredDataType { get; set; }
    
    /// <summary>Resource requirements for the dependency.</summary>
    public IResourceRequirements? ResourceRequirements { get; set; }
    
    /// <summary>Conditions that must be met for the dependency.</summary>
    public List<IDependencyCondition> Conditions { get; set; } = new();
    
    /// <summary>Priority of the dependency request.</summary>
    public DependencyPriority Priority { get; set; } = DependencyPriority.Normal;
    
    /// <summary>Timeout for dependency resolution.</summary>
    public TimeSpan? ResolutionTimeout { get; set; }
    
    /// <summary>Custom metadata for the dependency query.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Priority levels for dependency resolution.
/// </summary>
public enum DependencyPriority
{
    /// <summary>Low priority - can wait for other dependencies.</summary>
    Low,
    
    /// <summary>Normal priority - standard dependency resolution.</summary>
    Normal,
    
    /// <summary>High priority - expedited dependency resolution.</summary>
    High,
    
    /// <summary>Critical priority - must be resolved immediately.</summary>
    Critical
}

/// <summary>
/// Result of dynamic dependency resolution.
/// </summary>
public interface IDynamicDependencyResolutionResult
{
    /// <summary>Whether the dependency was successfully resolved.</summary>
    bool IsResolved { get; }
    
    /// <summary>Resolved dependency information.</summary>
    IResolvedDependency? ResolvedDependency { get; }
    
    /// <summary>Alternative dependencies if exact match not found.</summary>
    IReadOnlyList<IResolvedDependency> AlternativeDependencies { get; }
    
    /// <summary>Time taken to resolve the dependency.</summary>
    TimeSpan ResolutionTime { get; }
    
    /// <summary>Any errors encountered during resolution.</summary>
    IReadOnlyList<DependencyResolutionError> Errors { get; }
    
    /// <summary>Recommendations for improving dependency resolution.</summary>
    IReadOnlyList<string> Recommendations { get; }
    
    /// <summary>Cache hit information for the resolution.</summary>
    DependencyCacheInfo CacheInfo { get; }
}

/// <summary>
/// Metrics for dependency resolution operations.
/// </summary>
public interface IDependencyResolutionMetrics
{
    /// <summary>Total number of dependencies resolved.</summary>
    long TotalDependenciesResolved { get; }
    
    /// <summary>Number of dependencies currently being resolved.</summary>
    int ActiveResolutions { get; }
    
    /// <summary>Average time to resolve dependencies.</summary>
    TimeSpan AverageResolutionTime { get; }
    
    /// <summary>Dependency resolution success rate.</summary>
    double SuccessRate { get; }
    
    /// <summary>Number of circular dependencies detected.</summary>
    int CircularDependenciesDetected { get; }
    
    /// <summary>Cache hit rate for dependency resolution.</summary>
    double CacheHitRate { get; }
    
    /// <summary>Number of parallelization opportunities identified.</summary>
    int ParallelizationOpportunitiesFound { get; }
    
    /// <summary>Average complexity of analyzed dependency graphs.</summary>
    double AverageGraphComplexity { get; }
    
    /// <summary>Performance improvement from parallelization.</summary>
    double ParallelizationSpeedup { get; }
    
    /// <summary>Timestamp when metrics were last updated.</summary>
    DateTimeOffset LastUpdated { get; }
}

/// <summary>
/// Events related to dependency resolution operations.
/// </summary>
public abstract class DependencyResolutionEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();
    
    /// <summary>Timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    
    /// <summary>Pipeline identifier associated with the event.</summary>
    public Guid PipelineId { get; set; }
    
    /// <summary>Event severity level.</summary>
    public EventSeverity Severity { get; set; } = EventSeverity.Information;
    
    /// <summary>Human-readable description of the event.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Event fired when a circular dependency is detected.
/// </summary>
public class CircularDependencyDetectedEvent : DependencyResolutionEvent
{
    /// <summary>Information about the detected circular dependency.</summary>
    public ICircularDependency CircularDependency { get; set; } = null!;
    
    /// <summary>Suggested strategies for resolving the circular dependency.</summary>
    public List<CycleBreakingStrategy> SuggestedStrategies { get; set; } = new();
    
    /// <summary>Impact analysis of the circular dependency.</summary>
    public CircularDependencyImpact Impact { get; set; }
}

/// <summary>
/// Impact analysis for circular dependencies.
/// </summary>
public class CircularDependencyImpact
{
    /// <summary>Whether the cycle prevents execution.</summary>
    public bool PreventsExecution { get; set; }
    
    /// <summary>Performance impact of the circular dependency.</summary>
    public PerformanceImpact PerformanceImpact { get; set; }
    
    /// <summary>Resource utilization impact.</summary>
    public ResourceImpact ResourceImpact { get; set; }
    
    /// <summary>Estimated cost of resolving the dependency.</summary>
    public ResolutionCost ResolutionCost { get; set; }
}

/// <summary>
/// Performance impact levels.
/// </summary>
public enum PerformanceImpact
{
    /// <summary>No significant performance impact.</summary>
    None,
    
    /// <summary>Minor performance degradation.</summary>
    Minor,
    
    /// <summary>Moderate performance impact.</summary>
    Moderate,
    
    /// <summary>Significant performance degradation.</summary>
    Significant,
    
    /// <summary>Severe performance impact.</summary>
    Severe
}

/// <summary>
/// Resource utilization impact levels.
/// </summary>
public enum ResourceImpact
{
    /// <summary>No additional resource usage.</summary>
    None,
    
    /// <summary>Minor increase in resource usage.</summary>
    Minor,
    
    /// <summary>Moderate increase in resource usage.</summary>
    Moderate,
    
    /// <summary>Significant increase in resource usage.</summary>
    Significant,
    
    /// <summary>Excessive resource usage.</summary>
    Excessive
}

/// <summary>
/// Cost estimation for resolving circular dependencies.
/// </summary>
public enum ResolutionCost
{
    /// <summary>No cost - easy to resolve.</summary>
    None,
    
    /// <summary>Low cost - simple changes required.</summary>
    Low,
    
    /// <summary>Medium cost - moderate restructuring needed.</summary>
    Medium,
    
    /// <summary>High cost - significant changes required.</summary>
    High,
    
    /// <summary>Very high cost - major redesign needed.</summary>
    VeryHigh
}