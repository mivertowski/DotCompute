// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;

namespace DotCompute.Backends.OpenCL.Configuration;

/// <summary>
/// Root configuration class for the OpenCL backend providing comprehensive control
/// over backend behavior, resource management, and performance tuning.
/// </summary>
/// <remarks>
/// This configuration class serves as the central point for all OpenCL backend settings,
/// including pool sizes, timeout values, performance optimizations, and vendor-specific
/// overrides. All properties are init-only and include validation to ensure correctness.
/// </remarks>
[DisplayName("OpenCL Backend Configuration")]
public sealed class OpenCLConfiguration
{
    /// <summary>
    /// Gets the minimum number of command queues to maintain in the pool.
    /// </summary>
    /// <value>
    /// The minimum pool size. Must be greater than 0. Default is 2.
    /// </value>
    /// <remarks>
    /// Setting this value too low may result in queue allocation overhead during bursts.
    /// Setting it too high wastes system resources. A value of 2-4 is recommended for
    /// most applications.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int MinimumPoolSize
    {
        get => _minimumPoolSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumPoolSize),
                    value,
                    "Minimum pool size must be at least 1.");
            }
            _minimumPoolSize = value;
        }
    }
    private readonly int _minimumPoolSize = 2;

    /// <summary>
    /// Gets the maximum number of command queues that can be created in the pool.
    /// </summary>
    /// <value>
    /// The maximum pool size. Must be greater than or equal to MinimumPoolSize. Default is 16.
    /// </value>
    /// <remarks>
    /// This limit prevents unbounded resource consumption. When the pool reaches this size,
    /// additional queue requests will wait for an available queue to be returned.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than MinimumPoolSize.
    /// </exception>
    public int MaximumPoolSize
    {
        get => _maximumPoolSize;
        init
        {
            if (value < MinimumPoolSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumPoolSize),
                    value,
                    $"Maximum pool size must be at least {MinimumPoolSize} (MinimumPoolSize).");
            }
            _maximumPoolSize = value;
        }
    }
    private readonly int _maximumPoolSize = 16;

    /// <summary>
    /// Gets the timeout duration for acquiring a command queue from the pool.
    /// </summary>
    /// <value>
    /// The acquisition timeout. Must be greater than TimeSpan.Zero. Default is 30 seconds.
    /// </value>
    /// <remarks>
    /// If a queue cannot be acquired within this timeout, an exception is thrown.
    /// This prevents indefinite blocking when the pool is exhausted.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan QueueAcquisitionTimeout
    {
        get => _queueAcquisitionTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(QueueAcquisitionTimeout),
                    value,
                    "Queue acquisition timeout must be greater than zero.");
            }
            _queueAcquisitionTimeout = value;
        }
    }
    private readonly TimeSpan _queueAcquisitionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the timeout duration for command queue operations.
    /// </summary>
    /// <value>
    /// The operation timeout. Must be greater than TimeSpan.Zero. Default is 60 seconds.
    /// </value>
    /// <remarks>
    /// This timeout applies to individual operations like kernel execution, memory transfers,
    /// and event waits. Long-running kernels may need a higher value.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan OperationTimeout
    {
        get => _operationTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(OperationTimeout),
                    value,
                    "Operation timeout must be greater than zero.");
            }
            _operationTimeout = value;
        }
    }
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets a value indicating whether to enable automatic performance profiling.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable profiling; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, command queues are created with CL_QUEUE_PROFILING_ENABLE, allowing
    /// timing information to be collected. This incurs a small performance overhead.
    /// </remarks>
    public bool EnableProfiling { get; init; }

    /// <summary>
    /// Gets a value indicating whether to prefer out-of-order execution queues.
    /// </summary>
    /// <value>
    /// <c>true</c> to prefer out-of-order queues; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Out-of-order queues can provide better performance by allowing the OpenCL runtime
    /// to reorder commands for optimal execution. However, they require explicit event-based
    /// synchronization to maintain correctness.
    /// </remarks>
    public bool PreferOutOfOrderExecution { get; init; } = true;

    /// <summary>
    /// Gets the stream configuration for command queue management.
    /// </summary>
    /// <value>
    /// The stream configuration instance. Never null.
    /// </value>
    /// <remarks>
    /// This configuration controls command queue pooling, priority mapping, and profiling settings.
    /// </remarks>
    public StreamConfiguration Stream { get; init; } = new();

    /// <summary>
    /// Gets the event configuration for OpenCL event management.
    /// </summary>
    /// <value>
    /// The event configuration instance. Never null.
    /// </value>
    /// <remarks>
    /// This configuration controls event pooling, profiling, and wait timeout settings.
    /// </remarks>
    public EventConfiguration Event { get; init; } = new();

    /// <summary>
    /// Gets the memory configuration for buffer management and P2P transfers.
    /// </summary>
    /// <value>
    /// The memory configuration instance. Never null.
    /// </value>
    /// <remarks>
    /// This configuration controls buffer pooling, alignment, pinned memory, and
    /// peer-to-peer transfer settings for multi-device scenarios.
    /// </remarks>
    public MemoryConfiguration Memory { get; init; } = new();

    /// <summary>
    /// Gets vendor-specific configuration overrides.
    /// </summary>
    /// <value>
    /// A dictionary of vendor-specific settings. Never null.
    /// </value>
    /// <remarks>
    /// This allows customization for specific OpenCL implementations:
    /// - "NVIDIA": CUDA-specific optimizations
    /// - "AMD": ROCm/PAL-specific settings
    /// - "Intel": NEO driver optimizations
    /// - "Apple": Metal interop settings
    /// </remarks>
    public IReadOnlyDictionary<string, object> VendorOverrides { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Gets a value indicating whether to enable kernel caching.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable kernel caching; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, compiled kernels are cached to avoid recompilation on subsequent uses.
    /// This significantly improves performance for repeated kernel executions.
    /// </remarks>
    public bool EnableKernelCache { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of compiled kernels to cache.
    /// </summary>
    /// <value>
    /// The kernel cache size. Must be greater than 0. Default is 256.
    /// </value>
    /// <remarks>
    /// When the cache reaches this size, the least recently used kernels are evicted.
    /// A larger cache reduces compilation overhead but uses more memory.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int KernelCacheSize
    {
        get => _kernelCacheSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(KernelCacheSize),
                    value,
                    "Kernel cache size must be at least 1.");
            }
            _kernelCacheSize = value;
        }
    }
    private readonly int _kernelCacheSize = 256;

    /// <summary>
    /// Gets the OpenCL build options to use for kernel compilation.
    /// </summary>
    /// <value>
    /// The build options string. Can be null or empty for default behavior.
    /// </value>
    /// <remarks>
    /// Common options include:
    /// - "-cl-fast-relaxed-math": Enable aggressive optimizations (may reduce precision)
    /// - "-cl-mad-enable": Enable mad (multiply-add) instruction
    /// - "-cl-no-signed-zeros": Allow optimizations for signed zero
    /// - "-cl-std=CL2.0": Specify OpenCL C version
    /// </remarks>
    public string? BuildOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether to validate all inputs before execution.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable validation; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, all kernel arguments, buffer sizes, and work dimensions are validated
    /// before execution. Disabling this can improve performance in production after thorough testing.
    /// </remarks>
    public bool EnableValidation { get; init; } = true;

    /// <summary>
    /// Creates a default OpenCL configuration instance.
    /// </summary>
    /// <returns>
    /// A new <see cref="OpenCLConfiguration"/> instance with default settings.
    /// </returns>
    public static OpenCLConfiguration Default => new();

    /// <summary>
    /// Creates a high-performance configuration optimized for throughput.
    /// </summary>
    /// <returns>
    /// A new <see cref="OpenCLConfiguration"/> instance optimized for performance.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Disables validation for maximum throughput
    /// - Uses larger pool sizes
    /// - Enables out-of-order execution
    /// - Disables profiling
    /// </remarks>
    public static OpenCLConfiguration HighPerformance => new()
    {
        EnableValidation = false,
        EnableProfiling = false,
        MinimumPoolSize = 4,
        MaximumPoolSize = 32,
        PreferOutOfOrderExecution = true,
        BuildOptions = "-cl-fast-relaxed-math -cl-mad-enable",
        Stream = StreamConfiguration.HighThroughput,
        Event = EventConfiguration.MinimalOverhead,
        Memory = MemoryConfiguration.LargeBuffers
    };

    /// <summary>
    /// Creates a development configuration optimized for debugging and profiling.
    /// </summary>
    /// <returns>
    /// A new <see cref="OpenCLConfiguration"/> instance optimized for development.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Enables validation and profiling
    /// - Uses smaller pool sizes to reduce resource usage
    /// - Prefers in-order execution for deterministic behavior
    /// </remarks>
    public static OpenCLConfiguration Development => new()
    {
        EnableValidation = true,
        EnableProfiling = true,
        MinimumPoolSize = 1,
        MaximumPoolSize = 4,
        PreferOutOfOrderExecution = false,
        Stream = StreamConfiguration.Development,
        Event = EventConfiguration.Debugging,
        Memory = MemoryConfiguration.Development
    };
}
