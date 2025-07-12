// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotCompute.Backends.Metal.Registration;

/// <summary>
/// Plugin registration for the Metal backend.
/// </summary>
public static class MetalBackendPlugin
{
    /// <summary>
    /// Adds the Metal backend to the service collection.
    /// </summary>
    public static IServiceCollection AddMetalBackend(
        this IServiceCollection services,
        Action<MetalAcceleratorOptions>? configureAccelerator = null)
    {
        // Check if we're on a supported platform
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("Metal backend is only supported on macOS and iOS platforms.");
        }

        // Register options
        if (configureAccelerator != null)
        {
            services.Configure(configureAccelerator);
        }
        else
        {
            services.Configure<MetalAcceleratorOptions>(options => { });
        }

        // Register the Metal accelerator
        services.TryAddSingleton<MetalAccelerator>();
        
        // Register as IAccelerator with a factory that includes the backend name
        services.AddSingleton<IAccelerator>(provider =>
        {
            var accelerator = provider.GetRequiredService<MetalAccelerator>();
            return new NamedAcceleratorWrapper("metal", accelerator);
        });

        return services;
    }

    /// <summary>
    /// Adds the Metal backend with default configuration.
    /// </summary>
    public static IServiceCollection AddMetalBackend(this IServiceCollection services)
    {
        return services.AddMetalBackend(null);
    }

    /// <summary>
    /// Adds the Metal backend with device selection.
    /// </summary>
    public static IServiceCollection AddMetalBackend(
        this IServiceCollection services,
        MetalDeviceSelector deviceSelector,
        Action<MetalAcceleratorOptions>? configureAccelerator = null)
    {
        // Configure device selection
        services.Configure<MetalAcceleratorOptions>(options =>
        {
            if (deviceSelector == MetalDeviceSelector.PreferIntegrated)
            {
                options.PreferIntegratedGpu = true;
            }
            else if (deviceSelector == MetalDeviceSelector.PreferDiscrete)
            {
                options.PreferIntegratedGpu = false;
            }
        });

        return services.AddMetalBackend(configureAccelerator);
    }
}

/// <summary>
/// Device selection preference for Metal backend.
/// </summary>
public enum MetalDeviceSelector
{
    /// <summary>
    /// Use the system default device.
    /// </summary>
    Default,

    /// <summary>
    /// Prefer integrated GPU (better for power efficiency).
    /// </summary>
    PreferIntegrated,

    /// <summary>
    /// Prefer discrete GPU (better for performance).
    /// </summary>
    PreferDiscrete
}

/// <summary>
/// Wrapper to provide named accelerator support.
/// </summary>
internal sealed class NamedAcceleratorWrapper : IAccelerator
{
    private readonly string _name;
    private readonly IAccelerator _accelerator;

    public NamedAcceleratorWrapper(string name, IAccelerator accelerator)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    public string Name => _name;

    public AcceleratorInfo Info => _accelerator.Info;

    public IMemoryManager Memory => _accelerator.Memory;

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        return _accelerator.CompileKernelAsync(definition, options, cancellationToken);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return _accelerator.SynchronizeAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _accelerator.DisposeAsync();
    }
}