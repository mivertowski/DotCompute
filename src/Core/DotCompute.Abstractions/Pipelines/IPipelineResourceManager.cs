// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Manages resource allocation and backend selection for pipeline stages with intelligent optimization.
/// Provides comprehensive resource management including compute backends, memory, and hardware accelerators.
/// </summary>
public interface IPipelineResourceManager : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this resource manager instance.
    /// </summary>
    Guid ManagerId { get; }
    
    /// <summary>
    /// Gets current resource utilization metrics.
    /// </summary>
    IResourceUtilizationMetrics UtilizationMetrics { get; }
    
    /// <summary>
    /// Gets the current status of the resource manager.
    /// </summary>
    ResourceManagerStatus Status { get; }

    #region Resource Allocation
    
    /// <summary>
    /// Allocates optimal resources for pipeline execution based on workload analysis.
    /// </summary>
    /// <param name="pipeline">The pipeline requiring resource allocation</param>
    /// <param name="constraints">Resource constraints and requirements</param>
    /// <param name="cancellationToken">Cancellation token for the allocation operation</param>
    /// <returns>Resource allocation plan with assigned compute resources</returns>
    Task<IResourceAllocation> AllocateResourcesAsync(
        IKernelPipeline pipeline,
        ResourceConstraints constraints,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Allocates resources for streaming pipeline execution with continuous resource management.
    /// </summary>
    /// <param name="pipeline">The streaming pipeline requiring resources</param>
    /// <param name="streamingConstraints">Streaming-specific resource constraints</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming resource allocation with dynamic scaling capabilities</returns>
    Task<IStreamingResourceAllocation> AllocateStreamingResourcesAsync(
        IKernelPipeline pipeline,
        StreamingResourceConstraints streamingConstraints,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pre-allocates resources for anticipated pipeline executions to reduce startup latency.
    /// </summary>
    /// <param name="allocationProfile">Profile of expected resource needs</param>
    /// <param name="preAllocationOptions">Configuration for pre-allocation behavior</param>
    /// <returns>Pre-allocated resource pool for fast pipeline startup</returns>
    Task<IPreAllocatedResourcePool> PreAllocateResourcesAsync(
        ResourceAllocationProfile allocationProfile,
        PreAllocationOptions preAllocationOptions);
    
    /// <summary>
    /// Releases allocated resources back to the resource pool.
    /// </summary>
    /// <param name="allocation">The resource allocation to release</param>
    /// <param name="releaseMode">How to release the resources</param>
    /// <returns>Task representing the resource release operation</returns>
    Task ReleaseResourcesAsync(
        IResourceAllocation allocation, 
        ResourceReleaseMode releaseMode = ResourceReleaseMode.Immediate);
    
    #endregion

    #region Backend Selection and Management
    
    /// <summary>
    /// Selects optimal backends for each stage of a pipeline execution.
    /// </summary>
    /// <param name="pipeline">The pipeline requiring backend selection</param>
    /// <param name="resources">Available resource allocation</param>
    /// <returns>Backend allocation plan mapping stages to optimal backends</returns>
    Task<IBackendAllocationPlan> SelectBackendsAsync(
        IKernelPipeline pipeline,
        IResourceAllocation resources);
    
    /// <summary>
    /// Creates a load balancer for distributing work across multiple backend instances.
    /// </summary>
    /// <param name="plan">The backend allocation plan to load balance</param>
    /// <param name="loadBalancingStrategy">Strategy for distributing workload</param>
    /// <returns>Load balancer for optimal work distribution</returns>
    Task<ILoadBalancer> CreateLoadBalancerAsync(
        IBackendAllocationPlan plan,
        ILoadBalancingStrategy loadBalancingStrategy);
    
    /// <summary>
    /// Gets information about all available compute backends and their capabilities.
    /// </summary>
    /// <returns>Collection of available backends with their current status</returns>
    Task<IReadOnlyList<IBackendInfo>> GetAvailableBackendsAsync();
    
    /// <summary>
    /// Monitors backend health and performance in real-time.
    /// </summary>
    /// <param name="backends">Specific backends to monitor, or null for all</param>
    /// <param name="monitoringOptions">Configuration for backend monitoring</param>
    /// <returns>Backend monitor for health tracking</returns>
    Task<IBackendMonitor> StartBackendMonitoringAsync(
        IEnumerable<string>? backends = null,
        BackendMonitoringOptions? monitoringOptions = null);
    
    #endregion

    #region Resource Contention and Optimization
    
    /// <summary>
    /// Handles resource contention between competing pipeline executions.
    /// </summary>
    /// <param name="contentionEvent">Information about the resource contention</param>
    /// <returns>True if contention was successfully resolved</returns>
    Task<bool> HandleResourceContentionAsync(ResourceContentionEvent contentionEvent);
    
    /// <summary>
    /// Optimizes resource allocation based on historical usage patterns and performance data.
    /// </summary>
    /// <param name="optimizationContext">Context for resource optimization</param>
    /// <returns>Optimized resource allocation strategy</returns>
    Task<IResourceOptimizationResult> OptimizeResourceAllocationAsync(
        ResourceOptimizationContext optimizationContext);
    
    /// <summary>
    /// Performs predictive resource scaling based on workload forecasting.
    /// </summary>
    /// <param name="workloadForecast">Predicted future resource needs</param>
    /// <param name="scalingPolicy">Policy for resource scaling decisions</param>
    /// <returns>Resource scaling plan for anticipated workload</returns>
    Task<IResourceScalingPlan> PredictiveResourceScalingAsync(
        IWorkloadForecast workloadForecast,
        IResourceScalingPolicy scalingPolicy);
    
    /// <summary>
    /// Creates a resource reservation for guaranteed availability during critical operations.
    /// </summary>
    /// <param name="reservationRequest">Details of the resource reservation needed</param>
    /// <param name="reservationDuration">How long to hold the reservation</param>
    /// <returns>Resource reservation handle</returns>
    Task<IResourceReservation> CreateResourceReservationAsync(
        ResourceReservationRequest reservationRequest,
        TimeSpan reservationDuration);
    
    #endregion

    #region Resource Monitoring and Analytics
    
    /// <summary>
    /// Gets an observable stream of resource utilization events for real-time monitoring.
    /// </summary>
    IObservable<ResourceUtilizationEvent> ResourceEvents { get; }
    
    /// <summary>
    /// Gets an observable stream of backend performance events.
    /// </summary>
    IObservable<BackendPerformanceEvent> BackendEvents { get; }
    
    /// <summary>
    /// Monitors resource usage patterns and provides optimization recommendations.
    /// </summary>
    /// <param name="monitoringOptions">Configuration for resource monitoring</param>
    /// <returns>Resource monitoring service with analytics</returns>
    Task<IResourceMonitor> StartResourceMonitoringAsync(
        IResourceMonitoringOptions monitoringOptions);
    
    /// <summary>
    /// Analyzes resource usage patterns to identify optimization opportunities.
    /// </summary>
    /// <param name="analysisOptions">Configuration for resource analysis</param>
    /// <returns>Resource usage analysis with recommendations</returns>
    Task<IResourceUsageAnalysis> AnalyzeResourceUsageAsync(
        ResourceAnalysisOptions analysisOptions);
    
    /// <summary>
    /// Generates comprehensive resource utilization reports.
    /// </summary>
    /// <param name="reportOptions">Configuration for report generation</param>
    /// <returns>Detailed resource utilization report</returns>
    Task<IResourceUtilizationReport> GenerateUtilizationReportAsync(
        ResourceReportOptions reportOptions);
    
    #endregion

    #region Configuration and Policies
    
    /// <summary>
    /// Configures resource allocation policies for different types of workloads.
    /// </summary>
    /// <param name="allocationPolicy">Resource allocation policy configuration</param>
    /// <returns>Resource allocation manager with the configured policy</returns>
    Task<IResourceAllocationManager> ConfigureResourceAllocationAsync(
        IResourceAllocationPolicy allocationPolicy);
    
    /// <summary>
    /// Sets up resource quotas and limits for different user groups or applications.
    /// </summary>
    /// <param name="quotaConfiguration">Resource quota configuration</param>
    /// <returns>Resource quota manager</returns>
    Task<IResourceQuotaManager> ConfigureResourceQuotasAsync(
        ResourceQuotaConfiguration quotaConfiguration);
    
    /// <summary>
    /// Configures automatic resource cleanup policies for unused allocations.
    /// </summary>
    /// <param name="cleanupPolicy">Resource cleanup policy configuration</param>
    /// <returns>Resource cleanup manager</returns>
    Task<IResourceCleanupManager> ConfigureResourceCleanupAsync(
        IResourceCleanupPolicy cleanupPolicy);
    
    /// <summary>
    /// Gets the current resource management configuration.
    /// </summary>
    /// <returns>Current resource manager configuration</returns>
    Task<IResourceManagerConfiguration> GetConfigurationAsync();
    
    #endregion

    #region Advanced Resource Management
    
    /// <summary>
    /// Creates a resource manager for multi-tenant environments with isolation.
    /// </summary>
    /// <param name="tenantConfiguration">Configuration for tenant resource isolation</param>
    /// <returns>Multi-tenant resource manager</returns>
    Task<IMultiTenantResourceManager> CreateMultiTenantResourceManagerAsync(
        ITenantConfiguration tenantConfiguration);
    
    /// <summary>
    /// Creates a distributed resource manager for cross-node resource coordination.
    /// </summary>
    /// <param name="distributedConfiguration">Configuration for distributed resource management</param>
    /// <returns>Distributed resource coordinator</returns>
    Task<IDistributedResourceManager> CreateDistributedResourceManagerAsync(
        IDistributedResourceConfiguration distributedConfiguration);
    
    /// <summary>
    /// Creates resource manager with automatic failover capabilities.
    /// </summary>
    /// <param name="failoverConfiguration">Configuration for resource failover</param>
    /// <returns>Failover-enabled resource manager</returns>
    Task<IFailoverResourceManager> CreateFailoverResourceManagerAsync(
        IFailoverConfiguration failoverConfiguration);
    
    /// <summary>
    /// Creates a resource manager with machine learning-based optimization.
    /// </summary>
    /// <param name="mlConfiguration">Configuration for ML-based resource optimization</param>
    /// <returns>ML-optimized resource manager</returns>
    Task<IMLResourceManager> CreateMLResourceManagerAsync(
        IMLResourceConfiguration mlConfiguration);
    
    #endregion
}

/// <summary>
/// Status of the resource manager and its operational state.
/// </summary>
public enum ResourceManagerStatus
{
    /// <summary>Resource manager is initializing.</summary>
    Initializing,
    
    /// <summary>Resource manager is ready and operational.</summary>
    Ready,
    
    /// <summary>Resource manager is under high load but functional.</summary>
    HighLoad,
    
    /// <summary>Resource manager is experiencing resource pressure.</summary>
    ResourcePressure,
    
    /// <summary>Resource manager is in maintenance mode.</summary>
    Maintenance,
    
    /// <summary>Resource manager is degraded but still functional.</summary>
    Degraded,
    
    /// <summary>Resource manager has failed and needs attention.</summary>
    Failed,
    
    /// <summary>Resource manager is shutting down.</summary>
    ShuttingDown
}

/// <summary>
/// Resource constraints and requirements for pipeline execution.
/// </summary>
public class ResourceConstraints
{
    /// <summary>Maximum memory that can be allocated for the pipeline.</summary>
    public long? MaxMemoryUsage { get; set; }
    
    /// <summary>Maximum CPU cores that can be used.</summary>
    public int? MaxCpuCores { get; set; }
    
    /// <summary>Maximum GPU memory that can be allocated.</summary>
    public long? MaxGpuMemory { get; set; }
    
    /// <summary>Required compute capabilities (e.g., CUDA compute capability).</summary>
    public ComputeCapabilityRequirements? ComputeRequirements { get; set; }
    
    /// <summary>Backends that are explicitly disallowed.</summary>
    public HashSet<string> DisallowedBackends { get; set; } = new();
    
    /// <summary>Backends that are required if available.</summary>
    public HashSet<string> RequiredBackends { get; set; } = new();
    
    /// <summary>Priority level for resource allocation.</summary>
    public ResourcePriority Priority { get; set; } = ResourcePriority.Normal;
    
    /// <summary>Maximum execution time before resource reclamation.</summary>
    public TimeSpan? MaxExecutionTime { get; set; }
    
    /// <summary>Whether to allow shared resource usage with other pipelines.</summary>
    public bool AllowSharedResources { get; set; } = true;
    
    /// <summary>Custom resource requirements specific to the application.</summary>
    public Dictionary<string, object> CustomRequirements { get; set; } = new();
}

/// <summary>
/// Specialized resource constraints for streaming pipeline operations.
/// </summary>
public class StreamingResourceConstraints : ResourceConstraints
{
    /// <summary>Maximum acceptable latency for resource allocation.</summary>
    public TimeSpan? MaxAllocationLatency { get; set; }
    
    /// <summary>Required minimum throughput for resource allocation.</summary>
    public double? MinThroughput { get; set; }
    
    /// <summary>Whether resources should scale automatically with load.</summary>
    public bool EnableAutoScaling { get; set; } = true;
    
    /// <summary>Configuration for auto-scaling behavior.</summary>
    public AutoScalingConfiguration? AutoScalingConfig { get; set; }
    
    /// <summary>Buffer size requirements for streaming operations.</summary>
    public StreamingBufferRequirements? BufferRequirements { get; set; }
}

/// <summary>
/// Priority levels for resource allocation decisions.
/// </summary>
public enum ResourcePriority
{
    /// <summary>Lowest priority - use leftover resources.</summary>
    Low = 1,
    
    /// <summary>Normal priority - standard resource allocation.</summary>
    Normal = 5,
    
    /// <summary>High priority - prioritized resource allocation.</summary>
    High = 8,
    
    /// <summary>Critical priority - reserved resources if available.</summary>
    Critical = 10,
    
    /// <summary>Emergency priority - preempt other allocations if necessary.</summary>
    Emergency = 15
}

/// <summary>
/// Requirements for compute capabilities (hardware features).
/// </summary>
public class ComputeCapabilityRequirements
{
    /// <summary>Minimum CUDA compute capability required.</summary>
    public Version? MinCudaComputeCapability { get; set; }
    
    /// <summary>Required SIMD instruction sets (e.g., AVX2, AVX512).</summary>
    public HashSet<string> RequiredSimdInstructions { get; set; } = new();
    
    /// <summary>Minimum memory bandwidth required (GB/s).</summary>
    public double? MinMemoryBandwidth { get; set; }
    
    /// <summary>Required hardware features.</summary>
    public HashSet<string> RequiredHardwareFeatures { get; set; } = new();
    
    /// <summary>Minimum compute units (cores, CUs, etc.) required.</summary>
    public int? MinComputeUnits { get; set; }
}

/// <summary>
/// Configuration for automatic resource scaling.
/// </summary>
public class AutoScalingConfiguration
{
    /// <summary>Minimum number of resources to maintain.</summary>
    public int MinResources { get; set; } = 1;
    
    /// <summary>Maximum number of resources to scale to.</summary>
    public int MaxResources { get; set; } = 16;
    
    /// <summary>CPU utilization threshold to trigger scale-up.</summary>
    public double ScaleUpThreshold { get; set; } = 0.8;
    
    /// <summary>CPU utilization threshold to trigger scale-down.</summary>
    public double ScaleDownThreshold { get; set; } = 0.3;
    
    /// <summary>Time window for measuring utilization.</summary>
    public TimeSpan EvaluationWindow { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>Delay before scaling actions to avoid flapping.</summary>
    public TimeSpan ScalingDelay { get; set; } = TimeSpan.FromMinutes(2);
    
    /// <summary>Scale-up increment (number of resources to add).</summary>
    public int ScaleUpIncrement { get; set; } = 1;
    
    /// <summary>Scale-down increment (number of resources to remove).</summary>
    public int ScaleDownIncrement { get; set; } = 1;
}

/// <summary>
/// Resource allocation result with assigned compute resources.
/// </summary>
public interface IResourceAllocation
{
    /// <summary>Unique identifier for this resource allocation.</summary>
    Guid AllocationId { get; }
    
    /// <summary>Pipeline this allocation was created for.</summary>
    IKernelPipeline Pipeline { get; }
    
    /// <summary>Allocated compute backends with their assignments.</summary>
    IReadOnlyDictionary<string, IAllocatedBackend> AllocatedBackends { get; }
    
    /// <summary>Total memory allocated across all backends.</summary>
    long TotalMemoryAllocated { get; }
    
    /// <summary>Total CPU cores allocated.</summary>
    int TotalCpuCoresAllocated { get; }
    
    /// <summary>GPU resources allocated, if any.</summary>
    IReadOnlyList<IAllocatedGpuResource> GpuResources { get; }
    
    /// <summary>Current utilization of allocated resources.</summary>
    IResourceUtilizationSnapshot CurrentUtilization { get; }
    
    /// <summary>Timestamp when the allocation was created.</summary>
    DateTimeOffset AllocationTime { get; }
    
    /// <summary>Expected duration of the allocation.</summary>
    TimeSpan? ExpectedDuration { get; }
    
    /// <summary>Priority of this resource allocation.</summary>
    ResourcePriority Priority { get; }
    
    /// <summary>Checks if the allocation is still valid and active.</summary>
    bool IsValid { get; }
    
    /// <summary>Extends the allocation duration if possible.</summary>
    Task<bool> ExtendAsync(TimeSpan extension);
    
    /// <summary>Gets detailed metrics for this allocation.</summary>
    Task<IAllocationMetrics> GetMetricsAsync();
}

/// <summary>
/// Information about an allocated compute backend.
/// </summary>
public interface IAllocatedBackend
{
    /// <summary>Name/identifier of the backend.</summary>
    string BackendName { get; }
    
    /// <summary>Type of backend (CPU, CUDA, etc.).</summary>
    string BackendType { get; }
    
    /// <summary>Allocated resources on this backend.</summary>
    IBackendResourceAllocation ResourceAllocation { get; }
    
    /// <summary>Current status of the backend allocation.</summary>
    BackendAllocationStatus Status { get; }
    
    /// <summary>Performance characteristics of the allocated backend.</summary>
    IBackendPerformanceCharacteristics PerformanceCharacteristics { get; }
    
    /// <summary>Usage statistics for this backend allocation.</summary>
    IBackendUsageStatistics UsageStatistics { get; }
}

/// <summary>
/// Status of an individual backend allocation.
/// </summary>
public enum BackendAllocationStatus
{
    /// <summary>Allocation is being prepared.</summary>
    Allocating,
    
    /// <summary>Allocation is ready for use.</summary>
    Ready,
    
    /// <summary>Allocation is currently in use.</summary>
    Active,
    
    /// <summary>Allocation is temporarily unavailable.</summary>
    Unavailable,
    
    /// <summary>Allocation has failed.</summary>
    Failed,
    
    /// <summary>Allocation is being released.</summary>
    Releasing,
    
    /// <summary>Allocation has been released.</summary>
    Released
}

/// <summary>
/// Backend allocation plan mapping pipeline stages to optimal backends.
/// </summary>
public interface IBackendAllocationPlan
{
    /// <summary>Unique identifier for this allocation plan.</summary>
    Guid PlanId { get; }
    
    /// <summary>Pipeline this plan was created for.</summary>
    IKernelPipeline Pipeline { get; }
    
    /// <summary>Mapping of pipeline stages to selected backends.</summary>
    IReadOnlyDictionary<string, string> StageBackendMapping { get; }
    
    /// <summary>Resource allocation for each backend.</summary>
    IResourceAllocation ResourceAllocation { get; }
    
    /// <summary>Load balancing configuration for parallel stages.</summary>
    ILoadBalancingConfiguration LoadBalancingConfig { get; }
    
    /// <summary>Estimated performance characteristics of the plan.</summary>
    IPlanPerformanceEstimate PerformanceEstimate { get; }
    
    /// <summary>Validates the allocation plan for correctness.</summary>
    Task<IAllocationPlanValidationResult> ValidateAsync();
    
    /// <summary>Optimizes the allocation plan for better performance.</summary>
    Task<IBackendAllocationPlan> OptimizeAsync(OptimizationGoals goals);
}

/// <summary>
/// Resource contention event information.
/// </summary>
public class ResourceContentionEvent
{
    /// <summary>Unique identifier for the contention event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();
    
    /// <summary>Timestamp when contention was detected.</summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    
    /// <summary>Type of resource experiencing contention.</summary>
    public ResourceType ResourceType { get; set; }
    
    /// <summary>Competing allocations causing the contention.</summary>
    public List<Guid> CompetingAllocations { get; set; } = new();
    
    /// <summary>Severity of the resource contention.</summary>
    public ContentionSeverity Severity { get; set; }
    
    /// <summary>Current utilization level of the contended resource.</summary>
    public double UtilizationLevel { get; set; }
    
    /// <summary>Suggested resolution strategies.</summary>
    public List<ContentionResolutionStrategy> SuggestedResolutions { get; set; } = new();
    
    /// <summary>Additional context about the contention.</summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Types of resources that can experience contention.
/// </summary>
public enum ResourceType
{
    /// <summary>CPU processing resources.</summary>
    Cpu,
    
    /// <summary>System memory resources.</summary>
    Memory,
    
    /// <summary>GPU compute resources.</summary>
    GpuCompute,
    
    /// <summary>GPU memory resources.</summary>
    GpuMemory,
    
    /// <summary>Memory bandwidth resources.</summary>
    MemoryBandwidth,
    
    /// <summary>Network bandwidth resources.</summary>
    NetworkBandwidth,
    
    /// <summary>Storage I/O resources.</summary>
    StorageIO,
    
    /// <summary>Custom application-specific resources.</summary>
    Custom
}

/// <summary>
/// Severity levels for resource contention.
/// </summary>
public enum ContentionSeverity
{
    /// <summary>Minor contention with minimal impact.</summary>
    Minor,
    
    /// <summary>Moderate contention affecting performance.</summary>
    Moderate,
    
    /// <summary>Severe contention significantly impacting performance.</summary>
    Severe,
    
    /// <summary>Critical contention preventing proper operation.</summary>
    Critical
}

/// <summary>
/// Strategies for resolving resource contention.
/// </summary>
public enum ContentionResolutionStrategy
{
    /// <summary>Queue competing requests in priority order.</summary>
    Queue,
    
    /// <summary>Scale up resources if possible.</summary>
    ScaleUp,
    
    /// <summary>Migrate workload to different backend.</summary>
    Migrate,
    
    /// <summary>Throttle lower priority requests.</summary>
    Throttle,
    
    /// <summary>Preempt lower priority allocations.</summary>
    Preempt,
    
    /// <summary>Split workload across multiple backends.</summary>
    Split,
    
    /// <summary>Defer execution until resources are available.</summary>
    Defer,
    
    /// <summary>Notify user of resource unavailability.</summary>
    Notify
}

/// <summary>
/// Current resource utilization metrics across all managed resources.
/// </summary>
public interface IResourceUtilizationMetrics
{
    /// <summary>Current CPU utilization percentage (0.0 to 1.0).</summary>
    double CpuUtilization { get; }
    
    /// <summary>Current memory utilization percentage (0.0 to 1.0).</summary>
    double MemoryUtilization { get; }
    
    /// <summary>Current GPU utilization percentage (0.0 to 1.0).</summary>
    double GpuUtilization { get; }
    
    /// <summary>Total memory allocated in bytes.</summary>
    long TotalMemoryAllocated { get; }
    
    /// <summary>Total memory available in bytes.</summary>
    long TotalMemoryAvailable { get; }
    
    /// <summary>Number of active resource allocations.</summary>
    int ActiveAllocations { get; }
    
    /// <summary>Number of pending resource requests.</summary>
    int PendingRequests { get; }
    
    /// <summary>Current throughput in operations per second.</summary>
    double CurrentThroughput { get; }
    
    /// <summary>Average resource allocation time.</summary>
    TimeSpan AverageAllocationTime { get; }
    
    /// <summary>Number of resource contention events in the last period.</summary>
    int RecentContentionEvents { get; }
    
    /// <summary>Efficiency rating of resource utilization (0.0 to 1.0).</summary>
    double UtilizationEfficiency { get; }
    
    /// <summary>Timestamp when metrics were last updated.</summary>
    DateTimeOffset LastUpdated { get; }
}