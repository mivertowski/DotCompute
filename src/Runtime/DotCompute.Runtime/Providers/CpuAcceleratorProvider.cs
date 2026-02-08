// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Providers;

/// <summary>
/// Provider for creating CPU accelerator instances using reflection to avoid tight coupling.
/// </summary>
[RequiresUnreferencedCode("CPU accelerator creation uses reflection which requires runtime type information")]
public class CpuAcceleratorProvider : IAcceleratorProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type? _cpuAcceleratorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuAcceleratorProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    public CpuAcceleratorProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Try to find CpuAccelerator type from loaded assemblies
        try
        {
            _cpuAcceleratorType = Type.GetType("DotCompute.Backends.CPU.CpuAccelerator, DotCompute.Backends.CPU");
        }
        catch
        {
            // CPU backend not available (should always be available)
        }
    }

    /// <inheritdoc/>
    public string Name => "CPU";

    /// <inheritdoc/>
    public IReadOnlyList<AcceleratorType> SupportedTypes => new[] { AcceleratorType.CPU };

    /// <inheritdoc/>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // Discovery is handled by the factory
        // This provider only creates accelerators
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    /// <inheritdoc/>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (_cpuAcceleratorType == null)
        {
            throw new NotSupportedException("CPU backend is not available. Install DotCompute.Backends.CPU package.");
        }

        // Get logger from service provider
        var loggerType = typeof(ILogger<>).MakeGenericType(_cpuAcceleratorType);
        var logger = _serviceProvider.GetService(loggerType);

        // Create CPU accelerator using reflection (always available)
        if (Activator.CreateInstance(_cpuAcceleratorType, logger) is not IAccelerator accelerator)
        {
            throw new InvalidOperationException("Failed to create CPU accelerator instance");
        }

        return ValueTask.FromResult(accelerator);
    }
}
