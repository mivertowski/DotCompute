// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace DotCompute.Runtime.Configuration;


/// <summary>
/// Configuration options for DotCompute Runtime
/// </summary>
public class DotComputeRuntimeOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    /// <summary>
    /// The configuration section name
    /// </summary>
    public const string SectionName = "DotCompute";

    /// <summary>
    /// Gets or sets whether to enable automatic accelerator discovery
    /// </summary>
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the preferred accelerator type for default selection
    /// </summary>
    public AcceleratorType PreferredAcceleratorType { get; set; } = AcceleratorType.GPU;

    /// <summary>
    /// Gets or sets the maximum number of accelerators to discover
    /// </summary>
    [Range(1, 64)]
    public int MaxAccelerators { get; set; } = 16;

    /// <summary>
    /// Gets or sets whether to enable kernel compilation caching
    /// </summary>
    public bool EnableKernelCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the kernel cache directory path
    /// </summary>
    public string? KernelCacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum cache size in MB
    /// </summary>
    [Range(1, 10240)]
    public int MaxCacheSizeMB { get; set; } = 1024;

    /// <summary>
    /// Gets or sets whether to enable memory pooling
    /// </summary>
    public bool EnableMemoryPooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the initial memory pool size in MB
    /// </summary>
    [Range(1, 8192)]
    public int InitialPoolSizeMB { get; set; } = 256;

    /// <summary>
    /// Gets or sets whether to enable performance profiling
    /// </summary>
    public bool EnableProfiling { get; set; }


    /// <summary>
    /// Gets or sets the runtime initialization timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int InitializationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to validate accelerator capabilities at startup
    /// </summary>
    public bool ValidateCapabilities { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable debug logging
    /// </summary>
    public bool EnableDebugLogging { get; set; }


    /// <summary>
    /// Gets or sets custom provider configuration
    /// </summary>
    public Dictionary<string, object> ProviderSettings { get; } = [];

    /// <summary>
    /// Gets or sets whether to enable graceful degradation when accelerators fail
    /// </summary>
    public bool EnableGracefulDegradation { get; set; } = true;

    /// <summary>
    /// Gets or sets the service lifetime for accelerator instances
    /// </summary>
    public ServiceLifetime AcceleratorLifetime { get; set; } = ServiceLifetime.Singleton;
}

/// <summary>
/// Configuration options for DotCompute plugin system
/// </summary>
public class DotComputePluginOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    /// <summary>
    /// The configuration section name
    /// </summary>
    public const string SectionName = "DotCompute:Plugins";

    /// <summary>
    /// Gets or sets whether plugin loading is enabled
    /// </summary>
    public bool EnablePlugins { get; set; } = true;

    /// <summary>
    /// Gets or sets the plugin directories to scan
    /// </summary>
    public IList<string> PluginDirectories { get; set; } = ["plugins"];

    /// <summary>
    /// Gets or sets whether to enable plugin isolation
    /// </summary>
    public bool EnableIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable hot reload of plugins
    /// </summary>
    public bool EnableHotReload { get; set; }


    /// <summary>
    /// Gets or sets the maximum number of concurrent plugin loads
    /// </summary>
    [Range(1, 10)]
    public int MaxConcurrentLoads { get; set; } = 3;

    /// <summary>
    /// Gets or sets the plugin load timeout in seconds
    /// </summary>
    [Range(1, 120)]
    public int LoadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to validate plugin signatures
    /// </summary>
    public bool ValidateSignatures { get; set; }


    /// <summary>
    /// Gets or sets the trusted plugin publishers
    /// </summary>
    public IList<string> TrustedPublishers { get; } = [];

    /// <summary>
    /// Gets or sets plugin-specific configuration
    /// </summary>
    public Dictionary<string, object> PluginSettings { get; } = [];

    /// <summary>
    /// Gets or sets whether to enable dependency injection for plugins
    /// </summary>
    public bool EnableDependencyInjection { get; set; } = true;

    /// <summary>
    /// Gets or sets the service lifetime for plugin instances
    /// </summary>
    public ServiceLifetime PluginLifetime { get; set; } = ServiceLifetime.Scoped;
}

/// <summary>
/// Configuration options for advanced memory management
/// </summary>
public class AdvancedMemoryOptions
{
    /// <summary>
    /// Gets or sets whether to enable unified memory management
    /// </summary>
    public bool EnableUnifiedMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable peer-to-peer memory transfers
    /// </summary>
    public bool EnableP2PTransfers { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory coherence strategy
    /// </summary>
    public MemoryCoherenceStrategy CoherenceStrategy { get; set; } = MemoryCoherenceStrategy.Automatic;

    /// <summary>
    /// Gets or sets the buffer pool growth factor
    /// </summary>
    [Range(1.1, 5.0)]
    public double PoolGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum buffer pool size in MB
    /// </summary>
    [Range(64, 16384)]
    public int MaxPoolSizeMB { get; set; } = 2048;

    /// <summary>
    /// Gets or sets whether to enable memory compression
    /// </summary>
    public bool EnableCompression { get; set; }


    /// <summary>
    /// Gets or sets the compression threshold in MB
    /// </summary>
    [Range(1, 1024)]
    public int CompressionThresholdMB { get; set; } = 64;
}

/// <summary>
/// Configuration options for performance monitoring
/// </summary>
public class PerformanceMonitoringOptions
{
    /// <summary>
    /// Gets or sets whether performance monitoring is enabled
    /// </summary>
    public bool EnableMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics collection interval in seconds
    /// </summary>
    [Range(1, 3600)]
    public int CollectionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to enable detailed kernel profiling
    /// </summary>
    public bool EnableKernelProfiling { get; set; }


    /// <summary>
    /// Gets or sets whether to enable memory usage tracking
    /// </summary>
    public bool EnableMemoryTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of metrics to retain
    /// </summary>
    [Range(100, 100000)]
    public int MaxMetricsCount { get; set; } = 10000;

    /// <summary>
    /// Gets or sets whether to export metrics to external systems
    /// </summary>
    public bool EnableMetricsExport { get; set; }


    /// <summary>
    /// Gets or sets the metrics export endpoint
    /// </summary>
    public string? MetricsEndpoint { get; set; }

    /// <summary>
    /// Gets or sets custom performance thresholds
    /// </summary>
    public Dictionary<string, double> PerformanceThresholds { get; } = [];
}

/// <summary>
/// Memory coherence strategies
/// </summary>
public enum MemoryCoherenceStrategy
{
    /// <summary>
    /// Automatic coherence management
    /// </summary>
    Automatic,

    /// <summary>
    /// Manual coherence with explicit synchronization
    /// </summary>
    Manual,

    /// <summary>
    /// Lazy coherence with on-demand synchronization
    /// </summary>
    Lazy,

    /// <summary>
    /// Eager coherence with immediate synchronization
    /// </summary>
    Eager
}

/// <summary>
/// Service lifetime options for DI registration
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// Singleton - one instance for the application lifetime
    /// </summary>
    Singleton,

    /// <summary>
    /// Scoped - one instance per scope (e.g., request)
    /// </summary>
    Scoped,

    /// <summary>
    /// Transient - new instance every time it's requested
    /// </summary>
    Transient
}

/// <summary>
/// Validates DotCompute runtime options
/// </summary>
public class RuntimeOptionsValidator : IValidateOptions<DotComputeRuntimeOptions>
{
    /// <summary>
    /// Validates the .
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result of the operation.</returns>
    public ValidateOptionsResult Validate(string? name, DotComputeRuntimeOptions options)
    {
        var failures = new List<string>();

        if (options.MaxAccelerators is < 1 or > 64)
        {
            failures.Add("MaxAccelerators must be between 1 and 64");
        }

        if (options.MaxCacheSizeMB is < 1 or > 10240)
        {
            failures.Add("MaxCacheSizeMB must be between 1 and 10240");
        }

        if (options.InitialPoolSizeMB is < 1 or > 8192)
        {
            failures.Add("InitialPoolSizeMB must be between 1 and 8192");
        }

        if (options.InitializationTimeoutSeconds is < 1 or > 300)
        {
            failures.Add("InitializationTimeoutSeconds must be between 1 and 300");
        }

        if (!string.IsNullOrEmpty(options.KernelCacheDirectory))
        {
            try
            {
                var fullPath = Path.GetFullPath(options.KernelCacheDirectory);
                // Validate path is writable (if it exists)
                if (Directory.Exists(fullPath))
                {
                    var testFile = Path.Combine(fullPath, $"test_{Guid.NewGuid():N}.tmp");
                    try
                    {
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch
                    {
                        failures.Add($"KernelCacheDirectory '{options.KernelCacheDirectory}' is not writable");
                    }
                }
            }
            catch
            {
                failures.Add($"KernelCacheDirectory '{options.KernelCacheDirectory}' is not a valid path");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
