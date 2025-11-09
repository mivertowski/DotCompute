// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Execution;

namespace DotCompute.Abstractions.Pipelines.Configuration;

/// <summary>
/// Comprehensive configuration for pipeline behavior and execution.
/// </summary>
/// <remarks>
/// <para>
/// Defines the complete configuration contract for pipeline execution including
/// naming, versioning, timeouts, priorities, caching, profiling, error handling,
/// and resource allocation preferences.
/// </para>
/// <para>
/// Implementations of this interface should be immutable once created to ensure
/// configuration consistency throughout pipeline execution.
/// </para>
/// </remarks>
public interface IPipelineConfiguration
{
    /// <summary>Unique name for the pipeline.</summary>
    public string Name { get; }

    /// <summary>Description of the pipeline's purpose.</summary>
    public string? Description { get; }

    /// <summary>Version of the pipeline configuration.</summary>
    public Version Version { get; }

    /// <summary>Global timeout for the entire pipeline execution.</summary>
    public TimeSpan? GlobalTimeout { get; }

    /// <summary>Default execution priority for all stages.</summary>
    public ExecutionPriority DefaultPriority { get; }

    /// <summary>Whether to enable pipeline-wide caching.</summary>
    public bool EnableCaching { get; }

    /// <summary>Whether to enable detailed performance profiling.</summary>
    public bool EnableProfiling { get; }

    /// <summary>Error handling strategy for the pipeline.</summary>
    public ErrorHandlingStrategy ErrorHandling { get; }

    /// <summary>Resource allocation preferences.</summary>
    public ResourceAllocationPreferences ResourcePreferences { get; }

    /// <summary>Pipeline-specific metadata.</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Configuration options for individual pipeline stages.
/// </summary>
/// <remarks>
/// <para>
/// Provides fine-grained control over stage execution including backend selection,
/// timeouts, priority, parallelization, memory optimization, and retry behavior.
/// </para>
/// <para>
/// These options can override pipeline-level defaults for specific stages that
/// have unique requirements.
/// </para>
/// </remarks>
public class PipelineStageOptions
{
    /// <summary>Preferred backend for this stage execution.</summary>
    public string? PreferredBackend { get; set; }

    /// <summary>Maximum execution time for this stage.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Priority level for resource allocation.</summary>
    public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;

    /// <summary>Whether this stage can be executed in parallel with others.</summary>
    public bool AllowParallelExecution { get; set; } = true;

    /// <summary>Memory allocation hints for this stage.</summary>
    public MemoryAllocationHints? MemoryHints { get; set; }

    /// <summary>Custom metadata for stage configuration.</summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>Whether to enable detailed profiling for this stage.</summary>
    public bool EnableProfiling { get; set; }

    /// <summary>Retry configuration for stage execution.</summary>
    public RetryConfiguration? RetryConfig { get; set; }

    /// <summary>Whether to enable optimization for this stage.</summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>Whether to enable memory optimization for this stage.</summary>
    public bool EnableMemoryOptimization { get; set; } = true;
}

/// <summary>
/// Memory allocation hints for optimizing stage execution.
/// </summary>
/// <remarks>
/// <para>
/// Provides guidance to the pipeline infrastructure about expected memory usage patterns,
/// enabling proactive optimization and resource allocation.
/// </para>
/// <para>
/// These hints are advisory and may not always be honored depending on
/// system constraints and runtime conditions.
/// </para>
/// </remarks>
public class MemoryAllocationHints
{
    /// <summary>Expected memory usage in bytes.</summary>
    public long? ExpectedMemoryUsage { get; set; }

    /// <summary>Whether the stage benefits from memory pooling.</summary>
    public bool PreferMemoryPooling { get; set; } = true;

    /// <summary>Preferred memory location (host, device, unified).</summary>
    public MemoryLocation PreferredLocation { get; set; } = MemoryLocation.Host;

    // TODO: Define missing type
    /* <summary>Memory access pattern for optimization.</summary>
    public MemoryAccessPattern AccessPattern { get; set; } = MemoryAccessPattern.Sequential; */

    /// <summary>Whether the stage can work with pinned memory.</summary>
    public bool SupportsPinnedMemory { get; set; }
}

/// <summary>
/// Retry configuration for handling transient failures.
/// </summary>
/// <remarks>
/// <para>
/// Defines retry behavior including maximum attempts, delay strategies, and
/// custom retry conditions.
/// </para>
/// <para>
/// Supports exponential backoff with jitter to prevent thundering herd problems
/// in distributed pipeline scenarios.
/// </para>
/// </remarks>
public class RetryConfiguration
{
    /// <summary>Maximum number of retry attempts.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay between retry attempts.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Backoff strategy for increasing delays.</summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>Maximum delay between retries.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Condition to determine if an exception should trigger retry.</summary>
    public Func<Exception, bool>? ShouldRetry { get; set; }

    /// <summary>Action to execute before each retry attempt.</summary>
    public Func<int, Exception, Task>? OnRetry { get; set; }
}

/// <summary>
/// Backoff strategies for retry delays.
/// </summary>
/// <remarks>
/// Different strategies provide varying tradeoffs between retry aggressiveness
/// and system load distribution.
/// </remarks>
public enum BackoffStrategy
{
    /// <summary>Fixed delay between retries.</summary>
    Fixed,

    /// <summary>Linearly increasing delay.</summary>
    Linear,

    /// <summary>Exponentially increasing delay.</summary>
    Exponential,

    /// <summary>Exponential with jitter to prevent thundering herd.</summary>
    ExponentialWithJitter
}

/// <summary>
/// Error handling strategies for pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// Defines how the pipeline should respond to errors during execution.
/// Different strategies are appropriate for different reliability and performance requirements.
/// </para>
/// </remarks>
public enum ErrorHandlingStrategy
{
    /// <summary>Stop pipeline execution on first error.</summary>
    StopOnFirstError,

    /// <summary>Continue execution and collect all errors.</summary>
    ContinueOnError,

    /// <summary>Attempt to recover from errors automatically.</summary>
    AutoRecover,

    /// <summary>Use custom error handling logic.</summary>
    Custom,

    /// <summary>Retry the failed operation.</summary>
    Retry,

    /// <summary>Fall back to a default value and continue.</summary>
    Fallback
}

/// <summary>
/// Resource allocation preferences for pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// Specifies preferences for compute backend selection, memory limits,
/// CPU core allocation, GPU acceleration, and resource sharing policies.
/// </para>
/// <para>
/// These preferences guide but do not strictly enforce resource allocation,
/// as actual allocation depends on system availability.
/// </para>
/// </remarks>
public class ResourceAllocationPreferences
{
    /// <summary>Preferred compute backends in order of preference.</summary>
    public IList<string> PreferredBackends { get; } = [];

    /// <summary>Maximum memory usage for the pipeline.</summary>
    public long? MaxMemoryUsage { get; set; }

    /// <summary>Maximum CPU cores to use.</summary>
    public int? MaxCpuCores { get; set; }

    /// <summary>Whether to allow GPU acceleration.</summary>
    public bool AllowGpuAcceleration { get; set; } = true;

    /// <summary>Resource sharing policy with other pipelines.</summary>
    public ResourceSharingPolicy SharingPolicy { get; set; } = ResourceSharingPolicy.Fair;
}

/// <summary>
/// Resource sharing policies for multiple concurrent pipelines.
/// </summary>
/// <remarks>
/// Determines how resources are allocated when multiple pipelines
/// compete for the same compute resources.
/// </remarks>
public enum ResourceSharingPolicy
{
    /// <summary>Fair sharing of resources among all pipelines.</summary>
    Fair,

    /// <summary>Priority-based resource allocation.</summary>
    PriorityBased,

    /// <summary>Exclusive resource usage when possible.</summary>
    Exclusive,

    /// <summary>Adaptive sharing based on workload characteristics.</summary>
    Adaptive
}
