// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace DotCompute.Backends.OpenCL.Configuration;

/// <summary>
/// Configuration for OpenCL stream (command queue) management.
/// </summary>
/// <remarks>
/// <para>
/// This configuration controls how OpenCL command queues are pooled, created, and managed.
/// Stream pooling reduces the overhead of queue creation and improves performance for workloads
/// with many short-lived kernels.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Connection pooling pattern for command queues</description></item>
/// <item><description>Automatic pool scaling based on usage patterns</description></item>
/// <item><description>Queue property configuration (out-of-order, profiling)</description></item>
/// <item><description>Priority-based queue management</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class OpenCLStreamConfiguration
{
    /// <summary>
    /// Gets the initial size of the stream pool.
    /// </summary>
    /// <value>
    /// The number of command queues to create initially. Default is 4.
    /// </value>
    /// <remarks>
    /// This value determines how many command queues are pre-allocated when the pool is initialized.
    /// A higher value reduces initial latency but increases memory usage.
    /// </remarks>
    public int InitialPoolSize { get; init; } = 4;

    /// <summary>
    /// Gets the maximum size of the stream pool.
    /// </summary>
    /// <value>
    /// The maximum number of command queues that can be pooled. Default is 16.
    /// </value>
    /// <remarks>
    /// Once this limit is reached, requests for new streams will wait for available queues
    /// or fail based on the configured timeout.
    /// </remarks>
    public int MaxPoolSize { get; init; } = 16;

    /// <summary>
    /// Gets the minimum size of the stream pool.
    /// </summary>
    /// <value>
    /// The minimum number of command queues to keep alive. Default is 2.
    /// </value>
    /// <remarks>
    /// When auto-scaling is enabled, the pool will not shrink below this size.
    /// This ensures a baseline of immediately available queues.
    /// </remarks>
    public int MinPoolSize { get; init; } = 2;

    /// <summary>
    /// Gets the timeout for acquiring a stream from the pool.
    /// </summary>
    /// <value>
    /// The maximum time to wait for an available queue. Default is 30 seconds.
    /// </value>
    /// <remarks>
    /// If no queue becomes available within this timeout, the acquire operation will fail.
    /// </remarks>
    public TimeSpan PoolTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets a value indicating whether to enable out-of-order execution.
    /// </summary>
    /// <value>
    /// <c>true</c> if out-of-order execution is enabled; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Out-of-order execution allows the OpenCL runtime to reorder commands for better performance.
    /// This can improve throughput but may complicate synchronization. Only enable if your workload
    /// benefits from it and you have proper synchronization in place.
    /// </remarks>
    public bool EnableOutOfOrder { get; init; }

    /// <summary>
    /// Gets a value indicating whether to enable profiling for command queues.
    /// </summary>
    /// <value>
    /// <c>true</c> if profiling is enabled; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Enabling profiling allows you to measure execution time of commands but may impact performance.
    /// Only enable this for development, debugging, or production monitoring scenarios.
    /// </remarks>
    public bool EnableProfiling { get; init; }

    /// <summary>
    /// Gets the default priority for command queues.
    /// </summary>
    /// <value>
    /// The default queue priority. Default is <see cref="QueuePriority.Normal"/>.
    /// </value>
    /// <remarks>
    /// Queue priority affects scheduling on devices that support priority-based execution.
    /// Not all OpenCL implementations support queue priorities.
    /// </remarks>
    public QueuePriority DefaultPriority { get; init; } = QueuePriority.Normal;

    /// <summary>
    /// Gets a value indicating whether to enable automatic pool scaling.
    /// </summary>
    /// <value>
    /// <c>true</c> if auto-scaling is enabled; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the pool size automatically adjusts based on usage patterns to maintain
    /// the target pool hit rate. The pool will grow up to <see cref="MaxPoolSize"/> and
    /// shrink down to <see cref="MinPoolSize"/>.
    /// </remarks>
    public bool EnableAutoScaling { get; init; } = true;

    /// <summary>
    /// Gets the target pool hit rate for auto-scaling.
    /// </summary>
    /// <value>
    /// The desired ratio of successful immediate acquisitions to total acquisition attempts.
    /// Default is 0.8 (80%).
    /// </value>
    /// <remarks>
    /// The auto-scaling algorithm attempts to maintain this hit rate by adjusting the pool size.
    /// A higher value means more queues kept available but higher memory usage.
    /// Valid range is 0.0 to 1.0.
    /// </remarks>
    public double TargetPoolHitRate { get; init; } = 0.8;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more configuration values are invalid.
    /// </exception>
    public void Validate()
    {
        if (InitialPoolSize < 0)
        {
            throw new ArgumentException(
                $"Initial pool size must be non-negative, but was {InitialPoolSize}.",
                nameof(InitialPoolSize));
        }

        if (MinPoolSize < 0)
        {
            throw new ArgumentException(
                $"Minimum pool size must be non-negative, but was {MinPoolSize}.",
                nameof(MinPoolSize));
        }

        if (MaxPoolSize < 1)
        {
            throw new ArgumentException(
                $"Maximum pool size must be at least 1, but was {MaxPoolSize}.",
                nameof(MaxPoolSize));
        }

        if (InitialPoolSize > MaxPoolSize)
        {
            throw new ArgumentException(
                $"Initial pool size ({InitialPoolSize}) cannot exceed maximum pool size ({MaxPoolSize}).",
                nameof(InitialPoolSize));
        }

        if (MinPoolSize > MaxPoolSize)
        {
            throw new ArgumentException(
                $"Minimum pool size ({MinPoolSize}) cannot exceed maximum pool size ({MaxPoolSize}).",
                nameof(MinPoolSize));
        }

        if (PoolTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"Pool timeout must be positive, but was {PoolTimeout}.",
                nameof(PoolTimeout));
        }

        if (TargetPoolHitRate < 0.0 || TargetPoolHitRate > 1.0)
        {
            throw new ArgumentException(
                $"Target pool hit rate must be between 0.0 and 1.0, but was {TargetPoolHitRate}.",
                nameof(TargetPoolHitRate));
        }
    }

    /// <summary>
    /// Gets the default stream configuration.
    /// </summary>
    /// <value>
    /// A configuration suitable for general-purpose workloads with balanced performance and resource usage.
    /// </value>
    public static OpenCLStreamConfiguration Default { get; } = new();

    /// <summary>
    /// Gets a stream configuration optimized for high throughput.
    /// </summary>
    /// <value>
    /// A configuration with larger pool sizes and out-of-order execution enabled.
    /// </value>
    /// <remarks>
    /// This configuration is suitable for workloads that submit many concurrent kernels
    /// and can benefit from out-of-order execution. Memory usage will be higher.
    /// </remarks>
    public static OpenCLStreamConfiguration HighThroughput { get; } = new()
    {
        InitialPoolSize = 8,
        MaxPoolSize = 32,
        MinPoolSize = 4,
        EnableOutOfOrder = true,
        EnableAutoScaling = true,
        TargetPoolHitRate = 0.9,
        PoolTimeout = TimeSpan.FromSeconds(60)
    };

    /// <summary>
    /// Gets a stream configuration optimized for low latency.
    /// </summary>
    /// <value>
    /// A configuration with smaller pool sizes and in-order execution for predictable latency.
    /// </value>
    /// <remarks>
    /// This configuration is suitable for latency-sensitive workloads where predictable
    /// execution order is more important than maximum throughput.
    /// </remarks>
    public static OpenCLStreamConfiguration LowLatency { get; } = new()
    {
        InitialPoolSize = 2,
        MaxPoolSize = 8,
        MinPoolSize = 1,
        EnableOutOfOrder = false,
        EnableAutoScaling = false,
        TargetPoolHitRate = 0.7,
        PoolTimeout = TimeSpan.FromSeconds(10)
    };
}

/// <summary>
/// Specifies the priority level for command queues.
/// </summary>
/// <remarks>
/// Queue priority affects scheduling on devices that support priority-based execution.
/// Not all OpenCL implementations support queue priorities (requires cl_khr_priority_hints extension).
/// </remarks>
public enum QueuePriority
{
    /// <summary>
    /// Low priority queue for background or batch workloads.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority queue for general-purpose workloads.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority queue for latency-sensitive or critical workloads.
    /// </summary>
    High = 2
}
