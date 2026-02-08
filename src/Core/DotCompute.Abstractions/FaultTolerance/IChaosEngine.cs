// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace DotCompute.Abstractions.FaultTolerance;

/// <summary>
/// Types of faults that can be injected.
/// </summary>
public enum FaultType
{
    /// <summary>
    /// No fault - normal operation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Introduces a delay before operation completes.
    /// </summary>
    Latency,

    /// <summary>
    /// Throws an exception.
    /// </summary>
    Exception,

    /// <summary>
    /// Causes the operation to timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// Corrupts data or returns incorrect results.
    /// </summary>
    DataCorruption,

    /// <summary>
    /// Simulates resource exhaustion (memory, connections, etc.).
    /// </summary>
    ResourceExhaustion,

    /// <summary>
    /// Simulates partial failure (some operations succeed, some fail).
    /// </summary>
    PartialFailure,

    /// <summary>
    /// Simulates network partition.
    /// </summary>
    NetworkPartition,

    /// <summary>
    /// Simulates GPU device error.
    /// </summary>
    GpuError,

    /// <summary>
    /// Terminates the operation abruptly.
    /// </summary>
    ProcessCrash
}

/// <summary>
/// Configuration for a fault injection.
/// </summary>
public sealed class FaultConfiguration
{
    /// <summary>
    /// Gets or sets the type of fault to inject.
    /// </summary>
    public FaultType FaultType { get; init; } = FaultType.None;

    /// <summary>
    /// Gets or sets the probability of fault occurring (0.0 to 1.0).
    /// </summary>
    public double Probability { get; init; } = 1.0;

    /// <summary>
    /// Gets or sets the target component or operation pattern.
    /// Use "*" for all components.
    /// </summary>
    public string TargetPattern { get; init; } = "*";

    /// <summary>
    /// Gets or sets the latency to inject for Latency faults.
    /// </summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>
    /// Gets or sets the minimum latency for random latency range.
    /// </summary>
    public TimeSpan? MinLatency { get; init; }

    /// <summary>
    /// Gets or sets the maximum latency for random latency range.
    /// </summary>
    public TimeSpan? MaxLatency { get; init; }

    /// <summary>
    /// Gets or sets the exception type to throw for Exception faults.
    /// </summary>
    public Type? ExceptionType { get; init; }

    /// <summary>
    /// Gets or sets the exception message.
    /// </summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>
    /// Gets or sets custom exception factory.
    /// </summary>
    public Func<Exception>? ExceptionFactory { get; init; }

    /// <summary>
    /// Gets or sets the timeout duration for Timeout faults.
    /// </summary>
    public TimeSpan? TimeoutDuration { get; init; }

    /// <summary>
    /// Gets or sets how long this fault should remain active.
    /// Null means indefinite.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of times to inject this fault.
    /// Null means unlimited.
    /// </summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>
    /// Gets or sets whether the fault is currently enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets additional metadata for the fault.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a latency fault configuration.
    /// </summary>
    public static FaultConfiguration CreateLatency(
        TimeSpan delay,
        double probability = 1.0,
        string targetPattern = "*") => new()
        {
            FaultType = FaultType.Latency,
            Latency = delay,
            Probability = probability,
            TargetPattern = targetPattern
        };

    /// <summary>
    /// Creates a latency fault with random delay.
    /// </summary>
    public static FaultConfiguration CreateRandomLatency(
        TimeSpan minDelay,
        TimeSpan maxDelay,
        double probability = 1.0,
        string targetPattern = "*") => new()
        {
            FaultType = FaultType.Latency,
            MinLatency = minDelay,
            MaxLatency = maxDelay,
            Probability = probability,
            TargetPattern = targetPattern
        };

    /// <summary>
    /// Creates an exception fault configuration.
    /// </summary>
    public static FaultConfiguration CreateException<TException>(
        string? message = null,
        double probability = 1.0,
        string targetPattern = "*")
        where TException : Exception => new()
        {
            FaultType = FaultType.Exception,
            ExceptionType = typeof(TException),
            ExceptionMessage = message,
            Probability = probability,
            TargetPattern = targetPattern
        };

    /// <summary>
    /// Creates an exception fault with factory.
    /// </summary>
    public static FaultConfiguration CreateException(
        Func<Exception> factory,
        double probability = 1.0,
        string targetPattern = "*") => new()
        {
            FaultType = FaultType.Exception,
            ExceptionFactory = factory,
            Probability = probability,
            TargetPattern = targetPattern
        };

    /// <summary>
    /// Creates a timeout fault configuration.
    /// </summary>
    public static FaultConfiguration CreateTimeout(
        TimeSpan? duration = null,
        double probability = 1.0,
        string targetPattern = "*") => new()
        {
            FaultType = FaultType.Timeout,
            TimeoutDuration = duration,
            Probability = probability,
            TargetPattern = targetPattern
        };
}

/// <summary>
/// Record of an injected fault.
/// </summary>
public sealed record FaultInjectionRecord
{
    /// <summary>
    /// Gets the fault identifier.
    /// </summary>
    public required Guid FaultId { get; init; }

    /// <summary>
    /// Gets the fault configuration.
    /// </summary>
    public required FaultConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the target component that was affected.
    /// </summary>
    public required string TargetComponent { get; init; }

    /// <summary>
    /// Gets the operation that was affected.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// Gets when the fault was injected.
    /// </summary>
    public DateTimeOffset InjectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets whether the fault was actually applied.
    /// </summary>
    public bool WasApplied { get; init; }

    /// <summary>
    /// Gets the actual delay injected (for latency faults).
    /// </summary>
    public TimeSpan? ActualDelay { get; init; }

    /// <summary>
    /// Gets the exception that was thrown (for exception faults).
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// Statistics about fault injection.
/// </summary>
public sealed record ChaosStatistics
{
    /// <summary>
    /// Gets the total number of fault opportunities.
    /// </summary>
    public int TotalOpportunities { get; init; }

    /// <summary>
    /// Gets the number of faults injected.
    /// </summary>
    public int FaultsInjected { get; init; }

    /// <summary>
    /// Gets the number of faults skipped (due to probability).
    /// </summary>
    public int FaultsSkipped { get; init; }

    /// <summary>
    /// Gets breakdown by fault type.
    /// </summary>
    public IReadOnlyDictionary<FaultType, int> ByFaultType { get; init; } = new Dictionary<FaultType, int>();

    /// <summary>
    /// Gets breakdown by component.
    /// </summary>
    public IReadOnlyDictionary<string, int> ByComponent { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets when the chaos session started.
    /// </summary>
    public DateTimeOffset SessionStarted { get; init; }

    /// <summary>
    /// Gets when statistics were captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Engine for injecting faults during testing and chaos engineering.
/// </summary>
public interface IChaosEngine : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the chaos engine is currently active.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the current fault configurations.
    /// </summary>
    public IReadOnlyList<FaultConfiguration> ActiveFaults { get; }

    /// <summary>
    /// Starts the chaos engine with the specified configurations.
    /// </summary>
    /// <param name="faults">Fault configurations to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StartAsync(
        IEnumerable<FaultConfiguration> faults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the chaos engine.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a fault configuration while running.
    /// </summary>
    /// <param name="fault">The fault configuration.</param>
    public void AddFault(FaultConfiguration fault);

    /// <summary>
    /// Removes a fault configuration.
    /// </summary>
    /// <param name="faultType">The fault type to remove.</param>
    /// <param name="targetPattern">Optional target pattern to match.</param>
    public void RemoveFault(FaultType faultType, string? targetPattern = null);

    /// <summary>
    /// Clears all fault configurations.
    /// </summary>
    public void ClearFaults();

    /// <summary>
    /// Evaluates whether a fault should be injected for the given operation.
    /// </summary>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fault to inject, or null if no fault.</returns>
    public Task<FaultConfiguration?> EvaluateFaultAsync(
        string componentId,
        string? operation = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects a fault according to the configuration.
    /// </summary>
    /// <param name="fault">The fault configuration.</param>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task InjectFaultAsync(
        FaultConfiguration fault,
        string componentId,
        string? operation = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fault injection history.
    /// </summary>
    /// <param name="limit">Maximum records to return.</param>
    /// <returns>List of fault injection records.</returns>
    public IReadOnlyList<FaultInjectionRecord> GetHistory(int limit = 100);

    /// <summary>
    /// Gets chaos statistics.
    /// </summary>
    /// <returns>Current statistics.</returns>
    public ChaosStatistics GetStatistics();

    /// <summary>
    /// Occurs when a fault is about to be injected.
    /// </summary>
    public event EventHandler<FaultInjectionEventArgs>? FaultInjecting;

    /// <summary>
    /// Occurs after a fault has been injected.
    /// </summary>
    public event EventHandler<FaultInjectionEventArgs>? FaultInjected;
}

/// <summary>
/// Event arguments for fault injection events.
/// </summary>
public sealed class FaultInjectionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the fault configuration.
    /// </summary>
    public required FaultConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the target component.
    /// </summary>
    public required string ComponentId { get; init; }

    /// <summary>
    /// Gets the operation being performed.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// Gets or sets whether to cancel the fault injection (for FaultInjecting event).
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Extension methods for using the chaos engine.
/// </summary>
public static class ChaosEngineExtensions
{
    /// <summary>
    /// Executes an operation with potential fault injection.
    /// </summary>
    public static async Task<T> ExecuteWithChaosAsync<T>(
        this IChaosEngine engine,
        string componentId,
        string operation,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (!engine.IsActive)
        {
            return await action().ConfigureAwait(false);
        }

        var fault = await engine.EvaluateFaultAsync(componentId, operation, cancellationToken)
            .ConfigureAwait(false);

        if (fault != null)
        {
            await engine.InjectFaultAsync(fault, componentId, operation, cancellationToken)
                .ConfigureAwait(false);
        }

        return await action().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with potential fault injection.
    /// </summary>
    public static async Task ExecuteWithChaosAsync(
        this IChaosEngine engine,
        string componentId,
        string operation,
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!engine.IsActive)
        {
            await action().ConfigureAwait(false);
            return;
        }

        var fault = await engine.EvaluateFaultAsync(componentId, operation, cancellationToken)
            .ConfigureAwait(false);

        if (fault != null)
        {
            await engine.InjectFaultAsync(fault, componentId, operation, cancellationToken)
                .ConfigureAwait(false);
        }

        await action().ConfigureAwait(false);
    }
}
