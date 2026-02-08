// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Providers;

/// <summary>
/// Provider for creating CUDA accelerator instances using reflection to avoid tight coupling.
/// </summary>
[RequiresUnreferencedCode("CUDA accelerator creation uses reflection which requires runtime type information")]
public class CudaAcceleratorProvider : IAcceleratorProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type? _cudaAcceleratorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaAcceleratorProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    public CudaAcceleratorProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Try to find CudaAccelerator type from loaded assemblies
        try
        {
            _cudaAcceleratorType = Type.GetType("DotCompute.Backends.CUDA.CudaAccelerator, DotCompute.Backends.CUDA");
        }
        catch
        {
            // CUDA backend not available
        }
    }

    /// <inheritdoc/>
    public string Name => "CUDA";

    /// <inheritdoc/>
    public IReadOnlyList<AcceleratorType> SupportedTypes => new[] { AcceleratorType.CUDA };

    /// <inheritdoc/>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // Discovery is handled by the factory using CudaDeviceManager
        // This provider only creates accelerators
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    /// <inheritdoc/>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (_cudaAcceleratorType == null)
        {
            throw new NotSupportedException("CUDA backend is not available. Install DotCompute.Backends.CUDA package.");
        }

        // Extract device ID from the info
        var deviceId = info.DeviceIndex;

        // Get logger from service provider
        var loggerType = typeof(ILogger<>).MakeGenericType(_cudaAcceleratorType);
        var logger = _serviceProvider.GetService(loggerType);

        // Create CUDA accelerator using reflection
        if (Activator.CreateInstance(_cudaAcceleratorType, deviceId, logger) is not IAccelerator accelerator)
        {
            throw new InvalidOperationException("Failed to create CUDA accelerator instance");
        }

        return ValueTask.FromResult(accelerator);
    }
}
