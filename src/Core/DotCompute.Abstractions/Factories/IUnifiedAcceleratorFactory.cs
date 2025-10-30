// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Abstractions.Factories;

/// <summary>
/// Unified factory interface for creating accelerator instances.
/// This is the ONLY accelerator factory interface in the entire solution.
/// Combines capabilities from Core.Factories and Runtime.Factories versions.
/// </summary>
public interface IUnifiedAcceleratorFactory
{
    /// <summary>
    /// Creates an accelerator instance based on the specified type.
    /// </summary>
    /// <param name="type">The type of accelerator to create.</param>
    /// <param name="configuration">Optional configuration for the accelerator.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(
        AcceleratorType type,
        AcceleratorConfiguration? configuration = null,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator instance with a specific backend.
    /// </summary>
    /// <param name="backendName">The backend name (e.g., "CPU", "CUDA", "Metal").</param>
    /// <param name="configuration">Optional configuration for the accelerator.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(
        string backendName,
        AcceleratorConfiguration? configuration = null,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator instance with specific device info.
    /// </summary>
    /// <param name="acceleratorInfo">The accelerator information.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(
        AcceleratorInfo acceleratorInfo,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator provider with DI support.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator provider.</returns>
    public ValueTask<TProvider> CreateProviderAsync<TProvider>(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)

        where TProvider : class, IAcceleratorProvider;

    /// <summary>
    /// Gets available accelerator types on the current system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available accelerator types.</returns>
    public ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about available accelerator devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available accelerator information.</returns>
    public ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an accelerator type can be created.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>True if the accelerator can be created.</returns>
    public bool CanCreateAccelerator(AcceleratorType acceleratorType);

    /// <summary>
    /// Registers a custom accelerator provider type.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <param name="supportedTypes">The accelerator types supported by this provider.</param>
    public void RegisterProvider([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type providerType, params AcceleratorType[] supportedTypes);

    /// <summary>
    /// Unregisters a custom accelerator provider type.
    /// </summary>
    /// <param name="providerType">The provider type to unregister.</param>
    public bool UnregisterProvider(Type providerType);
}

/// <summary>
/// Configuration for accelerator creation.
/// </summary>
public class AcceleratorConfiguration
{
    /// <summary>
    /// Gets or sets the device index to use (for multi-device systems).
    /// </summary>
    public int DeviceIndex { get; set; }


    /// <summary>
    /// Gets or sets whether to enable debug mode.
    /// </summary>
    public bool EnableDebugMode { get; set; }


    /// <summary>
    /// Gets or sets whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }


    /// <summary>
    /// Gets or sets the memory allocation strategy.
    /// </summary>
    public MemoryAllocationStrategy MemoryStrategy { get; set; } = MemoryAllocationStrategy.Default;


    /// <summary>
    /// Gets or sets whether to enable NUMA optimization for CPU.
    /// </summary>
    public bool EnableNumaOptimization { get; set; }


    /// <summary>
    /// Gets or sets the performance profile.
    /// </summary>
    public PerformanceProfile PerformanceProfile { get; set; } = PerformanceProfile.Balanced;


    /// <summary>
    /// Gets or sets custom properties for backend-specific configuration.
    /// </summary>
    public Dictionary<string, object> CustomProperties { get; } = [];
}

/// <summary>
/// Memory allocation strategies.
/// </summary>
public enum MemoryAllocationStrategy
{
    /// <summary>
    /// Default allocation strategy.
    /// </summary>
    Default,

    /// <summary>
    /// Pooled memory allocation.
    /// </summary>
    Pooled,

    /// <summary>
    /// Optimized pooled memory allocation with lock-free structures.
    /// </summary>
    OptimizedPooled,

    /// <summary>
    /// Unified memory allocation.
    /// </summary>
    Unified,

    /// <summary>
    /// Aggressive caching strategy.
    /// </summary>
    AggressiveCaching,


    /// <summary>
    /// Conservative memory usage.
    /// </summary>
    Conservative
}
