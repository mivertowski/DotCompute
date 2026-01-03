// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Kernels;

namespace DotCompute.Abstractions.Resilience;

/// <summary>
/// Circuit breaker specifically designed for GPU kernel execution.
/// Provides per-kernel and per-accelerator failure tracking with GPU-aware policies.
/// </summary>
/// <remarks>
/// Unlike general-purpose circuit breakers, this implementation:
/// - Tracks failures per kernel name and accelerator type
/// - Understands GPU-specific error categories (memory, timeout, resource exhaustion)
/// - Supports adaptive timeout based on kernel execution history
/// - Integrates with IErrorClassifier for smart recovery decisions
/// </remarks>
public interface IKernelCircuitBreaker
{
    /// <summary>
    /// Checks if kernel execution is allowed for the specified kernel and accelerator.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type (e.g., "CUDA", "Metal", "OpenCL").</param>
    /// <returns>True if execution is allowed; false if circuit is open.</returns>
    bool CanExecute(string kernelName, string acceleratorType);

    /// <summary>
    /// Executes a kernel operation with circuit breaker protection.
    /// </summary>
    /// <param name="kernelName">The kernel name for tracking.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="operation">The kernel execution operation.</param>
    /// <param name="policy">Optional kernel-specific policy override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The kernel execution result.</returns>
    Task<KernelExecutionResult> ExecuteKernelAsync(
        string kernelName,
        string acceleratorType,
        Func<CancellationToken, Task<KernelExecutionResult>> operation,
        KernelCircuitBreakerPolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a kernel operation with retry and circuit breaker protection.
    /// </summary>
    /// <param name="kernelName">The kernel name for tracking.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="operation">The kernel execution operation.</param>
    /// <param name="policy">Optional kernel-specific policy override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The kernel execution result.</returns>
    Task<KernelExecutionResult> ExecuteKernelWithRetryAsync(
        string kernelName,
        string acceleratorType,
        Func<CancellationToken, Task<KernelExecutionResult>> operation,
        KernelCircuitBreakerPolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the circuit state for a specific kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The circuit state for the kernel.</returns>
    KernelCircuitState GetKernelState(string kernelName);

    /// <summary>
    /// Gets the circuit state for a specific accelerator.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>The circuit state for the accelerator.</returns>
    KernelCircuitState GetAcceleratorState(string acceleratorType);

    /// <summary>
    /// Gets comprehensive statistics for all tracked kernels and accelerators.
    /// </summary>
    /// <returns>Circuit breaker statistics.</returns>
    KernelCircuitBreakerStatistics GetStatistics();

    /// <summary>
    /// Records a successful kernel execution.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="executionTime">The execution duration.</param>
    void RecordSuccess(string kernelName, string acceleratorType, TimeSpan executionTime);

    /// <summary>
    /// Records a failed kernel execution.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorCategory">Optional error category for smart recovery.</param>
    void RecordFailure(string kernelName, string acceleratorType, Exception exception, KernelErrorCategory? errorCategory = null);

    /// <summary>
    /// Resets the circuit state for a specific kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name to reset.</param>
    void ResetKernel(string kernelName);

    /// <summary>
    /// Resets the circuit state for a specific accelerator.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type to reset.</param>
    void ResetAccelerator(string acceleratorType);

    /// <summary>
    /// Resets all circuit states.
    /// </summary>
    void ResetAll();

    /// <summary>
    /// Forces the circuit state for a kernel (for testing/manual intervention).
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="state">The state to force.</param>
    void ForceKernelState(string kernelName, CircuitBreakerState state);

    /// <summary>
    /// Gets recommended recovery action based on failure history.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Recommended recovery action.</returns>
    KernelRecoveryRecommendation GetRecoveryRecommendation(string kernelName, string acceleratorType);
}

/// <summary>
/// Circuit breaker state enumeration.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, kernel execution flows normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, kernel execution is blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, limited kernel execution allowed to test recovery.
    /// </summary>
    HalfOpen
}

/// <summary>
/// GPU-specific error categories for intelligent circuit breaking.
/// </summary>
public enum KernelErrorCategory
{
    /// <summary>
    /// Unknown error category.
    /// </summary>
    Unknown,

    /// <summary>
    /// Transient error that may resolve on retry.
    /// </summary>
    Transient,

    /// <summary>
    /// GPU memory allocation or access error.
    /// </summary>
    MemoryError,

    /// <summary>
    /// Kernel execution timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// GPU resource exhaustion (out of resources).
    /// </summary>
    ResourceExhaustion,

    /// <summary>
    /// GPU device not ready or driver issue.
    /// </summary>
    DeviceNotReady,

    /// <summary>
    /// Kernel compilation or loading failure.
    /// </summary>
    CompilationError,

    /// <summary>
    /// Invalid kernel arguments or configuration.
    /// </summary>
    InvalidArguments,

    /// <summary>
    /// GPU hardware error.
    /// </summary>
    HardwareError,

    /// <summary>
    /// Context or stream error.
    /// </summary>
    ContextError,

    /// <summary>
    /// Permanent error that will not resolve.
    /// </summary>
    Permanent
}

/// <summary>
/// Policy configuration for kernel circuit breaker behavior.
/// </summary>
public class KernelCircuitBreakerPolicy
{
    /// <summary>
    /// Gets or sets the failure percentage threshold to open the circuit (0-100).
    /// </summary>
    public double FailureThresholdPercentage { get; set; } = 50.0;

    /// <summary>
    /// Gets or sets the consecutive failure count threshold to open the circuit.
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the minimum number of executions before failure rate is calculated.
    /// </summary>
    public int MinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for kernel execution.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long the circuit stays open before transitioning to half-open.
    /// </summary>
    public TimeSpan OpenCircuitDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the duration for half-open testing period.
    /// </summary>
    public TimeSpan HalfOpenTestDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential retry delay.
    /// </summary>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum retry delay.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to enable GPU memory monitoring.
    /// </summary>
    public bool EnableGpuMemoryMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the GPU memory threshold percentage (0-100) to trigger warnings.
    /// </summary>
    public double GpuMemoryThresholdPercentage { get; set; } = 90.0;

    /// <summary>
    /// Gets or sets whether to use adaptive timeouts based on execution history.
    /// </summary>
    public bool UseAdaptiveTimeout { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout multiplier for adaptive timeout calculation.
    /// </summary>
    public double AdaptiveTimeoutMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets error categories that should not trigger circuit opening.
    /// </summary>
    public HashSet<KernelErrorCategory> IgnoredErrorCategories { get; set; } = [];

    /// <summary>
    /// Gets or sets error categories that should immediately open the circuit.
    /// </summary>
    public HashSet<KernelErrorCategory> CriticalErrorCategories { get; set; } =
    [
        KernelErrorCategory.HardwareError,
        KernelErrorCategory.ContextError,
        KernelErrorCategory.Permanent
    ];

    /// <summary>
    /// Gets the default policy.
    /// </summary>
    public static KernelCircuitBreakerPolicy Default => new();

    /// <summary>
    /// Gets a strict policy with lower thresholds.
    /// </summary>
    public static KernelCircuitBreakerPolicy Strict => new()
    {
        FailureThresholdPercentage = 25.0,
        ConsecutiveFailureThreshold = 2,
        OpenCircuitDuration = TimeSpan.FromMinutes(2),
        MaxRetries = 2
    };

    /// <summary>
    /// Gets a lenient policy with higher thresholds.
    /// </summary>
    public static KernelCircuitBreakerPolicy Lenient => new()
    {
        FailureThresholdPercentage = 75.0,
        ConsecutiveFailureThreshold = 5,
        OpenCircuitDuration = TimeSpan.FromSeconds(30),
        MaxRetries = 5
    };
}

/// <summary>
/// State information for a kernel circuit.
/// </summary>
public class KernelCircuitState
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accelerator type.
    /// </summary>
    public string? AcceleratorType { get; set; }

    /// <summary>
    /// Gets or sets the current circuit state.
    /// </summary>
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;

    /// <summary>
    /// Gets or sets the last state change timestamp.
    /// </summary>
    public DateTimeOffset LastStateChange { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets the failure rate (0.0 to 1.0).
    /// </summary>
    public double FailureRate => TotalExecutions > 0 ? (double)FailedExecutions / TotalExecutions : 0.0;

    /// <summary>
    /// Gets or sets the number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets failure counts by error category.
    /// </summary>
    public Dictionary<KernelErrorCategory, int> FailuresByCategory { get; set; } = [];

    /// <summary>
    /// Gets or sets the last error that occurred.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the last error category.
    /// </summary>
    public KernelErrorCategory? LastErrorCategory { get; set; }

    /// <summary>
    /// Gets or sets the last error timestamp.
    /// </summary>
    public DateTimeOffset? LastErrorTime { get; set; }

    /// <summary>
    /// Gets or sets the number of times the circuit has been opened.
    /// </summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Gets or sets the total time the circuit has been open.
    /// </summary>
    public TimeSpan TotalOpenTime { get; set; }

    /// <summary>
    /// Gets or sets when the circuit can transition from Open to HalfOpen.
    /// </summary>
    public DateTimeOffset? NextRetryTime { get; set; }
}

/// <summary>
/// Statistics for kernel circuit breaker.
/// </summary>
public class KernelCircuitBreakerStatistics
{
    /// <summary>
    /// Gets or sets the global circuit state.
    /// </summary>
    public CircuitBreakerState GlobalState { get; set; }

    /// <summary>
    /// Gets or sets the total kernel executions across all kernels.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the total failed executions.
    /// </summary>
    public long TotalFailedExecutions { get; set; }

    /// <summary>
    /// Gets the overall failure rate.
    /// </summary>
    public double OverallFailureRate => TotalExecutions > 0
        ? (double)TotalFailedExecutions / TotalExecutions * 100.0
        : 0.0;

    /// <summary>
    /// Gets or sets per-kernel statistics.
    /// </summary>
    public Dictionary<string, KernelCircuitState> KernelStatistics { get; set; } = [];

    /// <summary>
    /// Gets or sets per-accelerator statistics.
    /// </summary>
    public Dictionary<string, KernelCircuitState> AcceleratorStatistics { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of active kernels being tracked.
    /// </summary>
    public int ActiveKernels { get; set; }

    /// <summary>
    /// Gets or sets the number of accelerators being tracked.
    /// </summary>
    public int ActiveAccelerators { get; set; }

    /// <summary>
    /// Gets or sets the number of kernels with open circuits.
    /// </summary>
    public int OpenCircuitKernels { get; set; }

    /// <summary>
    /// Gets or sets the last statistics update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Recovery recommendation for a failing kernel.
/// </summary>
public class KernelRecoveryRecommendation
{
    /// <summary>
    /// Gets or sets the recommended action.
    /// </summary>
    public KernelRecoveryAction RecommendedAction { get; set; }

    /// <summary>
    /// Gets or sets the confidence level (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the reason for the recommendation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets suggested wait time before retry.
    /// </summary>
    public TimeSpan? SuggestedWaitTime { get; set; }

    /// <summary>
    /// Gets or sets whether a fallback backend is recommended.
    /// </summary>
    public bool SuggestFallbackBackend { get; set; }

    /// <summary>
    /// Gets or sets the suggested fallback backend type.
    /// </summary>
    public string? FallbackBackendType { get; set; }
}

/// <summary>
/// Recovery actions for kernel failures.
/// </summary>
public enum KernelRecoveryAction
{
    /// <summary>
    /// Retry immediately.
    /// </summary>
    RetryImmediate,

    /// <summary>
    /// Retry with exponential backoff.
    /// </summary>
    RetryWithBackoff,

    /// <summary>
    /// Wait for circuit to recover.
    /// </summary>
    WaitForRecovery,

    /// <summary>
    /// Use fallback backend.
    /// </summary>
    UseFallbackBackend,

    /// <summary>
    /// Reduce problem size and retry.
    /// </summary>
    ReduceProblemSize,

    /// <summary>
    /// Reset GPU context and retry.
    /// </summary>
    ResetContextAndRetry,

    /// <summary>
    /// Fail fast - do not retry.
    /// </summary>
    FailFast,

    /// <summary>
    /// Alert operator - manual intervention required.
    /// </summary>
    AlertOperator
}

/// <summary>
/// Exception thrown when kernel circuit breaker is open.
/// </summary>
public class KernelCircuitOpenException : Exception
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string KernelName { get; }

    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public string AcceleratorType { get; }

    /// <summary>
    /// Gets the circuit state.
    /// </summary>
    public KernelCircuitState CircuitState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCircuitOpenException"/> class.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="circuitState">The circuit state.</param>
    public KernelCircuitOpenException(string kernelName, string acceleratorType, KernelCircuitState circuitState)
        : base($"Circuit breaker is OPEN for kernel '{kernelName}' on accelerator '{acceleratorType}'")
    {
        KernelName = kernelName;
        AcceleratorType = acceleratorType;
        CircuitState = circuitState;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCircuitOpenException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public KernelCircuitOpenException(string message) : base(message)
    {
        KernelName = string.Empty;
        AcceleratorType = string.Empty;
        CircuitState = new KernelCircuitState();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCircuitOpenException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public KernelCircuitOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
        KernelName = string.Empty;
        AcceleratorType = string.Empty;
        CircuitState = new KernelCircuitState();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCircuitOpenException"/> class.
    /// </summary>
    public KernelCircuitOpenException()
    {
        KernelName = string.Empty;
        AcceleratorType = string.Empty;
        CircuitState = new KernelCircuitState();
    }
}
