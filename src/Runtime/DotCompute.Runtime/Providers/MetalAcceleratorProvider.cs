// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Providers;

/// <summary>
/// Provider for creating Metal accelerator instances using reflection to avoid tight coupling.
/// </summary>
[RequiresUnreferencedCode("Metal accelerator creation uses reflection which requires runtime type information")]
public class MetalAcceleratorProvider : IAcceleratorProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type? _metalAcceleratorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalAcceleratorProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    public MetalAcceleratorProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Try to find MetalAccelerator type from loaded assemblies (macOS only)
        try
        {
            _metalAcceleratorType = Type.GetType("DotCompute.Backends.Metal.MetalAccelerator, DotCompute.Backends.Metal");
        }
        catch
        {
            // Metal backend not available (expected on non-macOS platforms)
        }
    }

    /// <inheritdoc/>
    public string Name => "Metal";

    /// <inheritdoc/>
    public IReadOnlyList<AcceleratorType> SupportedTypes => new[] { AcceleratorType.Metal };

    /// <inheritdoc/>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // Discovery is handled by the factory using MetalNative
        // This provider only creates accelerators
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    /// <inheritdoc/>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (_metalAcceleratorType == null)
        {
            throw new NotSupportedException("Metal backend is not available. Metal is only supported on macOS. Install DotCompute.Backends.Metal package.");
        }

        // Extract device ID from the info
        var deviceId = info.DeviceIndex;

        // Get logger from service provider
        var loggerType = typeof(ILogger<>).MakeGenericType(_metalAcceleratorType);
        var logger = _serviceProvider.GetService(loggerType);

        // Create Metal accelerator using reflection
        if (Activator.CreateInstance(_metalAcceleratorType, deviceId, logger) is not IAccelerator accelerator)
        {
            throw new InvalidOperationException("Failed to create Metal accelerator instance");
        }

        return ValueTask.FromResult(accelerator);
    }
}
