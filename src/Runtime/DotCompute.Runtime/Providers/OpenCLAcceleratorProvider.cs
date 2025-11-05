// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Providers;

/// <summary>
/// Provider for creating OpenCL accelerator instances using reflection to avoid tight coupling.
/// </summary>
[RequiresUnreferencedCode("OpenCL accelerator creation uses reflection which requires runtime type information")]
public class OpenCLAcceleratorProvider : IAcceleratorProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type? _openclAcceleratorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAcceleratorProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    public OpenCLAcceleratorProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Try to find OpenCLAccelerator type from loaded assemblies
        try
        {
            _openclAcceleratorType = Type.GetType("DotCompute.Backends.OpenCL.OpenCLAccelerator, DotCompute.Backends.OpenCL");
        }
        catch
        {
            // OpenCL backend not available
        }
    }

    /// <inheritdoc/>
    public string Name => "OpenCL";

    /// <inheritdoc/>
    public IReadOnlyList<AcceleratorType> SupportedTypes => new[] { AcceleratorType.OpenCL };

    /// <inheritdoc/>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // Discovery is handled by the factory using OpenCLDeviceManager
        // This provider only creates accelerators
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    /// <inheritdoc/>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (_openclAcceleratorType == null)
        {
            throw new NotSupportedException("OpenCL backend is not available. Install DotCompute.Backends.OpenCL package.");
        }

        // Get logger from service provider
        var loggerType = typeof(ILogger<>).MakeGenericType(_openclAcceleratorType);
        var logger = _serviceProvider.GetService(loggerType);

        // Create OpenCL accelerator using reflection
        if (Activator.CreateInstance(_openclAcceleratorType, logger) is not IAccelerator accelerator)
        {
            throw new InvalidOperationException("Failed to create OpenCL accelerator instance");
        }

        return ValueTask.FromResult(accelerator);
    }
}
