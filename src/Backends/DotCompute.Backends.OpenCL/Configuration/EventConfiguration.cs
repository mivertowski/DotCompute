// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;

namespace DotCompute.Backends.OpenCL.Configuration;

/// <summary>
/// Configuration for OpenCL event management, pooling, and profiling behavior.
/// </summary>
/// <remarks>
/// OpenCL events are the primary synchronization mechanism for coordinating command
/// execution across queues and devices. This configuration controls event lifecycle,
/// pooling strategies, and profiling capabilities.
/// </remarks>
[DisplayName("OpenCL Event Configuration")]
public sealed class EventConfiguration
{
    /// <summary>
    /// Gets the minimum number of events to maintain in the pool.
    /// </summary>
    /// <value>
    /// The minimum pool size. Must be greater than 0. Default is 8.
    /// </value>
    /// <remarks>
    /// Events are pooled to avoid the overhead of creating and destroying them for
    /// every operation. This value ensures a baseline number of events are always
    /// available for immediate use.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int MinimumEventPoolSize
    {
        get => _minimumEventPoolSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumEventPoolSize),
                    value,
                    "Minimum event pool size must be at least 1.");
            }
            _minimumEventPoolSize = value;
        }
    }
    private readonly int _minimumEventPoolSize = 8;

    /// <summary>
    /// Gets the maximum number of events that can exist in the pool.
    /// </summary>
    /// <value>
    /// The maximum pool size. Must be greater than or equal to MinimumEventPoolSize. Default is 128.
    /// </value>
    /// <remarks>
    /// This limit prevents unbounded event creation. When the pool is exhausted,
    /// callers must wait for events to be returned to the pool or create events
    /// outside the pool with potential performance implications.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than MinimumEventPoolSize.
    /// </exception>
    public int MaximumEventPoolSize
    {
        get => _maximumEventPoolSize;
        init
        {
            if (value < MinimumEventPoolSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumEventPoolSize),
                    value,
                    $"Maximum event pool size must be at least {MinimumEventPoolSize} (MinimumEventPoolSize).");
            }
            _maximumEventPoolSize = value;
        }
    }
    private readonly int _maximumEventPoolSize = 128;

    /// <summary>
    /// Gets a value indicating whether to enable event profiling by default.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable profiling; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, timing information is collected for all events, allowing measurement
    /// of kernel execution time, memory transfer time, etc. This requires command queues
    /// to be created with CL_QUEUE_PROFILING_ENABLE.
    /// </remarks>
    public bool EnableEventProfiling { get; init; }

    /// <summary>
    /// Gets the default timeout for waiting on events.
    /// </summary>
    /// <value>
    /// The wait timeout. Must be greater than TimeSpan.Zero. Default is 60 seconds.
    /// </value>
    /// <remarks>
    /// This timeout is used when waiting for events to complete using clWaitForEvents
    /// or event.Wait(). If an event does not complete within this duration, a timeout
    /// exception is thrown. Set to a higher value for long-running kernels.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan DefaultWaitTimeout
    {
        get => _defaultWaitTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(DefaultWaitTimeout),
                    value,
                    "Default wait timeout must be greater than zero.");
            }
            _defaultWaitTimeout = value;
        }
    }
    private readonly TimeSpan _defaultWaitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the polling interval for event status checks.
    /// </summary>
    /// <value>
    /// The polling interval. Must be greater than TimeSpan.Zero. Default is 1 millisecond.
    /// </value>
    /// <remarks>
    /// When waiting for events asynchronously, the status is checked at this interval.
    /// Shorter intervals provide lower latency but higher CPU usage. Longer intervals
    /// reduce CPU overhead but may delay event completion detection.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan PollingInterval
    {
        get => _pollingInterval;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(PollingInterval),
                    value,
                    "Polling interval must be greater than zero.");
            }
            _pollingInterval = value;
        }
    }
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Gets a value indicating whether to automatically release completed events.
    /// </summary>
    /// <value>
    /// <c>true</c> to auto-release events; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, events are automatically returned to the pool once they complete
    /// and all event callbacks have executed. This reduces manual resource management
    /// but requires care to avoid use-after-free scenarios.
    /// </remarks>
    public bool AutoReleaseCompletedEvents { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of event callbacks that can be registered per event.
    /// </summary>
    /// <value>
    /// The maximum callbacks. Must be greater than 0. Default is 16.
    /// </value>
    /// <remarks>
    /// OpenCL allows multiple callbacks to be registered for event completion.
    /// This limit prevents unbounded callback registration which could impact
    /// performance and memory usage.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int MaximumEventCallbacks
    {
        get => _maximumEventCallbacks;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumEventCallbacks),
                    value,
                    "Maximum event callbacks must be at least 1.");
            }
            _maximumEventCallbacks = value;
        }
    }
    private readonly int _maximumEventCallbacks = 16;

    /// <summary>
    /// Creates a default event configuration instance.
    /// </summary>
    /// <returns>
    /// A new <see cref="EventConfiguration"/> instance with default settings.
    /// </returns>
    public static EventConfiguration Default => new();

    /// <summary>
    /// Creates an event configuration optimized for minimal overhead.
    /// </summary>
    /// <returns>
    /// A new <see cref="EventConfiguration"/> instance optimized for performance.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Uses larger pool sizes to reduce allocation overhead
    /// - Disables profiling for minimal CPU overhead
    /// - Uses longer polling intervals to reduce CPU usage
    /// - Enables automatic event release for reduced manual management
    /// </remarks>
    public static EventConfiguration MinimalOverhead => new()
    {
        MinimumEventPoolSize = 16,
        MaximumEventPoolSize = 256,
        EnableEventProfiling = false,
        PollingInterval = TimeSpan.FromMilliseconds(5),
        AutoReleaseCompletedEvents = true,
        DefaultWaitTimeout = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// Creates an event configuration optimized for debugging.
    /// </summary>
    /// <returns>
    /// A new <see cref="EventConfiguration"/> instance optimized for debugging.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Uses smaller pool sizes to track event usage
    /// - Enables profiling for detailed timing information
    /// - Uses short polling intervals for responsive debugging
    /// - Disables automatic release for explicit resource management
    /// - Uses shorter timeouts to detect hung operations quickly
    /// </remarks>
    public static EventConfiguration Debugging => new()
    {
        MinimumEventPoolSize = 4,
        MaximumEventPoolSize = 32,
        EnableEventProfiling = true,
        PollingInterval = TimeSpan.FromMilliseconds(0.5),
        AutoReleaseCompletedEvents = false,
        DefaultWaitTimeout = TimeSpan.FromSeconds(30)
    };
}
