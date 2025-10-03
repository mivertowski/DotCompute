// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Defines the types of events that can occur during pipeline execution.
/// Used for monitoring, debugging, and telemetry collection.
/// </summary>
public enum PipelineEvent
{
    /// <summary>
    /// Pipeline execution has started.
    /// </summary>
    PipelineStarted,

    /// <summary>
    /// Pipeline execution has completed successfully.
    /// </summary>
    PipelineCompleted,

    /// <summary>
    /// Pipeline execution failed with an error.
    /// </summary>
    PipelineFailed,

    /// <summary>
    /// Pipeline execution was cancelled.
    /// </summary>
    PipelineCancelled,

    /// <summary>
    /// Pipeline execution was paused.
    /// </summary>
    PipelinePaused,

    /// <summary>
    /// Pipeline execution was resumed from a paused state.
    /// </summary>
    PipelineResumed,

    /// <summary>
    /// A pipeline stage has started executing.
    /// </summary>
    StageStarted,

    /// <summary>
    /// A pipeline stage has completed successfully.
    /// </summary>
    StageCompleted,

    /// <summary>
    /// A pipeline stage failed with an error.
    /// </summary>
    StageFailed,

    /// <summary>
    /// A pipeline stage was skipped due to conditions or errors.
    /// </summary>
    StageSkipped,

    /// <summary>
    /// A pipeline stage is being retried after a failure.
    /// </summary>
    StageRetrying,

    /// <summary>
    /// A kernel within a stage has started executing.
    /// </summary>
    KernelStarted,

    /// <summary>
    /// A kernel within a stage has completed successfully.
    /// </summary>
    KernelCompleted,

    /// <summary>
    /// A kernel within a stage failed with an error.
    /// </summary>
    KernelFailed,

    /// <summary>
    /// Memory allocation has occurred for a pipeline operation.
    /// </summary>
    MemoryAllocated,

    /// <summary>
    /// Memory has been deallocated after a pipeline operation.
    /// </summary>
    MemoryDeallocated,

    /// <summary>
    /// Data transfer between host and device has started.
    /// </summary>
    DataTransferStarted,

    /// <summary>
    /// Data transfer between host and device has completed.
    /// </summary>
    DataTransferCompleted,

    /// <summary>
    /// Data transfer between host and device has failed.
    /// </summary>
    DataTransferFailed,

    /// <summary>
    /// Pipeline optimization has started.
    /// </summary>
    OptimizationStarted,

    /// <summary>
    /// Pipeline optimization has completed.
    /// </summary>
    OptimizationCompleted,

    /// <summary>
    /// Pipeline optimization has failed.
    /// </summary>
    OptimizationFailed,

    /// <summary>
    /// Cache hit occurred for a pipeline stage or kernel.
    /// </summary>
    CacheHit,

    /// <summary>
    /// Cache miss occurred for a pipeline stage or kernel.
    /// </summary>
    CacheMiss,

    /// <summary>
    /// Backend selection has occurred for a pipeline stage.
    /// </summary>
    BackendSelected,

    /// <summary>
    /// Backend fallback has occurred due to unavailability or failure.
    /// </summary>
    BackendFallback,

    /// <summary>
    /// Resource allocation has occurred for pipeline execution.
    /// </summary>
    ResourceAllocated,

    /// <summary>
    /// Resource deallocation has occurred after pipeline execution.
    /// </summary>
    ResourceDeallocated,

    /// <summary>
    /// Synchronization point has been reached in parallel execution.
    /// </summary>
    SynchronizationPoint,

    /// <summary>
    /// Load balancing has been triggered for parallel execution.
    /// </summary>
    LoadBalancingTriggered,

    /// <summary>
    /// Performance threshold has been exceeded (warning).
    /// </summary>
    PerformanceThresholdExceeded,

    /// <summary>
    /// Memory threshold has been exceeded (warning).
    /// </summary>
    MemoryThresholdExceeded,

    /// <summary>
    /// Deadlock has been detected in pipeline execution.
    /// </summary>
    DeadlockDetected,

    /// <summary>
    /// Pipeline validation has started.
    /// </summary>
    ValidationStarted,

    /// <summary>
    /// Pipeline validation has completed successfully.
    /// </summary>
    ValidationCompleted,

    /// <summary>
    /// Pipeline validation has failed.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// Debug checkpoint has been reached during execution.
    /// </summary>
    DebugCheckpoint,

    /// <summary>
    /// Profiling data has been collected for a pipeline operation.
    /// </summary>
    ProfilingDataCollected,

    /// <summary>
    /// Kernel compilation has started.
    /// </summary>
    KernelCompilationStarted,

    /// <summary>
    /// Kernel compilation has completed successfully.
    /// </summary>
    KernelCompilationCompleted,

    /// <summary>
    /// Kernel compilation has failed.
    /// </summary>
    KernelCompilationFailed,

    /// <summary>
    /// Custom user-defined event for extensibility.
    /// </summary>
    CustomEvent
}
