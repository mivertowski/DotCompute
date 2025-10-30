namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Defines the types of events that can occur during pipeline execution
/// </summary>
public enum PipelineEventType
{
    /// <summary>
    /// Pipeline execution started
    /// </summary>
    Started = 0,

    /// <summary>
    /// Stage execution started
    /// </summary>
    StageStarted = 1,

    /// <summary>
    /// Stage execution completed successfully
    /// </summary>
    StageCompleted = 2,

    /// <summary>
    /// Stage execution failed
    /// </summary>
    StageFailed = 3,

    /// <summary>
    /// Pipeline execution completed successfully
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Pipeline execution failed
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Pipeline execution was cancelled
    /// </summary>
    Cancelled = 6,

    /// <summary>
    /// Pipeline was paused
    /// </summary>
    Paused = 7,

    /// <summary>
    /// Pipeline was resumed
    /// </summary>
    Resumed = 8,

    /// <summary>
    /// Progress update event
    /// </summary>
    Progress = 9,

    /// <summary>
    /// Warning occurred during execution
    /// </summary>
    Warning = 10,

    /// <summary>
    /// Information event
    /// </summary>
    Information = 11,

    /// <summary>
    /// Memory allocation event
    /// </summary>
    MemoryAllocated = 12,

    /// <summary>
    /// Memory deallocation event
    /// </summary>
    MemoryDeallocated = 13,

    /// <summary>
    /// Kernel compilation started
    /// </summary>
    KernelCompilationStarted = 14,

    /// <summary>
    /// Kernel compilation completed
    /// </summary>
    KernelCompilationCompleted = 15,

    /// <summary>
    /// Kernel execution started
    /// </summary>
    KernelExecutionStarted = 16,

    /// <summary>
    /// Kernel execution completed
    /// </summary>
    KernelExecutionCompleted = 17,

    /// <summary>
    /// Data transfer started
    /// </summary>
    DataTransferStarted = 18,

    /// <summary>
    /// Data transfer completed
    /// </summary>
    DataTransferCompleted = 19,

    /// <summary>
    /// Performance metric collected
    /// </summary>
    PerformanceMetric = 20,

    /// <summary>
    /// Error occurred during execution
    /// </summary>
    Error = 21,

    /// <summary>
    /// Optimization was applied
    /// </summary>
    OptimizationApplied = 22
}
