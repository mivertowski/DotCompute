// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;

namespace DotCompute.Backends.OpenCL.Configuration;

/// <summary>
/// Configuration for OpenCL command queue (stream) management and pooling behavior.
/// </summary>
/// <remarks>
/// This configuration controls how command queues are created, managed, and pooled
/// within the OpenCL backend. Command queues are the primary mechanism for submitting
/// work to OpenCL devices and managing execution order.
/// </remarks>
[DisplayName("OpenCL Stream Configuration")]
public sealed class StreamConfiguration
{
    /// <summary>
    /// Gets the minimum number of command queues to maintain in the pool per device.
    /// </summary>
    /// <value>
    /// The minimum pool size. Must be greater than 0. Default is 2.
    /// </value>
    /// <remarks>
    /// Each OpenCL device gets its own pool of command queues. This value determines
    /// the baseline number of queues that are pre-allocated and kept warm.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int MinimumQueuePoolSize
    {
        get => _minimumQueuePoolSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumQueuePoolSize),
                    value,
                    "Minimum queue pool size must be at least 1.");
            }
            _minimumQueuePoolSize = value;
        }
    }
    private readonly int _minimumQueuePoolSize = 2;

    /// <summary>
    /// Gets the maximum number of command queues that can exist in the pool per device.
    /// </summary>
    /// <value>
    /// The maximum pool size. Must be greater than or equal to MinimumQueuePoolSize. Default is 16.
    /// </value>
    /// <remarks>
    /// This limit prevents unbounded queue creation. When the limit is reached, requests for
    /// new queues will block until an existing queue is returned to the pool.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than MinimumQueuePoolSize.
    /// </exception>
    public int MaximumQueuePoolSize
    {
        get => _maximumQueuePoolSize;
        init
        {
            if (value < MinimumQueuePoolSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumQueuePoolSize),
                    value,
                    $"Maximum queue pool size must be at least {MinimumQueuePoolSize} (MinimumQueuePoolSize).");
            }
            _maximumQueuePoolSize = value;
        }
    }
    private readonly int _maximumQueuePoolSize = 16;

    /// <summary>
    /// Gets a value indicating whether to enable profiling on command queues by default.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable profiling; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, all command queues are created with CL_QUEUE_PROFILING_ENABLE,
    /// allowing collection of timing information for commands. This incurs a small
    /// performance overhead (~5-10%).
    /// </remarks>
    public bool EnableProfilingByDefault { get; init; }

    /// <summary>
    /// Gets a value indicating whether to prefer out-of-order execution queues.
    /// </summary>
    /// <value>
    /// <c>true</c> to prefer out-of-order queues; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Out-of-order queues (CL_QUEUE_OUT_OF_ORDER_EXEC_MODE_ENABLE) allow the OpenCL
    /// runtime to reorder commands for better performance. In-order queues provide
    /// simpler semantics but may have lower throughput.
    /// </remarks>
    public bool PreferOutOfOrderQueues { get; init; } = true;

    /// <summary>
    /// Gets the priority mapping for command queues.
    /// </summary>
    /// <value>
    /// The queue priority. Valid values are "low", "normal", "high". Default is "normal".
    /// </value>
    /// <remarks>
    /// This maps to OpenCL queue priorities if supported by the implementation:
    /// - "low": CL_QUEUE_PRIORITY_LOW_KHR
    /// - "normal": Default priority
    /// - "high": CL_QUEUE_PRIORITY_HIGH_KHR
    /// Not all OpenCL implementations support queue priorities.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is not "low", "normal", or "high".
    /// </exception>
    public string QueuePriority
    {
        get => _queuePriority;
        init
        {
            if (value != "low" && value != "normal" && value != "high")
            {
                throw new ArgumentException(
                    "Queue priority must be 'low', 'normal', or 'high'.",
                    nameof(QueuePriority));
            }
            _queuePriority = value;
        }
    }
    private readonly string _queuePriority = "normal";

    /// <summary>
    /// Gets the idle timeout for pooled command queues.
    /// </summary>
    /// <value>
    /// The idle timeout. Must be greater than TimeSpan.Zero. Default is 5 minutes.
    /// </value>
    /// <remarks>
    /// Command queues that have been idle for longer than this duration may be
    /// released from the pool to conserve resources, but the pool will never
    /// shrink below MinimumQueuePoolSize.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan IdleTimeout
    {
        get => _idleTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(IdleTimeout),
                    value,
                    "Idle timeout must be greater than zero.");
            }
            _idleTimeout = value;
        }
    }
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets a value indicating whether to enable on-device queue support (OpenCL 2.0+).
    /// </summary>
    /// <value>
    /// <c>true</c> to enable on-device queues; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// On-device queues (CL_QUEUE_ON_DEVICE) allow kernels to enqueue other kernels
    /// directly on the device without host intervention. This is only available in
    /// OpenCL 2.0 and later, and not all devices support it.
    /// </remarks>
    public bool EnableOnDeviceQueue { get; init; }

    /// <summary>
    /// Gets the maximum size of on-device queues in bytes (OpenCL 2.0+).
    /// </summary>
    /// <value>
    /// The on-device queue size. Must be greater than 0. Default is 1 MB (1048576 bytes).
    /// </value>
    /// <remarks>
    /// This value is only used when EnableOnDeviceQueue is true. It determines the
    /// size of the queue that kernels can use for device-side enqueuing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int OnDeviceQueueSize
    {
        get => _onDeviceQueueSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(OnDeviceQueueSize),
                    value,
                    "On-device queue size must be at least 1 byte.");
            }
            _onDeviceQueueSize = value;
        }
    }
    private readonly int _onDeviceQueueSize = 1024 * 1024; // 1 MB

    /// <summary>
    /// Creates a default stream configuration instance.
    /// </summary>
    /// <returns>
    /// A new <see cref="StreamConfiguration"/> instance with default settings.
    /// </returns>
    public static StreamConfiguration Default => new();

    /// <summary>
    /// Creates a high-throughput stream configuration.
    /// </summary>
    /// <returns>
    /// A new <see cref="StreamConfiguration"/> instance optimized for throughput.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Uses larger pool sizes for high concurrency
    /// - Enables out-of-order execution
    /// - Disables profiling for minimal overhead
    /// - Uses longer idle timeouts to keep queues warm
    /// </remarks>
    public static StreamConfiguration HighThroughput => new()
    {
        MinimumQueuePoolSize = 4,
        MaximumQueuePoolSize = 32,
        EnableProfilingByDefault = false,
        PreferOutOfOrderQueues = true,
        QueuePriority = "high",
        IdleTimeout = TimeSpan.FromMinutes(30)
    };

    /// <summary>
    /// Creates a development stream configuration.
    /// </summary>
    /// <returns>
    /// A new <see cref="StreamConfiguration"/> instance optimized for development.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Uses minimal pool sizes to reduce resource usage
    /// - Enables profiling for debugging
    /// - Prefers in-order queues for deterministic behavior
    /// - Uses short idle timeouts to release resources quickly
    /// </remarks>
    public static StreamConfiguration Development => new()
    {
        MinimumQueuePoolSize = 1,
        MaximumQueuePoolSize = 4,
        EnableProfilingByDefault = true,
        PreferOutOfOrderQueues = false,
        QueuePriority = "normal",
        IdleTimeout = TimeSpan.FromMinutes(1)
    };
}
