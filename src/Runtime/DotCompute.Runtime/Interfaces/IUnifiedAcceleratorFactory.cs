// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.Interfaces;

/// <summary>
/// Factory interface for creating and managing accelerator instances with unified configuration.
/// </summary>
public interface IUnifiedAcceleratorFactory : IDisposable
{
    /// <summary>
    /// Creates an accelerator instance from accelerator information.
    /// </summary>
    /// <param name="acceleratorInfo">The accelerator information to create from.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo acceleratorInfo, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator instance by type.
    /// </summary>
    /// <param name="type">The accelerator type to create.</param>
    /// <param name="configuration">Optional accelerator configuration.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorType type, object? configuration = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator instance by backend name.
    /// </summary>
    /// <param name="backendName">The name of the backend (e.g., "CUDA", "CPU", "Metal").</param>
    /// <param name="configuration">Optional accelerator configuration.</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(string backendName, object? configuration = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available accelerator types that can be created by this factory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the list of available accelerator types.</returns>
    public ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available accelerator devices that can be created by this factory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the list of available accelerator devices.</returns>
    public ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the factory can create an accelerator of the specified type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type to check.</param>
    /// <returns>True if the factory can create the accelerator type, false otherwise.</returns>
    public bool CanCreateAccelerator(AcceleratorType acceleratorType);

    /// <summary>
    /// Gets all supported accelerator types.
    /// </summary>
    /// <returns>An enumerable of supported accelerator types.</returns>
    public IEnumerable<AcceleratorType> GetSupportedTypes();

    /// <summary>
    /// Registers a provider for specific accelerator types.
    /// </summary>
    /// <param name="providerType">The provider type to register.</param>
    /// <param name="supportedTypes">The accelerator types supported by this provider.</param>
    public void RegisterProvider(Type providerType, params AcceleratorType[] supportedTypes);

    /// <summary>
    /// Unregisters a provider type from the factory.
    /// </summary>
    /// <param name="providerType">The provider type to unregister.</param>
    /// <returns>True if the provider was successfully unregistered, false otherwise.</returns>
    public bool UnregisterProvider(Type providerType);

    /// <summary>
    /// Creates a service scope for a specific accelerator.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <returns>A service scope for the accelerator.</returns>
    public IServiceScope CreateAcceleratorScope(string acceleratorId);

    /// <summary>
    /// Creates an accelerator provider of the specified type.
    /// </summary>
    /// <typeparam name="TProvider">The type of provider to create.</typeparam>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that resolves to the created provider instance.</returns>
    public ValueTask<TProvider> CreateProviderAsync<TProvider>(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        where TProvider : class, IAcceleratorProvider;
}