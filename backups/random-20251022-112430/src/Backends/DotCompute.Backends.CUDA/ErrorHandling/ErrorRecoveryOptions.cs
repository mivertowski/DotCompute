// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// Error recovery configuration options.
/// </summary>
public sealed class ErrorRecoveryOptions
{
    /// <summary>
    /// Gets the maximum retry attempts.
    /// </summary>
    /// <value>
    /// The maximum retry attempts.
    /// </value>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum retry delay ms.
    /// </summary>
    /// <value>
    /// The maximum retry delay ms.
    /// </value>
    public int MaxRetryDelayMs { get; init; } = 5000;

    /// <summary>
    /// Gets the memory retry attempts.
    /// </summary>
    /// <value>
    /// The memory retry attempts.
    /// </value>
    public int MemoryRetryAttempts { get; init; } = 2;

    /// <summary>
    /// Gets the circuit breaker threshold.
    /// </summary>
    /// <value>
    /// The circuit breaker threshold.
    /// </value>
    public int CircuitBreakerThreshold { get; init; } = 5;

    /// <summary>
    /// Gets the circuit breaker duration seconds.
    /// </summary>
    /// <value>
    /// The circuit breaker duration seconds.
    /// </value>
    public int CircuitBreakerDurationSeconds { get; init; } = 30;

    /// <summary>
    /// Gets a value indicating whether [enable cpu fallback].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [enable cpu fallback]; otherwise, <c>false</c>.
    /// </value>
    public bool EnableCpuFallback { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether [allow device reset].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [allow device reset]; otherwise, <c>false</c>.
    /// </value>
    public bool AllowDeviceReset { get; init; }

    /// <summary>
    /// Gets a value indicating whether [enable diagnostic logging].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [enable diagnostic logging]; otherwise, <c>false</c>.
    /// </value>
    public bool EnableDiagnosticLogging { get; init; } = true;
}