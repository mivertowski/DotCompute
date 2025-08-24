// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Factories;

/// <summary>
/// Factory interface for creating accelerator instances.
/// Centralizes accelerator creation and configuration management.
/// </summary>
public interface IAcceleratorFactory
{
    /// <summary>
    /// Creates an accelerator instance based on the specified type.
    /// </summary>
    /// <param name="type">The type of accelerator to create.</param>
    /// <param name="configuration">Optional configuration for the accelerator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    ValueTask<IAccelerator> CreateAsync(
        AcceleratorType type,
        AcceleratorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates an accelerator instance with a specific backend.
    /// </summary>
    /// <param name="backendName">The backend name (e.g., "CPU", "CUDA", "Metal").</param>
    /// <param name="configuration">Optional configuration for the accelerator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    ValueTask<IAccelerator> CreateAsync(
        string backendName,
        AcceleratorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets available accelerator types on the current system.
    /// </summary>
    /// <returns>List of available accelerator types.</returns>
    ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about available accelerator devices.
    /// </summary>
    /// <returns>List of available accelerator information.</returns>
    ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers a custom accelerator backend.
    /// </summary>
    /// <param name="backendName">The backend name.</param>
    /// <param name="factoryMethod">Factory method for creating the accelerator.</param>
    void RegisterBackend(
        string backendName,
        Func<AcceleratorConfiguration, ILoggerFactory, ValueTask<IAccelerator>> factoryMethod);
    
    /// <summary>
    /// Selects the best available accelerator based on workload characteristics.
    /// </summary>
    /// <param name="workloadProfile">The workload profile for selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best accelerator for the workload.</returns>
    ValueTask<IAccelerator> CreateBestForWorkloadAsync(
        WorkloadProfile workloadProfile,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for accelerator creation.
/// </summary>
public class AcceleratorConfiguration
{
    /// <summary>
    /// Gets or sets the device index for multi-device systems.
    /// </summary>
    public int DeviceIndex { get; set; }
    
    /// <summary>
    /// Gets or sets the memory allocation strategy.
    /// </summary>
    public MemoryAllocationStrategy MemoryStrategy { get; set; } = MemoryAllocationStrategy.Pooled;
    
    /// <summary>
    /// Gets or sets the maximum memory limit in bytes.
    /// </summary>
    public long? MaxMemoryBytes { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable debug mode.
    /// </summary>
    public bool EnableDebugMode { get; set; }
    
    /// <summary>
    /// Gets or sets the thread pool size for CPU accelerators.
    /// </summary>
    public int? ThreadPoolSize { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable NUMA optimization for CPU.
    /// </summary>
    public bool EnableNumaOptimization { get; set; }
    
    /// <summary>
    /// Gets or sets custom backend-specific options.
    /// </summary>
    public Dictionary<string, object> CustomOptions { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the performance profile.
    /// </summary>
    public PerformanceProfile PerformanceProfile { get; set; } = PerformanceProfile.Balanced;
    
    /// <summary>
    /// Gets or sets the security profile.
    /// </summary>
    public SecurityProfile SecurityProfile { get; set; } = SecurityProfile.Default;
}

/// <summary>
/// Memory allocation strategy for accelerators.
/// </summary>
public enum MemoryAllocationStrategy
{
    /// <summary>Direct allocation without pooling.</summary>
    Direct,
    
    /// <summary>Pooled allocation for better performance.</summary>
    Pooled,
    
    /// <summary>Optimized pooling with lock-free operations.</summary>
    OptimizedPooled,
    
    /// <summary>Unified memory management across devices.</summary>
    Unified,
    
    /// <summary>Custom allocation strategy.</summary>
    Custom
}

/// <summary>
/// Performance profile for accelerator optimization.
/// </summary>
public enum PerformanceProfile
{
    /// <summary>Optimize for low latency.</summary>
    LowLatency,
    
    /// <summary>Optimize for high throughput.</summary>
    HighThroughput,
    
    /// <summary>Balanced performance.</summary>
    Balanced,
    
    /// <summary>Optimize for energy efficiency.</summary>
    PowerEfficient,
    
    /// <summary>Maximum performance regardless of power.</summary>
    MaxPerformance
}

/// <summary>
/// Security profile for accelerator operations.
/// </summary>
public enum SecurityProfile
{
    /// <summary>Default security settings.</summary>
    Default,
    
    /// <summary>Enhanced security with memory isolation.</summary>
    Enhanced,
    
    /// <summary>Maximum security with encrypted memory.</summary>
    Maximum,
    
    /// <summary>Development mode with relaxed security.</summary>
    Development
}

/// <summary>
/// Workload profile for accelerator selection.
/// </summary>
public class WorkloadProfile
{
    /// <summary>
    /// Gets or sets the expected data size in bytes.
    /// </summary>
    public long ExpectedDataSize { get; set; }
    
    /// <summary>
    /// Gets or sets whether the workload is compute-intensive.
    /// </summary>
    public bool IsComputeIntensive { get; set; }
    
    /// <summary>
    /// Gets or sets whether the workload is memory-intensive.
    /// </summary>
    public bool IsMemoryIntensive { get; set; }
    
    /// <summary>
    /// Gets or sets whether the workload requires real-time processing.
    /// </summary>
    public bool RequiresRealTime { get; set; }
    
    /// <summary>
    /// Gets or sets the expected parallelism level.
    /// </summary>
    public int ExpectedParallelism { get; set; }
    
    /// <summary>
    /// Gets or sets whether the workload uses machine learning.
    /// </summary>
    public bool UsesMachineLearning { get; set; }
    
    /// <summary>
    /// Gets or sets the preferred backend hints.
    /// </summary>
    public List<string> PreferredBackends { get; set; } = new();
}