namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Defines the actions to take when an error occurs during pipeline execution
/// </summary>
public enum ErrorHandlingAction
{
    /// <summary>
    /// Stop pipeline execution and propagate the error
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Continue pipeline execution, skipping the failed stage
    /// </summary>
    Continue = 1,

    /// <summary>
    /// Retry the failed stage
    /// </summary>
    Retry = 2,

    /// <summary>
    /// Use a fallback implementation
    /// </summary>
    Fallback = 3,

    /// <summary>
    /// Log the error and continue
    /// </summary>
    LogAndContinue = 4,

    /// <summary>
    /// Pause pipeline execution for manual intervention
    /// </summary>
    Pause = 5,

    /// <summary>
    /// Restart the entire pipeline
    /// </summary>
    Restart = 6,

    /// <summary>
    /// Use default values and continue
    /// </summary>
    UseDefault = 7,

    /// <summary>
    /// Skip this stage and continue with the next
    /// </summary>
    Skip = 8,

    /// <summary>
    /// Rollback to a previous checkpoint
    /// </summary>
    Rollback = 9,

    /// <summary>
    /// Ignore the error and proceed without any action
    /// </summary>
    Ignored = 10,

    /// <summary>
    /// Abort the entire pipeline execution immediately
    /// </summary>
    Abort = 11
}