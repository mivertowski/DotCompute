// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Strategies for retrying failed operations in pipelines.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// No retry, fail immediately on error.
    /// </summary>
    None,

    /// <summary>
    /// Immediate retry without delay.
    /// </summary>
    Immediate,

    /// <summary>
    /// Fixed delay between retry attempts.
    /// </summary>
    FixedDelay,

    /// <summary>
    /// Exponentially increasing delay between retries.
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Linear increase in delay between retries.
    /// </summary>
    LinearBackoff,

    /// <summary>
    /// Random delay with jitter to avoid thundering herd.
    /// </summary>
    RandomJitter,

    /// <summary>
    /// Fibonacci sequence delays between retries.
    /// </summary>
    FibonacciBackoff,

    /// <summary>
    /// Custom retry logic defined by user.
    /// </summary>
    Custom
}
